using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class ObjectSerializedPropertyTraverserSubSystem : SerializedPropertyTraverserSubSystem
    {
        private string ConnectionType = "Object";
        private string NodeType = "Asset";
        
        // Don't include m_CorrespondingSourceObject because otherwise every property would have a dependency to it
        public HashSet<string> ExcludedProperties = new HashSet<string>(new []{"m_CorrespondingSourceObject"});
        
        // Don't include any dependencies to UnityEngine internal scripts
        public HashSet<string> ExcludedDependencies = new HashSet<string>(new []{"UnityEngine.UI.dll", "UnityEngine.dll"});
		
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
                AddDependency(id, new Dependency(assetId, ConnectionType, NodeType, stack.ToArray()));
            }
        }

        public override Result GetDependency(Type objType, object obj, SerializedProperty property, string propertyPath, SerializedPropertyType type, Stack<PathSegment> stack)
        {
            if (type != SerializedPropertyType.ObjectReference)
                return null;
			
            var value = property.objectReferenceValue;

            if (value == null)
                return null;

            string assetPath = AssetDatabase.GetAssetPath(value);

            if (string.IsNullOrEmpty(assetPath))
                return null;

            if (ExcludedProperties.Contains(propertyPath))
                return null;

            if (ExcludedDependencies.Contains(Path.GetFileName(assetPath)))
                return null;
            
            string assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(value);
            string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

            if (!guid.StartsWith("0000000") && !(AssetDatabase.IsSubAsset(value) || AssetDatabase.IsMainAsset(value)))
            {
                return null;
            }

            return new Result {Id = assetId, ConnectionType = ConnectionType, NodeType = NodeType};
        }
    }
}