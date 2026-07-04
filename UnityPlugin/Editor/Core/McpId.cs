using System;
using System.Reflection;
using UnityEditor;
using Object = UnityEngine.Object;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// Instance-id compatibility shim.
    ///
    /// Unity 6.5 marked <c>UnityEngine.Object.GetInstanceID()</c> and
    /// <c>EditorUtility.InstanceIDToObject(int)</c> as obsolete-<b>errors</b> as part of the
    /// InstanceID -> EntityId migration. The replacement (<c>EntityId</c>) is an opaque struct
    /// that deliberately does not convert to <c>int</c>, but this server's JSON-RPC protocol
    /// identifies objects by their integer instance id end-to-end (the Node side sends and
    /// receives ints). The obsolete APIs still function at runtime and still return those ints;
    /// the obsolete attribute only blocks compilation.
    ///
    /// So we call them through cached reflection, which is not subject to the compile-time
    /// obsolete check. This keeps the plugin compiling on Unity 2022.3, Unity 6.0, and
    /// Unity 6.5+ without changing the id contract. MethodInfo is cached; the per-call Invoke
    /// overhead is negligible for editor tool calls.
    /// </summary>
    public static class McpId
    {
        private static readonly MethodInfo _getInstanceId =
            typeof(Object).GetMethod("GetInstanceID", Type.EmptyTypes);

        private static readonly MethodInfo _idToObject =
            typeof(EditorUtility).GetMethod("InstanceIDToObject", new[] { typeof(int) });

        /// <summary>Returns the integer instance id of a Unity object (0 if null).</summary>
        public static int Get(Object o)
        {
            if (o == null) return 0;
            return (int)_getInstanceId.Invoke(o, null);
        }

        /// <summary>Resolves a Unity object from its integer instance id (null if not found).</summary>
        public static Object ToObject(int instanceId)
        {
            return (Object)_idToObject.Invoke(null, new object[] { instanceId });
        }
    }
}
