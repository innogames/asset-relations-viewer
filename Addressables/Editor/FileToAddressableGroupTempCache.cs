using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.Addressables
{
	public static class FileToAddressableGroupDependency
	{
		public const string Name = "FileToAddressableGroup";
	}

	/// <summary>
	/// Dependency Cache to store connections from a File to AddressableAssetGroups
	/// This cache only makes sense to get bidirectional dependency done by
	/// AddressableAssetGroup -> Asset -> File -> AddressableAssetGroup
	/// </summary>
	[UsedImplicitly]
	public class FileToAddressableAssetGroupTempCache : AddressableGroupBaseCache, IDependencyCache
	{
		private const string Version = "2.0.0";
		private const string FileName = "FileToAddressableGroupDependencyCacheData_" + Version + ".cache";
		private const string HashesFileName = "FileToAddressableGroupDependencyCacheDataHashes_" + Version + ".cache";

		private GenericDependencyMappingNode[] _nodes = Array.Empty<GenericDependencyMappingNode>();

		private readonly List<string> _guidsInGroups = new List<string>();
		private readonly Dictionary<string, AddressableAssetGroup> _guidToGroup =
			new Dictionary<string, AddressableAssetGroup>();

		private CreatedDependencyCache _createdDependencyCache;

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		public bool CanUpdate() => !Application.isPlaying;

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			// Nothing to do
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
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, FileToAddressableGroupResolver.Id,
				    FileToAddressableGroupDependency.Name) && _dependencyLookup.ContainsKey(id))
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
			SaveGroupHashes(directory, HashesFileName);
		}

		public void InitLookup()
		{
		}

		public Type GetResolverType() => typeof(IAddressableAssetToGroupResolver);

		public void PreAssetUpdate(string[] allPaths)
		{
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

				EditorUtility.DisplayProgressBar("FileToAddressableAssetGroupTempCache", $"Preparing {group.Name}",
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

		public void PostAssetUpdate()
		{
			_nodes = _dependencyLookup.Values.ToArray();
		}

		public List<string> GetChangedAssetPaths(string[] allPaths, long[] pathTimestamps)
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

			var node = new GenericDependencyMappingNode(guid, FileNodeType.Name);
			node.Dependencies.Add(new Dependency(group.Name, FileToAddressableGroupDependency.Name,
				AddressableAssetGroupNodeType.Name, new[] { new PathSegment("AssetGroup", PathSegmentType.Property) }));

			return result;
		}
	}

	public interface IAddressableAssetToGroupResolver : IDependencyResolver
	{
	}

	[UsedImplicitly]
	public class FileToAddressableGroupResolver : IAddressableAssetToGroupResolver
	{
		public const string Id = "FileToAddressableGroupResolver";

		private readonly string[] ConnectionTypes = { FileToAddressableGroupDependency.Name };

		private const string ConnectionTypeDescription =
			"Dependencies from the file to the AddressableAssetGroup the file is part of";

		private static readonly DependencyType DependencyType = new AssetToAddressableAssetGroupDependencyType(
			"File->AddressableAssetGroup", new Color(0.85f, 0.55f, 0.35f), false, true, ConnectionTypeDescription);

		public class AssetToAddressableAssetGroupDependencyType : DependencyType
		{
			public AssetToAddressableAssetGroupDependencyType(string name, Color color, bool isIndirect, bool isHard,
				string description) : base(name, color, isIndirect, isHard, description)
			{
			}

			public override bool IsHardConnection(Node source, Node target)
			{
				var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

				foreach (var group in settings.groups)
				{
					if (group.Name == target.Name && group.HasSchema<BundledAssetGroupSchema>())
					{
						var groupSchema = group.GetSchema<BundledAssetGroupSchema>();
						return groupSchema.IncludeInBuild;
					}
				}

				return false;
			}
		}

		public string[] GetDependencyTypes() => ConnectionTypes;

		public string GetId() => Id;

		public DependencyType GetDependencyTypeForId(string typeId) => DependencyType;
	}
}
