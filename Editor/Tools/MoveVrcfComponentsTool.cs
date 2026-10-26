// MoveVrcfComponentsTool.cs
//
// Moves every VRCFury component on a source GameObject to a destination
// GameObject in one undoable operation. Each VRCFury MonoBehaviour on the
// source is recreated on the destination as its own component (preserves
// serialization shape exactly), then the source is destroyed. Uses
// ComponentUtility.CopyComponent / PasteComponentAsNew, which round-trips
// [SerializeReference] graphs correctly via Unity's own serialization.
//
// Entry points:
//   • GameObject hierarchy right-click  → "WhyKnot/wk-vrcfury-qol/Move all VRCFury
//     components to..." (most ergonomic; the user is already in the hierarchy
//     to pick a destination next).
//   • Top-level menu                    → "GameObject/WhyKnot/wk-vrcfury-qol/Move all
//     VRCFury components to..." (mirrors the right-click).
//
// Both entry points open a small modal-ish EditorWindow where the user picks a
// destination GameObject.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UmeVrcfQol.Internal.Styling;
using UmeVrcfQol.Internal.Utilities;

namespace UmeVrcfQol.Tools {

    internal static class MoveVrcfComponentsTool {

        private const string GameObjectMenuPath = "GameObject/WhyKnot/wk-vrcfury-qol/Move all VRCFury components to...";

        // Hierarchy right-click + GameObject menu. Priority 49 puts it just above
        // Unity's default "Center On Children" group (50).
        [MenuItem(GameObjectMenuPath, false, 49)]
        private static void Open(MenuCommand command) {
            var go = command.context as GameObject ?? Selection.activeGameObject;
            if (go == null) {
                EditorUtility.DisplayDialog("Move VRCFury Components",
                    "Select a GameObject first.", "OK");
                return;
            }
            if (!VrcfQol.Reflection.TryEnsure(out var error)) {
                EditorUtility.DisplayDialog("Move VRCFury Components", error, "OK");
                return;
            }

            var components = go.GetComponents(VrcfQol.Reflection.VRCFuryType);
            if (components == null || components.Length == 0) {
                EditorUtility.DisplayDialog("Move VRCFury Components",
                    $"'{go.name}' has no VRCFury components to move.", "OK");
                return;
            }

            MoveVrcfComponentsWindow.Show(go);
        }

        // Validate so the menu item is greyed out when the selection has no
        // VRCFury components on it. Cheap because we only check the immediate
        // GameObject (the operation is single-source by definition).
        [MenuItem(GameObjectMenuPath, true)]
        private static bool OpenValidate(MenuCommand command) {
            var go = command.context as GameObject ?? Selection.activeGameObject;
            if (go == null) return false;
            if (!VrcfQol.Reflection.TryEnsure(out _)) return false;
            return go.GetComponent(VrcfQol.Reflection.VRCFuryType) != null;
        }

        // ---------- Core move logic, called by the dialog ------------------

        /// <summary>
        /// Performs the move under a single Undo group. Returns the number of
        /// VRCFury components that were on the source (non-zero on success).
        /// On failure throws or shows a dialog, depending on the failure mode.
        /// </summary>
        internal static int Run(GameObject source, GameObject destination) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (source == destination) throw new InvalidOperationException(
                "Source and destination are the same GameObject.");
            if (!VrcfQol.Reflection.TryEnsure(out var error))
                throw new InvalidOperationException(error);

