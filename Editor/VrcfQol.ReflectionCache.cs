#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace UmeVrcfQol
{
    internal static partial class VrcfQol
    {
        internal static ReflectionCache Reflection { get; } = new ReflectionCache();

        internal sealed class ReflectionCache
        {
            public Assembly? VrcfuryAsm { get; private set; }
            public Type? VRCFuryType { get; private set; }
            public Type? ToggleType { get; private set; }
            public Type? StateType { get; private set; }
            public Type? FlipbookBuilderActionType { get; private set; }
            public Type? FlipbookPageType { get; private set; }

            public FieldInfo? ContentField { get; private set; }
            public FieldInfo? ToggleNameField { get; private set; }
            public FieldInfo? ToggleStateField { get; private set; }
            public FieldInfo? ToggleSliderField { get; private set; }
            public FieldInfo? ToggleUseGlobalParamField { get; private set; }
            public FieldInfo? ToggleGlobalParamField { get; private set; }
            public FieldInfo? StateActionsField { get; private set; }
            public FieldInfo? PagesField { get; private set; }
            public FieldInfo? PageStateField { get; private set; }

            private readonly Func<Assembly?> _findAssembly;

            internal ReflectionCache() : this(FindLoadedVrcfuryAssembly) { }

            internal ReflectionCache(Func<Assembly?> findAssembly)
            {
                _findAssembly = findAssembly;
            }

            private static Assembly? FindLoadedVrcfuryAssembly()
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "VRCFury", StringComparison.Ordinal));
            }

            [MemberNotNullWhen(
                returnValue: true,
                nameof(VrcfuryAsm),
                nameof(VRCFuryType),
                nameof(ToggleType),
                nameof(StateType),
                nameof(FlipbookBuilderActionType),
                nameof(FlipbookPageType),
                nameof(ContentField),
                nameof(ToggleNameField),
                nameof(ToggleStateField),
                nameof(StateActionsField),
                nameof(PagesField),
                nameof(PageStateField))]
            private bool IsResolved =>
                VrcfuryAsm != null && VRCFuryType != null && ToggleType != null && StateType != null
                && FlipbookBuilderActionType != null && FlipbookPageType != null && ContentField != null
                && ToggleNameField != null && ToggleStateField != null && StateActionsField != null
                && PagesField != null && PageStateField != null;

            [MemberNotNullWhen(
                returnValue: true,
                nameof(VrcfuryAsm),
                nameof(VRCFuryType),
                nameof(ToggleType),
                nameof(StateType),
                nameof(FlipbookBuilderActionType),
                nameof(FlipbookPageType),
                nameof(ContentField),
                nameof(ToggleNameField),
                nameof(ToggleStateField),
                nameof(StateActionsField),
                nameof(PagesField),
                nameof(PageStateField))]
            public bool TryEnsure(out string? error)
            {
                error = null;
                if (IsResolved)
                {
                    return true;
                }

                if (Resolve(out error))
                {
                    return true;
                }

                Reset();
                return false;
            }

            private void Reset()
            {
                VrcfuryAsm = null;
                VRCFuryType = null;
                ToggleType = null;
                StateType = null;
                FlipbookBuilderActionType = null;
                FlipbookPageType = null;
                ContentField = null;
                ToggleNameField = null;
                ToggleStateField = null;
                ToggleSliderField = null;
                ToggleUseGlobalParamField = null;
                ToggleGlobalParamField = null;
                StateActionsField = null;
                PagesField = null;
                PageStateField = null;
            }

            [MemberNotNullWhen(
                returnValue: true,
                nameof(VrcfuryAsm),
                nameof(VRCFuryType),
                nameof(ToggleType),
                nameof(StateType),
                nameof(FlipbookBuilderActionType),
                nameof(FlipbookPageType),
                nameof(ContentField),
                nameof(ToggleNameField),
                nameof(ToggleStateField),
                nameof(StateActionsField),
                nameof(PagesField),
                nameof(PageStateField))]
            private bool Resolve(out string? error)
            {
                error = null;

                VrcfuryAsm = _findAssembly();
                if (VrcfuryAsm == null)
                {
                    error = "VRCFury runtime assembly ('VRCFury') not found. Is VRCFury installed?";
                    return false;
                }

                VRCFuryType = VrcfuryAsm.GetType("VF.Model.VRCFury", throwOnError: false);
                ToggleType = VrcfuryAsm.GetType("VF.Model.Feature.Toggle", throwOnError: false);
                StateType = VrcfuryAsm.GetType("VF.Model.State", throwOnError: false);
                FlipbookBuilderActionType = VrcfuryAsm.GetType("VF.Model.StateAction.FlipBookBuilderAction", throwOnError: false);
                if (VRCFuryType == null || ToggleType == null || StateType == null || FlipbookBuilderActionType == null)
                {
                    error = "Could not locate one or more VRCFury internal types. The VRCFury API may have changed.";
                    return false;
                }
                FlipbookPageType = FlipbookBuilderActionType.GetNestedType("FlipBookPage",
                    BindingFlags.Public | BindingFlags.NonPublic);
                if (FlipbookPageType == null)
                {
                    error = "FlipBookBuilderAction.FlipBookPage not found.";
                    return false;
                }

                const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                ContentField = VRCFuryType.GetField("content", Any);
                ToggleNameField = ToggleType.GetField("name", Any);
                ToggleStateField = ToggleType.GetField("state", Any);
                ToggleSliderField = ToggleType.GetField("slider", Any);
                ToggleUseGlobalParamField = ToggleType.GetField("useGlobalParam", Any);
                ToggleGlobalParamField = ToggleType.GetField("globalParam", Any);
                StateActionsField = StateType.GetField("actions", Any);
                PagesField = FlipbookBuilderActionType.GetField("pages", Any);
                PageStateField = FlipbookPageType.GetField("state", Any);
                if (ContentField == null || ToggleNameField == null || ToggleStateField == null ||
                    StateActionsField == null || PagesField == null || PageStateField == null)
                {
                    error = "One or more expected fields on VRCFury model types were not found.";
                    return false;
                }
                return true;
            }
        }
    }
}
