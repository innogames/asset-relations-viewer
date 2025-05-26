using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
	public class AddressableGroupToAssetTempCache : AddressableGroupBaseCache, IDependencyCache
	{
		private const string Version = "2.0.0";
		private const string FileName = "AddressableGroupToAssetDependencyCacheData_" + Version + ".cache";
		private const string HashesFileName = "AddressableGroupToAssetDependencyCacheDataHashes_" + Version + ".cache";

		private GenericDependencyMappingNode[] _nodes = Array.Empty<GenericDependencyMappingNode>();

		private CreatedDependencyCache _createdDependencyCache;
		private readonly List<string> _guidsInGroups = new List<string>();
		private readonly Dictionary<string, AddressableAssetGroup> _guidToGroup =
			new Dictionary<string, AddressableAssetGroup>();

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		public bool CanUpdate() => !Application.isPlaying;

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			yield return null;
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (var node in _nodes)
			{
				nodes.Add(node);
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AddressableAssetGroupResolver.Id,
				    AddressableGroupToAssetDependency.Name))
			{
				return _dependencyLookup[id].Dependencies;
			}

			return new List<Dependency>();
		}

		public void Load(string directory)
		{
			var path = Path.Combine(directory, FileName);

			_nodes = CacheSerializerUtils.LoadGenericMapping(path);
			_dependencyLookup = CacheSerializerUtils.GenerateIdLookup(_nodes);
			LoadGroupHashes(directory, HashesFileName);
		}

		public void Save(string directory)
		{
			CacheSerializerUtils.SaveGenericMapping(directory, FileName, _nodes);
			LoadGroupHashes(directory, HashesFileName);
		}

		public void InitLookup()
		{
		}

		public Type GetResolverType() => typeof(IAddressableGroupResolver);

		/// <summary>
		/// Find all groups and their assets that need to be updated
		/// </summary>
		public void PreAssetUpdate(string[] allPaths)
		{
			_groupsToBeUpdated.Clear();
			var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

			if (settings == null)
			{
				Debug.LogWarning(
					"Could not find UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings. Please add Addressable Settings if you use the AddressableGroup -> Asset resolver");
				return;
			}

			var stringBuilder = new StringBuilder();

			for (var i = 0; i < settings.groups.Count; ++i)
			{
				stringBuilder.Clear();
				var group = settings.groups[i];
				var groupName = group.Name;

				EditorUtility.DisplayProgressBar("AddressableAssetGroupTempCache", $"Preparing {groupName}",
					i / (float)settings.groups.Count);

				foreach (var addressableAssetEntry in group.entries)
				{
					var entries = new List<AddressableAssetEntry>();
					addressableAssetEntry.GatherAllAssets(entries, true, true, false);

					foreach (var assetEntry in entries)
					{
						_guidsInGroups.Add(assetEntry.guid);
						_guidToGroup.Add(assetEntry.guid, group);

						stringBuilder.Append(assetEntry.guid);
					}
				}

				UpdateHashLookupsForGroup(groupName, stringBuilder.ToString());
			}
		}

		public List<IDependencyMappingNode> UpdateAssetsForPath(string path, long timeStamp,
			List<AssetListEntry> assetEntries)
		{
			var result = new List<IDependencyMappingNode>();

			if (assetEntries.Count == 0)
			{
				return result;
			}

			var guid = AssetDatabase.AssetPathToGUID(path);
			var group = _guidToGroup[guid];
			var groupName = group.Name;

			if (!_dependencyLookup.ContainsKey(groupName))
			{
				_dependencyLookup.Add(groupName,
					new GenericDependencyMappingNode(group.Name, AddressableAssetGroupNodeType.Name));
			}

			var genericDependencyMappingNode = _dependencyLookup[groupName];
			var componentName = "GroupUsage " + genericDependencyMappingNode.Dependencies.Count;

			genericDependencyMappingNode.Dependencies.Add(new Dependency(assetEntries[0].AssetId,
				AddressableGroupToAssetDependency.Name, AssetNodeType.Name,
				new[] { new PathSegment(componentName, PathSegmentType.Property) }));

			return result;
		}

		public void PostAssetUpdate()
		{
			_nodes = _dependencyLookup.Values.ToArray();
		}

		public List<string> GetChangedAssetPaths(string[] allPaths, long[] timeStamps)
		{
			var result = new List<string>();

			foreach (var guid in _guidsInGroups)
			{
				if (_groupsToBeUpdated.Contains(_guidToGroup[guid].Name))
				{
					result.Add(AssetDatabase.GUIDToAssetPath(guid));
				}
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

		private readonly string[] _connectionTypes = { AddressableGroupToAssetDependency.Name };

		private const string ConnectionTypeDescription =
			"Dependencies from the AddressableAssetGroup to its containing assets";

		private static readonly DependencyType _dependencyType = new DependencyType("AddressableAssetGroup->Asset",
			new Color(0.85f, 0.65f, 0.55f), false, true, ConnectionTypeDescription);

		public string[] GetDependencyTypes() => _connectionTypes;

		public string GetId() => Id;

		public DependencyType GetDependencyTypeForId(string typeId) => _dependencyType;
	}
}
