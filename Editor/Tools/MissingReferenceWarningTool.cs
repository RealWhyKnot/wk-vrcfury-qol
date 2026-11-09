#nullable enable

using System.Collections.Generic;
using UmeVrcfQol.Internal.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UmeVrcfQol.Tools
{
    [InitializeOnLoad]
    internal static class MissingReferenceWarningTool
    {
        private const string MenuPath = "Tools/WhyKnot/wk-vrcfury-qol/Check for missing references...";

        private static bool DismissedThisSession;

        private static bool ScanScheduled;

        static MissingReferenceWarningTool()
        {
            ScheduleAutoCheck();
            EditorSceneManager.sceneOpened += (s, m) => ScheduleAutoCheck();
            PrefabStage.prefabStageOpened += _ => ScheduleAutoCheck();
        }

        [MenuItem(MenuPath, false, 2010)]
        private static void OpenManually()
        {
            if (!VrcfQol.Reflection.TryEnsure(out string? error))
            {
                EditorUtility.DisplayDialog("Check for missing references", error, "OK");
                return;
            }
            List<MissingRef> refs = Scan();
            MissingReferenceWindow.OpenManual(refs);
        }

        internal static void MarkDismissedThisSession()
        {
            DismissedThisSession = true;
        }

        private static void ScheduleAutoCheck()
        {
            if (ScanScheduled)
            {
                return;
            }

            ScanScheduled = true;
            EditorApplication.delayCall += () =>
            {
                ScanScheduled = false;
                AutoCheck();
            };
        }

        private static void AutoCheck()
        {
            if (DismissedThisSession)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!VrcfQol.Reflection.TryEnsure(out _))
            {
                return;
            }

            List<MissingRef> refs = Scan();
            if (refs.Count == 0)
            {
                return;
            }

            MissingReferenceWindow.OpenAuto(refs);
        }

        internal static List<MissingRef> Scan()
        {
            var output = new List<MissingRef>();
            if (!VrcfQol.Reflection.TryEnsure(out _))
            {
                return output;
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;

            var seen = new HashSet<Component>();
            foreach (Object? o in Resources.FindObjectsOfTypeAll(r.VRCFuryType))
            {
                var c = o as Component;
                if (c == null || !seen.Add(c))
                {
                    continue;
                }

                if ((c.hideFlags & (HideFlags.NotEditable | HideFlags.HideAndDontSave)) != HideFlags.None)
                {
                    continue;
                }

                if (c.gameObject == null)
                {
                    continue;
                }

                if (!c.gameObject.scene.IsValid())
                {
                    continue;
                }

                ScanComponent(c, output);
            }
            return output;
        }

        private static void ScanComponent(Component vrcf, List<MissingRef> output)
        {
            using var so = new SerializedObject(vrcf);
            SerializedProperty iter = so.GetIterator();
            if (!iter.NextVisible(enterChildren: true))
            {
                return;
            }

            do
            {
                if (iter.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (string.Equals(iter.propertyPath, "m_Script", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (iter.objectReferenceValue != null)
                {
                    continue;
                }

                if (iter.objectReferenceInstanceIDValue == 0)
                {
                    continue;
                }

                output.Add(new MissingRef
                {
                    VrcfComponent = vrcf,
                    GameObjectPath = PathUtility.GetGameObjectPath(vrcf.gameObject),
                    FeatureType = GetEnclosingFeatureTypeName(so, iter.propertyPath),
                    PropertyPath = iter.propertyPath,
                    InstanceID = iter.objectReferenceInstanceIDValue,
                });
            } while (iter.NextVisible(enterChildren: true));
        }

        private static string GetEnclosingFeatureTypeName(SerializedObject so, string propertyPath)
        {
            string parent = propertyPath;
            while (true)
            {
                int dot = parent.LastIndexOf('.');
                if (dot < 0)
                {
                    break;
                }

                parent = parent[..dot];
                SerializedProperty p = so.FindProperty(parent);
                if (p == null)
                {
                    continue;
                }

                if (p.propertyType == SerializedPropertyType.ManagedReference)
                {
                    string fullName = p.managedReferenceFullTypename;
                    if (string.IsNullOrEmpty(fullName))
                    {
                        return "VRCFury";
                    }

                    int space = fullName.LastIndexOf(' ');
                    string typeName = space >= 0 ? fullName[(space + 1)..] : fullName;
                    int lastDot = typeName.LastIndexOf('.');
                    return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
                }
            }
            return "VRCFury";
        }

        internal sealed class MissingRef
        {
            public Component? VrcfComponent;
            public string? GameObjectPath;
            public string? FeatureType;
            public string? PropertyPath;
            public int InstanceID;
        }
    }
}
