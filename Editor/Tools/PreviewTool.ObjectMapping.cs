#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools
{
    internal static partial class PreviewTool
    {
        internal static GameObject? FindAvatarRoot(Component component)
        {
            if (component == null)
            {
                return null;
            }

            Animator animator = component.GetComponentInParent<Animator>(includeInactive: true);
            if (animator != null)
            {
                return animator.gameObject;
            }

            Transform t = component.transform;
            while (t.parent != null)
            {
                t = t.parent;
            }

            return t.gameObject;
        }

        private static void AlignCloneWithSource(GameObject source, GameObject clone)
        {
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;
        }

        private static Bounds CalculateBounds(GameObject root, bool visibleOnly = false)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            var bounds = new Bounds(root.transform.position, Vector3.one);
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (visibleOnly && (!renderer.enabled || !renderer.gameObject.activeInHierarchy))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        private static void MarkHierarchyTemporary(GameObject clone)
        {
            foreach (Transform transform in clone.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                transform.gameObject.hideFlags = HideFlags.DontSave;
                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        component.hideFlags = HideFlags.DontSave;
                    }
                }
            }
        }

        private static void StripPreviewComponents(GameObject clone)
        {
            foreach (Component component in clone.GetComponentsInChildren<Component>(includeInactive: true))
            {
                if (component == null)
                {
                    continue;
                }

                if (string.Equals(component.GetType().FullName, "VF.Model.VRCFury", StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        private static void AddFocus(PreviewSession session, Renderer renderer)
        {
            if (session == null || renderer == null)
            {
                return;
            }

            EncapsulateFocus(session, renderer.bounds);
        }

        private static void AddFocus(PreviewSession session, GameObject? obj)
        {
            if (session == null || obj == null)
            {
                return;
            }

            EncapsulateFocus(session, CalculateBounds(obj));
        }

        private static void EncapsulateFocus(PreviewSession session, Bounds bounds)
        {
            if (!session.HasFocusBounds)
            {
                session.FocusBounds = bounds;
                session.HasFocusBounds = true;
            }
            else
            {
                session.FocusBounds.Encapsulate(bounds);
            }
        }

        private static T? MapObject<T>(T? source, PreviewSession session) where T : Object
        {
            if (source == null || session == null || session.SourceRoot == null || session.CloneRoot == null)
            {
                return null;
            }

            if (source is GameObject go)
            {
                Transform? mapped = MapTransform(go.transform, session);
                return mapped != null ? mapped.gameObject as T : null;
            }
            if (source is Component component)
            {
                Transform? mappedTransform = MapTransform(component.transform, session);
                if (mappedTransform == null)
                {
                    return null;
                }

                Component[] sourceComponents = component.gameObject.GetComponents(component.GetType());
                int index = Array.IndexOf(sourceComponents, component);
                Component[] mappedComponents = mappedTransform.gameObject.GetComponents(component.GetType());
                return index >= 0 && index < mappedComponents.Length
                    ? mappedComponents[index] as T
                    : mappedTransform.GetComponent(component.GetType()) as T;
            }
            return source;
        }

        private static Transform? MapTransform(Transform source, PreviewSession session)
        {
            if (source == null)
            {
                return null;
            }

            if (source == session.SourceRoot.transform)
            {
                return session.CloneRoot.transform;
            }

            string? path = GetRelativePath(session.SourceRoot.transform, source);
            return string.IsNullOrEmpty(path) ? null : session.CloneRoot.transform.Find(path);
        }

        private static string? GetRelativePath(Transform root, Transform target)
        {
            var parts = new Stack<string>();
            Transform? cur = target;
            while (cur != null && cur != root)
            {
                parts.Push(cur.name);
                cur = cur.parent;
            }
            return cur == root ? string.Join("/", parts.ToArray()) : null;
        }

        private static bool IsPreviewObject(GameObject selected, GameObject cloneRoot)
        {
            return selected != null && cloneRoot != null && (selected == cloneRoot || selected.transform.IsChildOf(cloneRoot.transform));
        }

        private static T? GetObjectField<T>(object target, string fieldName) where T : Object
        {
            return GetField(target, fieldName)?.GetValue(target) as T;
        }

        private static string GetString(object target, string fieldName)
        {
            return GetField(target, fieldName)?.GetValue(target) as string ?? "";
        }

        private static int GetInt(object target, string fieldName)
        {
            object? value = GetField(target, fieldName)?.GetValue(target);
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return 0;
            }
        }

        private static float GetFloat(object target, string fieldName)
        {
            object? value = GetField(target, fieldName)?.GetValue(target);
            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return 0f;
            }
        }

        private static bool GetBool(object target, string fieldName)
        {
            object? value = GetField(target, fieldName)?.GetValue(target);
            return value is bool b && b;
        }

        private static Color GetColor(object target, string fieldName)
        {
            object? value = GetField(target, fieldName)?.GetValue(target);
            return value is Color color ? color : Color.white;
        }

        private static Vector4 GetVector4(object target, string fieldName)
        {
            object? value = GetField(target, fieldName)?.GetValue(target);
            return value is Vector4 vector ? vector : Vector4.zero;
        }

        private static Object? GetGuidObject(object? wrapper)
        {
            return wrapper == null ? null : GetField(wrapper, "objRef")?.GetValue(wrapper) as Object;
        }

        private static FieldInfo? GetField(object target, string fieldName)
        {
            return target == null ? null : FindFieldInHierarchy(target.GetType(), fieldName);
        }

        private static FieldInfo? FindFieldInHierarchy(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }
            return null;
        }

        private static string PreviewName(string type, string toggleName)
        {
            return string.IsNullOrEmpty(toggleName) ? $"{type} Preview" : $"{type}: {toggleName}";
        }
    }
}
