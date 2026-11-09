#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools
{
    internal sealed partial class ReplaceReferencesWindow : ISerializationCallbackReceiver
    {
        [SerializeField] private GameObject?[] _rootObjects = Array.Empty<GameObject?>();
        [SerializeField] private bool[] _rootIncludeChildren = Array.Empty<bool>();

        public void OnBeforeSerialize()
        {
            _rootObjects = new GameObject?[_searchRoots.Count];
            _rootIncludeChildren = new bool[_searchRoots.Count];
            for (int i = 0; i < _searchRoots.Count; i++)
            {
                _rootObjects[i] = _searchRoots[i].GameObject;
                _rootIncludeChildren[i] = _searchRoots[i].IncludeChildren;
            }
        }

        public void OnAfterDeserialize()
        {
            _searchRoots = new List<SearchRoot>(_rootObjects.Length);
            for (int i = 0; i < _rootObjects.Length; i++)
            {
                bool includeChildren = i >= _rootIncludeChildren.Length || _rootIncludeChildren[i];
                _searchRoots.Add(new SearchRoot(_rootObjects[i], includeChildren));
            }
        }

        private sealed class SearchRoot
        {
            public SearchRoot(GameObject? gameObject, bool includeChildren)
            {
                GameObject = gameObject;
                IncludeChildren = includeChildren;
            }

            public GameObject? GameObject { get; set; }

            public bool IncludeChildren { get; set; }
        }

        private sealed class RefSite
        {
            public RefSite(Component vrcfComponent, string gameObjectPath, string featureType, string propertyPath, Object currentValue)
            {
                VrcfComponent = vrcfComponent;
                GameObjectPath = gameObjectPath;
                FeatureType = featureType;
                PropertyPath = propertyPath;
                CurrentValue = currentValue;
            }

            public Component VrcfComponent { get; }

            public string GameObjectPath { get; }

            public string FeatureType { get; }

            public string PropertyPath { get; }

            public Object CurrentValue { get; }
        }

        private sealed class RefGroup
        {
            public RefGroup(Object currentValue)
            {
                CurrentValue = currentValue;
            }

            public Object CurrentValue { get; }

            public List<RefSite> Sites { get; } = new();

            public Object? Replacement { get; set; }

            public bool Foldout { get; set; }

            public bool HasReplacement => Replacement != null && Replacement != CurrentValue;
        }
    }
}
