// WkJsonClone.cs
//
// JsonUtility round-trip as a deep-clone shortcut for Unity-serialisable
// types. Useful when duplicating action / page / preset data structures
// inside an inspector tool, where MemberwiseClone() is too shallow and
// hand-written deep-clone is too verbose.
//
// Limits (the same as JsonUtility's serialisation contract):
//   - The source's runtime type must be [Serializable] or a Unity Object.
//   - SerializeReference / polymorphic fields are not preserved; the
//     clone gets a default-initialised instance in their place.
//   - Members not marked [SerializeField] and not public are dropped.
//   - Unity Object references are preserved as references (the asset is
//     not duplicated); deep-cloning a ScriptableObject that points at an
//     asset gives a clone that points at the same asset.
//   - Returns null on null source.

using UnityEngine;

namespace UmeVrcfQol.Internal.Reflection {

    public static class WkJsonClone {

        /// <summary>
        /// Round-trip <paramref name="source"/> through JsonUtility and
        /// return a new instance of the same runtime type populated with
        /// the same serialised state. Returns null when <paramref name="source"/>
        /// is null or when JsonUtility refuses the type.
        /// </summary>
        public static object Clone(object source) {
            if (source == null) return null;
            var type = source.GetType();
            try {
                var json = JsonUtility.ToJson(source);
                return JsonUtility.FromJson(json, type);
            } catch {
                return null;
            }
        }

        /// <summary>Generic overload of <see cref="Clone(object)"/>.</summary>
        public static T Clone<T>(T source) where T : class {
            return Clone((object) source) as T;
        }
    }
}
