using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * Cache to store all dependencies of assets to other assets
	 */
	public class AssetDependencyCache : IDependencyCache
	{
		private const string Version = "1.4.5";
		private const string FileName = "AssetDependencyCacheData";
		private const string VersionedFileName = FileName + "_" + Version + ".cache";

		private FileToAssetNode[] _fileToAssetNodes = new FileToAssetNode[0];
		private Dictionary<string, AssetNode> _assetNodesDict = new Dictionary<string, AssetNode>();

		private AssetSerializedPropertyTraverser _hierarchyTraverser = new AssetSerializedPropertyTraverser();

		private CreatedDependencyCache _createdDependencyCache;

		public Type GetResolverType()
		{
			return typeof(IAssetDependencyResolver);
		}

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		public void Load(string directory)
		{
			Profiler.BeginSample("AssetDependencyCache Load");

			EditorUtility.DisplayProgressBar("AssetDependencyCache", "Loading cache", 0);
			var path = Path.Combine(directory, VersionedFileName);

			if (File.Exists(path))
			{
				var bytes = File.ReadAllBytes(path);
				_fileToAssetNodes = AssetDependencyCacheSerializer.Deserialize(bytes);
			}
			else
			{
				_fileToAssetNodes = new FileToAssetNode[0];
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

		public List<Dependency> GetDependenciesForId(string id)
		{
			if (_assetNodesDict.TryGetValue(id, out var assetNode))
			{
				return assetNode.GetDependenciesForResolverUsages(_createdDependencyCache.ResolverUsagesLookup);
			}

			return new List<Dependency>();
		}

		public bool CanUpdate()
		{
			return !Application.isPlaying && !EditorApplication.isCompiling;
		}

		private IEnumerator FindDependenciesForChangedFilesForResolvers(CacheUpdateSettings settings,
			List<IAssetDependencyResolver> resolvers, string[] pathes, long[] timestamps,
			FileToAssetNode[] fileToAssetNodes)
		{
			var list = RelationLookup.ConvertToDictionary(fileToAssetNodes);
			var resolversToExecute = new List<IAssetDependencyResolver>();

			var cleaner = new CacheUpdateResourcesCleaner();

			for (int i = 0, k = 0; i < pathes.Length; ++i)
			{
				var path = pathes[i];
				var guid = AssetDatabase.AssetPathToGUID(path);

				resolversToExecute.Clear();

				foreach (var resolver in resolvers)
				{
					if (!resolver.IsGuidValid(guid))
					{
						continue;
					}

					if (list.ContainsKey(guid))
					{
						var fileToAssetNode = list[guid];

						if (fileToAssetNode.GetResolverTimeStamp(resolver.GetId()).TimeStamp != timestamps[i])
						{
							resolversToExecute.Add(resolver);
						}
					}
					else
					{
						resolversToExecute.Add(resolver);
					}
				}

				cleaner.Clean(settings, k);

				if (resolversToExecute.Count > 0)
				{
					k++;
					list.Remove(guid);
					FindDependenciesForResolvers(resolversToExecute, path, timestamps[i], list,
						(float) i / pathes.Length);
				}

				if (k % 200 == 0)
				{
					yield return null;
				}
			}

			_fileToAssetNodes = list.Values.ToArray();

			yield return null;
		}

		private List<AssetListEntry> entries = new List<AssetListEntry>();
		private ResolverDependencySearchContext searchContext = new ResolverDependencySearchContext();

		private void FindDependenciesForResolvers(List<IAssetDependencyResolver> resolvers, string path, long timeStamp,
			Dictionary<string, FileToAssetNode> list, float progress)
		{
			var progressBarTitle = $"AssetDependencyCache";

			entries.Clear();
			NodeDependencyLookupUtility.AddAssetsToList(entries, path);

			for (var i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];

				if (i % 10 == 0)
				{
					var info = "";

					if (entries.Count < 50)
					{
						info = path;
					}
					else
					{
						info = $"[{i + 1}/{entries.Count}] {path}";
					}

					if (EditorUtility.DisplayCancelableProgressBar(progressBarTitle, info, progress))
					{
						throw new DependencyUpdateAbortedException();
					}
				}

				searchContext.AssetId = entry.AssetId;
				searchContext.Asset = entry.Asset;
				searchContext.SetResolvers(resolvers);
				_hierarchyTraverser.Search(searchContext);

				foreach (var resolver in resolvers)
				{
					GetDependenciesForResolver(searchContext, timeStamp, resolver, list);
				}
			}
		}

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			if (!shouldUpdate)
			{
				yield break;
			}

			foreach (var resolverUsage in _createdDependencyCache.ResolverUsages)
			{
				if (resolverUsage.Resolver is IAssetDependencyResolver assetDependencyResolver)
				{
					assetDependencyResolver.SetValidGUIDs();
				}
			}

			Profiler.BeginSample("SpriteAtlasUtility.PackAllAtlases");
			SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
			Profiler.EndSample();

			var pathes = NodeDependencyLookupUtility.GetAllAssetPathes(true);

			NodeDependencyLookupUtility.RemoveNonExistingFilesFromIdentifyableList(pathes, ref _fileToAssetNodes);
			yield return GetDependenciesForAssets(cacheUpdateSettings, pathes, _createdDependencyCache);
		}

		private IEnumerator GetDependenciesForAssets(CacheUpdateSettings cacheUpdateSettings, string[] pathes,
			CreatedDependencyCache createdDependencyCache)
		{
			EditorUtility.DisplayProgressBar("AssetDependencyCache", "Checking file timestamps", 0);
			Profiler.BeginSample("TimeStamps");
			var timestamps = NodeDependencyLookupUtility.GetTimeStampsForFiles(pathes);
			Profiler.EndSample();

			_hierarchyTraverser.Initialize();

			var resolvers = new List<IAssetDependencyResolver>();

			foreach (var resolverUsage in createdDependencyCache.ResolverUsages)
			{
				if (!(resolverUsage.Resolver is IAssetDependencyResolver))
				{
					Debug.LogError(
						$"AssetDependencyCache {resolverUsage.Resolver.GetType().Name} is not of baseType {typeof(IAssetDependencyResolver).Name}");
					continue;
				}

				var resolver = (IAssetDependencyResolver) resolverUsage.Resolver;
				resolvers.Add(resolver);

				resolver.Initialize(this);
			}

			yield return FindDependenciesForChangedFilesForResolvers(cacheUpdateSettings, resolvers, pathes, timestamps,
				_fileToAssetNodes);
		}

		private void GetDependenciesForResolver(ResolverDependencySearchContext searchContext, long timeStamp,
			IAssetDependencyResolver resolver, Dictionary<string, FileToAssetNode> resultList)
		{
			var resolverId = resolver.GetId();
			var fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(searchContext.AssetId);

			if (!resultList.ContainsKey(fileId))
			{
				resultList.Add(fileId, new FileToAssetNode {FileId = fileId, AssetNodes = new List<AssetNode>()});
			}

			var fileToAssetNode = resultList[fileId];
			var dependencies = searchContext.ResolverDependencies[resolver];
			var assetNode = fileToAssetNode.GetAssetNode(searchContext.AssetId);

			if (dependencies.Count > 0)
			{
				assetNode.GetResolverData(resolverId).Dependencies = dependencies;
			}

			fileToAssetNode.GetResolverTimeStamp(resolverId).TimeStamp = timeStamp;
		}
	}
}