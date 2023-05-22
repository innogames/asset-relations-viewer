using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.U2D;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * Cache to store all dependencies of assets to other nodes
	 */
	public class AssetDependencyCache : IDependencyCache
	{
		private const string Version = "1.4.5";
		private const string FileName = "AssetDependencyCacheData";
		private const string VersionedFileName = FileName + "_" + Version + ".cache";

		private FileToAssetNode[] _fileToAssetNodes = new FileToAssetNode[0];
		private Dictionary<string, AssetNode> _assetNodesDict = new Dictionary<string, AssetNode>();

		public AssetSerializedPropertyTraverser _hierarchyTraverser = new AssetSerializedPropertyTraverser();

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
			string path = Path.Combine(directory, VersionedFileName);

			if (File.Exists(path))
			{
				byte[] bytes = File.ReadAllBytes(path);
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
			string path = Path.Combine(directory, VersionedFileName);

			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllBytes(path, AssetDependencyCacheSerializer.Serialize(_fileToAssetNodes));
		}

		public void ClearFile(string directory)
		{
			string path = Path.Combine(directory, VersionedFileName);

			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (FileToAssetNode fileToAssetNode in _fileToAssetNodes)
			{
				foreach (AssetNode assetNode in fileToAssetNode.AssetNodes)
				{
					nodes.Add(assetNode);
				}
			}
		}

		public void InitLookup()
		{
			_assetNodesDict.Clear();

			foreach (FileToAssetNode fileToAssetNode in _fileToAssetNodes)
			{
				foreach (AssetNode assetNode in fileToAssetNode.AssetNodes)
				{
					_assetNodesDict.Add(assetNode.Id, assetNode);
				}
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if(_assetNodesDict.TryGetValue(id, out AssetNode assetNode))
			{
				return assetNode.GetDependenciesForResolverUsages(_createdDependencyCache.ResolverUsagesLookup);
			}

			return new List<Dependency>();
		}

		public bool CanUpdate()
		{
			return !Application.isPlaying && !EditorApplication.isCompiling;
		}

		private HashSet<string> GetChangedAssetIdsForResolvers(List<IAssetDependencyResolver> resolvers, string[] pathes, long[] timestamps, ref FileToAssetNode[] fileToAssetNodes)
		{
			HashSet<string> result = new HashSet<string>();
			Dictionary<string, FileToAssetNode> list = RelationLookup.RelationLookupBuilder.ConvertToDictionary(fileToAssetNodes);
			string progressBarTitle = $"AssetDependencyCache";

			List<IAssetDependencyResolver> resolversToExecute = new List<IAssetDependencyResolver>();

			float lastDisplayedPercentage = 0;

			for (int i = 0, j = 0; i < pathes.Length; ++i)
			{
				if (EditorUtility.DisplayCancelableProgressBar(progressBarTitle, $"Finding Dependencies {pathes[i]}", (float)i / pathes.Length))
				{
					throw new DependencyUpdateAbortedException();
				}

				string path = pathes[i];
				string guid = AssetDatabase.AssetPathToGUID(path);

				resolversToExecute.Clear();

				foreach (IAssetDependencyResolver resolver in resolvers)
				{
					if (!resolver.IsGuidValid(guid))
					{
						continue;
					}

					if (list.ContainsKey(guid))
					{
						FileToAssetNode fileToAssetNode = list[guid];

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

				if (resolversToExecute.Count > 0)
				{
					j++;
					FindDependenciesForResolvers(resolversToExecute, result, path, list);
				}

				/*if (j % 3000 == 0)
				{
					Resources.UnloadUnusedAssets();
					EditorUtility.UnloadUnusedAssetsImmediate();
					GC.Collect();
				}*/
			}

			_fileToAssetNodes = list.Values.ToArray();

			return result;
		}

		private void FindDependenciesForResolvers(List<IAssetDependencyResolver> resolvers, HashSet<string> result, string path, Dictionary<string, FileToAssetNode> list)
		{
			HashSet<string> assetIds = new HashSet<string>();
			NodeDependencyLookupUtility.AddAssetsToList(assetIds, path);

			foreach (string assetId in assetIds)
			{
				ResolverDependencySearchContext searchContext = new ResolverDependencySearchContext(assetId, resolvers);
				_hierarchyTraverser.Search(searchContext);

				foreach (IAssetDependencyResolver resolver in resolvers)
				{
					GetDependenciesForResolver(searchContext, resolver, list);
				}

				result.Add(assetId);
			}
		}

		public bool Update(ResolverUsageDefinitionList resolverUsages, bool shouldUpdate)
		{
			if (!shouldUpdate)
			{
				return false;
			}

			foreach (CreatedResolver resolverUsage in _createdDependencyCache.ResolverUsages)
			{
				if(resolverUsage.Resolver is IAssetDependencyResolver assetDependencyResolver)
				{
					assetDependencyResolver.SetValidGUIDs();
				}
			}

			SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);

			string[] pathes = NodeDependencyLookupUtility.GetAllAssetPathes(true);

			NodeDependencyLookupUtility.RemoveNonExistingFilesFromIdentifyableList(pathes, ref _fileToAssetNodes);
			return GetDependenciesForAssets(pathes, _createdDependencyCache);
		}

		private bool GetDependenciesForAssets(string[] pathes, CreatedDependencyCache createdDependencyCache)
		{
			long[] timestamps = NodeDependencyLookupUtility.GetTimeStampsForFiles(pathes);

			_hierarchyTraverser.Initialize();
			bool hasChanges = false;

			List<IAssetDependencyResolver> resolvers = new List<IAssetDependencyResolver>();

			foreach (CreatedResolver resolverUsage in createdDependencyCache.ResolverUsages)
			{
				if (!(resolverUsage.Resolver is IAssetDependencyResolver))
				{
					Debug.LogError($"AssetDependencyCache {resolverUsage.Resolver.GetType().Name} is not of baseType {typeof(IAssetDependencyResolver).Name}");
					continue;
				}

				IAssetDependencyResolver resolver = (IAssetDependencyResolver) resolverUsage.Resolver;
				resolvers.Add(resolver);

				resolver.Initialize(this);
			}

			HashSet<string> changedAssets = GetChangedAssetIdsForResolvers(resolvers, pathes, timestamps, ref _fileToAssetNodes);
			hasChanges |= changedAssets.Count > 0;

			return hasChanges;
		}

		private void GetDependenciesForResolver(ResolverDependencySearchContext searchContext, IAssetDependencyResolver resolver, Dictionary<string, FileToAssetNode> resultList)
		{
			string resolverId = resolver.GetId();
			string fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(searchContext.AssetId);

			if (!resultList.ContainsKey(fileId))
			{
				resultList.Add(fileId, new FileToAssetNode{FileId = fileId, AssetNodes = new List<AssetNode>()});
			}

			FileToAssetNode fileToAssetNode = resultList[fileId];
			AssetNode assetNode = fileToAssetNode.GetAssetNode(searchContext.AssetId);

			List<Dependency> dependencies = searchContext.ResolverDependencies[resolver];

			AssetNode.ResolverData resolverData = assetNode.GetResolverData(resolverId);

			resolverData.Dependencies = dependencies;

			fileToAssetNode.GetResolverTimeStamp(resolverId).TimeStamp =
				NodeDependencyLookupUtility.GetTimeStampForFileId(fileId);
		}
	}
}
