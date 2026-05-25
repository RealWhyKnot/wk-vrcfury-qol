// AnimatorControllerUtility.cs
//
// Thin helpers for inspecting and mutating an AnimatorController by
// name. Mostly so call sites don't pepper the codebase with manual
// .layers.FirstOrDefault(l => l.name == ...) / .states.FirstOrDefault(s => s.state.name == ...)
// lookups. When a caller wants to MUTATE an existing avatar animator
// safely inside the pipeline, the expectation is they're inside an
// NDMF VirtualControllerContext (open via the NDMF API) which clones
// the controller -- these helpers operate on whatever AnimatorController
// the caller hands in, no special context handling.

using UnityEditor.Animations;

namespace UmeVrcfQol.Internal.Pipeline {

    public static class AnimatorControllerUtility {

        /// <summary>Layer matching <paramref name="name"/>, or null when absent.</summary>
        public static AnimatorControllerLayer FindLayer(AnimatorController controller, string name) {
            if (controller == null || string.IsNullOrEmpty(name)) return null;
            foreach (var layer in controller.layers) {
                if (layer.name == name) return layer;
            }
            return null;
        }

        /// <summary>Layer index, or -1 when no layer matches.</summary>
        public static int IndexOfLayer(AnimatorController controller, string name) {
            if (controller == null || string.IsNullOrEmpty(name)) return -1;
            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++) {
                if (layers[i].name == name) return i;
            }
            return -1;
        }

        /// <summary>State matching <paramref name="name"/> on <paramref name="machine"/>, or null when absent.</summary>
        public static AnimatorState FindState(AnimatorStateMachine machine, string name) {
            if (machine == null || string.IsNullOrEmpty(name)) return null;
            foreach (var child in machine.states) {
                if (child.state != null && child.state.name == name) return child.state;
            }
            return null;
        }

        /// <summary>
        /// Remove the layer with <paramref name="name"/> from
        /// <paramref name="controller"/>. No-op when the layer is
        /// absent; preserves the relative order of the remaining
        /// layers.
        /// </summary>
        public static void RemoveLayer(AnimatorController controller, string name) {
            var idx = IndexOfLayer(controller, name);
            if (idx < 0) return;
            var layers = controller.layers;
            var next = new AnimatorControllerLayer[layers.Length - 1];
            for (int i = 0, j = 0; i < layers.Length; i++) {
                if (i == idx) continue;
                next[j++] = layers[i];
            }
            controller.layers = next;
        }
    }
}
