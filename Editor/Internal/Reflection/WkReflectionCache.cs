// WkReflectionCache.cs
//
// Base class for "find a foreign package's assembly and resolve a fixed
// set of types/fields/methods once" cache objects. Each downstream tool
// that pokes at VRCFury / VRC SDK / NDMF / etc. internals has this same
// shape: probe for the assembly, resolve a handful of MemberInfo handles,
// fall back to "not resolved" when anything goes missing, and surface a
// human-readable error so the diagnostic log explains why the tool is
// inert.
//
// Usage:
//   internal sealed class VrcfReflection : WkReflectionCache {
//       protected override string TargetAssemblyName => "VRCFury";
//       public Type VRCFuryComponent;
//       public FieldInfo ContentField;
//       protected override bool TryResolveMembers(Assembly asm, out string error) {
//           VRCFuryComponent = asm.GetType("VF.Model.VRCFury");
//           if (VRCFuryComponent == null) { error = "type not found"; return false; }
//           ContentField = WkReflection.FindField(VRCFuryComponent, "content");
//           if (ContentField == null) { error = "field not found"; return false; }
//           error = null; return true;
//       }
//   }
// then `if (cache.TryEnsure(out var err)) { ... } else { logger.Warning(err); }`.

using System.Reflection;

namespace UmeVrcfQol.Internal.Reflection {

    public abstract class WkReflectionCache {

        /// <summary>Short name of the assembly to probe (matches AssemblyName.Name).</summary>
        protected abstract string TargetAssemblyName { get; }

        /// <summary>
        /// Resolve every MemberInfo this cache needs against
        /// <paramref name="assembly"/>. Return true on full success.
        /// Return false + a human-readable <paramref name="error"/> when
        /// any single member is missing; the cache will null-reset itself
        /// so partial state never leaks to callers.
        /// </summary>
        protected abstract bool TryResolveMembers(Assembly assembly, out string error);

        /// <summary>True once <see cref="TryEnsure"/> has completed successfully.</summary>
        public bool IsResolved { get; private set; }

        /// <summary>The resolved assembly, or null when not yet resolved.</summary>
        public Assembly TargetAssembly { get; private set; }

        /// <summary>
        /// Idempotent resolve. Returns true on first success and on every
        /// subsequent call. Returns false + an error string on failure; the
        /// cache stays in the unresolved state so the next call retries
        /// (handy when the foreign assembly loads lazily).
        /// </summary>
        public bool TryEnsure(out string error) {
            if (IsResolved) { error = null; return true; }

            if (!WkReflection.TryFindAssembly(TargetAssemblyName, out var asm)) {
                error = $"Assembly '{TargetAssemblyName}' is not loaded.";
                return false;
            }
            TargetAssembly = asm;

            if (!TryResolveMembers(asm, out error)) {
                ResetIfPartiallyResolved();
                return false;
            }

            IsResolved = true;
            return true;
        }

        /// <summary>
        /// Wipe back to the unresolved state. Subclasses override when they
        /// need to null out their MemberInfo fields explicitly; the default
        /// implementation handles the base flags. Called automatically by
        /// <see cref="TryEnsure"/> when <see cref="TryResolveMembers"/>
        /// returns false.
        /// </summary>
        protected virtual void ResetIfPartiallyResolved() {
            IsResolved = false;
            TargetAssembly = null;
        }
    }
}
