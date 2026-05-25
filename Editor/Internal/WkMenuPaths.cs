// WkMenuPaths.cs
//
// Canonical menu-path prefixes used by every WhyKnot tool. Each package
// extends these with its own display-name segment:
//   Tools/WhyKnot/<DisplayName>/<ToolName>     -- Avatar QoL, VRCFury QoL
//   GameObject/WhyKnot/<DisplayName>/<ToolName>
//   Window/WhyKnot/<WindowName>
//
// Convention: the path component after WhyKnot/ is the package's
// human-readable display name ("Avatar QoL", "VRCFury QoL"), not the
// VPM package id. This keeps the menu approachable for users and the
// constants here are the single source of truth so future packages don't
// drift to a different root by accident.

namespace UmeVrcfQol.Internal {

    public static class WkMenuPaths {

        /// <summary>Tools menu root: <c>"Tools/WhyKnot/"</c>.</summary>
        public const string ToolsRoot = "Tools/WhyKnot/";

        /// <summary>Hierarchy / GameObject menu root: <c>"GameObject/WhyKnot/"</c>.</summary>
        public const string GameObjectRoot = "GameObject/WhyKnot/";

        /// <summary>Window menu root: <c>"Window/WhyKnot/"</c>.</summary>
        public const string WindowRoot = "Window/WhyKnot/";
    }
}
