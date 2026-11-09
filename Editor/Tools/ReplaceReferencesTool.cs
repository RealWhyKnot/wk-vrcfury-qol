#nullable enable

using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Tools
{
    [InitializeOnLoad]
    internal static class ReplaceReferencesTool
    {
        private const string GameObjectMenuPath = "GameObject/WhyKnot/wk-vrcfury-qol/Replace references in selection...";
        private const string ToolsMenuPath = "Tools/WhyKnot/wk-vrcfury-qol/Replace References...";

        static ReplaceReferencesTool()
        {
        }

        [MenuItem(ToolsMenuPath, false, 2000)]
        private static void OpenFromToolsMenu()
        {
            ReplaceReferencesWindow.Open(prefillFromSelection: false);
        }

        [MenuItem(GameObjectMenuPath, false, 49)]
        private static void OpenFromHierarchy(MenuCommand command)
        {
            if (command.context != Selection.activeGameObject)
            {
                return;
            }

            ReplaceReferencesWindow.Open(prefillFromSelection: true);
        }

        [MenuItem(GameObjectMenuPath, true)]
        private static bool OpenFromHierarchyValidate(MenuCommand command)
        {
            if (command.context != Selection.activeGameObject)
            {
                return false;
            }

            if (!VrcfQol.Reflection.TryEnsure(out _))
            {
                return false;
            }

            var go = command.context as GameObject;
            return go != null && go.GetComponentInChildren(VrcfQol.Reflection.VRCFuryType, includeInactive: true) != null;
        }
    }
}
