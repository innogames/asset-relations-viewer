﻿using System;
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

		public bool Update(ResolverUsageDefinitionList resolverUsages, bool shouldUpdate)
		{
			if(!shouldUpdate && Nodes.Length > 0)
			{
				return false;
			}

			AddressableAssetSettings settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

			if (settings == null)
			{
				return false;
			}

			CacheUpdateInfo resolverUpdateInfo = resolverUsages.GetUpdateStateForResolver(typeof(AddressableAssetGroupResolver));
			RelationLookup.RelationsLookup assetToFileLookup = RelationLookup.GetAssetToFileLookup(resolverUpdateInfo);

			Lookup.Clear();
			Nodes = new GenericDependencyMappingNode[0];

			List<GenericDependencyMappingNode> nodes = new List<GenericDependencyMappingNode>();

			for (int i = 0; i < settings.groups.Count; ++i)
			{
				AddressableAssetGroup group = settings.groups[i];
				EditorUtility.DisplayProgressBar("FileToAddressableAssetGroupTempCache", $"Getting dependencies for {group.Name}", i / (float)(settings.groups.Count));

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

						GenericDependencyMappingNode node = new GenericDependencyMappingNode(fileNode.Id, FileNodeType.Name);
						node.Dependencies.Add(new Dependency(group.Name, FileToAddressableGroupDependency.Name, AddressableAssetGroupNodeType.Name, new []{new PathSegment("AssetGroup", PathSegmentType.Property)}));

						nodes.Add(node);
						Lookup.Add(node.Id, node);
					}
				}
			}

			Nodes = nodes.ToArray();
			return true;
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
			if(NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, FileToAddressableGroupResolver.Id, FileToAddressableGroupDependency.Name) && Lookup.ContainsKey(id))
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
		private const string ConnectionTypeDescription = "Dependencies from the file to the AddressableAssetGroup the file is part of";
		private static DependencyType DependencyType = new AssetToAddressableAssetGroupDependencyType("File->AddressableAssetGroup", new Color(0.85f, 0.55f, 0.35f), false, true, ConnectionTypeDescription);

		public class AssetToAddressableAssetGroupDependencyType : DependencyType
		{
			public AssetToAddressableAssetGroupDependencyType(string name, Color color, bool isIndirect, bool isHard, string description) :
				base(name, color, isIndirect, isHard, description)
			{
			}

			public override bool IsHardConnection(Node source, Node target)
			{
				AddressableAssetSettings settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
				foreach (AddressableAssetGroup group in settings.groups)
				{
					if (group.Name == target.Name)
					{
						if (group.HasSchema<BundledAssetGroupSchema>())
						{
							BundledAssetGroupSchema groupSchema = group.GetSchema<BundledAssetGroupSchema>();
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