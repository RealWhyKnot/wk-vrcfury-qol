// ReplaceReferencesWindow.cs
//
// EditorWindow that lists every Object reference inside any VRCFury component
// on a set of selected GameObjects, GROUPED BY the underlying object so each
// unique reference appears once with one drop target. Dragging a
// replacement onto the rows they want to swap and clicks Apply - every
// occurrence of that object across the scan is replaced in a single Undo step.
//
// Per-selection "include children" toggles let you scan a GameObject without
// recursing into its descendants - useful when two avatars share a parent or
// when you want to pin the scan to a single component.
//
// Why SerializedObject + SerializedProperty.NextVisible(true) instead of raw
// reflection: VRCFury features are [SerializeReference] polymorphic graphs,
// and Unity's SerializedProperty already descends into them safely. This is
// also future-proof - if VRCFury renames internal fields but keeps them
// serialized, the walk still works because we only care about
// `propertyType == ObjectReference`.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Styling;
using UmeVrcfQol.Internal.Utilities;

namespace UmeVrcfQol.Tools {

    internal sealed partial class ReplaceReferencesWindow : EditorWindow {

        // Persisted across domain reloads so the search list survives a script
        // recompile while the window is open.
        [SerializeField] private List<SearchRoot> _searchRoots = new List<SearchRoot>();

        // Scan output. Groups are recomputed on every scan; replacements live
        // on each group and are dropped on rescan (intentional - a domain
        // reload may have invalidated a queued replacement target).
        private readonly List<RefGroup> _groups = new List<RefGroup>();
        private bool _hideUnchanged;
        private string _scanSummary = "";

        private Vector2 _rootsScroll;
        private Vector2 _groupsScroll;

        // ---------------- Public entry point ------------------------------

        internal static void Open(bool prefillFromSelection) {
            var w = GetWindow<ReplaceReferencesWindow>(false, "Replace References", true);
            w.titleContent = new GUIContent("WhyKnot - Replace VRCFury References");
            w.minSize = new Vector2(620, 460);
            if (prefillFromSelection) {
                w._searchRoots = Selection.gameObjects
                    .Where(g => g != null)
                    .Distinct()
                    .Select(g => new SearchRoot { GameObject = g, IncludeChildren = true })
                    .ToList();
                w.Rescan();
            }
            w.Show();
            w.Focus();
        }

        // ---------------- GUI ---------------------------------------------

        private void OnGUI() {
            using var _wkTheme = WkStyles.Scope(WkTheme.VRCFury);
            DrawIntro();
            DrawSearchRoots();
            EditorGUILayout.Space(4);
            DrawDivider();
            DrawGroups();
            DrawDivider();
            DrawApplyBar();
        }

        private static void DrawIntro() {
            EditorGUILayout.HelpBox(
                "Replace references in three steps: choose the GameObjects to search, drop the new object onto each row you want to change, then apply the queued replacements.",
                MessageType.Info);
        }

        // -------- Search roots panel --------

