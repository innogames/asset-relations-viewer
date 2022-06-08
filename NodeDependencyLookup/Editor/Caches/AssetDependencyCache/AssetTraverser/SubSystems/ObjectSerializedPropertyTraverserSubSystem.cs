using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class ObjectSerializedPropertyTraverserSubSystem : SerializedPropertyTraverserSubSystem
    {
        // Don't include m_CorrespondingSourceObject because otherwise every property would have a dependency to it
        private HashSet<string> ExcludedProperties = new HashSet<string>(new []{"m_CorrespondingSourceObject"});
        
        // Don't include any dependencies to UnityEngine internal scripts
        private HashSet<string> ExcludedDependencies = new HashSet<string>(new []{"UnityEngine.UI.dll", "UnityEngine.dll"});
        
        private Dictionary<object, string> cachedAssetAsDependencyData = new Dictionary<object, string>();
		
        public override void TraversePrefab(string id, Object obj, Stack<PathSegment> stack)
        {
            AddPrefabAsDependency(id, obj, stack);
        }

        public override void TraversePrefabVariant(string id, Object obj, Stack<PathSegment> stack)
        {
            stack.Push(new PathSegment("Variant Of", PathSegmentType.Component));
            AddPrefabAsDependency(id, obj, stack);
            stack.Pop();
        }

        private void AddPrefabAsDependency(string id, Object obj, Stack<PathSegment> stack)
        {
            Object correspondingObjectFromSource = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            string assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(correspondingObjectFromSource);
            string assetPath = AssetDatabase.GetAssetPath(correspondingObjectFromSource);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            if (guid != NodeDependencyLookupUtility.GetGuidFromAssetId(id))
            {
                AddDependency(id, new Dependency(assetId, AssetToAssetObjectDependency.Name, AssetNodeType.Name, stack.ToArray()));
            }
        }

        private string GetAssetPathForAsset(string sourceAssetId, object obj)
        {
            Object value = obj as Object;

            if (value == null)
                return null;

            string assetPath = AssetDatabase.GetAssetPath(value);

            if (string.IsNullOrEmpty(assetPath) || ExcludedDependencies.Contains(Path.GetFileName(assetPath)))
            {
                return null;
            }

            string assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(value);
            string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

            bool isUnityAsset = guid.StartsWith("0000000");
            bool isScriptableObject = value is ScriptableObject;
            
            bool isMainAsset = AssetDatabase.IsMainAsset(value);
            bool isSubAsset = AssetDatabase.IsSubAsset(value);
            bool isAsset = isMainAsset || isSubAsset;
            bool isComponentAssetReference = value is Component && NodeDependencyLookupUtility.GetGuidFromAssetId(sourceAssetId) != guid;

            if (isUnityAsset || isScriptableObject || isAsset)
            {
                return assetId;
            }
            
            if (isComponentAssetReference)
            {
                Object mainAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                return NodeDependencyLookupUtility.GetAssetIdForAsset(mainAsset);
            }

            if (!(AssetDatabase.LoadMainAssetAtPath(assetPath) is GameObject))
            {
                return assetId;
            }

            return null;
        }

        public override Result GetDependency(string sourceAssetId, object obj, string propertyPath, SerializedPropertyType type)
        {
            if (type != SerializedPropertyType.ObjectReference || obj == null)
            {
                return null;
            }

            if(!cachedAssetAsDependencyData.TryGetValue(obj, out string assetId))
            {
                assetId = GetAssetPathForAsset(sourceAssetId, obj);
                cachedAssetAsDependencyData.Add(obj, assetId);
            }

            if (assetId == null || ExcludedProperties.Contains(propertyPath))
            {
                return null;
            }
                
            return new Result {Id = assetId, DependencyType = AssetToAssetObjectDependency.Name, NodeType = AssetNodeType.Name};
        }
    }
}