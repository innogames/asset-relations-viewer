using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.Addressables
{
	public class FileToAddressableGroupDependency
	{
		public const string Name = "FileToAddressableGroup";
	}

	public class FileToAddressableAssetGroupTempCache : IDependencyCache
	{
		private const string Version = "2.0.0";
		private const string FileName = "FileToAddressableGroupDependencyCacheData_" + Version + ".cache";

		private GenericDependencyMappingNode[] Nodes = new GenericDependencyMappingNode[0];

		private Dictionary<string, GenericDependencyMappingNode> Lookup =
			new Dictionary<string, GenericDependencyMappingNode>();

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
			if (!shouldUpdate && Nodes.Length > 0)
			{
				yield break;
			}

			var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

			if (settings == null)
			{
				yield break;
			}

			var resolverUpdateInfo = resolverUsages.GetUpdateStateForResolver(typeof(AddressableAssetGroupResolver));
			var assetToFileLookup = new RelationLookup.RelationsLookup();
			yield return RelationLookup.GetAssetToFileLookup(cacheUpdateSettings, resolverUpdateInfo,
				assetToFileLookup);

			Lookup.Clear();
			Nodes = new GenericDependencyMappingNode[0];

			var nodes = new List<GenericDependencyMappingNode>();

			for (var i = 0; i < settings.groups.Count; ++i)
			{
				var group = settings.groups[i];
				EditorUtility.DisplayProgressBar("FileToAddressableAssetGroupTempCache",
					$"Getting dependencies for {group.Name}", i / (float) settings.groups.Count);

				foreach (var addressableAssetEntry in group.entries)
				{
					var entries = new List<AddressableAssetEntry>();
					addressableAssetEntry.GatherAllAssets(entries, true, true, false);

					foreach (var assetEntry in entries)
					{
						var fileNode = assetToFileLookup.GetNode(assetEntry.guid, FileNodeType.Name);

						if (fileNode == null)
						{
							continue;
						}

						var node = new GenericDependencyMappingNode(fileNode.Id, FileNodeType.Name);
						node.Dependencies.Add(new Dependency(group.Name, FileToAddressableGroupDependency.Name,
							AddressableAssetGroupNodeType.Name,
							new[] {new PathSegment("AssetGroup", PathSegmentType.Property)}));

						nodes.Add(node);
						Lookup.Add(node.Id, node);
					}
				}
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
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, FileToAddressableGroupResolver.Id,
				    FileToAddressableGroupDependency.Name) && Lookup.ContainsKey(id))
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
			return typeof(IAddressableAssetToGroupResolver);
		}
	}

	public interface IAddressableAssetToGroupResolver : IDependencyResolver
	{
	}

	public class FileToAddressableGroupResolver : IAddressableAssetToGroupResolver
	{
		public const string Id = "FileToAddressableGroupResolver";

		private string[] ConnectionTypes = {FileToAddressableGroupDependency.Name};

		private const string ConnectionTypeDescription =
			"Dependencies from the file to the AddressableAssetGroup the file is part of";

		private static DependencyType DependencyType = new AssetToAddressableAssetGroupDependencyType(
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
					if (group.Name == target.Name)
					{
						if (group.HasSchema<BundledAssetGroupSchema>())
						{
							var groupSchema = group.GetSchema<BundledAssetGroupSchema>();
							return groupSchema.IncludeInBuild;
						}
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