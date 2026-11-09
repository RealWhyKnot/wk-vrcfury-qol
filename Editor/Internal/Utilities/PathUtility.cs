#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace UmeVrcfQol.Internal.Utilities
{
    public static class PathUtility
    {
        public static string GetGameObjectPath(GameObject go)
        {
            if (go == null)
            {
                return "(null)";
            }

            Transform t = go.transform;
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        public static string? GetRelativePath(Transform from, Transform descendant)
        {
            if (from == null || descendant == null)
            {
                return null;
            }

            if (descendant == from)
            {
                return string.Empty;
            }

            var parts = new Stack<string>();
            Transform? cursor = descendant;
            while (cursor != null && cursor != from)
            {
                parts.Push(cursor.name);
                cursor = cursor.parent;
            }
            return cursor != from ? null : string.Join("/", parts.ToArray());
        }

        public static string? GetRelativePath(GameObject from, GameObject descendant)
        {
            return from == null || descendant == null ? null : GetRelativePath(from.transform, descendant.transform);
        }
    }
}
