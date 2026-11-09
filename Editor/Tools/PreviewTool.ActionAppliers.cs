#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UmeVrcfQol.Tools
{
    internal static partial class PreviewTool
    {
        private static bool IsPreviewing(Component component, int pageIndex)
        {
            return ActiveSession != null &&
                ActiveSession.CloneRoot != null &&
                component != null &&
                ActiveSession.SourceComponent == component &&
                ActiveSession.PageIndex == pageIndex;
        }

        private static bool ApplyActionToClone(object action, PreviewSession session)
        {
            try
            {
                return action.GetType().Name switch
                {
                    "ObjectToggleAction" => ApplyObjectToggle(action, session),
                    "BlendShapeAction" => ApplyBlendShape(action, session),
                    "MaterialAction" => ApplyMaterialSwap(action, session),
                    "MaterialPropertyAction" => ApplyMaterialProperty(action, session),
                    "AnimationClipAction" => ApplyAnimationClip(action, session),
                    "FlipbookAction" => ApplyPoiyomiFlipbookFrame(action, session),
                    "PoiyomiUVTileAction" => ApplyPoiyomiUvTile(action, session),
                    "ScaleAction" => ApplyScale(action, session),
                    "FlipBookBuilderAction" or "FxFloatAction" or "ResetPhysboneAction" or "WorldDropAction" or "SpsOnAction" or "BlockBlinkingAction" or "BlockVisemesAction" or "DisableGesturesAction" => false,
                    _ => false,
                };
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                VrcfQolLogger.Instance.Warning($"Preview skipped {action.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static bool ApplyObjectToggle(object action, PreviewSession session)
        {
            GameObject? obj = MapObject(GetObjectField<GameObject>(action, "obj"), session);
            if (obj == null)
            {
                return false;
            }

            string mode = GetField(action, "mode")?.GetValue(action)?.ToString() ?? "TurnOn";
            if (string.Equals(mode, "TurnOff", StringComparison.Ordinal))
            {
                obj.SetActive(value: false);
            }
            else if (string.Equals(mode, "Toggle", StringComparison.Ordinal))
            {
                obj.SetActive(!obj.activeSelf);
            }
            else
            {
                obj.SetActive(value: true);
            }

            AddFocus(session, obj);
            return true;
        }

        private static bool ApplyBlendShape(object action, PreviewSession session)
        {
            string shape = GetString(action, "blendShape");
            if (string.IsNullOrEmpty(shape))
            {
                return false;
            }

            float value = GetFloat(action, "blendShapeValue");
            var targets = new List<SkinnedMeshRenderer>();
            if (GetBool(action, "allRenderers"))
            {
                targets.AddRange(session.CloneRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true));
            }
            else
            {
                var renderer = MapObject(GetObjectField<Renderer>(action, "renderer"), session) as SkinnedMeshRenderer;
                if (renderer != null)
                {
                    targets.Add(renderer);
                }
            }

            bool applied = false;
            foreach (SkinnedMeshRenderer target in targets)
            {
                if (target == null || target.sharedMesh == null)
                {
                    continue;
                }

                int idx = target.sharedMesh.GetBlendShapeIndex(shape);
                if (idx < 0)
                {
                    continue;
                }

                target.SetBlendShapeWeight(idx, value);
                AddFocus(session, target);
                applied = true;
            }
            return applied;
        }

        private static bool ApplyMaterialSwap(object action, PreviewSession session)
        {
            Renderer? renderer = MapObject(GetObjectField<Renderer>(action, "renderer"), session);
            var mat = GetGuidObject(GetField(action, "mat")?.GetValue(action)) as Material;
            if (renderer == null || mat == null)
            {
                return false;
            }

            int index = GetInt(action, "materialIndex");
            Material[] mats = renderer.sharedMaterials;
            if (index < 0 || index >= mats.Length)
            {
                return false;
            }

            mats[index] = mat;
            renderer.sharedMaterials = mats;
            AddFocus(session, renderer);
            return true;
        }

        private static bool ApplyMaterialProperty(object action, PreviewSession session)
        {
            string propertyName = GetString(action, "propertyName");
            if (string.IsNullOrEmpty(propertyName) || propertyName.Contains('.', StringComparison.Ordinal))
            {
                return false;
            }

            var renderers = new List<Renderer>();
            if (GetBool(action, "affectAllMeshes"))
            {
                renderers.AddRange(session.CloneRoot.GetComponentsInChildren<Renderer>(includeInactive: true));
            }
            else
            {
                GameObject? target = MapObject(GetObjectField<GameObject>(action, "renderer2"), session);
                Renderer? renderer = target != null ? target.GetComponent<Renderer>() : null;
                if (renderer != null)
                {
                    renderers.Add(renderer);
                }
            }

            string type = GetField(action, "propertyType")?.GetValue(action)?.ToString() ?? "Float";
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                if (string.Equals(type, "Color", StringComparison.Ordinal))
                {
                    block.SetColor(propertyName, GetColor(action, "valueColor"));
                }
                else if (type is "Vector" or "St")
                {
                    block.SetVector(propertyName, GetVector4(action, "valueVector"));
                }
                else
                {
                    block.SetFloat(propertyName, GetFloat(action, "value"));
                }

                renderer.SetPropertyBlock(block);
                AddFocus(session, renderer);
            }
            return renderers.Count > 0;
        }

        private static bool ApplyAnimationClip(object action, PreviewSession session)
        {
            var clip = GetGuidObject(GetField(action, "clip")?.GetValue(action)) as AnimationClip;
            if (clip == null)
            {
                return false;
            }

            clip.SampleAnimation(session.CloneRoot, Mathf.Max(0f, clip.length));
            AddFocus(session, session.CloneRoot);
            return true;
        }

        private static bool ApplyPoiyomiFlipbookFrame(object action, PreviewSession session)
        {
            Renderer? renderer = MapObject(GetObjectField<Renderer>(action, "renderer"), session);
            if (renderer == null)
            {
                return false;
            }

            SetRendererFloat(renderer, "_FlipbookCurrentFrame", Mathf.Floor(GetInt(action, "frame")) + 0.5f);
            AddFocus(session, renderer);
            return true;
        }

        private static bool ApplyPoiyomiUvTile(object action, PreviewSession session)
        {
            Renderer? renderer = MapObject(GetObjectField<Renderer>(action, "renderer"), session);
            if (renderer == null)
            {
                return false;
            }

            string prop = GetBool(action, "dissolve") ? "_UVTileDissolveAlpha_Row" : "_UDIMDiscardRow";
            prop += $"{GetInt(action, "row")}_{GetInt(action, "column")}";
            string renamed = GetString(action, "renamedMaterial");
            if (!string.IsNullOrEmpty(renamed))
            {
                prop += $"_{renamed}";
            }

            SetRendererFloat(renderer, prop, 0f);
            AddFocus(session, renderer);
            return true;
        }

        private static bool ApplyScale(object action, PreviewSession session)
        {
            GameObject? obj = MapObject(GetObjectField<GameObject>(action, "obj"), session);
            if (obj == null)
            {
                return false;
            }

            obj.transform.localScale *= GetFloat(action, "scale");
            AddFocus(session, obj);
            return true;
        }

        private static void SetRendererFloat(Renderer renderer, string propertyName, float value)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(propertyName, value);
            renderer.SetPropertyBlock(block);
        }
    }
}
