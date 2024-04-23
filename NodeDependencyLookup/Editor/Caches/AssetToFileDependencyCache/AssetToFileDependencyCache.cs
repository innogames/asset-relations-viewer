using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public static class AssetToFileDependency
	{
		public const string Name = "AssetToFile";
	}

	/// <summary>
	/// Cache to find get mapping of assets to the file the asset is included in
	/// </summary>
	public class AssetToFileDependencyCache : IDependencyCache
	{
		private const string Version = "3.0.0";
		private const string FileName = "AssetToFileDependencyCacheData_" + Version + ".cache";

		private readonly Dictionary<string, GenericDependencyMappingNode> _fileNodesDict =
			new Dictionary<string, GenericDependencyMappingNode>();

		private FileToAssetsMapping[] _fileToAssetsMappings = Array.Empty<FileToAssetsMapping>();

		private CreatedDependencyCache _createdDependencyCache;

		private bool _isLoaded;

		private readonly List<AssetListEntry> _tmpEntries = new List<AssetListEntry>();

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		private IEnumerator FindDependenciesInChangedAssets(CacheUpdateSettings settings, string[] paths,
			IAssetToFileDependencyResolver resolver, long[] timestamps)
		{
			float lastDisplayedPercentage = 0;

			var fileToAssetMappingDictionary = RelationLookup.ConvertToDictionary(_fileToAssetsMappings);
			var cacheUpdateResourcesCleaner = new CacheUpdateResourcesCleaner();

			for (int i = 0, j = 0; i < paths.Length; ++i)
			{
				var progressPercentage = (float) i / paths.Length;

				if (progressPercentage - lastDisplayedPercentage > 0.01f)
				{
					if (EditorUtility.DisplayCancelableProgressBar("AssetToFileDependencyCache",
						    $"Finding changed assets {i}", (float) i / paths.Length))
					{
						throw new DependencyUpdateAbortedException();
					}

					lastDisplayedPercentage = progressPercentage;
				}

				var path = paths[i];
				var guid = AssetDatabase.AssetPathToGUID(path);
				var changed = false;

				if (fileToAssetMappingDictionary.ContainsKey(guid))
				{
					var fileToAssetsMapping = fileToAssetMappingDictionary[guid];
					var timeStamp = timestamps[i];

					if (fileToAssetsMapping.Timestamp != timeStamp)
					{
						changed = true;
					}
				}
				else
				{
					changed = true;
				}

				cacheUpdateResourcesCleaner.Clean(settings, j);

				if (changed)
				{
					j++;
					FindDependenciesForAsset(resolver, path, timestamps[i], fileToAssetMappingDictionary);
				}

				if (j % 2000 == 0)
				{
					yield return null;
				}
			}

			_fileToAssetsMappings = fileToAssetMappingDictionary.Values.ToArray();
		}

		private void FindDependenciesForAsset(IAssetToFileDependencyResolver resolver, string path, long timeStamp,
			Dictionary<string, FileToAssetsMapping> fileToAssetMappingDictionary)
		{
			_tmpEntries.Clear();
			NodeDependencyLookupUtility.AddAssetsToList(_tmpEntries, path);

			// Delete to avoid piling up removed subassets from file
			fileToAssetMappingDictionary.Remove(AssetDatabase.AssetPathToGUID(path));

			foreach (var entry in _tmpEntries)
			{
				GetDependenciesForAssetInResolver(entry.AssetId, entry.Asset, timeStamp, resolver,
					fileToAssetMappingDictionary);
			}
		}

		public bool CanUpdate()
		{
			return true;
		}

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			if (!shouldUpdate)
			{
				yield break;
			}

			yield return GetDependenciesForAssets(cacheUpdateSettings, _createdDependencyCache);
		}

		private IEnumerator GetDependenciesForAssets(CacheUpdateSettings cacheUpdateSettings,
			CreatedDependencyCache createdDependencyCache)
		{
			var paths = NodeDependencyLookupUtility.GetAllAssetPaths(true);
			EditorUtility.DisplayProgressBar("AssetToFileDependencyCache", "Checking file timestamps", 0);
			Profiler.BeginSample("TimeStamps");
			var timestamps = NodeDependencyLookupUtility.GetTimeStampsForFiles(paths);
			Profiler.EndSample();
			NodeDependencyLookupUtility.RemoveNonExistingFilesFromIdentifyableList(paths, ref _fileToAssetsMappings);

			foreach (var resolverUsage in createdDependencyCache.ResolverUsages)
			{
				if (!(resolverUsage.Resolver is IAssetToFileDependencyResolver))
				{
					continue;
				}

				var resolver = (IAssetToFileDependencyResolver) resolverUsage.Resolver;
				resolver.Initialize(this);

				yield return FindDependenciesInChangedAssets(cacheUpdateSettings, paths, resolver, timestamps);
			}
		}

		private void GetDependenciesForAssetInResolver(string assetId, Object asset, long timeStamp,
			IAssetToFileDependencyResolver resolver, Dictionary<string, FileToAssetsMapping> resultList)
		{
			var fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

			if (!resultList.ContainsKey(fileId))
			{
				resultList.Add(fileId, new FileToAssetsMapping {FileId = fileId});
			}

			var fileToAssetsMapping = resultList[fileId];
			var genericDependencyMappingNode = fileToAssetsMapping.GetFileNode(assetId);

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
			var path = Path.Combine(directory, FileName);

			if (_isLoaded)
				return;

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

		public Type GetResolverType()
		{
			return typeof(IAssetToFileDependencyResolver);
		}
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

		private static DependencyType fileDependencyType = new DependencyType("Asset->File",
			new Color(0.7f, 0.9f, 0.7f), false, true, ConnectionTypeDescription);

		public const string Id = "AssetToFileDependencyResolver";

		public string[] GetDependencyTypes()
		{
			return new[] {AssetToFileDependency.Name};
		}

		public string GetId()
		{
			return Id;
		}

		public DependencyType GetDependencyTypeForId(string typeId)
		{
			return fileDependencyType;
		}

		public void Initialize(AssetToFileDependencyCache cache)
		{
		}

		public void GetDependenciesForAsset(string assetId, List<Dependency> dependencies)
		{
			var fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);
			dependencies.Add(new Dependency(fileId, AssetToFileDependency.Name, FileNodeType.Name,
				new[] {new PathSegment(FileNodeType.Name, PathSegmentType.Property)}));
		}
	}
}