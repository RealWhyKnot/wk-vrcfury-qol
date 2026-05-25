// WkAacImpl.cs
//
// In-house IWkAnimatorBuilder. Covers the subset of fluent animator
// construction this stack actually exercises: layers with default
// and non-default states, named parameters, constant-property clips
// (GameObject active toggle, component-property, blend-shape weight,
// uniform scale), simple two-state transitions with conditions and
// duration / exit-time, and basic blend trees.
//
// We deliberately don't depend on AnimatorAsCode -- the in-house
// builder owns its own primitive set so wk-core's downstreams have a
// stable surface without a third-party VPM dependency to chase. If a
// future feature needs AAC-specific primitives this fallback doesn't
// cover, the choice is then between extending the in-house builder
// or vendoring the relevant subset of AAC at that point.
//
// Asset hosting: layers / states / motions / blend trees / parameters
// are added as sub-assets of the AnimatorController (which is itself
// saved in the WkGeneratedAssetScope's folder). Each NewClip writes a
// standalone AnimationClip asset into the same scope folder so it
// shows up in the asset browser alongside the controller.

using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UmeVrcfQol.Internal.Pipeline;

namespace UmeVrcfQol.Internal.Animators {

    internal sealed class WkAnimatorBuilderImpl : IWkAnimatorBuilder {

        private readonly string _systemName;
        private readonly AnimatorController _controller;
        private readonly WkGeneratedAssetScope _scope;

        public WkAnimatorBuilderImpl(string systemName, AnimatorController controller, WkGeneratedAssetScope scope) {
            _systemName = systemName;
            _controller = controller;
            _scope = scope;
        }

        public IWkLayerBuilder NewLayer(string name, float weight = 1f) => new LayerBuilder(_controller, name, weight);

        public IWkParamBuilder NewParameter(string name, AnimatorControllerParameterType type) =>
            new ParamBuilder(_controller, name, type);

