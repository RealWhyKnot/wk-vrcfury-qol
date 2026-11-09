#nullable enable

using System;
using UmeVrcfQol.Internal.Styling;
using UmeVrcfQol.Internal.Utilities;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Tools
{
    internal sealed class MoveVrcfComponentsWindow : EditorWindow
    {
        private GameObject? _source;
        private GameObject? _destination;
        private Vector2 _pageScroll;
        private string? _autoSizeSignature;

        internal static void Show(GameObject source)
        {
            MoveVrcfComponentsWindow w = CreateInstance<MoveVrcfComponentsWindow>();
            w._source = source;
            w.titleContent = WkStyles.TitleContent("Move VRCFury Components");
            Vector2 p = GUIUtility.GUIToScreenPoint(Event.current?.mousePosition ?? Vector2.zero);
            w.position = new Rect(p.x + 10, p.y + 10, 420, 230);
            w.minSize = new Vector2(420, 230);
            w.ShowUtility();
        }

        private void OnGUI()
        {
            using IDisposable themeScope = WkStyles.Scope(WkTheme.VRCFury);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(expand: true), GUILayout.ExpandHeight(expand: true)))
            {
                WkStyles.TitleBar("Move VRCFury Components");

                using (var s = new EditorGUILayout.ScrollViewScope(
                        _pageScroll, alwaysShowHorizontal: false, alwaysShowVertical: false,
                        GUILayout.ExpandWidth(expand: true),
                        GUILayout.ExpandHeight(expand: true)))
                {
                    _pageScroll = s.scrollPosition;
                    if (_source == null)
                    {
                        EditorGUILayout.HelpBox("Source GameObject was deleted. Close this window.", MessageType.Error);
                        if (GUILayout.Button(new GUIContent("Close", "Close this window.")))
                        {
                            Close();
                        }
                    }
                    else
                    {
                        DrawMoveForm(_source);
                    }
                }

                WkStyles.WindowFooter();
            }
            RequestAutoSize();
        }

        private void DrawMoveForm(GameObject source)
        {
            EditorGUILayout.HelpBox(
                "Move every VRCFury component from the source GameObject to one destination GameObject. The operation is one Undo step.",
                MessageType.Info);

            EditorGUILayout.LabelField(
                new GUIContent("Source", "The GameObject currently holding the VRCFury components."),
                new GUIContent(PathUtility.GetGameObjectPath(source)));

            using (new EditorGUI.DisabledScope(disabled: false))
            {
                _destination = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Destination", "Drop the GameObject that should receive the VRCFury components."),
                    _destination, typeof(GameObject), allowSceneObjects: true);
            }

            EditorGUILayout.Space(8);

            string? disabledReason = ValidationError();
            if (disabledReason != null)
            {
                EditorGUILayout.HelpBox(disabledReason, MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Cancel", "Close without moving anything."), GUILayout.Width(80)))
                {
                    Close();
                }
                using (new EditorGUI.DisabledScope(disabledReason != null))
                {
                    if (GUILayout.Button(new GUIContent("Move components", "Move the source VRCFury components to the destination."),
                            GUILayout.Width(140)))
                    {
                        DoMove();
                    }
                }
            }
        }

        private void RequestAutoSize()
        {
            int sourceId = _source != null ? _source.GetInstanceID() : 0;
            int destinationId = _destination != null ? _destination.GetInstanceID() : 0;
            string signature = $"{sourceId}|{destinationId}|{ValidationError()}";
            WkStyles.AutoSizeWindow(
                this,
                ref _autoSizeSignature,
                signature,
                new Vector2(420f, 230f),
                new Vector2(520f, _source == null ? 260f : 340f),
                new Vector2(680f, 420f));
        }

        private string? ValidationError()
        {
            return _source == null
                ? "Source GameObject was deleted."
                : _destination == null
                    ? "Pick a destination GameObject."
                    : _destination == _source
                        ? "Destination must differ from the source."
                        : _destination.scene != _source.scene
                            ? "Source and destination must be in the same scene / prefab stage."
                            : null;
        }

        private void DoMove()
        {
            if (_source == null || _destination == null)
            {
                return;
            }

            try
            {
                int count = MoveVrcfComponentsTool.Run(_source, _destination);
                if (count == 0)
                {
                    EditorUtility.DisplayDialog("Move VRCFury Components",
                        "No VRCFury components were found on the source.", "OK");
                }
                else
                {
                    Selection.activeGameObject = _destination;
                }
                Close();
            }
            catch (Exception ex) when (VrcfQol.IsCallbackFailure(ex))
            {
                EditorUtility.DisplayDialog("Move VRCFury Components",
                    "Move failed; changes reverted.\n\n" + ex.Message, "OK");
            }
        }
    }
}
