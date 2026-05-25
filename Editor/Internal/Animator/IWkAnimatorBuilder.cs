// IWkAnimatorBuilder.cs
//
// Public surface for the animator-builder abstraction. Same shape on
// both the AAC-backed impl and the self-built fallback, so callers
// pass through the WkAac.For(...) factory and don't care which backend
// is doing the work.
//
// Sub-interfaces (IWkLayerBuilder, IWkParamBuilder, IWkClipBuilder,
// IWkBlendTreeBuilder) shape after AAC's fluent surface for the
// methods we exercise in this stack. The fallback implements the same
// subset directly via UnityEditor.Animations.AnimatorController +
// AnimationClip construction.

using System;
using UnityEditor.Animations;
using UnityEngine;

namespace UmeVrcfQol.Internal.Animators {

    public interface IWkAnimatorBuilder {
        IWkLayerBuilder NewLayer(string name, float weight = 1f);
        IWkParamBuilder NewParameter(string name, AnimatorControllerParameterType type);
        AnimationClip NewClip(string name, Action<IWkClipBuilder> configure);
        BlendTree NewBlendTree(string name, Action<IWkBlendTreeBuilder> configure);
        AnimatorController Build();
    }

    public interface IWkLayerBuilder {
        IWkLayerBuilder DefaultState(string stateName, AnimationClip motion = null);
        IWkLayerBuilder State(string stateName, AnimationClip motion = null);
        IWkLayerBuilder Transition(string fromState, string toState, Action<IWkTransitionBuilder> configure);
        AnimatorControllerLayer Build();
    }

    public interface IWkTransitionBuilder {
        IWkTransitionBuilder When(string parameter, AnimatorConditionMode mode, float threshold);
        IWkTransitionBuilder WithDuration(float seconds);
        IWkTransitionBuilder WithExitTime(float exitTime);
    }

    public interface IWkParamBuilder {
        IWkParamBuilder WithDefault(float value);
        IWkParamBuilder WithDefault(int value);
        IWkParamBuilder WithDefault(bool value);
        AnimatorControllerParameter Build();
    }

    public interface IWkClipBuilder {
        /// <summary>Single-frame constant curve toggling GameObject.SetActive(value).</summary>
        IWkClipBuilder Animates(GameObject target, bool active);

        /// <summary>Single-frame constant curve on a component's property.</summary>
        IWkClipBuilder Animates(Component target, string propertyPath, float value);

        /// <summary>Single-frame blend-shape weight curve.</summary>
        IWkClipBuilder AnimatesBlendShape(SkinnedMeshRenderer renderer, string blendShapeName, float weight);

        /// <summary>Uniform scale curve on the transform.</summary>
        IWkClipBuilder ScaleUniform(Transform target, float scale);
    }

    public interface IWkBlendTreeBuilder {
        IWkBlendTreeBuilder BlendType(BlendTreeType type);
        IWkBlendTreeBuilder Parameter(string parameter);
        IWkBlendTreeBuilder AddChild(AnimationClip clip, float threshold);
    }
}
