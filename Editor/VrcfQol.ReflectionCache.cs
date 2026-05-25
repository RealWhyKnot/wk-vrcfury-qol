// VrcfQol.ReflectionCache.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol {

    internal static partial class VrcfQol {

        // =========================== Reflection cache =========================
        //
        // Exposed as an instance singleton (via the static `Reflection` property)
        // so tools can write `var r = VrcfQol.Reflection;` and then read `r.X`.
        // A purely static class would force `VrcfQol.Reflection.X` everywhere,
        // and would also prevent `var r = ...` captures at all.

        internal static ReflectionCache Reflection { get; } = new ReflectionCache();

        internal sealed class ReflectionCache {
            public Assembly VrcfuryAsm { get; private set; }
            public Type VRCFuryType { get; private set; }
            public Type ToggleType { get; private set; }
            public Type StateType { get; private set; }
            public Type FlipbookBuilderActionType { get; private set; }
            public Type FlipbookPageType { get; private set; }
            // Optional - present in versions that still expose a top-level `config.features`
            // list. Null on newer versions that switched to a single `content` slot.
            public Type ConfigType { get; private set; }

            public FieldInfo ContentField { get; private set; }
            public FieldInfo ToggleNameField { get; private set; }
            public FieldInfo ToggleStateField { get; private set; }
            public FieldInfo ToggleSliderField { get; private set; }
            public FieldInfo ToggleUseGlobalParamField { get; private set; }
            public FieldInfo ToggleGlobalParamField { get; private set; }
            public FieldInfo StateActionsField { get; private set; }
            public FieldInfo PagesField { get; private set; }
            public FieldInfo PageStateField { get; private set; }
            // Optional - see ConfigType.
            public FieldInfo ConfigField { get; private set; }
            public FieldInfo FeaturesField { get; private set; }

            public bool TryEnsure(out string error) {
                error = null;
                if (VRCFuryType != null) return true;

                VrcfuryAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "VRCFury");
                if (VrcfuryAsm == null) {
                    error = "VRCFury runtime assembly ('VRCFury') not found. Is VRCFury installed?";
                    return false;
                }

                VRCFuryType = VrcfuryAsm.GetType("VF.Model.VRCFury", false);
                ToggleType = VrcfuryAsm.GetType("VF.Model.Feature.Toggle", false);
                StateType = VrcfuryAsm.GetType("VF.Model.State", false);
                FlipbookBuilderActionType = VrcfuryAsm.GetType("VF.Model.StateAction.FlipBookBuilderAction", false);
                if (VRCFuryType == null || ToggleType == null || StateType == null || FlipbookBuilderActionType == null) {
                    error = "Could not locate one or more VRCFury internal types. The VRCFury API may have changed.";
                    return false;
                }
                FlipbookPageType = FlipbookBuilderActionType.GetNestedType("FlipBookPage",
                    BindingFlags.Public | BindingFlags.NonPublic);
                if (FlipbookPageType == null) {
                    error = "FlipBookBuilderAction.FlipBookPage not found.";
                    return false;
                }

                const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                ContentField = VRCFuryType.GetField("content", any);
                ToggleNameField = ToggleType.GetField("name", any);
                ToggleStateField = ToggleType.GetField("state", any);
                ToggleSliderField = ToggleType.GetField("slider", any);
                ToggleUseGlobalParamField = ToggleType.GetField("useGlobalParam", any);
                ToggleGlobalParamField = ToggleType.GetField("globalParam", any);
                StateActionsField = StateType.GetField("actions", any);
                PagesField = FlipbookBuilderActionType.GetField("pages", any);
                PageStateField = FlipbookPageType.GetField("state", any);
                if (ContentField == null || ToggleNameField == null || ToggleStateField == null ||
                    StateActionsField == null || PagesField == null || PageStateField == null) {
                    error = "One or more expected fields on VRCFury model types were not found.";
                    VRCFuryType = null;
                    return false;
                }
                // ToggleSliderField, ToggleUseGlobalParamField and ToggleGlobalParamField are
                // optional - older VRCFury versions may not expose them. Tools that depend on
                // them should null-check.

                // Optional: legacy `config.features` list. Present on versions that still
                // ship VRCFuryConfig (or equivalent). Used by the merge-mode of the move
                // tool; null-checked at call sites so its absence doesn't fail TryEnsure.
                ConfigType = VrcfuryAsm.GetType("VF.Model.VRCFuryConfig", false);
                ConfigField = VRCFuryType.GetField("config", any);
                if (ConfigType != null) {
                    FeaturesField = ConfigType.GetField("features", any);
                }
                return true;
            }
        }
    }
}
