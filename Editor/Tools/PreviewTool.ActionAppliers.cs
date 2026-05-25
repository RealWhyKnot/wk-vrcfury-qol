// PreviewTool.ActionAppliers.cs

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

        private static bool IsPreviewing(Component component, int pageIndex) {
            return _active != null &&
                _active.CloneRoot != null &&
                component != null &&
                _active.SourceComponent == component &&
                _active.PageIndex == pageIndex;
        }

        private static bool ApplyActionToClone(object action, PreviewSession session) {
            try {
                switch (action.GetType().Name) {
                    case "ObjectToggleAction":
                        return ApplyObjectToggle(action, session);
                    case "BlendShapeAction":
                        return ApplyBlendShape(action, session);
                    case "MaterialAction":
                        return ApplyMaterialSwap(action, session);
                    case "MaterialPropertyAction":
                        return ApplyMaterialProperty(action, session);
                    case "AnimationClipAction":
                        return ApplyAnimationClip(action, session);
                    case "FlipbookAction":
                        return ApplyPoiyomiFlipbookFrame(action, session);
                    case "PoiyomiUVTileAction":
                        return ApplyPoiyomiUvTile(action, session);
                    case "ScaleAction":
                        return ApplyScale(action, session);
                    case "FlipBookBuilderAction":
                    case "FxFloatAction":
                    case "ResetPhysboneAction":
                    case "WorldDropAction":
                    case "SpsOnAction":
                    case "BlockBlinkingAction":
                    case "BlockVisemesAction":
                    case "DisableGesturesAction":
                        return false;
                    default:
                        return false;
                }
            } catch (Exception ex) {
                VrcfQolLogger.Instance.Warning($"Preview skipped {action.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static bool ApplyObjectToggle(object action, PreviewSession session) {
            var obj = MapObject(GetObjectField<GameObject>(action, "obj"), session);
            if (obj == null) return false;
            var mode = GetField(action, "mode")?.GetValue(action)?.ToString() ?? "TurnOn";
            if (mode == "TurnOff") obj.SetActive(false);
            else if (mode == "Toggle") obj.SetActive(!obj.activeSelf);
            else obj.SetActive(true);
            AddFocus(session, obj);
            return true;
        }

        private static bool ApplyBlendShape(object action, PreviewSession session) {
            var shape = GetString(action, "blendShape");
            if (string.IsNullOrEmpty(shape)) return false;
            var value = GetFloat(action, "blendShapeValue");
            var targets = new List<SkinnedMeshRenderer>();
            if (GetBool(action, "allRenderers")) {
                targets.AddRange(session.CloneRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            } else {
                var renderer = MapObject(GetObjectField<Renderer>(action, "renderer"), session) as SkinnedMeshRenderer;
                if (renderer != null) targets.Add(renderer);
            }

            var applied = false;
            foreach (var target in targets) {
                if (target == null || target.sharedMesh == null) continue;
                var idx = target.sharedMesh.GetBlendShapeIndex(shape);
                if (idx < 0) continue;
                target.SetBlendShapeWeight(idx, value);
                AddFocus(session, target);
                applied = true;
            }
            return applied;
        }

        private static bool ApplyMaterialSwap(object action, PreviewSession session) {
            var renderer = MapObject(GetObjectField<Renderer>(action, "renderer"), session);
            var mat = GetGuidObject(GetField(action, "mat")?.GetValue(action)) as Material;
            if (renderer == null || mat == null) return false;
            var index = GetInt(action, "materialIndex");
            var mats = renderer.sharedMaterials;
            if (index < 0 || index >= mats.Length) return false;
            mats[index] = mat;
            renderer.sharedMaterials = mats;
            AddFocus(session, renderer);
            return true;
        }

        private static bool ApplyMaterialProperty(object action, PreviewSession session) {
            var propertyName = GetString(action, "propertyName");
            if (string.IsNullOrEmpty(propertyName) || propertyName.Contains(".")) return false;

            var renderers = new List<Renderer>();
            if (GetBool(action, "affectAllMeshes")) {
                renderers.AddRange(session.CloneRoot.GetComponentsInChildren<Renderer>(true));
            } else {
                var target = MapObject(GetObjectField<GameObject>(action, "renderer2"), session);
                var renderer = target != null ? target.GetComponent<Renderer>() : null;
                if (renderer != null) renderers.Add(renderer);
            }

            var type = GetField(action, "propertyType")?.GetValue(action)?.ToString() ?? "Float";
            foreach (var renderer in renderers) {
                if (renderer == null) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                if (type == "Color") block.SetColor(propertyName, GetColor(action, "valueColor"));
                else if (type == "Vector" || type == "St") block.SetVector(propertyName, GetVector4(action, "valueVector"));
                else block.SetFloat(propertyName, GetFloat(action, "value"));
                renderer.SetPropertyBlock(block);
                AddFocus(session, renderer);
            }
            return renderers.Count > 0;
        }

        private static bool ApplyAnimationClip(object action, PreviewSession session) {
            var clip = GetGuidObject(GetField(action, "clip")?.GetValue(action)) as AnimationClip;
            if (clip == null) return false;
            clip.SampleAnimation(session.CloneRoot, Mathf.Max(0f, clip.length));
            AddFocus(session, session.CloneRoot);
            return true;
        }

        private static bool ApplyPoiyomiFlipbookFrame(object action, PreviewSession session) {
            var renderer = MapObject(GetObjectField<Renderer>(action, "renderer"), session);
            if (renderer == null) return false;
            SetRendererFloat(renderer, "_FlipbookCurrentFrame", Mathf.Floor(GetInt(action, "frame")) + 0.5f);
            AddFocus(session, renderer);
            return true;
        }

        private static bool ApplyPoiyomiUvTile(object action, PreviewSession session) {
            var renderer = MapObject(GetObjectField<Renderer>(action, "renderer"), session);
            if (renderer == null) return false;
            var prop = GetBool(action, "dissolve") ? "_UVTileDissolveAlpha_Row" : "_UDIMDiscardRow";
            prop += $"{GetInt(action, "row")}_{GetInt(action, "column")}";
            var renamed = GetString(action, "renamedMaterial");
            if (!string.IsNullOrEmpty(renamed)) prop += $"_{renamed}";
            SetRendererFloat(renderer, prop, 0f);
            AddFocus(session, renderer);
            return true;
        }

        private static bool ApplyScale(object action, PreviewSession session) {
            var obj = MapObject(GetObjectField<GameObject>(action, "obj"), session);
            if (obj == null) return false;
            obj.transform.localScale *= GetFloat(action, "scale");
            AddFocus(session, obj);
            return true;
        }

        private static void SetRendererFloat(Renderer renderer, string propertyName, float value) {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(propertyName, value);
            renderer.SetPropertyBlock(block);
        }
    }
}
