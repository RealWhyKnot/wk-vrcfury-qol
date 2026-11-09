#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UmeVrcfQol.Internal.Styling;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools
{
    internal sealed partial class ReplaceReferencesWindow : EditorWindow
    {
        private readonly List<RefGroup> _groups = new();
        private List<SearchRoot> _searchRoots = new();
        private bool _hideUnchanged;
        private string _scanSummary = "";

        private Vector2 _rootsScroll;
        private Vector2 _groupsScroll;
        private Vector2 _pageScroll;
        private string? _autoSizeSignature;

        internal static void Open(bool prefillFromSelection)
        {
            ReplaceReferencesWindow w = GetWindow<ReplaceReferencesWindow>(utility: false, "Replace References", focus: true);
            w.titleContent = WkStyles.TitleContent("WhyKnot - Replace VRCFury References");
            w.minSize = new Vector2(620, 460);
            if (prefillFromSelection)
            {
                w._searchRoots = SelectedRoots();
                w.Rescan();
            }
            w.Show();
            w.Focus();
        }

        private static List<SearchRoot> SelectedRoots()
        {
            return Selection.gameObjects
                .Where(g => g != null)
                .Distinct()
                .Select(g => new SearchRoot(g, includeChildren: true))
                .ToList();
        }

        private void OnGUI()
        {
            using IDisposable themeScope = WkStyles.Scope(WkTheme.VRCFury);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(expand: true), GUILayout.ExpandHeight(expand: true)))
            {
                WkStyles.TitleBar("Replace References");

                using (var s = new EditorGUILayout.ScrollViewScope(
                        _pageScroll, alwaysShowHorizontal: false, alwaysShowVertical: false,
                        GUILayout.ExpandWidth(expand: true),
                        GUILayout.ExpandHeight(expand: true)))
                {
                    _pageScroll = s.scrollPosition;
                    DrawIntro();
                    DrawSearchRoots();
                    EditorGUILayout.Space(4);
                    DrawDivider();
                    DrawGroups();
                }

                DrawDivider();
                DrawApplyBar();
                WkStyles.WindowFooter();
            }
            RequestAutoSize();
        }

        private static void DrawIntro()
        {
            EditorGUILayout.HelpBox(
                "Replace references in three steps: choose the GameObjects to search, drop the new object onto each row you want to change, then apply the queued replacements.",
                MessageType.Info);
        }

        private void DrawSearchRoots()
        {
            EditorGUILayout.LabelField(
                new GUIContent("1. Search these GameObjects",
                    "Pick the avatar, outfit, or prop roots that contain the VRCFury components you want to scan."),
                EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(
                    EditorStyles.helpBox,
                    GUILayout.Height(WkStyles.CappedListHeight(_searchRoots.Count, 24f, 60f, 150f))))
            {
                _rootsScroll = EditorGUILayout.BeginScrollView(_rootsScroll);
                if (_searchRoots.Count == 0)
                {
                    EditorGUILayout.LabelField(
                        new GUIContent("Select GameObjects in the Hierarchy, then click 'Use selected'.",
                            "The scan only looks inside the GameObjects listed here."),
                        EditorStyles.centeredGreyMiniLabel);
                }
                else if (DrawSearchRootRows())
                {
                    Rescan();
                }
                EditorGUILayout.EndScrollView();
            }

            DrawSearchRootButtons();
        }

        private bool DrawSearchRootRows()
        {
            int removeIndex = -1;
            bool dirty = false;
            for (int i = 0; i < _searchRoots.Count; i++)
            {
                dirty |= DrawSearchRootRow(_searchRoots[i], out bool remove);
                if (remove)
                {
                    removeIndex = i;
                }
            }

            if (removeIndex >= 0)
            {
                _searchRoots.RemoveAt(removeIndex);
                dirty = true;
            }

            return dirty;
        }

        private static bool DrawSearchRootRow(SearchRoot entry, out bool remove)
        {
            bool dirty = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                var newGo = (GameObject)EditorGUILayout.ObjectField(
                    GUIContent.none, entry.GameObject, typeof(GameObject), allowSceneObjects: true);
                if (newGo != entry.GameObject)
                {
                    entry.GameObject = newGo;
                    dirty = true;
                }

                bool newIncludeChildren = GUILayout.Toggle(entry.IncludeChildren,
                    new GUIContent("Include children",
                        "When on, scan this GameObject and all descendants. Turn off to scan only this exact GameObject."),
                    EditorStyles.miniButton, GUILayout.Width(108));
                if (newIncludeChildren != entry.IncludeChildren)
                {
                    entry.IncludeChildren = newIncludeChildren;
                    dirty = true;
                }

                remove = GUILayout.Button(new GUIContent("x", "Remove this GameObject from the search list."),
                    EditorStyles.miniButton, GUILayout.Width(22));
            }

            return dirty;
        }

        private void DrawSearchRootButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Use selected",
                        "Replace the search list with the currently selected GameObjects.")))
                {
                    _searchRoots = SelectedRoots();
                    Rescan();
                }
                if (GUILayout.Button(new GUIContent("Add selected",
                        "Add the currently selected GameObjects to the search list.")))
                {
                    foreach (GameObject g in Selection.gameObjects)
                    {
                        if (g == null || _searchRoots.Exists(r => r.GameObject == g))
                        {
                            continue;
                        }

                        _searchRoots.Add(new SearchRoot(g, includeChildren: true));
                    }
                    Rescan();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Clear", "Empty the search list."), GUILayout.Width(70)))
                {
                    _searchRoots.Clear();
                    _groups.Clear();
                    _scanSummary = "";
                }
            }
        }

        private void DrawGroups()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
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
            if (!string.IsNullOrEmpty(_scanSummary))
            {
                EditorGUILayout.LabelField(_scanSummary, EditorStyles.miniLabel);
            }

            int visibleGroups = _groups.Count(g => !_hideUnchanged || g.HasReplacement);
            using (new EditorGUILayout.VerticalScope(
                    EditorStyles.helpBox,
                    GUILayout.Height(WkStyles.CappedListHeight(visibleGroups, 42f, 140f, 320f))))
            {
                _groupsScroll = EditorGUILayout.BeginScrollView(_groupsScroll);
                if (_searchRoots.Count == 0)
                {
                    EditorGUILayout.LabelField("Step 1: add GameObjects above to begin.", EditorStyles.centeredGreyMiniLabel);
                }
                else if (_groups.Count == 0)
                {
                    EditorGUILayout.LabelField("No replaceable object references found in the selected roots.", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    foreach (RefGroup g in _groups)
                    {
                        if (_hideUnchanged && !g.HasReplacement)
                        {
                            continue;
                        }

                        DrawGroupRow(g);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawGroupRow(RefGroup g)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawGroupHeader(g);
                    DrawGroupFields(g);
                    DrawGroupSites(g);
                }
                if (GUILayout.Button(new GUIContent("Ping", "Highlight one of the VRCFury components in the Hierarchy."),
                        EditorStyles.miniButton, GUILayout.Width(44))
                    && g.Sites.Count > 0
                    && g.Sites[0].VrcfComponent != null)
                {
                    EditorGUIUtility.PingObject(g.Sites[0].VrcfComponent);
                }
            }
            EditorGUILayout.Space(2);
            DrawSubtleDivider();
        }

        private static void DrawGroupHeader(RefGroup g)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string typeName = g.CurrentValue == null ? "Object" : g.CurrentValue.GetType().Name;
                EditorGUILayout.LabelField(
                    $"{(g.CurrentValue == null ? "(null)" : g.CurrentValue.name)}  ({typeName})",
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    g.Sites.Count == 1 ? "1 reference" : $"{g.Sites.Count} references",
                    EditorStyles.miniLabel, GUILayout.Width(110));
            }
        }

        private static void DrawGroupFields(RefGroup g)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Current", "The object currently used by one or more VRCFury fields."),
                    GUILayout.Width(82));
                using (new EditorGUI.DisabledScope(disabled: true))
                {
                    EditorGUILayout.ObjectField(g.CurrentValue, typeof(Object), allowSceneObjects: true);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Replace with", "Drop the object that should replace the current one. Leave empty to keep this row unchanged."),
                    GUILayout.Width(82));
                Object newReplacement = EditorGUILayout.ObjectField(
                    g.Replacement, typeof(Object), allowSceneObjects: true);
                if (newReplacement != g.Replacement)
                {
                    g.Replacement = newReplacement;
                }
            }
        }

        private static void DrawGroupSites(RefGroup g)
        {
            if (g.Sites.Count == 1)
            {
                RefSite only = g.Sites[0];
                EditorGUILayout.LabelField(
                    $"{only.GameObjectPath}  ▸  {only.FeatureType}  —  {only.PropertyPath}",
                    EditorStyles.miniLabel);
                return;
            }

            if (g.Sites.Count == 0)
            {
                return;
            }

            g.Foldout = EditorGUILayout.Foldout(g.Foldout, $"Locations ({g.Sites.Count})", toggleOnLabelClick: true);
            if (!g.Foldout)
            {
                return;
            }

            EditorGUI.indentLevel++;
            foreach (RefSite s in g.Sites)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"• {s.GameObjectPath} ▸ {s.FeatureType}",
                        EditorStyles.miniLabel);
                    if (GUILayout.Button(new GUIContent("Ping", "Highlight this VRCFury component in the Hierarchy."),
                            EditorStyles.miniButton, GUILayout.Width(44))
                        && s.VrcfComponent != null)
                    {
                        EditorGUIUtility.PingObject(s.VrcfComponent);
                    }
                }
                EditorGUILayout.LabelField($"   {s.PropertyPath}", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawApplyBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int queuedSites = _groups.Where(g => g.HasReplacement).Sum(g => g.Sites.Count);
                using (new EditorGUI.DisabledScope(queuedSites == 0))
                {
                    string label = queuedSites > 0
                        ? $"3. Apply {queuedSites} Replacement{(queuedSites == 1 ? "" : "s")}"
                        : "3. Apply";
                    if (GUILayout.Button(new GUIContent(label,
                            "Apply every queued replacement in one Undo step. Rows without a replacement are left untouched."),
                        GUILayout.Height(24), GUILayout.MinWidth(180)))
                    {
                        Apply();
                    }
                }
                using (new EditorGUI.DisabledScope(_searchRoots.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Re-scan", "Re-scan the search roots and refresh the reference list."),
                            GUILayout.Height(24), GUILayout.Width(88)))
                    {
                        Rescan();
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Close", "Close this window. No queued replacements are applied."),
                        GUILayout.Height(24), GUILayout.Width(80)))
                {
                    Close();
                }
            }
        }

        private static void DrawDivider()
        {
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: false, 1);
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.18f));
        }

        private static void DrawSubtleDivider()
        {
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: false, 1);
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.06f));
        }

        private void RequestAutoSize()
        {
            int queued = _groups.Where(g => g.HasReplacement).Sum(g => g.Sites.Count);
            int visibleGroups = _groups.Count(g => !_hideUnchanged || g.HasReplacement);
            string signature = $"{_searchRoots.Count}|{_groups.Count}|{visibleGroups}|{queued}|{_hideUnchanged}|{_scanSummary}";
            var preferred = new Vector2(
                _groups.Count > 0 ? 820f : 640f,
                300f
                    + WkStyles.CappedListHeight(_searchRoots.Count, 24f, 60f, 150f)
                    + WkStyles.CappedListHeight(visibleGroups, 42f, 140f, 320f));
            WkStyles.AutoSizeWindow(
                this,
                ref _autoSizeSignature,
                signature,
                new Vector2(620f, 460f),
                preferred,
                new Vector2(980f, 800f));
        }
    }
}
