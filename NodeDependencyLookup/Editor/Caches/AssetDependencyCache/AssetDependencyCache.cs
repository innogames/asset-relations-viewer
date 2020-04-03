using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{	
	/**
	 * Cache to store all dependencies of assets to other nodes
	 */
	public class AssetDependencyCache : IDependencyCache
	{
		public const string Version = "1.01";
		public const string FileName = "AssetDependencyCacheData_" + Version + ".cache";

		private AssetNode[] _assetNodes = new AssetNode[0];
		private Dictionary<string, AssetNode> _assetNodesDict = new Dictionary<string, AssetNode>();
		
		public AssetSerializedPropertyTraverser _hierarchyTraverser = new AssetSerializedPropertyTraverser();

		private CreatedDependencyCache _createdDependencyCache;

		private bool _isLoaded;

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
			string path = Path.Combine(directory, FileName);

			if (_isLoaded)
				return;
			
			if (File.Exists(path))
			{
				byte[] bytes = File.ReadAllBytes(path);
				_assetNodes = AssetDependencyCacheSerializer.Deserialize(bytes);
			}
			else
			{
				_assetNodes = new AssetNode[0];
			}

			_isLoaded = true;
		}

		public void Save(string directory)
		{
			string path = Path.Combine(directory, FileName);

			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			
			File.WriteAllBytes(path, AssetDependencyCacheSerializer.Serialize(_assetNodes));
		}
		
		public void ClearFile(string directory)
		{
			string path = Path.Combine(directory, FileName);
			
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}

		public string GetHandledNodeType()
		{
			return "Asset";
		}

		public IResolvedNode[] GetNodes()
		{
			return _assetNodes;
		}

		public void InitLookup()
		{
			_assetNodesDict.Clear();

			foreach (AssetNode node in _assetNodes)
			{
				_assetNodesDict.Add(node.Id, node);
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			AssetNode assetNode = _assetNodesDict[id];
			return assetNode.GetDependenciesForResolverUsages(_createdDependencyCache.ResolverUsagesLookup);
		}

		public bool NeedsUpdate()
		{
			string[] pathes = NodeDependencyLookupUtility.GetAllAssetPathes();
			long[] timeStampsForFiles = GetTimeStampsForFiles(pathes);

			foreach (CreatedResolver resolverUsage in _createdDependencyCache.ResolverUsages)
			{
				IAssetDependencyResolver assetDependencyResolver = resolverUsage.Resolver as IAssetDependencyResolver;
				assetDependencyResolver.SetValidGUIDs();
			}

			foreach (CreatedResolver resolverUsage in _createdDependencyCache.ResolverUsages)
			{
				IAssetDependencyResolver assetDependencyResolver = resolverUsage.Resolver as IAssetDependencyResolver;

				if (GetChangedGuidsForResolver(assetDependencyResolver, pathes, timeStampsForFiles, _assetNodes).Count > 0)
				{
					return true;
				}
			}

			return false;
		}

		public bool CanUpdate()
		{
			return !Application.isPlaying && !EditorApplication.isCompiling;
		}

		private long[] GetTimeStampsForFiles(string[] pathes)
		{
			long[] results = new long[pathes.Length];

			for (int i = 0; i < pathes.Length; ++i)
			{
				results[i] = GetTimeStampForFile(pathes[i]);
			}

			return results;
		}

		private HashSet<string> GetChangedGuidsForResolver(IAssetDependencyResolver resolver, string[] pathes, long[] timestamps, AssetNode[] assetNodes)
		{
			HashSet<string> result = new HashSet<string>();
			Dictionary<string, AssetNode> list = RelationLookup.RelationLookupBuilder.ConvertToDictionary(assetNodes);
			string id = resolver.GetId();

			for (int i = 0; i < pathes.Length; ++i)
			{
				string path = pathes[i];
				string guid = AssetDatabase.AssetPathToGUID(path);

				if (!resolver.IsGuidValid(guid))
				{
					continue;
				}

				if (list.ContainsKey(guid))
				{
					AssetNode entry = list[guid];
					entry.Existing = true;

					AssetNode.ResolverData resolverData = entry.GetResolverData(id);
					long timeStamp = timestamps[i];

					if (resolverData.TimeStamp != timeStamp)
					{
						result.Add(guid);
					}
				}
				else
				{
					result.Add(guid);
				}
			}

			return result;
		}

		public void Update(ProgressBase progress)
		{
			AssetNode[] assetNodes = GetDependenciesForAssets(_assetNodes, _createdDependencyCache, progress);
			_assetNodes = assetNodes;
		}

		private struct ResolverData
		{
			public IAssetDependencyResolver Resolver;
			public HashSet<string> ChangedAssets;
		}

		private AssetNode[] GetDependenciesForAssets(AssetNode[] assetNodes,
			CreatedDependencyCache createdDependencyCache, ProgressBase progress)
		{
			string[] pathes = NodeDependencyLookupUtility.GetAllAssetPathes();
			long[] timestamps = GetTimeStampsForFiles(pathes);

			List<ResolverData> data = new List<ResolverData>();
			
			_hierarchyTraverser.Clear();

			foreach (CreatedResolver resolverUsage in createdDependencyCache.ResolverUsages)
			{
				if (!(resolverUsage.Resolver is IAssetDependencyResolver))
					continue;
				
				IAssetDependencyResolver resolver = (IAssetDependencyResolver) resolverUsage.Resolver;
				
				HashSet<string> changedAssets = GetChangedGuidsForResolver(resolver, pathes, timestamps, assetNodes);
				data.Add(new ResolverData{ChangedAssets = changedAssets, Resolver = resolver});

				resolver.Initialize(this, changedAssets, progress);
			}
			
			// Execute the searcher for all registered subsystems here to find hierarchy and property pathes
			_hierarchyTraverser.Initialize(progress);
			_hierarchyTraverser.Search();
			
			Dictionary<string, AssetNode> nodeDict = RelationLookup.RelationLookupBuilder.ConvertToDictionary(assetNodes);

			foreach (ResolverData resolverData in data)
			{
				GetDependenciesForAssetsInResolver(resolverData.ChangedAssets, resolverData.Resolver, nodeDict, progress);
			}

			return nodeDict.Values.ToArray();
		}

		private void GetDependenciesForAssetsInResolver(HashSet<string> changedAssets, IAssetDependencyResolver resolver, Dictionary<string, AssetNode> resultList, ProgressBase progress)
		{
			foreach (var changedAsset in changedAssets)
			{
				string guid = changedAsset;

				if (!resultList.ContainsKey(guid))
				{
					resultList.Add(guid, new AssetNode(guid) { Existing = true });
				}

				AssetNode entry = resultList[guid];

				List<Dependency> dependencies = new List<Dependency>();

				resolver.GetDependenciesForId(guid, dependencies);

				AssetNode.ResolverData resolverData = entry.GetResolverData(resolver.GetId());

				resolverData.Dep = dependencies.ToArray();
				
				string path = AssetDatabase.GUIDToAssetPath(guid);
				resolverData.TimeStamp = GetTimeStampForFile(path);
			}
		}
		
		private long GetTimeStampForFile(string path)
		{
			long fileTimeStamp = File.GetLastWriteTime(path).ToFileTimeUtc();
			long metaFileTimeStamp = File.GetLastWriteTime(path + ".meta").ToFileTimeUtc();
			long timeStamp = Math.Max(fileTimeStamp, metaFileTimeStamp);

			return timeStamp;
		}
	}
}
