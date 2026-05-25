// UndoUtility.cs
//
// Undo grouping helpers for "apply" actions. Without grouping, a single
// user-facing Apply that touches a renderer + adds a component + creates
// an asset shows up as three separate Ctrl+Z steps. Wrapping the action
// in `using (UndoUtility.Group("Apply Fix")) { ... }` collapses everything
// touched inside into one undo step with a sensible name.
//
// RecordAdd is a small convenience that pairs Undo.AddComponent with a
// label in one call; RecordPropertyChange does the same for arbitrary
// Object property edits.

using System;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Utilities {

    public static class UndoUtility {

        /// <summary>
        /// Open an undo group, name it, and collapse every Undo-tracked
        /// change made inside the using-block into a single Ctrl+Z step
        /// on dispose. Safe to nest -- inner scopes leave the outer
        /// group untouched.
        /// </summary>
        public static IDisposable Group(string label) {
            return new GroupScope(label);
        }

        private sealed class GroupScope : IDisposable {
            private readonly int _group;
            private readonly string _label;
            private bool _disposed;

            public GroupScope(string label) {
                _label = label ?? "WhyKnot Action";
                Undo.IncrementCurrentGroup();
                _group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(_label);
            }

            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                Undo.CollapseUndoOperations(_group);
            }
        }

        /// <summary>
        /// Add a component to <paramref name="host"/> with Undo registered
        /// so Ctrl+Z removes it cleanly. Returns the added component or
        /// null when <paramref name="host"/> is null.
        /// </summary>
        public static T RecordAdd<T>(GameObject host, string label) where T : Component {
            if (host == null) return null;
            var added = Undo.AddComponent<T>(host);
            if (!string.IsNullOrEmpty(label)) Undo.SetCurrentGroupName(label);
            return added;
        }

        /// <summary>
        /// Register an Undo entry for an upcoming property change on
        /// <paramref name="target"/>. Call before the property write so
        /// Unity captures the pre-change value. No-op on null target.
        /// </summary>
        public static void RecordPropertyChange(UnityEngine.Object target, string label) {
            if (target == null) return;
            Undo.RecordObject(target, label ?? "Property Change");
        }
    }
}
