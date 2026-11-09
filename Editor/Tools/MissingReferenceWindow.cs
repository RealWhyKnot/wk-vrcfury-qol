#nullable enable

using System;
using System.Collections.Generic;
using UmeVrcfQol.Internal.Styling;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Tools
{
    internal sealed class MissingReferenceWindow : EditorWindow
    {
        private List<MissingReferenceWarningTool.MissingRef> _missing =
            new();
        [SerializeField] private bool _shouldDismissOnClose;
        [SerializeField] private bool _autoShow;
        private Vector2 _scroll;
        private string? _autoSizeSignature;


        internal static void OpenAuto(List<MissingReferenceWarningTool.MissingRef> missing)
        {
            MissingReferenceWindow w = GetWindow<MissingReferenceWindow>(utility: false, "Missing References", focus: true);
            w.titleContent = WkStyles.TitleContent("WhyKnot - Missing VRCFury References");
            w._missing = missing ?? new List<MissingReferenceWarningTool.MissingRef>();
            w._shouldDismissOnClose = true;
            w._autoShow = true;
            w.minSize = new Vector2(520, 280);
            w.Show();
        }

        internal static void OpenManual(List<MissingReferenceWarningTool.MissingRef> missing)
        {
            MissingReferenceWindow w = GetWindow<MissingReferenceWindow>(utility: false, "Missing References", focus: true);
            w.titleContent = WkStyles.TitleContent("WhyKnot - Missing VRCFury References");
            w._missing = missing ?? new List<MissingReferenceWarningTool.MissingRef>();
            w._shouldDismissOnClose = false;
            w._autoShow = false;
            w.minSize = new Vector2(520, 280);
            w.Show();
            w.Focus();
        }

        private void OnEnable()
        {



            if (_missing == null || _missing.Count == 0)
            {
                _missing = MissingReferenceWarningTool.Scan();
            }
        }

        private void OnDestroy()
        {
            if (_shouldDismissOnClose)
            {
                MissingReferenceWarningTool.MarkDismissedThisSession();
            }
        }



        private void OnGUI()
        {
            using IDisposable themeScope = WkStyles.Scope(WkTheme.VRCFury);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(expand: true), GUILayout.ExpandHeight(expand: true)))
            {
                WkStyles.TitleBar("Missing References");
                DrawHeader();
                DrawDivider();
                DrawList();
                DrawDivider();
                DrawFooter();
            }
            RequestAutoSize();
        }
        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_missing.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No missing references found. VRCFury components look healthy.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"{_missing.Count} missing reference{(_missing.Count == 1 ? "" : "s")} on VRCFury component(s). " +
                        (_autoShow
                            ? "This warning won't pop again until the next reload (script recompile / restart). " +
                              "You can re-check anytime from Tools/WhyKnot/wk-vrcfury-qol/Check for missing references..."
                            : "Fix them in the Inspector or replace via Tools/WhyKnot/wk-vrcfury-qol/Replace References."),
                        MessageType.Warning);
                }
            }
        }

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(
                    EditorStyles.helpBox,
                    GUILayout.Height(WkStyles.CappedListHeight(_missing.Count, 28f, 120f, 260f))))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                if (_missing.Count == 0)
                {
                    EditorGUILayout.LabelField(
                        "Click Re-scan to check again.",
                        EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    string? lastGroup = null;
                    foreach (MissingReferenceWarningTool.MissingRef m in _missing)
                    {
                        string group = $"{m.GameObjectPath}  ▸  {m.FeatureType}";
                        if (!string.Equals(group, lastGroup, StringComparison.Ordinal))
                        {
                            EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                            lastGroup = group;
                        }
                        DrawRow(m);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawRow(MissingReferenceWarningTool.MissingRef m)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(m.PropertyPath, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        $"missing — instanceID was {m.InstanceID}",
                        EditorStyles.miniLabel);
                }
                if (GUILayout.Button(new GUIContent("Ping", "Highlight the affected VRCFury component."),
                        EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    if (m.VrcfComponent != null)
                    {
                        Selection.activeObject = m.VrcfComponent;
                        EditorGUIUtility.PingObject(m.VrcfComponent);
                    }
                }
            }
            EditorGUILayout.Space(2);
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Re-scan", "Walk the scenes again and refresh the list."),
                        GUILayout.Height(22), GUILayout.Width(100)))
                {
                    _missing = MissingReferenceWarningTool.Scan();
                }
                string closeLabel = _shouldDismissOnClose ? "Dismiss" : "Close";
                if (GUILayout.Button(new GUIContent(closeLabel,
                        _shouldDismissOnClose
                            ? "Close this warning for the rest of this editor session."
                            : "Close this window."),
                        GUILayout.Height(22), GUILayout.Width(100)))
                {
                    Close();
                }
            }
            WkStyles.BrandFooter();
        }

        private void RequestAutoSize()
        {
            string signature = $"{_missing.Count}|{_autoShow}|{_shouldDismissOnClose}";
            var preferred = new Vector2(
                620f,
                210f + WkStyles.CappedListHeight(_missing.Count, 28f, 120f, 260f));
            WkStyles.AutoSizeWindow(
                this,
                ref _autoSizeSignature,
                signature,
                new Vector2(520f, 280f),
                preferred,
                new Vector2(820f, 620f));
        }

        private static void DrawDivider()
        {
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: false, 1);
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.18f));
        }
    }
}
