#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;
#if UNITY_2019_2_OR_NEWER
using UnityEditor.Experimental;
#endif

#endregion

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class AssetListEntry
	{
		public string AssetId;
		public Object Asset;
	}

	/// <summary>
	/// Contains utility functions that are needed by the AssetRelationsWindow but should be independent from the class so they
	/// can be used from other places
	/// </summary>
	public static class NodeDependencyLookupUtility
	{
		public static readonly string DEFAULT_CACHE_PATH = Path.Combine("Library", "NodeDependencyCache");

		[MenuItem("Window/Node Dependency Cache/Clear Cache Files")]
		public static void ClearCacheFiles()
		{
			if (Directory.Exists(DEFAULT_CACHE_PATH))
			{
				Directory.Delete(DEFAULT_CACHE_PATH, true);
			}
		}

		public static void ClearCachedContexts()
		{
			NodeDependencyLookupContext.ResetContexts();
		}

		public static bool IsResolverActive(CreatedDependencyCache createdCache, string id, string connectionType)
		{
			var resolverUsagesLookup = createdCache.ResolverUsagesLookup;
			return resolverUsagesLookup.TryGetValue(id, out var resolver) &&
				resolver.DependencyTypes.Contains(connectionType);
		}

		public static long[] GetTimeStampsForFiles(string[] pathes)
		{
			var timestamps = new long[pathes.Length];

			Parallel.For(0, pathes.Length, index => { timestamps[index] = GetTimeStampForPath(pathes[index]); });

			return timestamps;
		}

		public static long GetTimeStampForFileId(string fileId)
		{
			var guid = GetGuidFromAssetId(fileId);
			var path = AssetDatabase.GUIDToAssetPath(guid);

			if (string.IsNullOrEmpty(path))
			{
				return 0;
			}

			return GetTimeStampForPath(path);
		}

		public static long GetTimeStampForPath(string path)
		{
			var fileTimeStamp = File.GetLastWriteTime(path).ToFileTimeUtc();
			var metaFileTimeStamp = File.GetLastWriteTime(path + ".meta").ToFileTimeUtc();
			var timeStamp = Math.Max(fileTimeStamp, metaFileTimeStamp);

			return timeStamp;
		}

		public static void LoadDependencyLookupForCaches(NodeDependencyLookupContext stateContext,
			ResolverUsageDefinitionList resolverUsageDefinitionList, bool isPartialUpdate = false,
			bool isFastUpdate = false, string fileDirectory = null)
		{
			IterateEnumeratorRec(LoadDependencyLookupForCachesAsync(stateContext, resolverUsageDefinitionList,
				isPartialUpdate, isFastUpdate, fileDirectory));
		}

		public static IEnumerator LoadDependencyLookupForCachesAsync(NodeDependencyLookupContext stateContext,
			ResolverUsageDefinitionList resolverUsageDefinitionList, bool isPartialUpdate = false,
			bool isFastUpdate = false, string fileDirectory = null)
		{
			NodeDependencySearchState.IsRunning = true;

			try
			{
				yield return LoadDependenciesForCachesInternal(stateContext, resolverUsageDefinitionList,
					isPartialUpdate, isFastUpdate, fileDirectory);
			}
			finally
			{
				NodeDependencySearchState.IsRunning = false;
			}
		}

		private static void IterateEnumeratorRec(IEnumerator enumerator)
		{
			while (enumerator.MoveNext())
			{
				if (enumerator.Current is IEnumerator childEnumerator)
				{
					IterateEnumeratorRec(childEnumerator);
				}
			}
		}

		private static IEnumerator LoadDependenciesForCachesInternal(NodeDependencyLookupContext stateContext,
			ResolverUsageDefinitionList resolverUsageDefinitionList, bool isPartialUpdate, bool isFastUpdate,
			string fileDirectory)
		{
			if (string.IsNullOrEmpty(fileDirectory))
			{
				fileDirectory = DEFAULT_CACHE_PATH;
			}

			if (!isPartialUpdate)
			{
				stateContext.ResetCacheUsages();
			}

			stateContext.UpdateFromDefinition(resolverUsageDefinitionList);

			var caches = stateContext.GetCaches();
			var needsDataUpdate = false;

			foreach (var cacheUsage in caches)
			{
				if (cacheUsage.ResolverUsages.Count == 0)
				{
					continue;
				}

				var cache = cacheUsage.Cache;

				if (!resolverUsageDefinitionList.IsCacheActive(cache.GetType()))
				{
					continue;
				}

				var updateInfo = resolverUsageDefinitionList.GetUpdateStateForCache(cache.GetType());

				if (updateInfo.Load && !cacheUsage.IsLoaded)
				{
					Profiler.BeginSample($"Load cache: {cacheUsage.Cache.GetType().Name}");
					cache.Load(fileDirectory);
					cacheUsage.IsLoaded = true;
					Profiler.EndSample();
				}

				needsDataUpdate |= updateInfo.Update;

				if (cache.CanUpdate())
				{
					yield return cache.Update(stateContext.CacheUpdateSettings, resolverUsageDefinitionList,
						updateInfo.Update);

					if (updateInfo.Save)
					{
						Profiler.BeginSample($"Save cache: {cacheUsage.Cache.GetType().Name}");
						cache.Save(fileDirectory);
						Profiler.EndSample();
						yield return null;
					}
				}
				else
				{
					Debug.LogErrorFormat("{0} could not be updated", cache.GetType().FullName);
				}
			}

			yield return stateContext.RelationsLookup.Build(stateContext, caches, stateContext.nodeDictionary,
				isFastUpdate, needsDataUpdate);
		}

		public static Dictionary<string, INodeHandler> BuildNodeHandlerLookup()
		{
			var result = new Dictionary<string, INodeHandler>();

			foreach (var nodeHandler in GetNodeHandlers())
			{
				result.Add(nodeHandler.GetHandledNodeType(), nodeHandler);
			}

			return result;
		}

		public static List<INodeHandler> GetNodeHandlers()
		{
			var types = GetTypesForBaseType(typeof(INodeHandler));
			var nodeHandlers = new List<INodeHandler>();

			foreach (var type in types)
			{
				nodeHandlers.Add(InstantiateClass<INodeHandler>(type));
			}

			return nodeHandlers;
		}

		/// <summary>
		/// Used to get the size of an asset inside the packed build.
		/// Currently sounds are not correct since the file isnt going to be written into the library in the final format.
		/// </summary>
		public static int GetPackedAssetSize(string assetId)
		{
			var fullpath = GetLibraryFullPath(GetGuidFromAssetId(assetId));

			if (!string.IsNullOrEmpty(fullpath) && File.Exists(fullpath))
			{
				var info = new FileInfo(fullpath);
				return (int)info.Length;
			}

			return 0;
		}

		public static string GetLibraryFullPath(string guid)
		{
			if (string.IsNullOrEmpty(guid))
			{
				return null;
			}

			var path = AssetDatabase.GUIDToAssetPath(guid);

			if (Path.GetExtension(path).Equals(".asset") || Path.GetExtension(path).Equals(".anim"))
			{
				return path;
			}

#if UNITY_2019_2_OR_NEWER
			if (EditorSettings.assetPipelineMode == AssetPipelineMode.Version1)
			{
				return GetAssetDatabaseVersion1LibraryDataPath(guid);
			}
#if UNITY_2020_2_OR_NEWER
			var artifactHash = AssetDatabaseExperimental.LookupArtifact(new ArtifactKey(new GUID(guid))).value;
#else
                Hash128 artifactHash = AssetDatabaseExperimental.GetArtifactHash(guid);
#endif

			if (!artifactHash.isValid)
			{
				return null;
			}

#if UNITY_2020_2_OR_NEWER
			var artifactID = new ArtifactID();
			artifactID.value = artifactHash;
			AssetDatabaseExperimental.GetArtifactPaths(artifactID, out var paths);
#else
                AssetDatabaseExperimental.GetArtifactPaths(artifactHash, out string[] paths);
#endif

			foreach (var artifactPath in paths)
			{
				if (artifactPath.EndsWith(".info"))
					continue;

				return Path.GetFullPath(artifactPath);
			}
#else // For older unity versions that dont have asset database V2 yet
                return return GetAssetDatabaseVersion1LibraryDataPath(guid);
#endif

			return null;
		}

		private static string GetAssetDatabaseVersion1LibraryDataPath(string guid)
		{
			return Application.dataPath + "../../Library/metadata/" + guid.Substring(0, 2) + "/" + guid;
		}

		/// <summary>
		/// Right now this only works if the asset or one of its parents (referencers) are in a packaged scene or in a resources
		/// folder.
		/// If the asset is just in a bundle this is currently not tracked. Trying to find a solution for this.
		/// </summary>
		public static bool IsNodePackedToApp(Node node, NodeDependencyLookupContext stateContext,
			Dictionary<string, bool> checkedPackedStates)
		{
			if (checkedPackedStates.ContainsKey(node.Key))
			{
				return checkedPackedStates[node.Key];
			}

			checkedPackedStates.Add(node.Key, false);

			var nodeHandler = stateContext.NodeHandlerLookup[node.Type];

			if (nodeHandler.GetHandledNodeType().Contains(node.Type))
			{
				if (!nodeHandler.IsNodePackedToApp(node, true))
				{
					return false;
				}

				if (nodeHandler.IsNodePackedToApp(node))
				{
					checkedPackedStates[node.Key] = true;
					return true;
				}
			}

			foreach (var connection in node.Referencers)
			{
				var refNode = connection.Node;

				if (!stateContext.DependencyTypeLookup.GetDependencyType(connection.DependencyType).IsIndirect &&
				    IsNodePackedToApp(refNode, stateContext, checkedPackedStates))
				{
					checkedPackedStates[node.Key] = true;
					return true;
				}
			}

			return false;
		}

		private static INodeHandler GetNodeHandler(Node node, NodeDependencyLookupContext context)
		{
			return context.NodeHandlerLookup[node.Type];
		}

		public static void UpdateOwnFileSizeDependenciesForNode(Node node, NodeDependencyLookupContext context,
			HashSet<Node> calculatedNodes)
		{
			if (calculatedNodes.Contains(node))
			{
				return;
			}

			calculatedNodes.Add(node);
			GetNodeHandler(node, context).CalculateOwnFileDependencies(node, context, calculatedNodes);
		}

		public static int GetTreeSize(Node node, NodeDependencyLookupContext context, HashSet<Node> flattenedHierarchy)
		{
			var size = 0;
			flattenedHierarchy.Clear();

			TraverseHardDependencyNodesRecNoFlattened(node, context, flattenedHierarchy);

			foreach (var traversedNode in flattenedHierarchy)
			{
				if (traversedNode.OwnSize.ContributesToTreeSize)
				{
					size += traversedNode.OwnSize.Size;
				}
			}

			return size;
		}

		public static void TraverseHardDependencyNodesRecNoFlattened(Node node, NodeDependencyLookupContext context,
			HashSet<Node> traversedNodes)
		{
			if (node == null)
			{
				return;
			}

			if (traversedNodes.Contains(node))
			{
				return;
			}

			traversedNodes.Add(node);

			foreach (var connection in node.Dependencies)
			{
				if (connection.IsHardDependency)
				{
					TraverseHardDependencyNodesRecNoFlattened(connection.Node, context, traversedNodes);
				}
			}
		}

		public static string GetGuidFromAssetId(string id)
		{
			return id.Split('_')[0];
		}

		public static string GetFileIdFromAssetId(string id)
		{
			return id.Split('_')[1];
		}

		public static string GetAssetIdForAsset(Object asset)
		{
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long fileId);
			return $"{guid}_{fileId}";
		}

		public static Object GetAssetById(string id)
		{
			var fileId = GetFileIdFromAssetId(id);
			var guid = GetGuidFromAssetId(id);
			var path = AssetDatabase.GUIDToAssetPath(guid);
			var assetsAtPath = LoadAllAssetsAtPath(path);

			foreach (var asset in assetsAtPath)
			{
				if (asset == null)
				{
					continue;
				}

				AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var aguid, out long afileId);
				if (afileId.ToString() == fileId)
				{
					return asset;
				}
			}

			return null;
		}

		public static Object GetMainAssetById(string id)
		{
			var guid = GetGuidFromAssetId(id);
			var path = AssetDatabase.GUIDToAssetPath(guid);

			return AssetDatabase.LoadAssetAtPath<Object>(path);
		}

		public static Object[] LoadAllAssetsAtPath(string path)
		{
			if (path.EndsWith(".unity"))
			{
				return new[] { AssetDatabase.LoadMainAssetAtPath(path) };
			}

			return AssetDatabase.LoadAllAssetsAtPath(path);
		}

		public static string[] GetAllAssetPathes(bool unityBuiltin)
		{
			var pathes = AssetDatabase.GetAllAssetPaths();
			var pathList = new List<string>();

			foreach (var path in pathes)
			{
				pathList.Add(path);
			}

			if (unityBuiltin)
			{
				pathList.Add("Resources/unity_builtin_extra");
				pathList.Add("Library/unity default resources");
			}

			return pathList.ToArray();
		}

		public static void AddAssetsToList(List<AssetListEntry> assetList, string path)
		{
			Profiler.BeginSample($"AddAssetsToList Load {path}");
			var mainAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
			var allAssets = LoadAllAssetsAtPath(path);
			Profiler.EndSample();

			for (var i = 0; i < allAssets.Length; i++)
			{
				if (allAssets[i] == mainAsset)
				{
					allAssets[i] = allAssets[0];
					allAssets[0] = mainAsset;
					break;
				}
			}

			foreach (var asset in allAssets)
			{
				if (asset == null)
				{
					continue;
				}

				if (!(mainAsset is GameObject) || AssetDatabase.IsMainAsset(asset) || AssetDatabase.IsSubAsset(asset))
				{
					AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long fileID);
					assetList.Add(new AssetListEntry { AssetId = $"{guid}_{fileID}", Asset = asset });
				}
			}
		}

		public static List<Type> GetTypesForBaseType(Type interfaceType)
		{
			var result = new List<Type>();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();

			foreach (var assembly in assemblies)
			{
				foreach (var type in assembly.GetTypes())
				{
					if (type.IsClass && !type.IsAbstract && interfaceType.IsAssignableFrom(type))
					{
						try
						{
							result.Add(type);
						}
						catch (Exception e)
						{
							Debug.LogWarning(e);
						}
					}
				}
			}

			return result;
		}

		public static T InstantiateClass<T>(Type type) where T : class
		{
			return Activator.CreateInstance(type) as T;
		}

		public static string GetNodeKey(string id, string type)
		{
#if UNITY_2021_3_OR_NEWER
			Span<char> result = stackalloc char[id.Length + type.Length + 1];
			var c = 0;

			for (var i = 0; i < id.Length; i++)
			{
				result[c++] = id[i];
			}

			result[c++] = '_';

			for (var i = 0; i < type.Length; i++)
			{
				result[c++] = type[i];
			}

			return result.ToString();
#else
			return $"{id}@{type}";
#endif
		}

		public static void RemoveNonExistingFilesFromIdentifyableList<T>(string[] pathes, ref T[] list)
			where T : IIdentifyable
		{
			var pathesLookup = new HashSet<string>(pathes);
			var deletedNodes = new HashSet<T>();

			foreach (var listItem in list)
			{
				var filePath = AssetDatabase.GUIDToAssetPath(listItem.Id);
				if (!pathesLookup.Contains(filePath))
				{
					deletedNodes.Add(listItem);
				}
			}

			if (deletedNodes.Count > 0)
			{
				var fileToAssetNodesLists = list.ToList();
				fileToAssetNodesLists.RemoveAll(deletedNodes.Contains);
				list = fileToAssetNodesLists.ToArray();
			}
		}

		public static RelationType InvertRelationType(RelationType relationType)
		{
			switch (relationType)
			{
				case RelationType.DEPENDENCY:
					return RelationType.REFERENCER;
				case RelationType.REFERENCER:
					return RelationType.DEPENDENCY;
			}

			return RelationType.DEPENDENCY;
		}

		public static IEnumerator CalculateAllNodeSizes(List<Node> nodes, NodeDependencyLookupContext context,
			bool updateNodeData = true)
		{
			if (nodes.Count == 0)
			{
				yield break;
			}

			for (var i = 0; i < nodes.Count; i++)
			{
				var node = nodes[i];

				GetNodeHandler(node, context).InitializeOwnFileSize(node, context, updateNodeData);

				if (i % 5000 == 0)
				{
					EditorUtility.DisplayProgressBar("Updating all node sizes", $"[{node.Type}] {node.Name}",
						i / (float)nodes.Count);
					yield return null;
				}
			}

			var currentNode = nodes[0];

			var count = 0;
			var compressedSizeTask = Task.Run(() =>
			{
				Parallel.For(0, nodes.Count, i =>
				{
					currentNode = nodes[i];
					GetNodeHandler(nodes[i], context).CalculateOwnFileSize(nodes[i], context, updateNodeData);
					count++;
				});
			});

			while (!compressedSizeTask.IsCompleted)
			{
				EditorUtility.DisplayProgressBar("Updating all node sizes compressed",
					$"[{currentNode.Type}] {currentNode.Name}", count / (float)nodes.Count);
				yield return null;
			}

			yield return null;

			var calculatedNodes = new HashSet<Node>();

			for (var i = 0; i < nodes.Count; i++)
			{
				var node = nodes[i];
				UpdateOwnFileSizeDependenciesForNode(node, context, calculatedNodes);

				if (i % 1000 == 0)
				{
					EditorUtility.DisplayProgressBar("Updating all node sizes", $"[{node.Type}] {node.Name}",
						i / (float)nodes.Count);
				}
			}

			EditorUtility.ClearProgressBar();
		}

		/**
         * Return the dependency lookup for Objects using the ObjectDependencyResolver
         */
		public static void BuildDefaultAssetLookup(NodeDependencyLookupContext stateContext, bool loadFromCache,
			string savePath)
		{
			var usageDefinitionList = new ResolverUsageDefinitionList();
			usageDefinitionList.Add<AssetDependencyCache, ObjectSerializedDependencyResolver>(loadFromCache, true,
				false);

			LoadDependencyLookupForCaches(stateContext, usageDefinitionList, false, false, savePath);
		}
	}
}
