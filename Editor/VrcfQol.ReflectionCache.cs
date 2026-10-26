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

            public FieldInfo ContentField { get; private set; }
            public FieldInfo ToggleNameField { get; private set; }
            public FieldInfo ToggleStateField { get; private set; }
            public FieldInfo ToggleSliderField { get; private set; }
            public FieldInfo ToggleUseGlobalParamField { get; private set; }
            public FieldInfo ToggleGlobalParamField { get; private set; }
            public FieldInfo StateActionsField { get; private set; }
            public FieldInfo PagesField { get; private set; }
            public FieldInfo PageStateField { get; private set; }

            private readonly Func<Assembly> _findAssembly;
            private bool _ready;

            internal ReflectionCache() : this(FindLoadedVrcfuryAssembly) { }

            internal ReflectionCache(Func<Assembly> findAssembly) {
                _findAssembly = findAssembly;
            }

            private static Assembly FindLoadedVrcfuryAssembly() {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "VRCFury");
            }

            public bool TryEnsure(out string error) {
                error = null;
                if (_ready) return true;
                if (Resolve(out error)) {
                    _ready = true;
                    return true;
                }
                Reset();
                return false;
            }

            private void Reset() {
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

            private bool Resolve(out string error) {
                error = null;

                VrcfuryAsm = _findAssembly?.Invoke();
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
                    return false;
                }
                // ToggleSliderField, ToggleUseGlobalParamField and ToggleGlobalParamField are
                // optional - older VRCFury versions may not expose them. Tools that depend on
                // them should null-check.
                return true;
            }
        }
    }
}
