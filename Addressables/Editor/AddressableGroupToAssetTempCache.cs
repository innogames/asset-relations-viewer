using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.Addressables
{
	public static class AddressableAssetGroupNodeType
	{
		public const string Name = "AddressableAssetGroup";
	}

	public static class AddressableGroupToAssetDependency
	{
		public const string Name = "AddressableGroupToAsset";
	}

	/// <summary>
	/// DependencyCache to store connections from an AddressableAssetGroup to an Asset
	/// </summary>
	[UsedImplicitly]
	public class AddressableGroupToAssetTempCache : IAssetBasedDependencyCache
	{
		private const string Version = "2.0.0";
		private const string FileName = "AddressableGroupToAssetDependencyCacheData_" + Version + ".cache";

		private GenericDependencyMappingNode[] Nodes = Array.Empty<GenericDependencyMappingNode>();

		private Dictionary<string, GenericDependencyMappingNode> Lookup =
			new Dictionary<string, GenericDependencyMappingNode>();

		private CreatedDependencyCache _createdDependencyCache;
		private readonly List<string> _guidsInGroups = new List<string>();
		private readonly Dictionary<string, AddressableAssetGroup> _guidToGroup =
			new Dictionary<string, AddressableAssetGroup>();

		private readonly Dictionary<AddressableAssetGroup, GenericDependencyMappingNode> _groupDependencies =
			new Dictionary<AddressableAssetGroup, GenericDependencyMappingNode>();

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
			// Nothing to do
			yield return null;
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (var node in Nodes)
			{
				nodes.Add(node);
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AddressableAssetGroupResolver.Id,
				    AddressableGroupToAssetDependency.Name))
			{
				return Lookup[id].Dependencies;
			}

			return new List<Dependency>();
		}

		public void Load(string directory)
		{
			var path = Path.Combine(directory, FileName);

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

		public void PreAssetUpdate()
		{
			var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

			if (settings == null)
			{
				Debug.LogWarning(
					"Could not find UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings. Please add Addressable Settings if you use the AddressableGroup -> Asset resolver");
				return;
			}

			for (var i = 0; i < settings.groups.Count; ++i)
			{
				var group = settings.groups[i];
				EditorUtility.DisplayProgressBar("AddressableAssetGroupTempCache",
					$"Preparing {group.Name}", i / (float) settings.groups.Count);

				foreach (var addressableAssetEntry in group.entries)
				{
					var entries = new List<AddressableAssetEntry>();
					addressableAssetEntry.GatherAllAssets(entries, true, true, false);

					foreach (var assetEntry in entries)
					{
						_guidsInGroups.Add(assetEntry.guid);
						_guidToGroup.Add(assetEntry.guid, group);
					}
				}
			}
		}

		public void PostAssetUpdate()
		{
			var nodes = new List<GenericDependencyMappingNode>();

			foreach (var pair in _groupDependencies)
			{
				var node = pair.Value;
				nodes.Add(node);
				Lookup.Add(node.Id, node);
			}

			Nodes = nodes.ToArray();
		}

		public List<IDependencyMappingNode> UpdateAssetsForPath(string path, long timeStamp, List<AssetListEntry> assetEntries)
		{
			var result = new List<IDependencyMappingNode>();

			if (assetEntries.Count == 0)
			{
				return result;
			}

			var guid = AssetDatabase.AssetPathToGUID(path);
			var group = _guidToGroup[guid];

			if (!_groupDependencies.ContainsKey(group))
			{
				_groupDependencies.Add(group,
					new GenericDependencyMappingNode(group.Name, AddressableAssetGroupNodeType.Name));
			}

			var genericDependencyMappingNode = _groupDependencies[group];
			var componentName = "GroupUsage " + genericDependencyMappingNode.Dependencies.Count;

			genericDependencyMappingNode.Dependencies.Add(new Dependency(assetEntries[0].AssetId, AddressableGroupToAssetDependency.Name,
				AssetNodeType.Name, new[] {new PathSegment(componentName, PathSegmentType.Property)}));

			return result;
		}

		public List<string> GetChangedAssetPaths(string[] allPathes, long[] timeStamps)
		{
			var result = new List<string>();

			foreach (var guid in _guidsInGroups)
			{
				result.Add(AssetDatabase.GUIDToAssetPath(guid));
			}

			return result;
		}
	}

	public interface IAddressableGroupResolver : IDependencyResolver
	{
	}

	public class AddressableAssetGroupResolver : IAddressableGroupResolver
	{
		public const string Id = "AddressableAssetGroupResolver";

		private readonly string[] _connectionTypes = {AddressableGroupToAssetDependency.Name};

		private const string ConnectionTypeDescription =
			"Dependencies from the AddressableAssetGroup to its containing assets";

		private static readonly DependencyType _dependencyType = new DependencyType("AddressableAssetGroup->Asset",
			new Color(0.85f, 0.65f, 0.55f), false, true, ConnectionTypeDescription);

		public string[] GetDependencyTypes()
		{
			return _connectionTypes;
		}

		public string GetId()
		{
			return Id;
		}

		public DependencyType GetDependencyTypeForId(string typeId)
		{
			return _dependencyType;
		}
	}
}