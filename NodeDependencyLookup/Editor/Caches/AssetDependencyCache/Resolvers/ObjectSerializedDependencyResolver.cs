using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * Resolver for resolving Object references by using the SerializedPropertySearcher
	 * This one provided hierarchy and property pathes but is most likely slower than the SimpleObjectResolver
	 */
	public class ObjectSerializedDependencyResolver : IAssetDependencyResolver
	{
		private static ConnectionType ObjectType = new ConnectionType(new Color(0.8f, 0.8f, 0.8f), false, true);

		private readonly HashSet<string> _inValidGuids = new HashSet<string>();

		public const string NodeType = "Asset";
		public const string ResolvedType = "Object";
		public const string Id = "ObjectSerializedDependencyResolver";
		
		public readonly SerializedPropertyTraverserSubSystem TraverserSubSystem = new ObjectSerializedPropertyTraverserSubSystem();
		
		public void GetDependenciesForId(string guid, List<Dependency> dependencies)
		{
			List<Dependency> subSystemDependencies = new List<Dependency>();
			HashSet<string> foundDependenciesHashSet = new HashSet<string>();

			GetDependenciesForIdFromSerializedPropertyTraverser(guid, subSystemDependencies);
			
			foreach (Dependency dependency in subSystemDependencies)
			{
				foundDependenciesHashSet.Add(dependency.Id);
				dependencies.Add(dependency);
			}
		}

		public void GetDependenciesForIdFromSerializedPropertyTraverser(string guid, List<Dependency> dependencies)
		{
			if (TraverserSubSystem.Dependencies.ContainsKey(guid))
			{
				foreach (var foundDependency in TraverserSubSystem.Dependencies[guid])
				{
					string dependencyGuid = foundDependency.Id;

					// Avoid adding node itself as dependency
					if (dependencyGuid != guid)
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

		public ConnectionType GetDependencyTypeForId(string typeId)
		{
			return ObjectType;
		}

		public string[] GetConnectionTypes()
		{
			return new[] { ResolvedType };
		}

		public void SetValidGUIDs()
		{
			_inValidGuids.Clear();

			string[] filters = 
			{
				"t:Texture",
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

		public void Initialize(AssetDependencyCache cache, HashSet<string> changedAssets, ProgressBase progress)
		{
			TraverserSubSystem.Clear();
			
			foreach (string guid in changedAssets)
			{
				if (!_inValidGuids.Contains(guid))
				{
					cache._hierarchyTraverser.AddGuid(guid, TraverserSubSystem);
				}
			}
		}
	}
}