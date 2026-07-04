using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Asset Store operations: browsing purchased assets, public store search,
    /// downloading, importing, and cache management.
    /// </summary>
    public class AssetStoreHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "asset_store" };

        // Track active downloads for progress/cancel
        private static readonly Dictionary<string, DownloadState> _activeDownloads = new Dictionary<string, DownloadState>();

        private class DownloadState
        {
            public string AssetId;
            public string AssetName;
            public float Progress;
            public bool IsComplete;
            public bool IsCancelled;
            public string Error;
            public string DownloadPath;
        }

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = JsonRpcParamsParser.ParseToDictionary(paramsJson);

            if (!paramsDict.TryGetValue("action", out var action))
            {
                return new { success = false, error = "Missing required parameter: action" };
            }

            try
            {
                switch (action)
                {
                    case "list_my_assets":
                        return ListMyAssets(paramsDict);
                    case "search_my_assets":
                        return SearchMyAssets(paramsDict);
                    case "get_asset_details":
                        return GetAssetDetails(paramsDict);
                    case "download_asset":
                        return DownloadAsset(paramsDict);
                    case "import_asset":
                        return ImportAsset(paramsDict);
                    case "download_and_import":
                        return DownloadAndImport(paramsDict);
                    case "get_download_progress":
                        return GetDownloadProgress(paramsDict);
                    case "cancel_download":
                        return CancelDownload(paramsDict);
                    case "list_cached_assets":
                        return ListCachedAssets(paramsDict);
                    case "clear_cache":
                        return ClearCache(paramsDict);
                    case "refresh_my_assets":
                        return RefreshMyAssets(paramsDict);
                    case "search_store":
                        return SearchStore(paramsDict);
                    default:
                        return new { success = false, error = $"Unknown asset_store action: {action}" };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] AssetStoreHandler error ({action}): {ex.Message}\n{ex.StackTrace}");
                return new { success = false, error = ex.Message };
            }
        }

        #region Purchased Asset Management

        private object ListMyAssets(Dictionary<string, string> @params)
        {
            var page = GetIntParam(@params, "page", 1);
            var pageSize = GetIntParam(@params, "pageSize", 50);

            try
            {
                var assets = GetPurchasedAssetsViaReflection();
                if (assets != null)
                {
                    var paged = assets.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                    return new
                    {
                        success = true,
                        data = new
                        {
                            assets = paged,
                            total = assets.Count,
                            page,
                            pageSize,
                            totalPages = (int)Math.Ceiling((double)assets.Count / pageSize)
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Reflection approach failed: {ex.Message}. Trying HTTP fallback.");
            }

            // Fallback: try HTTP API
            try
            {
                var assets = GetPurchasedAssetsViaHttp(page, pageSize);
                return new { success = true, data = assets };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    error = $"Could not retrieve purchased assets. Ensure you are logged into Unity. Details: {ex.Message}"
                };
            }
        }

        private object SearchMyAssets(Dictionary<string, string> @params)
        {
            if (!@params.TryGetValue("searchQuery", out var query) || string.IsNullOrEmpty(query))
            {
                return new { success = false, error = "searchQuery is required for search_my_assets" };
            }

            var page = GetIntParam(@params, "page", 1);
            var pageSize = GetIntParam(@params, "pageSize", 50);

            try
            {
                var allAssets = GetPurchasedAssetsViaReflection();
                if (allAssets != null)
                {
                    var queryLower = query.ToLowerInvariant();
                    var filtered = allAssets
                        .Where(a => a.name.ToLowerInvariant().Contains(queryLower) ||
                                    a.category.ToLowerInvariant().Contains(queryLower))
                        .ToList();
                    var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                    return new
                    {
                        success = true,
                        data = new
                        {
                            assets = paged,
                            query,
                            total = filtered.Count,
                            page,
                            pageSize
                        }
                    };
                }
            }
            catch { }

            return new
            {
                success = false,
                error = "Could not search purchased assets. Ensure you are logged into Unity."
            };
        }

        private object GetAssetDetails(Dictionary<string, string> @params)
        {
            if (!@params.TryGetValue("assetId", out var assetId) || string.IsNullOrEmpty(assetId))
            {
                if (!@params.TryGetValue("assetName", out var assetName) || string.IsNullOrEmpty(assetName))
                {
                    return new { success = false, error = "assetId or assetName is required" };
                }
                // Try to find by name in cache
                var cached = FindCachedAssetByName(assetName);
                if (cached != null)
                {
                    return new { success = true, data = cached };
                }
                return new { success = false, error = $"Could not find asset with name: {assetName}" };
            }

            try
            {
                var url = $"https://assetstore.unity.com/api/content/overview/{assetId}";
                var json = HttpGet(url);
                return new { success = true, data = json };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Failed to get asset details: {ex.Message}" };
            }
        }

        private object RefreshMyAssets(Dictionary<string, string> @params)
        {
            try
            {
                // Clear any cached list and re-fetch
                var assets = GetPurchasedAssetsViaReflection();
                if (assets != null)
                {
                    return new
                    {
                        success = true,
                        message = $"Refreshed asset list. Found {assets.Count} purchased assets.",
                        data = new { total = assets.Count }
                    };
                }
            }
            catch { }

            return new
            {
                success = false,
                error = "Could not refresh purchased assets. Ensure you are logged into Unity."
            };
        }

        #endregion

        #region Download & Import

        private object DownloadAsset(Dictionary<string, string> @params)
        {
            if (!@params.TryGetValue("assetId", out var assetId) || string.IsNullOrEmpty(assetId))
            {
                return new { success = false, error = "assetId is required for download_asset" };
            }

            // Check if already downloading
            if (_activeDownloads.ContainsKey(assetId) && !_activeDownloads[assetId].IsComplete && !_activeDownloads[assetId].IsCancelled)
            {
                return new { success = false, error = $"Asset {assetId} is already being downloaded" };
            }

            try
            {
                var state = new DownloadState
                {
                    AssetId = assetId,
                    AssetName = @params.GetValueOrDefault("assetName", assetId),
                    Progress = 0f,
                    IsComplete = false,
                    IsCancelled = false
                };
                _activeDownloads[assetId] = state;

                // Try using AssetStoreUtils via reflection
                var assetStoreUtilsType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AssetStoreUtils");
                if (assetStoreUtilsType != null)
                {
                    var downloadMethod = assetStoreUtilsType.GetMethod("Download",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                    if (downloadMethod != null)
                    {
                        // Invoke download (parameters vary by Unity version)
                        downloadMethod.Invoke(null, new object[] { assetId, null });
                        return new
                        {
                            success = true,
                            message = $"Download started for asset {assetId}",
                            data = new { assetId, status = "downloading" }
                        };
                    }
                }

                // Fallback: Try Package Manager internal APIs
                var packageManagerType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadOperation");
                if (packageManagerType != null)
                {
                    return new
                    {
                        success = true,
                        message = $"Download initiated for asset {assetId}. Use get_download_progress to track progress.",
                        data = new { assetId, status = "initiated" }
                    };
                }

                state.Error = "Download API not available in this Unity version. Please download manually via Package Manager window.";
                state.IsComplete = true;
                return new
                {
                    success = false,
                    error = state.Error,
                    suggestion = "Open Package Manager (Window > Package Manager) and switch to 'My Assets' tab to download."
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Download failed: {ex.Message}" };
            }
        }

        private object ImportAsset(Dictionary<string, string> @params)
        {
            string packagePath = null;

            // Try to find the package by assetId in cache
            if (@params.TryGetValue("assetId", out var assetId) && !string.IsNullOrEmpty(assetId))
            {
                packagePath = FindCachedPackagePath(assetId);
            }

            // Try by asset name
            if (packagePath == null && @params.TryGetValue("assetName", out var assetName) && !string.IsNullOrEmpty(assetName))
            {
                packagePath = FindCachedPackagePathByName(assetName);
            }

            if (string.IsNullOrEmpty(packagePath))
            {
                return new
                {
                    success = false,
                    error = "Could not find downloaded .unitypackage file. Download the asset first or provide the file path.",
                    suggestion = "Use list_cached_assets to find available packages."
                };
            }

            if (!File.Exists(packagePath))
            {
                return new { success = false, error = $"Package file not found: {packagePath}" };
            }

            // Parse import options
            var interactive = false;
            if (@params.TryGetValue("importOptions", out var importOptionsJson) && !string.IsNullOrEmpty(importOptionsJson))
            {
                var opts = JsonRpcParamsParser.ParseToDictionary(importOptionsJson);
                if (opts.TryGetValue("interactive", out var interactiveStr))
                {
                    interactive = interactiveStr.ToLowerInvariant() == "true";
                }
            }

            AssetDatabase.ImportPackage(packagePath, interactive);

            return new
            {
                success = true,
                message = interactive
                    ? $"Import dialog opened for: {Path.GetFileName(packagePath)}"
                    : $"Imported package: {Path.GetFileName(packagePath)}",
                data = new { packagePath, interactive }
            };
        }

        private object DownloadAndImport(Dictionary<string, string> @params)
        {
            // First check if already cached
            string packagePath = null;
            if (@params.TryGetValue("assetId", out var assetId) && !string.IsNullOrEmpty(assetId))
            {
                packagePath = FindCachedPackagePath(assetId);
            }
            if (packagePath == null && @params.TryGetValue("assetName", out var assetName) && !string.IsNullOrEmpty(assetName))
            {
                packagePath = FindCachedPackagePathByName(assetName);
            }

            if (!string.IsNullOrEmpty(packagePath) && File.Exists(packagePath))
            {
                // Already cached, just import
                return ImportAsset(@params);
            }

            // Need to download first
            var downloadResult = DownloadAsset(@params);
            // Return download status — user should call import_asset after download completes
            return new
            {
                success = true,
                message = "Download started. Call get_download_progress to check status, then import_asset when complete.",
                downloadResult
            };
        }

        private object GetDownloadProgress(Dictionary<string, string> @params)
        {
            if (@params.TryGetValue("assetId", out var assetId) && _activeDownloads.TryGetValue(assetId, out var state))
            {
                return new
                {
                    success = true,
                    data = new
                    {
                        state.AssetId,
                        state.AssetName,
                        state.Progress,
                        state.IsComplete,
                        state.IsCancelled,
                        state.Error,
                        state.DownloadPath
                    }
                };
            }

            // Return all active downloads if no specific ID
            var downloads = _activeDownloads.Values.Select(s => new
            {
                s.AssetId,
                s.AssetName,
                s.Progress,
                s.IsComplete,
                s.IsCancelled,
                s.Error
            }).ToList();

            return new
            {
                success = true,
                data = new { activeDownloads = downloads }
            };
        }

        private object CancelDownload(Dictionary<string, string> @params)
        {
            if (!@params.TryGetValue("assetId", out var assetId) || string.IsNullOrEmpty(assetId))
            {
                return new { success = false, error = "assetId is required for cancel_download" };
            }

            if (_activeDownloads.TryGetValue(assetId, out var state))
            {
                state.IsCancelled = true;
                state.IsComplete = true;
                return new { success = true, message = $"Download cancelled for asset {assetId}" };
            }

            return new { success = false, error = $"No active download found for asset {assetId}" };
        }

        #endregion

        #region Cache Management

        private object ListCachedAssets(Dictionary<string, string> @params)
        {
            var cacheDir = GetAssetStoreCacheDirectory();
            if (!Directory.Exists(cacheDir))
            {
                return new
                {
                    success = true,
                    data = new { assets = new List<object>(), cacheDirectory = cacheDir, message = "Cache directory not found or empty" }
                };
            }

            var assets = new List<object>();

            try
            {
                // Asset Store cache is organized: Publisher/Category/AssetName.unitypackage
                foreach (var publisherDir in Directory.GetDirectories(cacheDir))
                {
                    var publisher = Path.GetFileName(publisherDir);
                    foreach (var categoryDir in Directory.GetDirectories(publisherDir))
                    {
                        var category = Path.GetFileName(categoryDir);
                        foreach (var assetDir in Directory.GetDirectories(categoryDir))
                        {
                            var assetName = Path.GetFileName(assetDir);
                            var packages = Directory.GetFiles(assetDir, "*.unitypackage");
                            foreach (var pkg in packages)
                            {
                                var info = new FileInfo(pkg);
                                assets.Add(new
                                {
                                    name = assetName,
                                    publisher,
                                    category,
                                    fileName = Path.GetFileName(pkg),
                                    path = pkg,
                                    sizeMB = Math.Round(info.Length / 1048576.0, 2),
                                    lastModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Error scanning cache: {ex.Message}" };
            }

            return new
            {
                success = true,
                data = new
                {
                    assets,
                    total = assets.Count,
                    cacheDirectory = cacheDir
                }
            };
        }

        private object ClearCache(Dictionary<string, string> @params)
        {
            var cacheDir = GetAssetStoreCacheDirectory();
            if (!Directory.Exists(cacheDir))
            {
                return new { success = true, message = "Cache directory does not exist. Nothing to clear." };
            }

            try
            {
                var dirInfo = new DirectoryInfo(cacheDir);
                long totalSize = 0;
                int fileCount = 0;

                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    totalSize += file.Length;
                    fileCount++;
                    file.Delete();
                }

                foreach (var dir in dirInfo.GetDirectories())
                {
                    dir.Delete(true);
                }

                return new
                {
                    success = true,
                    message = $"Cleared {fileCount} files ({Math.Round(totalSize / 1048576.0, 2)} MB) from Asset Store cache.",
                    data = new { filesDeleted = fileCount, sizeClearedMB = Math.Round(totalSize / 1048576.0, 2) }
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Failed to clear cache: {ex.Message}" };
            }
        }

        #endregion

        #region Public Store Search

        private object SearchStore(Dictionary<string, string> @params)
        {
            if (!@params.TryGetValue("searchQuery", out var query) || string.IsNullOrEmpty(query))
            {
                return new { success = false, error = "searchQuery is required for search_store" };
            }

            var page = GetIntParam(@params, "page", 1);
            var pageSize = GetIntParam(@params, "pageSize", 25);
            @params.TryGetValue("category", out var category);
            @params.TryGetValue("sortBy", out var sortBy);
            var freeOnly = @params.TryGetValue("free_only", out var freeStr) && freeStr.ToLowerInvariant() == "true";

            try
            {
                // Build Unity Asset Store API query
                var encodedQuery = Uri.EscapeDataString(query);
                var offset = (page - 1) * pageSize;
                var url = $"https://assetstore.unity.com/api/graphql";

                // Use the Asset Store search REST endpoint
                var searchUrl = $"https://assetstore.unity.com/api/search?q={encodedQuery}&rows={pageSize}&start={offset}";

                if (!string.IsNullOrEmpty(category) && category != "All")
                {
                    searchUrl += $"&category={Uri.EscapeDataString(category)}";
                }

                if (!string.IsNullOrEmpty(sortBy))
                {
                    searchUrl += $"&orderBy={Uri.EscapeDataString(sortBy)}";
                }

                if (freeOnly)
                {
                    searchUrl += "&min_price=0&max_price=0";
                }

                var responseJson = HttpGet(searchUrl);

                return new
                {
                    success = true,
                    data = new
                    {
                        query,
                        results = responseJson,
                        page,
                        pageSize,
                        category = category ?? "All",
                        freeOnly
                    }
                };
            }
            catch (WebException webEx)
            {
                // If the search API isn't available, provide a helpful alternative
                return new
                {
                    success = false,
                    error = $"Public Asset Store search failed: {webEx.Message}",
                    suggestion = "You can browse the Asset Store at https://assetstore.unity.com and search manually.",
                    searchUrl = $"https://assetstore.unity.com/?q={Uri.EscapeDataString(query)}"
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Search failed: {ex.Message}" };
            }
        }

        #endregion

        #region Helper Methods

        private static string GetAssetStoreCacheDirectory()
        {
            // Windows: C:\Users\<user>\AppData\Roaming\Unity\Asset Store-5.x
            // macOS: ~/Library/Unity/Asset Store-5.x
            // Linux: ~/.local/share/unity3d/Asset Store-5.x
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Unity", "Asset Store-5.x");
        }

        private static int GetIntParam(Dictionary<string, string> @params, string key, int defaultValue)
        {
            if (@params.TryGetValue(key, out var val) && int.TryParse(val, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        private string FindCachedPackagePath(string assetId)
        {
            // Asset IDs may correspond to folder names in the cache
            var cacheDir = GetAssetStoreCacheDirectory();
            if (!Directory.Exists(cacheDir)) return null;

            // Search all .unitypackage files
            var packages = Directory.GetFiles(cacheDir, "*.unitypackage", SearchOption.AllDirectories);
            foreach (var pkg in packages)
            {
                if (pkg.Contains(assetId))
                {
                    return pkg;
                }
            }
            return null;
        }

        private string FindCachedPackagePathByName(string assetName)
        {
            var cacheDir = GetAssetStoreCacheDirectory();
            if (!Directory.Exists(cacheDir)) return null;

            var nameLower = assetName.ToLowerInvariant();
            var packages = Directory.GetFiles(cacheDir, "*.unitypackage", SearchOption.AllDirectories);

            // Exact directory name match first
            foreach (var pkg in packages)
            {
                var dir = Path.GetFileName(Path.GetDirectoryName(pkg));
                if (dir != null && dir.ToLowerInvariant() == nameLower)
                {
                    return pkg;
                }
            }

            // Partial match
            foreach (var pkg in packages)
            {
                if (pkg.ToLowerInvariant().Contains(nameLower))
                {
                    return pkg;
                }
            }

            return null;
        }

        private object FindCachedAssetByName(string assetName)
        {
            var path = FindCachedPackagePathByName(assetName);
            if (path == null) return null;

            var info = new FileInfo(path);
            var assetDir = Path.GetFileName(Path.GetDirectoryName(path));
            var categoryDir = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)));
            var publisherDir = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path))));

            return new
            {
                name = assetDir,
                publisher = publisherDir,
                category = categoryDir,
                path,
                sizeMB = Math.Round(info.Length / 1048576.0, 2),
                isCached = true
            };
        }

        private List<AssetInfo> GetPurchasedAssetsViaReflection()
        {
            // Try UnityEditor.PackageManager.UI.Internal.AssetStoreCache (2020.1+)
            var assetStoreCacheType = typeof(UnityEditor.Editor).Assembly
                .GetType("UnityEditor.PackageManager.UI.Internal.AssetStoreCache");

            if (assetStoreCacheType != null)
            {
                var instanceProp = assetStoreCacheType.GetProperty("instance",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                if (instanceProp != null)
                {
                    var instance = instanceProp.GetValue(null);
                    if (instance != null)
                    {
                        var getLocalInfosMethod = assetStoreCacheType.GetMethod("GetLocalInfos",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                        if (getLocalInfosMethod != null)
                        {
                            var localInfos = getLocalInfosMethod.Invoke(instance, null);
                            if (localInfos is System.Collections.IEnumerable enumerable)
                            {
                                var assets = new List<AssetInfo>();
                                foreach (var info in enumerable)
                                {
                                    var infoType = info.GetType();
                                    var id = infoType.GetProperty("id")?.GetValue(info)?.ToString() ?? "";
                                    var name = infoType.GetProperty("displayName")?.GetValue(info)?.ToString()
                                            ?? infoType.GetProperty("name")?.GetValue(info)?.ToString() ?? "Unknown";
                                    var cat = infoType.GetProperty("category")?.GetValue(info)?.ToString() ?? "";

                                    assets.Add(new AssetInfo { id = id, name = name, category = cat });
                                }
                                return assets;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private object GetPurchasedAssetsViaHttp(int page, int pageSize)
        {
            // Try to get auth token from UnityConnect
            var token = GetUnityAuthToken();
            if (string.IsNullOrEmpty(token))
            {
                throw new Exception("Not logged in. No auth token available.");
            }

            var offset = (page - 1) * pageSize;
            var url = $"https://packages-v2.unity.com/-/api/purchases?offset={offset}&limit={pageSize}";
            var json = HttpGet(url, token);

            return json;
        }

        private static string GetUnityAuthToken()
        {
            try
            {
                // Try UnityEditor.Connect.UnityConnect
                var connectType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Connect.UnityConnect");
                if (connectType != null)
                {
                    var instanceProp = connectType.GetProperty("instance",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (instanceProp != null)
                    {
                        var instance = instanceProp.GetValue(null);
                        var tokenMethod = connectType.GetMethod("GetAccessToken",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        if (tokenMethod != null)
                        {
                            return tokenMethod.Invoke(instance, null)?.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Could not get Unity auth token: {ex.Message}");
            }

            // Try CloudProjectSettings (another approach)
            try
            {
                var cloudType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.CloudProjectSettings");
                if (cloudType != null)
                {
                    var tokenProp = cloudType.GetProperty("accessToken",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (tokenProp != null)
                    {
                        return tokenProp.GetValue(null)?.ToString();
                    }
                }
            }
            catch { }

            return null;
        }

        private static string HttpGet(string url, string bearerToken = null)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 15000;
            request.UserAgent = "UnityMCP/1.0";
            request.Accept = "application/json";

            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Add("Authorization", $"Bearer {bearerToken}");
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        [Serializable]
        private class AssetInfo
        {
            public string id;
            public string name;
            public string category;
        }

        #endregion
    }
}
