#nullable enable

namespace UmeVrcfQol.Tools
{
    internal struct ToggleInfo
    {
        public string GlobalObjectId;
        public string MenuPath;
        public string CurrentGlobalParam;
        public bool IsOptedOut;
        public bool UsesGlobalParam;
        public bool AssignedByTool;
        public bool HeldPreviously;

        public readonly bool IsPinned =>
            !IsOptedOut && UsesGlobalParam && !AssignedByTool
            && !string.IsNullOrWhiteSpace(CurrentGlobalParam);
    }
}
