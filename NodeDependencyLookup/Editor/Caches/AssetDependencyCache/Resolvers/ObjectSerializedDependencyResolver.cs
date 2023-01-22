using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

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

		public override bool IsHardConnection(Connection connection, Node source)
		{
			return !IsSpriteOfSpriteAtlas(source, connection.Node);
		}

		private bool IsSpriteOfSpriteAtlas(Node source, Node target)
		{
			Type spriteType = typeof(Sprite);
			Type spriteAtlasType = typeof(SpriteAtlas);

			return source.ConcreteType == spriteAtlasType.FullName && target.ConcreteType == spriteType.FullName;
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

		private readonly SerializedPropertyTraverserSubSystem TraverserSubSystem = new ObjectSerializedPropertyTraverserSubSystem();
		
		public void GetDependenciesForId(string assetId, List<Dependency> dependencies)
		{
			List<Dependency> subSystemDependencies = new List<Dependency>();
			HashSet<string> foundDependenciesHashSet = new HashSet<string>();

			GetDependenciesForIdFromSerializedPropertyTraverser(assetId, subSystemDependencies);
			
			foreach (Dependency dependency in subSystemDependencies)
			{
				foundDependenciesHashSet.Add(dependency.Id);
				dependencies.Add(dependency);
			}
		}

		public void GetDependenciesForIdFromSerializedPropertyTraverser(string assetId, List<Dependency> dependencies)
		{
			if (TraverserSubSystem.Dependencies.ContainsKey(assetId))
			{
				foreach (var foundDependency in TraverserSubSystem.Dependencies[assetId])
				{
					string dependency = foundDependency.Id;

					// Avoid adding node itself as dependency
					if (dependency != assetId)
					{
						dependencies.Add(foundDependency);
					}
				}
			}
		}

		public bool IsGuidValid(string guid)
		{
			return true;
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

		public void Initialize(AssetDependencyCache cache, HashSet<string> changedAssets)
		{
			TraverserSubSystem.Clear();
			
			foreach (string assetId in changedAssets)
			{
				string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);
				if (!_inValidGuids.Contains(guid))
				{
					cache._hierarchyTraverser.AddAssetId(assetId, TraverserSubSystem);
				}
			}
		}
	}
}