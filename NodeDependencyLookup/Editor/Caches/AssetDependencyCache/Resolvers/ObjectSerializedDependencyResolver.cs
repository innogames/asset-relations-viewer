using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class AssetToAssetObjectDependency
	{
		public const string Name = "ATOA_Object";
	}

	public class AssetToAssetByObjectDependencyType : DependencyType
	{
		public AssetToAssetByObjectDependencyType(string name, Color color, bool isIndirect, bool isHard, string description) :
			base(name, color, isIndirect, isHard, description)
		{
		}

		public override bool IsHardConnection(Node source, Node target)
		{
			return !IsSpriteOfSpriteAtlas(source, target);
		}

		private string spriteTypeFullName = typeof(Sprite).FullName;
		private string spriteAtlasTypeName = typeof(SpriteAtlas).FullName;

		private bool IsSpriteOfSpriteAtlas(Node source, Node target)
		{
			return source.ConcreteType == spriteAtlasTypeName && target.ConcreteType == spriteTypeFullName;
		}
	}

	/**
	 * Resolver for resolving Object references by using the SerializedPropertySearcher
	 * This one provided hierarchy and property pathes but is most likely slower than the SimpleObjectResolver
	 */
	public class ObjectSerializedDependencyResolver : IAssetDependencyResolver
	{
		private const string ConnectionTypeDescription = "Dependencies between assets by a direct Object reference";
		private static DependencyType ObjectType = new AssetToAssetByObjectDependencyType("Asset->Asset by Object", new Color(0.8f, 0.8f, 0.8f), false, true, ConnectionTypeDescription);

		private readonly HashSet<string> _inValidGuids = new HashSet<string>();
		private const string Id = "ObjectSerializedDependencyResolver";

		// Don't include m_CorrespondingSourceObject because otherwise every property would have a dependency to it
        private HashSet<string> ExcludedProperties = new HashSet<string>(new []{"m_CorrespondingSourceObject"});

        // Don't include any dependencies to UnityEngine internal scripts
        private HashSet<string> ExcludedDependencies = new HashSet<string>(new []{"UnityEngine.UI.dll", "UnityEngine.dll"});

        private Dictionary<object, string> cachedAssetAsDependencyData = new Dictionary<object, string>();

        public void TraversePrefab(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
        {
            AddPrefabAsDependency(searchContext, obj, stack);
        }

        public void TraversePrefabVariant(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
        {
            stack.Push(new PathSegment("Variant Of", PathSegmentType.Component));
            AddPrefabAsDependency(searchContext, obj, stack);
            stack.Pop();
        }

        private void AddPrefabAsDependency(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
        {
            Object correspondingObjectFromSource = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            string assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(correspondingObjectFromSource);
            string assetPath = AssetDatabase.GetAssetPath(correspondingObjectFromSource);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            if (guid != NodeDependencyLookupUtility.GetGuidFromAssetId(searchContext.AssetId))
            {
	            searchContext.AddDependency(this, new Dependency(assetId, AssetToAssetObjectDependency.Name, AssetNodeType.Name, stack.ToArray()));
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

        public AssetDependencyResolverResult GetDependency(string sourceAssetId, object obj, string propertyPath, SerializedPropertyType type)
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

            return new AssetDependencyResolverResult {Id = assetId, DependencyType = AssetToAssetObjectDependency.Name, NodeType = AssetNodeType.Name};
        }

		public bool IsGuidValid(string guid)
		{
			return !_inValidGuids.Contains(guid);
		}

		public string GetId()
		{
			return Id;
		}

		public DependencyType GetDependencyTypeForId(string typeId)
		{
			return ObjectType;
		}

		public string[] GetDependencyTypes()
		{
			return new[] { AssetToAssetObjectDependency.Name };
		}

		public void SetValidGUIDs()
		{
			_inValidGuids.Clear();

			string[] filters =
			{
				"t:Script",
			};

			foreach (string filter in filters)
			{
				foreach (string guid in AssetDatabase.FindAssets(filter))
				{
					_inValidGuids.Add(guid);
				}
			}
		}

		public void Initialize(AssetDependencyCache cache)
		{
		}
	}
}