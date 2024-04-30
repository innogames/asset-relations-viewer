using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
	public class FileToAddressableAssetGroupTempCache : IAssetBasedDependencyCache
	{
		private const string Version = "2.0.0";
		private const string FileName = "FileToAddressableGroupDependencyCacheData_" + Version + ".cache";

		private GenericDependencyMappingNode[] _nodes = Array.Empty<GenericDependencyMappingNode>();

		private Dictionary<string, GenericDependencyMappingNode> _lookup =
			new Dictionary<string, GenericDependencyMappingNode>();

		private readonly List<string> _guidsInGroups = new List<string>();
		private readonly Dictionary<string, AddressableAssetGroup> _guidToGroup =
			new Dictionary<string, AddressableAssetGroup>();

		private List<GenericDependencyMappingNode> _mappingNodes = new List<GenericDependencyMappingNode>();

		private CreatedDependencyCache _createdDependencyCache;

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
			foreach (var node in _nodes)
			{
				nodes.Add(node);
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, FileToAddressableGroupResolver.Id,
				    FileToAddressableGroupDependency.Name) && _lookup.ContainsKey(id))
			{
				return _lookup[id].Dependencies;
			}

			return new List<Dependency>();
		}

		public void Load(string directory)
		{
			var path = Path.Combine(directory, FileName);

			_nodes = CacheSerializerUtils.LoadGenericLookup(path);
			_lookup = CacheSerializerUtils.GenerateIdLookup(_nodes);
		}

		public void Save(string directory)
		{
			CacheSerializerUtils.SaveGenericMapping(directory, FileName, _nodes);
		}

		public void InitLookup()
		{
		}

		public Type GetResolverType()
		{
			return typeof(IAddressableAssetToGroupResolver);
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
				EditorUtility.DisplayProgressBar("FileToAddressableAssetGroupTempCache",
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
			foreach (var node in _mappingNodes)
			{
				_lookup.Add(node.Id, node);
			}

			_nodes = _mappingNodes.ToArray();
		}

		public List<string> GetChangedAssetPaths(string[] allPaths, long[] pathTimestamps)
		{
			var result = new List<string>();

			foreach (var guid in _guidsInGroups)
			{
				result.Add(AssetDatabase.GUIDToAssetPath(guid));
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
				AddressableAssetGroupNodeType.Name,
				new[] {new PathSegment("AssetGroup", PathSegmentType.Property)}));

			_mappingNodes.Add(node);

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

		private readonly string[] ConnectionTypes = {FileToAddressableGroupDependency.Name};

		private const string ConnectionTypeDescription =
			"Dependencies from the file to the AddressableAssetGroup the file is part of";

		private static readonly DependencyType DependencyType = new AssetToAddressableAssetGroupDependencyType(
			"File->AddressableAssetGroup", new Color(0.85f, 0.55f, 0.35f), false, true, ConnectionTypeDescription);

		public class AssetToAddressableAssetGroupDependencyType : DependencyType
		{
			public AssetToAddressableAssetGroupDependencyType(string name, Color color, bool isIndirect, bool isHard,
				string description) :
				base(name, color, isIndirect, isHard, description)
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