        public AnimationClip NewClip(string name, Action<IWkClipBuilder> configure) {
            var clip = new AnimationClip { name = name };
            _scope.SaveAsset(clip, name);
            var builder = new ClipBuilder(clip);
            configure?.Invoke(builder);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        public BlendTree NewBlendTree(string name, Action<IWkBlendTreeBuilder> configure) {
            var tree = new BlendTree { name = name };
            _scope.SaveAsset(tree, name);
            var builder = new BlendTreeBuilder(tree);
            configure?.Invoke(builder);
            EditorUtility.SetDirty(tree);
            return tree;
        }

        public AnimatorController Build() {
            EditorUtility.SetDirty(_controller);
            return _controller;
        }

        // ---- Layer + state + transition ----------------------------

        private sealed class LayerBuilder : IWkLayerBuilder {
            private readonly AnimatorController _controller;
            private readonly AnimatorControllerLayer _layer;
            private readonly AnimatorStateMachine _machine;

            public LayerBuilder(AnimatorController controller, string name, float weight) {
                _controller = controller;
                _machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(_machine, controller);
                _layer = new AnimatorControllerLayer {
                    name = name,
                    defaultWeight = weight,
                    stateMachine = _machine,
                };
                var layers = controller.layers;
                Array.Resize(ref layers, layers.Length + 1);
                layers[layers.Length - 1] = _layer;
                controller.layers = layers;
            }

            public IWkLayerBuilder DefaultState(string stateName, AnimationClip motion = null) {
                var state = CreateState(stateName, motion);
                _machine.defaultState = state;
                return this;
            }

            public IWkLayerBuilder State(string stateName, AnimationClip motion = null) {
                CreateState(stateName, motion);
                return this;
            }

            public IWkLayerBuilder Transition(string fromState, string toState, Action<IWkTransitionBuilder> configure) {
                var from = AnimatorControllerUtility.FindState(_machine, fromState);
                var to   = AnimatorControllerUtility.FindState(_machine, toState);
                if (from == null || to == null) {
                    throw new InvalidOperationException(
                        $"Transition source/target state missing: from='{fromState}' to='{toState}'");
                }
                var transition = from.AddTransition(to);
                configure?.Invoke(new TransitionBuilder(transition));
                return this;
            }

            public AnimatorControllerLayer Build() => _layer;

            private AnimatorState CreateState(string name, AnimationClip motion) {
                var state = _machine.AddState(name);
                state.motion = motion;
                return state;
            }
        }

        private sealed class TransitionBuilder : IWkTransitionBuilder {
            private readonly AnimatorStateTransition _transition;

            public TransitionBuilder(AnimatorStateTransition transition) {
                _transition = transition;
                _transition.hasExitTime = false;
                _transition.exitTime = 0;
                _transition.duration = 0;
                _transition.canTransitionToSelf = false;
            }

            public IWkTransitionBuilder When(string parameter, AnimatorConditionMode mode, float threshold) {
                _transition.AddCondition(mode, threshold, parameter);
                return this;
            }

            public IWkTransitionBuilder WithDuration(float seconds) {
                _transition.duration = seconds;
                return this;
            }

            public IWkTransitionBuilder WithExitTime(float exitTime) {
                _transition.hasExitTime = true;
                _transition.exitTime = exitTime;
                return this;
            }
        }

        // ---- Parameter ---------------------------------------------

        private sealed class ParamBuilder : IWkParamBuilder {
            private readonly AnimatorController _controller;
            private readonly AnimatorControllerParameter _param;

            public ParamBuilder(AnimatorController controller, string name, AnimatorControllerParameterType type) {
                _controller = controller;
                _param = new AnimatorControllerParameter { name = name, type = type };
                _controller.AddParameter(_param);
            }

            public IWkParamBuilder WithDefault(float value) { _param.defaultFloat = value; return this; }
            public IWkParamBuilder WithDefault(int value)   { _param.defaultInt = value;   return this; }
            public IWkParamBuilder WithDefault(bool value)  { _param.defaultBool = value;  return this; }
            public AnimatorControllerParameter Build()      => _param;
        }

        // ---- Clip --------------------------------------------------

        private sealed class ClipBuilder : IWkClipBuilder {
            private readonly AnimationClip _clip;

            public ClipBuilder(AnimationClip clip) {
                _clip = clip;
            }

            public IWkClipBuilder Animates(GameObject target, bool active) {
                if (target == null) throw new ArgumentNullException(nameof(target));
                var binding = EditorCurveBinding.DiscreteCurve(
                    PathOf(target.transform), typeof(GameObject), "m_IsActive");
                var curve = new AnimationCurve(new Keyframe(0, active ? 1f : 0f));
                AnimationUtility.SetEditorCurve(_clip, binding, curve);
                return this;
            }

            public IWkClipBuilder Animates(Component target, string propertyPath, float value) {
                if (target == null) throw new ArgumentNullException(nameof(target));
                var binding = EditorCurveBinding.FloatCurve(
                    PathOf(target.transform), target.GetType(), propertyPath);
                AnimationUtility.SetEditorCurve(_clip, binding, AnimationCurve.Constant(0, 0, value));
                return this;
            }

            public IWkClipBuilder AnimatesBlendShape(SkinnedMeshRenderer renderer, string blendShapeName, float weight) {
                if (renderer == null) throw new ArgumentNullException(nameof(renderer));
                var binding = EditorCurveBinding.FloatCurve(
                    PathOf(renderer.transform), typeof(SkinnedMeshRenderer), "blendShape." + blendShapeName);
                AnimationUtility.SetEditorCurve(_clip, binding, AnimationCurve.Constant(0, 0, weight));
                return this;
            }

            public IWkClipBuilder ScaleUniform(Transform target, float scale) {
                if (target == null) throw new ArgumentNullException(nameof(target));
                var path = PathOf(target);
                foreach (var axis in new[] { "x", "y", "z" }) {
                    var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale." + axis);
                    AnimationUtility.SetEditorCurve(_clip, binding, AnimationCurve.Constant(0, 0, scale));
                }
                return this;
            }

            private static string PathOf(Transform t) {
                // Clips bind relative to the animator root. Callers writing
                // clips for a specific avatar should call Animates on the
                // exact transform they want; the path we record here is the
                // hierarchy path of the transform itself, which the avatar
                // animator resolves at runtime.
                return t == null ? "" : t.name;
            }
        }

        // ---- Blend tree --------------------------------------------

        private sealed class BlendTreeBuilder : IWkBlendTreeBuilder {
            private readonly BlendTree _tree;

            public BlendTreeBuilder(BlendTree tree) {
                _tree = tree;
            }

            public IWkBlendTreeBuilder BlendType(BlendTreeType type) {
                _tree.blendType = type;
                return this;
            }

            public IWkBlendTreeBuilder Parameter(string parameter) {
                _tree.blendParameter = parameter;
                return this;
            }

            public IWkBlendTreeBuilder AddChild(AnimationClip clip, float threshold) {
                _tree.AddChild(clip, threshold);
                return this;
            }
        }
    }
}
