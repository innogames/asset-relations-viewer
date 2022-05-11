using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public abstract class SerializedPropertyTraverserSubSystem
    {
        public class Result
        {
            public string Id;
            public string DependencyType;
            public string NodeType;
        }

        public readonly Dictionary<string, List<Dependency>> Dependencies = new Dictionary<string, List<Dependency>>();
		
        // What to to when a prefab got found, in case of searching for assets, it should be added as a dependency
        public abstract void TraversePrefab(string id, Object obj, Stack<PathSegment> stack);
        
        public abstract void TraversePrefabVariant(string id, Object obj, Stack<PathSegment> stack);
        
        // Returns a dependency result of the given serialized property is a UnityEngine.Object
        public abstract Result GetDependency(string sourceAssetId, object obj, string propertyPath,
            SerializedPropertyType type);

        public void AddDependency(string id, Dependency dependency)
        {
            if (!Dependencies.ContainsKey(id))
            {
                Dependencies.Add(id, new List<Dependency>());
            }
			
            Dependencies[id].Add(dependency);
        }

        public void Clear()
        {
            Dependencies.Clear();
        }
    }
}