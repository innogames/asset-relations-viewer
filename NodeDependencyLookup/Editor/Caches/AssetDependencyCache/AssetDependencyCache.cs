﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{	
	public struct AssetResolverData
	{
		public IDependencyResolver Resolver;
		public HashSet<string> ChangedAssets;
	}
	
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

		private HashSet<string> GetChangedAssetIdsForResolver(IAssetDependencyResolver resolver, string[] pathes, long[] timestamps, FileToAssetNode[] fileToAssetNodes)
		{
			HashSet<string> result = new HashSet<string>();
			Dictionary<string, FileToAssetNode> list = RelationLookup.RelationLookupBuilder.ConvertToDictionary(fileToAssetNodes);
			string id = resolver.GetId();
			string progressBarTitle = $"AssetDependencyCache: {id}";

			float lastDisplayedPercentage = 0;

			for (int i = 0; i < pathes.Length; ++i)
			{
				float progressPercentage = (float) i / pathes.Length;

				if (progressPercentage - lastDisplayedPercentage > 0.01f)
				{
					if (EditorUtility.DisplayCancelableProgressBar(progressBarTitle, $"Finding and loading changed assets {result.Count}", (float)i / pathes.Length))
					{
						throw new DependencyUpdateAbortedException();
					}
					
					lastDisplayedPercentage = progressPercentage;
				}

				string path = pathes[i];
				string guid = AssetDatabase.AssetPathToGUID(path);

				if (!resolver.IsGuidValid(guid))
				{
					continue;
				}

				if (list.ContainsKey(guid))
				{
					FileToAssetNode fileToAssetNode = list[guid];

					if (fileToAssetNode.GetResolverTimeStamp(id).TimeStamp != timestamps[i])
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
			
			string[] pathes = NodeDependencyLookupUtility.GetAllAssetPathes(true);
			
			NodeDependencyLookupUtility.RemoveNonExistingFilesFromIdentifyableList(pathes, ref _fileToAssetNodes);
			return GetDependenciesForAssets(pathes, _createdDependencyCache);
		}

		private bool GetDependenciesForAssets(string[] pathes, CreatedDependencyCache createdDependencyCache)
		{
			long[] timestamps = NodeDependencyLookupUtility.GetTimeStampsForFiles(pathes);

			List<AssetResolverData> data = new List<AssetResolverData>();
			
			_hierarchyTraverser.Clear();
			bool hasChanges = false;

			foreach (CreatedResolver resolverUsage in createdDependencyCache.ResolverUsages)
			{
				if (!(resolverUsage.Resolver is IAssetDependencyResolver))
					continue;
				
				IAssetDependencyResolver resolver = (IAssetDependencyResolver) resolverUsage.Resolver;
				
				HashSet<string> changedAssets = GetChangedAssetIdsForResolver(resolver, pathes, timestamps, _fileToAssetNodes);
				data.Add(new AssetResolverData{ChangedAssets = changedAssets, Resolver = resolver});

				resolver.Initialize(this, changedAssets);

				hasChanges |= changedAssets.Count > 0;
			}
			
			// Execute the searcher for all registered subsystems here to find hierarchy and property pathes
			_hierarchyTraverser.Initialize();
			_hierarchyTraverser.Search();
			
			Dictionary<string, FileToAssetNode> nodeDict = RelationLookup.RelationLookupBuilder.ConvertToDictionary(_fileToAssetNodes);

			foreach (AssetResolverData resolverData in data)
			{
				GetDependenciesForAssetsInResolver(resolverData.ChangedAssets, resolverData.Resolver as IAssetDependencyResolver, nodeDict);
			}

			_fileToAssetNodes = nodeDict.Values.ToArray();

			return hasChanges;
		}

		private void GetDependenciesForAssetsInResolver(HashSet<string> changedAssets, IAssetDependencyResolver resolver, Dictionary<string, FileToAssetNode> resultList)
		{
			string resolverId = resolver.GetId();
			
			foreach (string assetId in changedAssets)
			{
				string fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

				if (!resultList.ContainsKey(fileId))
				{
					resultList.Add(fileId, new FileToAssetNode{FileId = fileId, AssetNodes = new List<AssetNode>()});
				}

				FileToAssetNode fileToAssetNode = resultList[fileId];
				AssetNode assetNode = fileToAssetNode.GetAssetNode(assetId);

				List<Dependency> dependencies = new List<Dependency>();

				resolver.GetDependenciesForId(assetId, dependencies);

				AssetNode.ResolverData resolverData = assetNode.GetResolverData(resolverId);

				resolverData.Dependencies = dependencies;

				fileToAssetNode.GetResolverTimeStamp(resolverId).TimeStamp =
					NodeDependencyLookupUtility.GetTimeStampForFileId(fileId);
			}
		}
	}
}
