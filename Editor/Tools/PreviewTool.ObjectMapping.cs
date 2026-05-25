// PreviewTool.ObjectMapping.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools {

    internal static partial class PreviewTool {

        internal static GameObject FindAvatarRoot(Component component) {
            if (component == null) return null;
            var animator = component.GetComponentInParent<Animator>(true);
            if (animator != null) return animator.gameObject;
            var t = component.transform;
            while (t.parent != null) t = t.parent;
            return t.gameObject;
        }

        private static void AlignCloneWithSource(GameObject source, GameObject clone) {
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;
        }

        private static Bounds CalculateBounds(GameObject root, bool visibleOnly = false) {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(root.transform.position, Vector3.one);
            var hasBounds = false;
            foreach (var renderer in renderers) {
                if (renderer == null) continue;
                if (visibleOnly && (!renderer.enabled || !renderer.gameObject.activeInHierarchy)) continue;
                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        private static void MarkHierarchyTemporary(GameObject clone) {
            foreach (var transform in clone.GetComponentsInChildren<Transform>(true)) {
                transform.gameObject.hideFlags = HideFlags.DontSave;
                foreach (var component in transform.GetComponents<Component>()) {
                    if (component != null) component.hideFlags = HideFlags.DontSave;
                }
            }
        }

        private static void StripPreviewComponents(GameObject clone) {
            foreach (var component in clone.GetComponentsInChildren<Component>(true)) {
                if (component == null) continue;
                if (component.GetType().FullName == "VF.Model.VRCFury") {
                    Object.DestroyImmediate(component);
                }
            }
        }

        private static void AddFocus(PreviewSession session, Renderer renderer) {
            if (session == null || renderer == null) return;
            EncapsulateFocus(session, renderer.bounds);
        }

        private static void AddFocus(PreviewSession session, GameObject obj) {
            if (session == null || obj == null) return;
            EncapsulateFocus(session, CalculateBounds(obj));
        }

        private static void EncapsulateFocus(PreviewSession session, Bounds bounds) {
            if (!session.HasFocusBounds) {
                session.FocusBounds = bounds;
                session.HasFocusBounds = true;
            } else {
                session.FocusBounds.Encapsulate(bounds);
            }
        }

        private static T MapObject<T>(T source, PreviewSession session) where T : Object {
            if (source == null || session == null || session.SourceRoot == null || session.CloneRoot == null) return null;
            if (source is GameObject go) {
                var mapped = MapTransform(go.transform, session);
                return mapped != null ? mapped.gameObject as T : null;
            }
            if (source is Component component) {
                var mappedTransform = MapTransform(component.transform, session);
                if (mappedTransform == null) return null;
                var sourceComponents = component.gameObject.GetComponents(component.GetType());
                var index = Array.IndexOf(sourceComponents, component);
                var mappedComponents = mappedTransform.gameObject.GetComponents(component.GetType());
                if (index >= 0 && index < mappedComponents.Length) return mappedComponents[index] as T;
                return mappedTransform.GetComponent(component.GetType()) as T;
            }
            return source;
        }

        private static Transform MapTransform(Transform source, PreviewSession session) {
            if (source == null) return null;
            if (source == session.SourceRoot.transform) return session.CloneRoot.transform;
            var path = GetRelativePath(session.SourceRoot.transform, source);
            return string.IsNullOrEmpty(path) ? null : session.CloneRoot.transform.Find(path);
        }

        private static string GetRelativePath(Transform root, Transform target) {
            var parts = new Stack<string>();
            var cur = target;
            while (cur != null && cur != root) {
                parts.Push(cur.name);
                cur = cur.parent;
            }
            return cur == root ? string.Join("/", parts.ToArray()) : null;
        }

        private static bool IsPreviewObject(GameObject selected, GameObject cloneRoot) {
            if (selected == null || cloneRoot == null) return false;
            return selected == cloneRoot || selected.transform.IsChildOf(cloneRoot.transform);
        }

        private static T GetObjectField<T>(object target, string fieldName) where T : Object {
            return GetField(target, fieldName)?.GetValue(target) as T;
        }

        private static string GetString(object target, string fieldName) {
            return GetField(target, fieldName)?.GetValue(target) as string ?? "";
        }

        private static int GetInt(object target, string fieldName) {
            var value = GetField(target, fieldName)?.GetValue(target);
            try { return Convert.ToInt32(value); } catch { return 0; }
        }

        private static float GetFloat(object target, string fieldName) {
            var value = GetField(target, fieldName)?.GetValue(target);
            try { return Convert.ToSingle(value); } catch { return 0f; }
        }

        private static bool GetBool(object target, string fieldName) {
            var value = GetField(target, fieldName)?.GetValue(target);
            return value is bool b && b;
        }

        private static Color GetColor(object target, string fieldName) {
            var value = GetField(target, fieldName)?.GetValue(target);
            return value is Color color ? color : Color.white;
        }

        private static Vector4 GetVector4(object target, string fieldName) {
            var value = GetField(target, fieldName)?.GetValue(target);
            return value is Vector4 vector ? vector : Vector4.zero;
        }

        private static Object GetGuidObject(object wrapper) {
            return wrapper == null ? null : GetField(wrapper, "objRef")?.GetValue(wrapper) as Object;
        }

        private static FieldInfo GetField(object target, string fieldName) {
            return target == null ? null : FindFieldInHierarchy(target.GetType(), fieldName);
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string name) {
            while (type != null) {
                var field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private static string PreviewName(string type, string toggleName) {
            return string.IsNullOrEmpty(toggleName) ? $"{type} Preview" : $"{type}: {toggleName}";
        }
    }
}
