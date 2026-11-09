#nullable enable

using System;
using System.Collections.Generic;
using UmeVrcfQol.Internal.Utilities;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UmeVrcfQol.Tools
{
    internal static class MoveVrcfComponentsTool
    {
        private const string GameObjectMenuPath = "GameObject/WhyKnot/wk-vrcfury-qol/Move all VRCFury components to...";

        [MenuItem(GameObjectMenuPath, false, 49)]
        private static void Open(MenuCommand command)
        {
            GameObject go = TargetOf(command);
            if (go == null)
            {
                EditorUtility.DisplayDialog("Move VRCFury Components",
                    "Select a GameObject first.", "OK");
                return;
            }
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out string? error))
            {
                EditorUtility.DisplayDialog("Move VRCFury Components", error, "OK");
                return;
            }

            Component[] components = go.GetComponents(r.VRCFuryType);
            if (components == null || components.Length == 0)
            {
                EditorUtility.DisplayDialog("Move VRCFury Components",
                    $"'{go.name}' has no VRCFury components to move.", "OK");
                return;
            }

            MoveVrcfComponentsWindow.Show(go);
        }

        [MenuItem(GameObjectMenuPath, true)]
        private static bool OpenValidate(MenuCommand command)
        {
            GameObject go = TargetOf(command);
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            return go != null && r.TryEnsure(out _) && go.GetComponent(r.VRCFuryType) != null;
        }

        private static GameObject TargetOf(MenuCommand command)
        {
            var context = command.context as GameObject;
            return context != null ? context : Selection.activeGameObject;
        }

        internal static int Run(GameObject source, GameObject destination)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (source == destination)
            {
                throw new InvalidOperationException(
                "Source and destination are the same GameObject.");
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out string? error))
            {
                throw new InvalidOperationException(error);
            }

            var sources = new List<Component>(source.GetComponents(r.VRCFuryType));
            return sources.Count == 0 ? 0 : MoveAll(sources, source, destination);
        }

        private static int MoveAll(List<Component> sources, GameObject source, GameObject destination)
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VRCF QoL: Move VRCFury components");
            try
            {
                foreach (Component src in sources)
                {
                    if (src == null)
                    {
                        continue;
                    }

                    if (!ComponentUtility.CopyComponent(src))
                    {
                        throw new InvalidOperationException(
                            $"CopyComponent failed for {src.name}.");
                    }

                    if (!ComponentUtility.PasteComponentAsNew(destination))
                    {
                        throw new InvalidOperationException(
                            $"PasteComponentAsNew failed for {src.name}.");
                    }
                }
                EditorUtility.SetDirty(destination);
                foreach (Component src in sources)
                {
                    if (src == null)
                    {
                        continue;
                    }

                    Undo.DestroyObjectImmediate(src);
                }
                Undo.CollapseUndoOperations(group);
                VrcfQolLogger.Instance.Info($"Moved {sources.Count} VRCFury component(s) " +
                          $"from '{PathUtility.GetGameObjectPath(source)}' to " +
                          $"'{PathUtility.GetGameObjectPath(destination)}'.");
                return sources.Count;
            }
            catch (Exception ex)
            {
                Undo.RevertAllInCurrentGroup();
                VrcfQolLogger.Instance.Exception(ex);
                throw;
            }
        }
    }
}
