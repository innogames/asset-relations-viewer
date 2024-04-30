using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public static class AssetToFileDependency
	{
		public const string Name = "AssetToFile";
	}

	/// <summary>
	/// Cache to find get mapping of assets to the file the asset is included in
	/// </summary>
	public class AssetToFileDependencyCache : IAssetBasedDependencyCache
	{
		private const string Version = "3.0.0";
		private const string FileName = "AssetToFileDependencyCacheData_" + Version + ".cache";

		private readonly Dictionary<string, GenericDependencyMappingNode> _fileNodesDict =
			new Dictionary<string, GenericDependencyMappingNode>();

		private FileToAssetsMapping[] _fileToAssetsMappings = Array.Empty<FileToAssetsMapping>();

		private CreatedDependencyCache _createdDependencyCache;

		private bool _isLoaded;

		private Dictionary<string, FileToAssetsMapping> _fileToAssetMappingLookup =
			new Dictionary<string, FileToAssetsMapping>();

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		public List<string> GetChangedAssetPaths(string[] allPathes, long[] pathTimestamps)
		{
			var changedAssetPaths = new List<string>();
			var fileToAssetMappingDictionary = RelationLookup.ConvertToDictionary(_fileToAssetsMappings);

			for (var i = 0; i < allPathes.Length; i++)
			{
				var path = allPathes[i];
				var timeStamp = pathTimestamps[i];

				var guid = AssetDatabase.AssetPathToGUID(path);
				var changed = false;

				if (fileToAssetMappingDictionary.TryGetValue(guid, out var fileToAssetsMapping))
				{
					if (fileToAssetsMapping.Timestamp != timeStamp)
					{
						changed = true;
					}
				}
				else
				{
					changed = true;
				}

				if (changed)
				{
					changedAssetPaths.Add(path);
				}
			}

			return changedAssetPaths;
		}

		public void PreAssetUpdate()
		{
			_fileToAssetMappingLookup = RelationLookup.ConvertToDictionary(_fileToAssetsMappings);
			// TODO
			//NodeDependencyLookupUtility.RemoveNonExistingFilesFromIdentifyableList(paths, ref _fileToAssetsMappings);
		}

		public void PostAssetUpdate()
		{
			_fileToAssetsMappings = _fileToAssetMappingLookup.Values.ToArray();
		}

		public List<IDependencyMappingNode> UpdateAssetsForPath(string path, long timeStamp,
			List<AssetListEntry> assetEntries)
		{
			var result = new List<IDependencyMappingNode>();

			foreach (var resolverUsage in _createdDependencyCache.ResolverUsages)
			{
				var resolver = (IAssetToFileDependencyResolver)resolverUsage.Resolver;
				resolver.Initialize(this);

				FindDependenciesForAsset(resolver, path, timeStamp, assetEntries, result);
			}

			result.Add(new GenericDependencyMappingNode(AssetDatabase.AssetPathToGUID(path), FileNodeType.Name));

			return result;
		}

		private void FindDependenciesForAsset(IAssetToFileDependencyResolver resolver, string path, long timeStamp,
			List<AssetListEntry> entries, List<IDependencyMappingNode> nodes)
		{
			_fileToAssetMappingLookup.Remove(AssetDatabase.AssetPathToGUID(path));

			foreach (var entry in entries)
			{
				GetDependenciesForAssetInResolver(entry.AssetId, timeStamp, resolver, nodes);
			}
		}

		public bool CanUpdate() => true;

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			if (!shouldUpdate)
			{
				yield break;
			}

			yield return null;
		}

		private void GetDependenciesForAssetInResolver(string assetId, long timeStamp,
			IAssetToFileDependencyResolver resolver, List<IDependencyMappingNode> nodes)
		{
			var fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

			if (!_fileToAssetMappingLookup.ContainsKey(fileId))
			{
				_fileToAssetMappingLookup.Add(fileId, new FileToAssetsMapping { FileId = fileId });
			}

			var fileToAssetsMapping = _fileToAssetMappingLookup[fileId];
			var genericDependencyMappingNode = fileToAssetsMapping.GetFileNode(assetId);

			nodes.Add(genericDependencyMappingNode);

			genericDependencyMappingNode.Dependencies.Clear();
			resolver.GetDependenciesForAsset(assetId, genericDependencyMappingNode.Dependencies);
			fileToAssetsMapping.Timestamp = timeStamp;
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (var fileToAssetsMapping in _fileToAssetsMappings)
			{
				foreach (var fileNode in fileToAssetsMapping.FileNodes)
				{
					nodes.Add(fileNode);
				}
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AssetToFileDependencyResolver.Id,
				    AssetToFileDependency.Name))
			{
				return _fileNodesDict[id].Dependencies;
			}

			return new List<Dependency>();
		}

		public void Load(string directory)
		{
			if (_isLoaded)
			{
				return;
			}

			var path = Path.Combine(directory, FileName);

			if (File.Exists(path))
			{
				var bytes = File.ReadAllBytes(path);
				_fileToAssetsMappings = AssetToFileDependencyCacheSerializer.Deserialize(bytes);
			}
			else
			{
				_fileToAssetsMappings = Array.Empty<FileToAssetsMapping>();
			}

			_isLoaded = true;
		}

		public void Save(string directory)
		{
			var path = Path.Combine(directory, FileName);

			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllBytes(path, AssetToFileDependencyCacheSerializer.Serialize(_fileToAssetsMappings));
		}

		public void InitLookup()
		{
			_fileNodesDict.Clear();

			foreach (var fileToAssetsMapping in _fileToAssetsMappings)
			{
				foreach (var fileNode in fileToAssetsMapping.FileNodes)
				{
					_fileNodesDict.Add(fileNode.Id, fileNode);
				}
			}
		}

		public Type GetResolverType() => typeof(IAssetToFileDependencyResolver);
	}

	public class FileToAssetsMapping : IIdentifyable
	{
		public string FileId;
		public long Timestamp;

		public string Id => FileId;

		public List<GenericDependencyMappingNode> FileNodes = new List<GenericDependencyMappingNode>();

		public GenericDependencyMappingNode GetFileNode(string id)
		{
			foreach (var fileNode in FileNodes)
			{
				if (fileNode.Id == id)
				{
					return fileNode;
				}
			}

			var newGenericDependencyMappingNode = new GenericDependencyMappingNode(id, AssetNodeType.Name);
			FileNodes.Add(newGenericDependencyMappingNode);

			return newGenericDependencyMappingNode;
		}
	}

	public interface IAssetToFileDependencyResolver : IDependencyResolver
	{
		void Initialize(AssetToFileDependencyCache cache);
		void GetDependenciesForAsset(string assetId, List<Dependency> dependencies);
	}

	public class AssetToFileDependencyResolver : IAssetToFileDependencyResolver
	{
		private const string ConnectionTypeDescription =
			"Dependencies between assets to the file they are contained in";

		private static readonly DependencyType fileDependencyType = new DependencyType("Asset->File",
			new Color(0.7f, 0.9f, 0.7f), false, true, ConnectionTypeDescription);

		public const string Id = "AssetToFileDependencyResolver";

		public string[] GetDependencyTypes()
		{
			return new[] { AssetToFileDependency.Name };
		}

		public string GetId() => Id;

		public DependencyType GetDependencyTypeForId(string typeId) => fileDependencyType;

		public void Initialize(AssetToFileDependencyCache cache)
		{
		}

		public void GetDependenciesForAsset(string assetId, List<Dependency> dependencies)
		{
			var fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);
			dependencies.Add(new Dependency(fileId, AssetToFileDependency.Name, FileNodeType.Name,
				new[] { new PathSegment(FileNodeType.Name, PathSegmentType.Property) }));
		}
	}
}