        private void DrawSearchRoots() {
            EditorGUILayout.LabelField(
                new GUIContent("1. Search these GameObjects",
                    "Pick the avatar, outfit, or prop roots that contain the VRCFury components you want to scan."),
                EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(60), GUILayout.MaxHeight(150))) {
                _rootsScroll = EditorGUILayout.BeginScrollView(_rootsScroll);
                if (_searchRoots.Count == 0) {
                    EditorGUILayout.LabelField(
                        new GUIContent("Select GameObjects in the Hierarchy, then click 'Use selected'.",
                            "The scan only looks inside the GameObjects listed here."),
                        EditorStyles.centeredGreyMiniLabel);
                } else {
                    int removeIndex = -1;
                    bool dirty = false;
                    for (int i = 0; i < _searchRoots.Count; i++) {
                        var entry = _searchRoots[i];
                        using (new EditorGUILayout.HorizontalScope()) {
                            var newGo = (GameObject)EditorGUILayout.ObjectField(
                                GUIContent.none, entry.GameObject, typeof(GameObject), allowSceneObjects: true);
                            if (newGo != entry.GameObject) { entry.GameObject = newGo; dirty = true; }

                            var newIncludeChildren = GUILayout.Toggle(entry.IncludeChildren,
                                new GUIContent("Include children",
                                    "When on, scan this GameObject and all descendants. Turn off to scan only this exact GameObject."),
                                EditorStyles.miniButton, GUILayout.Width(108));
                            if (newIncludeChildren != entry.IncludeChildren) {
                                entry.IncludeChildren = newIncludeChildren;
                                dirty = true;
                            }

                            if (GUILayout.Button(new GUIContent("x", "Remove this GameObject from the search list."),
                                    EditorStyles.miniButton, GUILayout.Width(22))) {
                                removeIndex = i;
                            }
                        }
                    }
                    if (removeIndex >= 0) { _searchRoots.RemoveAt(removeIndex); dirty = true; }
                    if (dirty) Rescan();
                }
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button(new GUIContent("Use selected",
                        "Replace the search list with the currently selected GameObjects."))) {
                    _searchRoots = Selection.gameObjects
                        .Where(g => g != null)
                        .Distinct()
                        .Select(g => new SearchRoot { GameObject = g, IncludeChildren = true })
                        .ToList();
                    Rescan();
                }
                if (GUILayout.Button(new GUIContent("Add selected",
                        "Add the currently selected GameObjects to the search list."))) {
                    foreach (var g in Selection.gameObjects) {
                        if (g == null) continue;
                        if (_searchRoots.Any(r => r.GameObject == g)) continue;
                        _searchRoots.Add(new SearchRoot { GameObject = g, IncludeChildren = true });
                    }
                    Rescan();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Clear", "Empty the search list."), GUILayout.Width(70))) {
                    _searchRoots.Clear();
                    _groups.Clear();
                    _scanSummary = "";
                }
            }
        }

        // -------- Groups list --------

        private void DrawGroups() {
            using (new EditorGUILayout.HorizontalScope()) {
                int queued = _groups.Count(g => g.HasReplacement);
                EditorGUILayout.LabelField(_groups.Count > 0
                        ? $"2. Pick replacements ({_groups.Count} unique, {queued} queued)"
                        : "2. Pick replacements",
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                _hideUnchanged = GUILayout.Toggle(_hideUnchanged,
                    new GUIContent("Show queued only", "Only show rows that already have a replacement queued."),
                    EditorStyles.miniButton, GUILayout.Width(118));
            }
            if (!string.IsNullOrEmpty(_scanSummary)) {
                EditorGUILayout.LabelField(_scanSummary, EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true))) {
                _groupsScroll = EditorGUILayout.BeginScrollView(_groupsScroll);
                if (_searchRoots.Count == 0) {
                    EditorGUILayout.LabelField("Step 1: add GameObjects above to begin.", EditorStyles.centeredGreyMiniLabel);
                } else if (_groups.Count == 0) {
                    EditorGUILayout.LabelField("No replaceable object references found in the selected roots.", EditorStyles.centeredGreyMiniLabel);
                } else {
                    foreach (var g in _groups) {
                        if (_hideUnchanged && !g.HasReplacement) continue;
                        DrawGroupRow(g);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawGroupRow(RefGroup g) {
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope()) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        var typeName = g.CurrentValue == null ? "Object" : g.CurrentValue.GetType().Name;
                        EditorGUILayout.LabelField(
                            $"{(g.CurrentValue == null ? "(null)" : g.CurrentValue.name)}  ({typeName})",
                            EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(
                            g.Sites.Count == 1 ? "1 reference" : $"{g.Sites.Count} references",
                            EditorStyles.miniLabel, GUILayout.Width(110));
                    }
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.LabelField(
                            new GUIContent("Current", "The object currently used by one or more VRCFury fields."),
                            GUILayout.Width(82));
                        using (new EditorGUI.DisabledScope(true)) {
                            EditorGUILayout.ObjectField(g.CurrentValue, typeof(Object), allowSceneObjects: true);
                        }
                    }
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.LabelField(
                            new GUIContent("Replace with", "Drop the object that should replace the current one. Leave empty to keep this row unchanged."),
                            GUILayout.Width(82));
                        var newReplacement = EditorGUILayout.ObjectField(
                            g.Replacement, typeof(Object), allowSceneObjects: true);
                        if (newReplacement != g.Replacement) g.Replacement = newReplacement;
                    }
                    if (g.Sites.Count > 1) {
                        g.Foldout = EditorGUILayout.Foldout(g.Foldout, $"Locations ({g.Sites.Count})", true);
                        if (g.Foldout) {
                            EditorGUI.indentLevel++;
                            foreach (var s in g.Sites) {
                                using (new EditorGUILayout.HorizontalScope()) {
                                    EditorGUILayout.LabelField(
                                        $"• {s.GameObjectPath} ▸ {s.FeatureType}",
                                        EditorStyles.miniLabel);
                                    if (GUILayout.Button(new GUIContent("Ping", "Highlight this VRCFury component in the Hierarchy."),
                                            EditorStyles.miniButton, GUILayout.Width(44))) {
                                        if (s.VrcfComponent != null) EditorGUIUtility.PingObject(s.VrcfComponent);
                                    }
                                }
                                EditorGUILayout.LabelField($"   {s.PropertyPath}", EditorStyles.miniLabel);
                            }
                            EditorGUI.indentLevel--;
                        }
                    } else if (g.Sites.Count == 1) {
                        var s = g.Sites[0];
                        EditorGUILayout.LabelField(
                            $"{s.GameObjectPath}  ▸  {s.FeatureType}  —  {s.PropertyPath}",
                            EditorStyles.miniLabel);
                    }
                }
                if (GUILayout.Button(new GUIContent("Ping", "Highlight one of the VRCFury components in the Hierarchy."),
                        EditorStyles.miniButton, GUILayout.Width(44))) {
                    var first = g.Sites.FirstOrDefault();
                    if (first != null && first.VrcfComponent != null)
                        EditorGUIUtility.PingObject(first.VrcfComponent);
                }
            }
            EditorGUILayout.Space(2);
            DrawSubtleDivider();
        }

        // -------- Apply bar --------

        private void DrawApplyBar() {
            using (new EditorGUILayout.HorizontalScope()) {
                int queuedSites = _groups.Where(g => g.HasReplacement).Sum(g => g.Sites.Count);
                using (new EditorGUI.DisabledScope(queuedSites == 0)) {
                    var label = queuedSites > 0
                        ? $"3. Apply {queuedSites} Replacement{(queuedSites == 1 ? "" : "s")}"
                        : "3. Apply";
                    if (GUILayout.Button(new GUIContent(label,
                            "Apply every queued replacement in one Undo step. Rows without a replacement are left untouched."),
                        GUILayout.Height(24), GUILayout.MinWidth(180))) {
                        Apply();
                    }
                }
                using (new EditorGUI.DisabledScope(_searchRoots.Count == 0)) {
                    if (GUILayout.Button(new GUIContent("Re-scan", "Re-scan the search roots and refresh the reference list."),
                            GUILayout.Height(24), GUILayout.Width(88))) {
                        Rescan();
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Close", "Close this window. No queued replacements are applied."),
                        GUILayout.Height(24), GUILayout.Width(80))) Close();
            }
        }

        private static void DrawDivider() {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.18f));
        }

        private static void DrawSubtleDivider() {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.06f));
        }



    }
}
