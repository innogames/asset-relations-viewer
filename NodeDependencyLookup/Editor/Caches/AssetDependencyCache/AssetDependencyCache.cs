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
		public const string Version = "1.10";
		public const string FileName = "AssetDependencyCacheData_" + Version + ".cache";

		private FileToAssetNode[] _fileToAssetNodes = new FileToAssetNode[0];
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
			
			/*if (File.Exists(path))
			{
				byte[] bytes = File.ReadAllBytes(path);
				_assetNodes = AssetDependencyCacheSerializer.Deserialize(bytes);
			}
			else*/
			{
				_fileToAssetNodes = new FileToAssetNode[0];
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
			
			//File.WriteAllBytes(path, AssetDependencyCacheSerializer.Serialize(_assetNodes));
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

		public void AddExistingNodes(List<IResolvedNode> nodes)
		{
			foreach (FileToAssetNode fileToAssetNode in _fileToAssetNodes)
			{
				foreach (AssetNode assetNode in fileToAssetNode.AssetNodes)
				{
					if (assetNode.Existing)
					{
						nodes.Add(assetNode);
					}
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
			AssetNode assetNode = _assetNodesDict[id];
			return assetNode.GetDependenciesForResolverUsages(_createdDependencyCache.ResolverUsagesLookup);
		}

		public bool NeedsUpdate(ProgressBase progress)
		{
			string[] assetIds = NodeDependencyLookupUtility.GetAllAssetPathes(progress);
			long[] timeStampsForFiles = NodeDependencyLookupUtility.GetTimeStampsForFiles(assetIds);

			foreach (CreatedResolver resolverUsage in _createdDependencyCache.ResolverUsages)
			{
				IAssetDependencyResolver assetDependencyResolver = resolverUsage.Resolver as IAssetDependencyResolver;
				assetDependencyResolver.SetValidGUIDs();
			}

			foreach (CreatedResolver resolverUsage in _createdDependencyCache.ResolverUsages)
			{
				IAssetDependencyResolver assetDependencyResolver = resolverUsage.Resolver as IAssetDependencyResolver;

				if (GetChangedAssetIdsForResolver(assetDependencyResolver, assetIds, timeStampsForFiles, _fileToAssetNodes).Count > 0)
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

		private HashSet<string> GetChangedAssetIdsForResolver(IAssetDependencyResolver resolver, string[] pathes, long[] timestamps, FileToAssetNode[] fileToAssetNodes)
		{
			HashSet<string> result = new HashSet<string>();
			Dictionary<string, FileToAssetNode> list = RelationLookup.RelationLookupBuilder.ConvertToDictionary(fileToAssetNodes);
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
					FileToAssetNode fileToAssetNode = list[guid];
					//entry.Existing = true; // TODO
					
					long resolverTimestamp = fileToAssetNode.GetResolverTimeStamp(id).TimeStamp;
					long timeStamp = timestamps[i];

					//if (resolverData.TimeStamp != timeStamp)
					{
						NodeDependencyLookupUtility.AddAssetsToList(result, path);
					}
				}
				else
				{
					NodeDependencyLookupUtility.AddAssetsToList(result, path);
				}
			}

			return result;
		}

		public void Update(ProgressBase progress)
		{
			_fileToAssetNodes = GetDependenciesForAssets(_fileToAssetNodes, _createdDependencyCache, progress);
		}

		private struct ResolverData
		{
			public IAssetDependencyResolver Resolver;
			public HashSet<string> ChangedAssets;
		}

		private FileToAssetNode[] GetDependenciesForAssets(FileToAssetNode[] fileToAssetNodes,
			CreatedDependencyCache createdDependencyCache, ProgressBase progress)
		{
			string[] pathes = NodeDependencyLookupUtility.GetAllAssetPathes(progress);
			long[] timestamps = NodeDependencyLookupUtility.GetTimeStampsForFiles(pathes);

			List<ResolverData> data = new List<ResolverData>();
			
			_hierarchyTraverser.Clear();

			foreach (CreatedResolver resolverUsage in createdDependencyCache.ResolverUsages)
			{
				if (!(resolverUsage.Resolver is IAssetDependencyResolver))
					continue;
				
				IAssetDependencyResolver resolver = (IAssetDependencyResolver) resolverUsage.Resolver;
				
				HashSet<string> changedAssets = GetChangedAssetIdsForResolver(resolver, pathes, timestamps, _fileToAssetNodes);
				data.Add(new ResolverData{ChangedAssets = changedAssets, Resolver = resolver});

				resolver.Initialize(this, changedAssets, progress);
			}
			
			// Execute the searcher for all registered subsystems here to find hierarchy and property pathes
			_hierarchyTraverser.Initialize(progress);
			_hierarchyTraverser.Search();
			
			Dictionary<string, FileToAssetNode> nodeDict = RelationLookup.RelationLookupBuilder.ConvertToDictionary(fileToAssetNodes);

			foreach (ResolverData resolverData in data)
			{
				GetDependenciesForAssetsInResolver(resolverData.ChangedAssets, resolverData.Resolver, nodeDict, progress);
			}

			return nodeDict.Values.ToArray();
		}

		private void GetDependenciesForAssetsInResolver(HashSet<string> changedAssets, IAssetDependencyResolver resolver, Dictionary<string, FileToAssetNode> resultList, ProgressBase progress)
		{
			string resolverId = resolver.GetId();
			
			foreach (string assetId in changedAssets)
			{
				string fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

				if (!resultList.ContainsKey(fileId))
				{
					resultList.Add(fileId, new FileToAssetNode{FileId = fileId});
				}

				FileToAssetNode fileToAssetNode = resultList[fileId];
				AssetNode assetNode = fileToAssetNode.GetAssetNode(assetId);

				List<Dependency> dependencies = new List<Dependency>();

				resolver.GetDependenciesForId(assetId, dependencies);

				AssetNode.ResolverData resolverData = assetNode.GetResolverData(resolverId);

				resolverData.Dependencies = dependencies.ToArray();

				fileToAssetNode.GetResolverTimeStamp(resolverId).TimeStamp =
					NodeDependencyLookupUtility.GetTimeStampForFileId(fileId);
			}
		}
	}
}
