// ReplaceReferencesWindow.Records.cs

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Styling;
using UmeVrcfQol.Internal.Utilities;

namespace UmeVrcfQol.Tools {

    internal sealed partial class ReplaceReferencesWindow {

        // ---------------- Records ------------------------------------------

        [System.Serializable]
        private sealed class SearchRoot {
            public GameObject GameObject;
            public bool IncludeChildren = true;
        }

        // A single property-path occurrence inside a VRCFury component.
        private sealed class RefSite {
            public Component VrcfComponent;
            public string GameObjectPath;
            public string FeatureType;
            public string PropertyPath;
            public Object CurrentValue;
        }

        // All occurrences that share the same CurrentValue, plus the queued
        // replacement. The unit the UI displays.
        private sealed class RefGroup {
            public Object CurrentValue;
            public readonly List<RefSite> Sites = new List<RefSite>();
            public Object Replacement;
            public bool Foldout;

            public bool HasReplacement => Replacement != null && Replacement != CurrentValue;
        }
    }
}
