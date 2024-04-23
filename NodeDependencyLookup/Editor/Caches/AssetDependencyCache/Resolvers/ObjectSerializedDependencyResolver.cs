using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public static class AssetToAssetObjectDependency
	{
		public const string Name = "ATOA_Object";
	}

	public class AssetToAssetByObjectDependencyType : DependencyType
	{
		public AssetToAssetByObjectDependencyType(string name, Color color, bool isIndirect, bool isHard,
			string description) :
			base(name, color, isIndirect, isHard, description)
		{
		}

		public override bool IsHardConnection(Node source, Node target)
		{
			return !IsSpriteOfSpriteAtlas(source, target);
		}

		private readonly string spriteTypeFullName = typeof(Sprite).FullName;
		private readonly string spriteAtlasTypeName = typeof(SpriteAtlas).FullName;

		private bool IsSpriteOfSpriteAtlas(Node source, Node target)
		{
			return source.ConcreteType == spriteAtlasTypeName && target.ConcreteType == spriteTypeFullName;
		}
	}

	/// <summary>
	/// Resolver for resolving Object references by using the SerializedPropertySearcher
	/// This one provided hierarchy and property paths but is most likely slower than the SimpleObjectResolver
	/// </summary>
	public class ObjectSerializedDependencyResolver : IAssetDependencyResolver
	{
		private const string ConnectionTypeDescription = "Dependencies between assets by a direct Object reference";

		private static readonly DependencyType ObjectType = new AssetToAssetByObjectDependencyType("Asset->Asset by Object",
			new Color(0.8f, 0.8f, 0.8f), false, true, ConnectionTypeDescription);

		private readonly HashSet<string> _inValidGuids = new HashSet<string>();
		private const string Id = "ObjectSerializedDependencyResolver";

		// Don't include m_CorrespondingSourceObject because otherwise every property would have a dependency to it
		private readonly HashSet<string> ExcludedProperties = new HashSet<string>(new[] {"m_CorrespondingSourceObject"});

		// Don't include any dependencies to UnityEngine internal scripts
		private readonly HashSet<string> ExcludedDependencies =
			new HashSet<string>(new[] {"UnityEngine.UI.dll", "UnityEngine.dll"});

		private Dictionary<string, string> cachedAssetAsDependencyData = new Dictionary<string, string>();

		public void TraversePrefab(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
		{
			AddPrefabAsDependency(searchContext, obj, stack);
		}

		public void TraversePrefabVariant(ResolverDependencySearchContext searchContext, Object obj,
			Stack<PathSegment> stack)
		{
			stack.Push(new PathSegment("Variant Of", PathSegmentType.Component));
			AddPrefabAsDependency(searchContext, obj, stack);
			stack.Pop();
		}

		private void AddPrefabAsDependency(ResolverDependencySearchContext searchContext, Object obj,
			Stack<PathSegment> stack)
		{
			var correspondingObjectFromSource = PrefabUtility.GetCorrespondingObjectFromSource(obj);
			var assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(correspondingObjectFromSource);
			var assetPath = AssetDatabase.GetAssetPath(correspondingObjectFromSource);
			var guid = AssetDatabase.AssetPathToGUID(assetPath);

			if (guid != NodeDependencyLookupUtility.GetGuidFromAssetId(searchContext.AssetId))
			{
				searchContext.AddDependency(this,
					new Dependency(assetId, AssetToAssetObjectDependency.Name, AssetNodeType.Name, stack.ToArray()));
			}
		}

		private string GetAssetPathForAsset(string sourceAssetId, Object value)
		{
			if (value == null)
			{
				return null;
			}

			var assetPath = AssetDatabase.GetAssetPath(value);

			if (string.IsNullOrEmpty(assetPath) || ExcludedDependencies.Contains(Path.GetFileName(assetPath)))
			{
				return null;
			}

			var assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(value);
			var guid = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

			var isUnityAsset = guid.StartsWith("0000000", StringComparison.Ordinal);
			var isScriptableObject = value is ScriptableObject;

			var isMainAsset = AssetDatabase.IsMainAsset(value);
			var isSubAsset = AssetDatabase.IsSubAsset(value);
			var isAsset = isMainAsset || isSubAsset;
			var isComponentAssetReference = value is Component &&
			                                NodeDependencyLookupUtility.GetGuidFromAssetId(sourceAssetId) != guid;

			if (isUnityAsset || isScriptableObject || isAsset)
			{
				return assetId;
			}

			if (isComponentAssetReference)
			{
				var mainAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
				return NodeDependencyLookupUtility.GetAssetIdForAsset(mainAsset);
			}

			if (!(AssetDatabase.LoadMainAssetAtPath(assetPath) is GameObject))
			{
				return assetId;
			}

			return null;
		}

		public AssetDependencyResolverResult GetDependency(ref string sourceAssetId, ref SerializedProperty property,
			ref string propertyPath, SerializedPropertyType type)
		{
			if (type != SerializedPropertyType.ObjectReference)
			{
				return null;
			}

			var asset = property.objectReferenceValue;

			if (asset == null)
			{
				return null;
			}

			var id = NodeDependencyLookupUtility.GetAssetIdForAsset(asset);

			if (!cachedAssetAsDependencyData.TryGetValue(id, out var assetId))
			{
				assetId = GetAssetPathForAsset(sourceAssetId, asset);
				cachedAssetAsDependencyData.Add(id, assetId);
			}

			if (assetId == null || ExcludedProperties.Contains(propertyPath))
			{
				return null;
			}

			return new AssetDependencyResolverResult
				{Id = assetId, DependencyType = AssetToAssetObjectDependency.Name, NodeType = AssetNodeType.Name};
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
			return new[] {AssetToAssetObjectDependency.Name};
		}

		public void SetValidGUIDs()
		{
			_inValidGuids.Clear();

			string[] invalidAssetFilter =
			{
				"t:Script",
				"t:TextAsset"
			};

			foreach (var filter in invalidAssetFilter)
			{
				foreach (var guid in AssetDatabase.FindAssets(filter))
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