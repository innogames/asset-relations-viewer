using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Profiling;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Cache to store all dependencies of assets to other assets
	/// </summary>
	public class AssetDependencyCache : IAssetBasedDependencyCache
	{
		private const string Version = "3.0.0";
		private const string FileName = "AssetDependencyCacheData";
		private const string VersionedFileName = FileName + "_" + Version + ".cache";

		private FileToAssetNode[] _fileToAssetNodes = Array.Empty<FileToAssetNode>();
		private readonly Dictionary<string, AssetNode> _assetNodesDict = new Dictionary<string, AssetNode>();
		private readonly AssetSerializedPropertyTraverser _hierarchyTraverser = new AssetSerializedPropertyTraverser();

		private readonly List<AssetListEntry> _entries = new List<AssetListEntry>();
		private readonly ResolverDependencySearchContext _searchContext = new ResolverDependencySearchContext();

		private CreatedDependencyCache _createdDependencyCache;
		private readonly List<IAssetDependencyResolver> _tmpResolvers = new List<IAssetDependencyResolver>();
		private Dictionary<string, FileToAssetNode> _tmpFileToAssetNodesLookup =
			new Dictionary<string, FileToAssetNode>();
		private readonly List<IAssetDependencyResolver> _resolversToExecute = new List<IAssetDependencyResolver>();

		public Type GetResolverType() => typeof(IAssetDependencyResolver);

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		public void Load(string directory)
		{
			Profiler.BeginSample("AssetDependencyCache Load");

			EditorUtility.DisplayProgressBar("AssetDependencyCache", "Loading cache", 0);
			var path = Path.Combine(directory, VersionedFileName);
			_fileToAssetNodes = new FileToAssetNode[0];

			if (File.Exists(path))
			{
				var bytes = File.ReadAllBytes(path);
				_fileToAssetNodes = AssetDependencyCacheSerializer.Deserialize(bytes);
			}

			Profiler.EndSample();
		}

		public void Save(string directory)
		{
			EditorUtility.DisplayProgressBar("AssetDependencyCache", "Saving cache", 0);
			var path = Path.Combine(directory, VersionedFileName);

			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllBytes(path, AssetDependencyCacheSerializer.Serialize(_fileToAssetNodes));
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (var fileToAssetNode in _fileToAssetNodes)
			{
				foreach (var assetNode in fileToAssetNode.AssetNodes)
				{
					nodes.Add(assetNode);
				}
			}
		}

		public void InitLookup()
		{
			_assetNodesDict.Clear();

			foreach (var fileToAssetNode in _fileToAssetNodes)
			{
				foreach (var assetNode in fileToAssetNode.AssetNodes)
				{
					_assetNodesDict.Add(assetNode.Id, assetNode);
				}
			}
		}

		public List<Dependency> GetDependenciesForId(string id) =>
			_assetNodesDict.TryGetValue(id, out var assetNode)
				? assetNode.GetDependenciesForResolverUsages(_createdDependencyCache.ResolverUsagesLookup)
				: new List<Dependency>();

		public bool CanUpdate() => !Application.isPlaying && !EditorApplication.isCompiling;

		public List<(string, long)> GetChangedAssetPaths()
		{
			var changedAssetPaths = new List<(string, long)>();

			var list = RelationLookup.ConvertToDictionary(_fileToAssetNodes);
			var paths = NodeDependencyLookupUtility.GetAllAssetPaths(true);
			NodeDependencyLookupUtility.RemoveNonExistingFilesFromIdentifyableList(paths, ref _fileToAssetNodes);

			EditorUtility.DisplayProgressBar("AssetDependencyCache", "Getting file timestamps", 0);
			var timestampLookup = NodeDependencyLookupUtility.GetTimeStampsForFilesDictionary(paths);
			EditorUtility.DisplayProgressBar("AssetDependencyCache", "Checking changed files", 0);

			foreach (var path in paths)
			{
				var timestamp = timestampLookup[path];
				var guid = AssetDatabase.AssetPathToGUID(path);
				var changed = false;

				foreach (var resolver in _tmpResolvers)
				{
					if (!resolver.IsGuidValid(guid))
					{
						continue;
					}

					if (list.TryGetValue(guid, out var fileToAssetNode))
					{
						if (fileToAssetNode.GetResolverTimeStamp(resolver.GetId()).TimeStamp != timestamp)
						{
							changed = true;
						}
					}
					else
					{
						changed = true;
					}
				}

				if (changed)
				{
					changedAssetPaths.Add((path, timestamp));
				}
			}

			return changedAssetPaths;
		}

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			if (!shouldUpdate)
			{
				yield break;
			}

			yield return null;
		}

		public void PreAssetUpdate()
		{
			_hierarchyTraverser.Initialize();
			_tmpResolvers.Clear();

			foreach (var resolverUsage in _createdDependencyCache.ResolverUsages)
			{
				if (!(resolverUsage.Resolver is IAssetDependencyResolver resolver))
				{
					Debug.LogError(
						$"AssetDependencyCache {resolverUsage.Resolver.GetType().Name} is not of baseType {nameof(IAssetDependencyResolver)}");
					continue;
				}

				resolver.Initialize(this);
				resolver.SetValidGUIDs();

				_tmpResolvers.Add(resolver);
			}

			Profiler.BeginSample("SpriteAtlasUtility.PackAllAtlases");
			SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
			Profiler.EndSample();

			_tmpFileToAssetNodesLookup = RelationLookup.ConvertToDictionary(_fileToAssetNodes);
		}

		public void PostAssetUpdate()
		{
			_fileToAssetNodes = _tmpFileToAssetNodesLookup.Values.ToArray();
		}

		public List<IDependencyMappingNode> UpdateAssetsForPath(string path, long timeStamp,
			List<AssetListEntry> assetEntries)
		{
			var result = new List<IDependencyMappingNode>();

			_resolversToExecute.Clear();
			var guid = AssetDatabase.AssetPathToGUID(path);
			_tmpFileToAssetNodesLookup.Remove(guid);

			foreach (var resolver in _tmpResolvers)
			{
				if (resolver.IsGuidValid(guid))
				{
					_resolversToExecute.Add(resolver);
				}
			}

			foreach (var entry in assetEntries)
			{
				_hierarchyTraverser.Search(_searchContext.Set(entry.Asset, entry.AssetId, _tmpResolvers));

				foreach (var resolver in _resolversToExecute)
				{
					var dependency = GetDependenciesForResolver(_searchContext, timeStamp, resolver);
					result.Add(dependency);
				}
			}

			return result;
		}

		private IDependencyMappingNode GetDependenciesForResolver(ResolverDependencySearchContext searchContext,
			long timeStamp, IAssetDependencyResolver resolver)
		{
			var resolverId = resolver.GetId();
			var fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(searchContext.AssetId);

			if (!_tmpFileToAssetNodesLookup.ContainsKey(fileId))
			{
				_tmpFileToAssetNodesLookup.Add(fileId,
					new FileToAssetNode { FileId = fileId, AssetNodes = new List<AssetNode>() });
			}

			var fileToAssetNode = _tmpFileToAssetNodesLookup[fileId];
			var dependencies = searchContext.ResolverDependencies[resolver];
			var assetNode = fileToAssetNode.GetAssetNode(searchContext.AssetId);

			if (dependencies.Count > 0)
			{
				assetNode.GetResolverData(resolverId).Dependencies = dependencies;
			}

			fileToAssetNode.GetResolverTimeStamp(resolverId).TimeStamp = timeStamp;

			return assetNode;
		}
	}
}
