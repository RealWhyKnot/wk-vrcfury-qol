// VrcExpressionUtility.cs
//
// Helpers for the VRChat avatar's VRCExpressionParameters and
// VRCExpressionsMenu, used when generating menu entries and synced
// parameters from a build pass. Reflection-based so this file compiles
// on a wk-core that doesn't reference the VRC SDK directly; downstream
// code that has SDK refs can cast the object returns back to typed
// VRC SDK references at the call site.
//
// Compiles unconditionally -- the reflection probe handles the case
// where the SDK is absent (everything no-ops). When the SDK IS
// installed, downstream code passes in typed instances and gets back
// typed control / parameter references.

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Reflection;

namespace UmeVrcfQol.Internal.Animators {

    public static class VrcExpressionUtility {

        // Lazy-resolved Type handles for the VRC SDK expression types.

        private sealed class VrcExprTypes : WkReflectionCache {
            public Type ExprParameters;
            public Type ExprParameter;
            public Type ExprMenu;
            public Type ExprControl;
            public Type ValueTypeEnum;
            public Type ControlTypeEnum;

            protected override string TargetAssemblyName => "VRC.SDK3A";

            protected override bool TryResolveMembers(Assembly asm, out string error) {
                ExprParameters = asm.GetType("VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters");
                if (ExprParameters == null) { error = "VRCExpressionParameters missing"; return false; }
                ExprParameter = ExprParameters.GetNestedType("Parameter", BindingFlags.Public);
                if (ExprParameter == null) { error = "VRCExpressionParameters.Parameter missing"; return false; }
                ValueTypeEnum = ExprParameters.GetNestedType("ValueType", BindingFlags.Public);

                ExprMenu = asm.GetType("VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu");
                if (ExprMenu == null) { error = "VRCExpressionsMenu missing"; return false; }
                ExprControl = ExprMenu.GetNestedType("Control", BindingFlags.Public);
                if (ExprControl == null) { error = "VRCExpressionsMenu.Control missing"; return false; }
                ControlTypeEnum = ExprControl.GetNestedType("ControlType", BindingFlags.Public);

                error = null;
                return true;
            }
        }

        private static readonly VrcExprTypes _types = new VrcExprTypes();

        /// <summary>
        /// Append a synced parameter to <paramref name="expressionParameters"/>'s
        /// list. Returns the created Parameter as object; callers with the
        /// VRC SDK referenced can cast back to
        /// VRCExpressionParameters.Parameter. No-op + null returned when
        /// the SDK isn't installed.
        /// </summary>
        public static object TryAddParameter(object expressionParameters, string name, int valueType,
                                              float defaultValue, bool saved, bool synced) {
            if (expressionParameters == null) return null;
            if (!_types.TryEnsure(out _)) return null;

            var parameter = Activator.CreateInstance(_types.ExprParameter);
            SetField(parameter, "name", name);
            SetField(parameter, "valueType", Enum.ToObject(_types.ValueTypeEnum, valueType));
            SetField(parameter, "defaultValue", defaultValue);
            SetField(parameter, "saved", saved);
            SetField(parameter, "networkSynced", synced);

            var parametersField = WkReflection.FindField(_types.ExprParameters, "parameters");
            if (parametersField == null) return null;
            var current = (Array) parametersField.GetValue(expressionParameters);
            var next = Array.CreateInstance(_types.ExprParameter, (current?.Length ?? 0) + 1);
            if (current != null) Array.Copy(current, next, current.Length);
            next.SetValue(parameter, next.Length - 1);
            parametersField.SetValue(expressionParameters, next);
            EditorUtility.SetDirty(expressionParameters as UnityEngine.Object);
            return parameter;
        }

        /// <summary>
        /// Append a Toggle control to <paramref name="expressionsMenu"/>'s
        /// list. Returns the created Control as object. No-op + null
        /// returned when the SDK isn't installed.
        /// </summary>
        public static object TryAddToggleControl(object expressionsMenu, string label, string parameterName,
                                                  float onValue = 1f, Texture2D icon = null) {
            if (expressionsMenu == null) return null;
            if (!_types.TryEnsure(out _)) return null;

            var control = Activator.CreateInstance(_types.ExprControl);
            SetField(control, "name", label);
            SetField(control, "icon", icon);
            SetField(control, "type", _types.ControlTypeEnum != null
                ? Enum.ToObject(_types.ControlTypeEnum, 102)   // ControlType.Toggle = 102 historically
                : null);
            // The parameter field is a struct nested below; SDK fluctuates between
            // VRCExpressionsMenu.Control.Parameter and a plain string. Use reflection
            // on whichever shape exists.
            SetParameterRef(control, parameterName, onValue);
            AddControlToMenu(expressionsMenu, control);
            EditorUtility.SetDirty(expressionsMenu as UnityEngine.Object);
            return control;
        }

        /// <summary>
        /// Append a SubMenu control referencing <paramref name="submenu"/>.
        /// No-op + null returned when the SDK isn't installed.
        /// </summary>
        public static object TryAddSubMenuControl(object expressionsMenu, string label, object submenu, Texture2D icon = null) {
            if (expressionsMenu == null || submenu == null) return null;
            if (!_types.TryEnsure(out _)) return null;

            var control = Activator.CreateInstance(_types.ExprControl);
            SetField(control, "name", label);
            SetField(control, "icon", icon);
            SetField(control, "type", _types.ControlTypeEnum != null
                ? Enum.ToObject(_types.ControlTypeEnum, 103)   // ControlType.SubMenu = 103
                : null);
            SetField(control, "subMenu", submenu);
            AddControlToMenu(expressionsMenu, control);
            EditorUtility.SetDirty(expressionsMenu as UnityEngine.Object);
            return control;
        }

        // ---- helpers ---------------------------------------------------

        private static void AddControlToMenu(object expressionsMenu, object control) {
            var controlsField = WkReflection.FindField(_types.ExprMenu, "controls");
            if (controlsField == null) return;
            var listType = controlsField.FieldType;
            var listValue = controlsField.GetValue(expressionsMenu);
            if (listValue == null) {
                listValue = Activator.CreateInstance(listType);
                controlsField.SetValue(expressionsMenu, listValue);
            }
            var addMethod = listType.GetMethod("Add", new[] { _types.ExprControl });
            addMethod?.Invoke(listValue, new[] { control });
        }

        private static void SetParameterRef(object control, string parameterName, float onValue) {
            // SDK shape: control.parameter is a Parameter struct with a `name` field;
            // control.value is a float for the active value.
            var paramField = WkReflection.FindField(_types.ExprControl, "parameter");
            if (paramField != null) {
                var paramValue = paramField.GetValue(control);
                if (paramValue == null) {
                    paramValue = Activator.CreateInstance(paramField.FieldType);
                }
                var nameField = WkReflection.FindField(paramField.FieldType, "name");
                nameField?.SetValue(paramValue, parameterName);
                paramField.SetValue(control, paramValue);
            }
            var valueField = WkReflection.FindField(_types.ExprControl, "value");
            valueField?.SetValue(control, onValue);
        }

        private static void SetField(object target, string name, object value) {
            var f = WkReflection.FindField(target.GetType(), name);
            f?.SetValue(target, value);
        }
    }
}