            var r = VrcfQol.Reflection;
            // Snapshot the source's VRCFury components up front. The list will
            // shrink as we destroy them, so iterating GetComponents() during the
            // loop would skip entries.
            var sources = new List<Component>(source.GetComponents(r.VRCFuryType));
            if (sources.Count == 0) return 0;

            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VRCF QoL: Move VRCFury components");
            try {
                foreach (var src in sources) {
                    if (src == null) continue;
                    if (!ComponentUtility.CopyComponent(src))
                        throw new InvalidOperationException(
                            $"CopyComponent failed for {src.name}.");
                    if (!ComponentUtility.PasteComponentAsNew(destination))
                        throw new InvalidOperationException(
                            $"PasteComponentAsNew failed for {src.name}.");
                    // PasteComponentAsNew creates a real component on the
                    // destination (with its own Undo entry); register the
                    // newly added one so the destination is also marked
                    // dirty for save.
                }
                EditorUtility.SetDirty(destination);
                foreach (var src in sources) {
                    if (src == null) continue;
                    Undo.DestroyObjectImmediate(src);
                }
                Undo.CollapseUndoOperations(group);
                VrcfQolLogger.Instance.Info($"Moved {sources.Count} VRCFury component(s) " +
                          $"from '{PathUtility.GetGameObjectPath(source)}' to " +
                          $"'{PathUtility.GetGameObjectPath(destination)}'.");
                return sources.Count;
            } catch (Exception ex) {
                Undo.RevertAllInCurrentGroup();
                VrcfQolLogger.Instance.Exception(ex);
                throw;
            }
        }
    }

    // ---------------- Modal dialog window ---------------------------------

    internal sealed class MoveVrcfComponentsWindow : EditorWindow {
        private GameObject _source;
        private GameObject _destination;
        private Vector2 _pageScroll;
        private string _autoSizeSignature;

        internal static void Show(GameObject source) {
            var w = CreateInstance<MoveVrcfComponentsWindow>();
            w._source = source;
            w.titleContent = WkStyles.TitleContent("Move VRCFury Components");
            // Position near the cursor so it feels like a contextual dialog.
            var p = GUIUtility.GUIToScreenPoint(Event.current?.mousePosition ?? Vector2.zero);
            w.position = new Rect(p.x + 10, p.y + 10, 420, 230);
            w.minSize = new Vector2(420, 230);
            w.ShowUtility();
        }

        private void OnGUI() {
            using var _wkTheme = WkStyles.Scope(WkTheme.VRCFury);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))) {
                WkStyles.TitleBar("Move VRCFury Components");

                using (var s = new EditorGUILayout.ScrollViewScope(
                        _pageScroll, false, false,
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true))) {
                    _pageScroll = s.scrollPosition;
                    if (_source == null) {
                        EditorGUILayout.HelpBox("Source GameObject was deleted. Close this window.", MessageType.Error);
                        if (GUILayout.Button(new GUIContent("Close", "Close this window."))) Close();
                    } else {
                        EditorGUILayout.HelpBox(
                            "Move every VRCFury component from the source GameObject to one destination GameObject. The operation is one Undo step.",
                            MessageType.Info);

                        EditorGUILayout.LabelField(
                            new GUIContent("Source", "The GameObject currently holding the VRCFury components."),
                            new GUIContent(PathUtility.GetGameObjectPath(_source)));

                        using (new EditorGUI.DisabledScope(false)) {
                            _destination = (GameObject)EditorGUILayout.ObjectField(
                                new GUIContent("Destination", "Drop the GameObject that should receive the VRCFury components."),
                                _destination, typeof(GameObject), allowSceneObjects: true);
                        }

                        EditorGUILayout.Space(8);

                        string disabledReason = ValidationError();
                        if (disabledReason != null) {
                            EditorGUILayout.HelpBox(disabledReason, MessageType.Info);
                        }

                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(new GUIContent("Cancel", "Close without moving anything."), GUILayout.Width(80))) {
                                Close();
                            }
                            using (new EditorGUI.DisabledScope(disabledReason != null)) {
                                if (GUILayout.Button(new GUIContent("Move components", "Move the source VRCFury components to the destination."),
                                        GUILayout.Width(140))) {
                                    DoMove();
                                }
                            }
                        }
                    }
                }

                WkStyles.WindowFooter();
            }
            RequestAutoSize();
        }

        private void RequestAutoSize() {
            var sourceId = _source != null ? _source.GetInstanceID() : 0;
            var destinationId = _destination != null ? _destination.GetInstanceID() : 0;
            var signature = $"{sourceId}|{destinationId}|{ValidationError()}";
            WkStyles.AutoSizeWindow(
                this,
                ref _autoSizeSignature,
                signature,
                new Vector2(420f, 230f),
                new Vector2(520f, _source == null ? 260f : 340f),
                new Vector2(680f, 420f));
        }

        private string ValidationError() {
            if (_source == null) return "Source GameObject was deleted.";
            if (_destination == null) return "Pick a destination GameObject.";
            if (_destination == _source) return "Destination must differ from the source.";
            // Same scene check — moving across scenes is technically possible,
            // but the resulting Undo step is split across scenes and tends to
            // confuse users. Easy guardrail.
            if (_destination.scene != _source.scene)
                return "Source and destination must be in the same scene / prefab stage.";
            return null;
        }

        private void DoMove() {
            try {
                int count = MoveVrcfComponentsTool.Run(_source, _destination);
                if (count == 0) {
                    EditorUtility.DisplayDialog("Move VRCFury Components",
                        "No VRCFury components were found on the source.", "OK");
                } else {
                    Selection.activeGameObject = _destination;
                }
                Close();
            } catch (Exception ex) {
                EditorUtility.DisplayDialog("Move VRCFury Components",
                    "Move failed; changes reverted.\n\n" + ex.Message, "OK");
            }
        }
    }
}
