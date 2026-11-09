#nullable enable

using System;
using UmeVrcfQol.Internal.Styling;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal
{
    public abstract class WkToolWindow : EditorWindow
    {
        protected abstract string Title { get; }

        protected virtual Uri? HelpUrl => null;

        protected virtual WkTheme Theme => WkTheme.WhyKnot;

        protected virtual Vector2 InitialMinSize => new(420, 320);

        protected virtual bool ShowScrollView => true;

        protected virtual bool ShowFooter => true;

        protected virtual bool ShowBrandFooter => true;

        protected virtual Vector2 PreferredSize => InitialMinSize;

        protected virtual Vector2 MaxAutoSize => WkStyles.DefaultMaxAutoWindowSize;

        protected virtual string AutoSizeSignature => Title;

        protected abstract void OnBodyGUI();

        protected virtual void OnFooterGUI()
        {
            if (GUILayout.Button(
                    new GUIContent("Close", "Close this window."),
                    GUILayout.Height(22), GUILayout.Width(100)))
            {
                Close();
            }
        }

        protected virtual void OnEnable()
        {
            titleContent = WkStyles.TitleContent(Title);
            minSize = InitialMinSize;
        }

        private string? _lastAutoSizeSignature;

        protected void RequestAutoSize()
        {
            WkStyles.AutoSizeWindow(
                this,
                ref _lastAutoSizeSignature,
                AutoSizeSignature,
                InitialMinSize,
                PreferredSize,
                MaxAutoSize);
        }

        private Vector2 _scroll;

        protected virtual void OnGUI()
        {
            using (WkStyles.Scope(Theme))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(expand: true), GUILayout.ExpandHeight(expand: true)))
                {
                    WkStyles.TitleBar(Title, HelpUrl);

                    if (ShowScrollView)
                    {
                        using var s = new EditorGUILayout.ScrollViewScope(
                            _scroll, alwaysShowHorizontal: false, alwaysShowVertical: false,
                            GUILayout.ExpandWidth(expand: true),
                            GUILayout.ExpandHeight(expand: true));
                        _scroll = s.scrollPosition;
                        OnBodyGUI();
                    }
                    else
                    {
                        OnBodyGUI();
                    }

                    if (ShowFooter || ShowBrandFooter)
                    {
                        WkStyles.Divider();
                        if (ShowFooter)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.FlexibleSpace();
                                OnFooterGUI();
                            }
                        }
                        if (ShowBrandFooter)
                        {
                            WkStyles.BrandFooter();
                        }
                    }
                }
            }
            RequestAutoSize();
        }
    }
}
