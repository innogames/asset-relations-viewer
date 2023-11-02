using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.Addressables
{
	public class AddressableAssetGroupNodeType
	{
		public const string Name = "AddressableAssetGroup";
	}

	public class AddressableGroupToAssetDependency
	{
		public const string Name = "AddressableGroupToAsset";
	}

	public class AddressableGroupToAssetTempCache : IDependencyCache
	{
		private const string Version = "2.0.0";
		private const string FileName = "AddressableGroupToAssetDependencyCacheData_" + Version + ".cache";

		private GenericDependencyMappingNode[] Nodes = new GenericDependencyMappingNode[0];
		private Dictionary<string, GenericDependencyMappingNode> Lookup = new Dictionary<string, GenericDependencyMappingNode>();

		private CreatedDependencyCache _createdDependencyCache;

		public void ClearFile(string directory)
		{
		}

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		public bool CanUpdate()
		{
			return !Application.isPlaying;
		}

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			if(!shouldUpdate && Nodes.Length > 0)
			{
				yield break;
			}

			AddressableAssetSettings settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

			if (settings == null)
			{
				Debug.LogWarning("Could not find UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings. Please add Addressable Settings if you use the AddressableGroup -> Asset resolver");
				yield break;
			}

			CacheUpdateInfo resolverUpdateInfo = resolverUsages.GetUpdateStateForResolver(typeof(AddressableAssetGroupResolver));
			RelationLookup.RelationsLookup assetToFileLookup = RelationLookup.GetAssetToFileLookup(cacheUpdateSettings, resolverUpdateInfo);

			Lookup.Clear();

			Nodes = new GenericDependencyMappingNode[0];

			List<GenericDependencyMappingNode> nodes = new List<GenericDependencyMappingNode>();

			for (int i = 0; i < settings.groups.Count; ++i)
			{
				AddressableAssetGroup group = settings.groups[i];
				EditorUtility.DisplayProgressBar("AddressableAssetGroupTempCache", $"Getting dependencies for {group.Name}", i / (float)(settings.groups.Count));
				GenericDependencyMappingNode node = new GenericDependencyMappingNode(group.Name, AddressableAssetGroupNodeType.Name);

				int g = 0;

				foreach (AddressableAssetEntry addressableAssetEntry in group.entries)
				{
					List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
					addressableAssetEntry.GatherAllAssets(entries, true, true, false);

					foreach (AddressableAssetEntry assetEntry in entries)
					{
						Node fileNode = assetToFileLookup.GetNode(assetEntry.guid, FileNodeType.Name);

						if (fileNode == null)
						{
							continue;
						}

						string assetId = fileNode.Referencers[0].Node.Id;
						string componentName = "GroupUsage " + g++;
						node.Dependencies.Add(new Dependency(assetId, AddressableGroupToAssetDependency.Name, AssetNodeType.Name, new []{new PathSegment(componentName, PathSegmentType.Property)}));
					}
				}

				nodes.Add(node);
				Lookup.Add(node.Id, node);
			}

			Nodes = nodes.ToArray();
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (IDependencyMappingNode node in Nodes)
			{
				nodes.Add(node);
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if(NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AddressableAssetGroupResolver.Id, AddressableGroupToAssetDependency.Name))
			{
				return Lookup[id].Dependencies;
			}

			return new List<Dependency>();
		}

		public void Load(string directory)
		{
			string path = Path.Combine(directory, FileName);

			Nodes = CacheSerializerUtils.LoadGenericLookup(path);
			Lookup = CacheSerializerUtils.GenerateIdLookup(Nodes);
		}

		public void Save(string directory)
		{
			CacheSerializerUtils.SaveGenericMapping(directory, FileName, Nodes);
		}

		public void InitLookup()
		{
		}

		public Type GetResolverType()
		{
			return typeof(IAddressableGroupResolver);
		}
	}

	public interface IAddressableGroupResolver : IDependencyResolver
	{
	}

	public class AddressableAssetGroupResolver : IAddressableGroupResolver
	{
		public const string Id = "AddressableAssetGroupResolver";

		private string[] ConnectionTypes = {AddressableGroupToAssetDependency.Name};
		private const string ConnectionTypeDescription = "Dependencies from the AddressableAssetGroup to its containing assets";
		private static DependencyType DependencyType = new DependencyType("AddressableAssetGroup->Asset", new Color(0.85f, 0.65f, 0.55f), false, true, ConnectionTypeDescription);

		public string[] GetDependencyTypes()
		{
			return ConnectionTypes;
		}

		public string GetId()
		{
			return Id;
		}

		public DependencyType GetDependencyTypeForId(string typeId)
		{
			return DependencyType;
		}
	}
}
