using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;
#if UNITY_2019_2_OR_NEWER
using UnityEditor.Experimental;
#endif

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    /// <summary>
    /// Contains utility functions that are needed by the AssetRelationsWindow but should be independent from the class so they can be used from other places
    /// </summary>
    public static class NodeDependencyLookupUtility
    {
        public static readonly string DEFAULT_CACHE_PATH = Path.Combine("Library", "NodeDependencyCache");

        [MenuItem("Window/Node Dependency Cache/Clear Cache Files")]
        public static void ClearCacheFiles()
        {
            List<Type> types = GetTypesForBaseType(typeof(IDependencyCache));

            foreach (Type type in types)
            {
                IDependencyCache cache = InstantiateClass<IDependencyCache>(type);
                cache.ClearFile(DEFAULT_CACHE_PATH);
            }
        }

        public static void ClearCachedContexts()
        {
            NodeDependencyLookupContext.ResetContexts();
        }

        public static bool IsResolverActive(CreatedDependencyCache createdCache, string id, string connectionType)
        {
            Dictionary<string, CreatedResolver> resolverUsagesLookup = createdCache.ResolverUsagesLookup;
            return resolverUsagesLookup.ContainsKey(id) && resolverUsagesLookup[id].DependencyTypes.Contains(connectionType);
        }

        public static long[] GetTimeStampsForFiles(string[] pathes)
        {
            long[] timestamps = new long[pathes.Length];

            for (int i = 0; i < pathes.Length; ++i)
            {
                timestamps[i] = GetTimeStampForPath(pathes[i]);
            }

            return timestamps;
        }

        public static long GetTimeStampForFileId(string fileId)
        {
            string guid = GetGuidFromAssetId(fileId);
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            return GetTimeStampForPath(path);
        }

        public static long GetTimeStampForPath(string path)
        {
            long fileTimeStamp = File.GetLastWriteTime(path).ToFileTimeUtc();
            long metaFileTimeStamp = File.GetLastWriteTime(path + ".meta").ToFileTimeUtc();
            long timeStamp = Math.Max(fileTimeStamp, metaFileTimeStamp);

            return timeStamp;
        }

        public static void LoadDependencyLookupForCaches(NodeDependencyLookupContext stateContext,
            ResolverUsageDefinitionList resolverUsageDefinitionList, bool isPartialUpdate = false, bool isFastUpdate = false,
            string fileDirectory = null)
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

            List<CreatedDependencyCache> caches = stateContext.GetCaches();

            foreach (CreatedDependencyCache cacheUsage in caches)
            {
                if (cacheUsage.ResolverUsages.Count == 0)
                {
                    continue;
                }

                IDependencyCache cache = cacheUsage.Cache;

                if (!resolverUsageDefinitionList.IsCacheActive(cache.GetType()))
                {
                    continue;
                }

                CacheUpdateInfo updateInfo = resolverUsageDefinitionList.GetUpdateStateForCache(cache.GetType());

                if (updateInfo.Load && !cacheUsage.IsLoaded)
                {
                    Profiler.BeginSample($"Load cache: {cacheUsage.Cache.GetType().Name}");
                    cache.Load(fileDirectory);
                    cacheUsage.IsLoaded = true;
                    Profiler.EndSample();
                }

                if (cache.CanUpdate())
                {
                    bool hasChanges = cache.Update(resolverUsageDefinitionList, updateInfo.Update);

                    if (hasChanges && updateInfo.Save)
                    {
                        cache.Save(fileDirectory);
                    }
                }
                else
                {
                    Debug.LogErrorFormat("{0} could not be updated", cache.GetType().FullName);
                }
            }

            RelationLookup.RelationsLookup lookup = new RelationLookup.RelationsLookup();
            Profiler.BeginSample("BuildLookup");
            lookup.Build(stateContext, caches, stateContext.nodeDictionary, isFastUpdate);
            Profiler.EndSample();

            stateContext.RelationsLookup = lookup;
        }

        public static Dictionary<string, INodeHandler> BuildNodeHandlerLookup()
        {
            Dictionary<string, INodeHandler> result = new Dictionary<string, INodeHandler>();

            foreach (INodeHandler nodeHandler in GetNodeHandlers())
            {
                result.Add(nodeHandler.GetHandledNodeType(), nodeHandler);
            }

            return result;
        }

        public static List<INodeHandler> GetNodeHandlers()
        {
            List<Type> types = GetTypesForBaseType(typeof(INodeHandler));
            List<INodeHandler> nodeHandlers = new List<INodeHandler>();

            foreach (Type type in types)
            {
                nodeHandlers.Add(InstantiateClass<INodeHandler>(type));
            }

            return nodeHandlers;
        }

        /// <summary>
        /// Used to get the size of an asset inside the packed build.
        /// Currently sounds are not correct since the file isnt going to be written into the libraray in the final format.
        /// </summary>
        public static int GetPackedAssetSize(string assetId)
        {
            string fullpath = GetLibraryFullPath(GetGuidFromAssetId(assetId));

            if (!String.IsNullOrEmpty(fullpath) && File.Exists(fullpath))
            {
                FileInfo info = new FileInfo(fullpath);
                return (int) (info.Length);
            }

            return 0;
        }

        private static string GetLibraryFullPath(string guid)
        {
            if (String.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Path.GetExtension(path).Equals(".asset"))
            {
                return path;
            }

#if UNITY_2019_2_OR_NEWER
            if (EditorSettings.assetPipelineMode == AssetPipelineMode.Version1)
            {
                return GetAssetDatabaseVersion1LibraryDataPath(guid);
            }
            else
            {
#if UNITY_2020_2_OR_NEWER
                Hash128 artifactHash = AssetDatabaseExperimental.LookupArtifact(new ArtifactKey(new GUID(guid))).value;
#else
                Hash128 artifactHash = AssetDatabaseExperimental.GetArtifactHash(guid);
#endif

                if (!artifactHash.isValid)
                {
                    return null;
                }

#if UNITY_2020_2_OR_NEWER
                ArtifactID artifactID = new ArtifactID();
                artifactID.value = artifactHash;
                AssetDatabaseExperimental.GetArtifactPaths(artifactID, out string[] paths);
#else
                AssetDatabaseExperimental.GetArtifactPaths(artifactHash, out string[] paths);
#endif

                foreach (string artifactPath in paths)
                {
                    if (artifactPath.EndsWith(".info"))
                        continue;

                    return Path.GetFullPath(artifactPath);
                }
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
        /// Right now this only works if the asset or one of its parents (referencers) are in a packaged scene or in a resources folder.
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

            INodeHandler nodeHandler = stateContext.NodeHandlerLookup[node.Type];

            if (nodeHandler.GetHandledNodeType().Contains(node.Type))
            {
                if (!nodeHandler.IsNodePackedToApp(node, true))
                {
                    return false;
                }

                if (nodeHandler.IsNodePackedToApp(node, false))
                {
                    checkedPackedStates[node.Key] = true;
                    return true;
                }
            }

            foreach (Connection connection in node.Referencers)
            {
                Node refNode = connection.Node;

                if (!stateContext.DependencyTypeLookup.GetDependencyType(connection.DependencyType).IsIndirect &&
                    IsNodePackedToApp(refNode, stateContext, checkedPackedStates))
                {
                    checkedPackedStates[node.Key] = true;
                    return true;
                }
            }

            return false;
        }

        public static Node.NodeSize UpdateNodeSize(Node node, NodeDependencyLookupContext stateContext, bool forceUpdate = true)
        {
            if (!(forceUpdate || node.OwnSize.Size == -1))
            {
                return node.OwnSize;
            }

            if (!stateContext.NodeHandlerLookup.ContainsKey(node.Type))
            {
                return new Node.NodeSize();
            }

            node.OwnSize = stateContext.NodeHandlerLookup[node.Type].GetOwnFileSize(node, stateContext);
            return node.OwnSize;
        }

        public static int GetTreeSize(Node node, NodeDependencyLookupContext stateContext, HashSet<Node> flattenedHierarchy)
        {
            int size = 0;
            flattenedHierarchy.Clear();

            TraverseHardDependencyNodesRecNoFlattened(node, stateContext, flattenedHierarchy);

            foreach (Node traversedNode in flattenedHierarchy)
            {
                if(traversedNode.OwnSize.ContributesToTreeSize)
                {
                    size += traversedNode.OwnSize.Size;
                }
            }

            return size;
        }

        public static void TraverseHardDependencyNodesRecNoFlattened(Node node, NodeDependencyLookupContext stateContext,
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

            foreach (Connection connection in node.Dependencies)
            {
                if (connection.IsHardDependency)
                {
                    TraverseHardDependencyNodesRecNoFlattened(connection.Node, stateContext, traversedNodes);
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
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId);
            return $"{guid}_{fileId}";
        }

        public static Object GetAssetById(string id)
        {
            string fileId = GetFileIdFromAssetId(id);
            string guid = GetGuidFromAssetId(id);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assetsAtPath = LoadAllAssetsAtPath(path);

            foreach (Object asset in assetsAtPath)
            {
                if (asset == null)
                {
                    continue;
                }

                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string aguid, out long afileId);
                if (afileId.ToString() == fileId)
                {
                    return asset;
                }
            }

            return null;
        }

        public static Object GetMainAssetById(string id)
        {
            string guid = GetGuidFromAssetId(id);
            string path = AssetDatabase.GUIDToAssetPath(guid);

            return AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        public static Object[] LoadAllAssetsAtPath(string path)
        {
            if (path.EndsWith(".unity"))
            {
                return new [] {AssetDatabase.LoadMainAssetAtPath(path)};
            }

            return AssetDatabase.LoadAllAssetsAtPath(path);
        }

        public static string[] GetAllAssetPathes(bool unityBuiltin)
        {
            string[] pathes = AssetDatabase.GetAllAssetPaths();

            List<string> pathList = new List<string>();

            foreach (string path in pathes)
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

        public static void AddAssetsToList(HashSet<string> assetList, string path)
        {
            Object mainAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
            Object[] allAssets = LoadAllAssetsAtPath(path);

            for (var i = 0; i < allAssets.Length; i++)
            {
                if (allAssets[i] == mainAsset)
                {
                    allAssets[i] = allAssets[0];
                    allAssets[0] = mainAsset;
                    break;
                }
            }

            foreach (Object asset in allAssets)
            {
                if (asset == null)
                {
                    continue;
                }

                if (!(mainAsset is GameObject) || (AssetDatabase.IsMainAsset(asset) || AssetDatabase.IsSubAsset(asset)))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileID);
                    assetList.Add($"{guid}_{fileID}");
                }
            }
        }

        public static List<Type> GetTypesForBaseType(Type interfaceType)
        {
            List<Type> result = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

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
            int c = 0;

            for (int i = 0; i < id.Length; i++)
            {
                result[c++] = id[i];
            }

            result[c++] = '_';

            for (int i = 0; i < type.Length; i++)
            {
                result[c++] = type[i];
            }

            return result.ToString();
#else
            return $"{id}@{type}";
#endif
        }

        public static void RemoveNonExistingFilesFromIdentifyableList<T>(string[] pathes, ref T[] list) where T : IIdentifyable
        {
            HashSet<string> pathesLookup = new HashSet<string>(pathes);
            HashSet<T> deletedNodes = new HashSet<T>();

            foreach (T listItem in list)
            {
                string filePath = AssetDatabase.GUIDToAssetPath(listItem.Id);
                if (!pathesLookup.Contains(filePath))
                {
                    deletedNodes.Add(listItem);
                }
            }

            if (deletedNodes.Count > 0)
            {
                List<T> fileToAssetNodesLists = list.ToList();
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

        public static void GetAllReachableNodes(Node node, HashSet<Node> reachedNodes, HashSet<Node> newNodes, RelationType relationType)
        {
            if (reachedNodes.Contains(node))
            {
                return;
            }

            reachedNodes.Add(node);
            newNodes.Add(node);

            foreach (Connection connection in node.GetRelations(relationType))
            {
                GetAllReachableNodes(connection.Node, reachedNodes, newNodes, relationType);
            }
        }

        public static HashSet<Node> CalculateAllReachableNodes(Node rootNode, HashSet<Node> reachedNodes)
        {
            HashSet<Node> newNodes = new HashSet<Node>();
            HashSet<Node> referencerNodes = new HashSet<Node>();

            GetAllReachableNodes(rootNode, referencerNodes, referencerNodes, RelationType.REFERENCER);

            foreach (Node referencerNode in referencerNodes)
            {
                GetAllReachableNodes(referencerNode, reachedNodes, newNodes, RelationType.DEPENDENCY);
            }

            return newNodes;
        }

        public static void CalculateAllNodeSizes(List<Node> nodes, NodeDependencyLookupContext context)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];

                if (node.OwnSize.Size == -1)
                {
                    node.OwnSize = UpdateNodeSize(node, context);
                }

                if (i % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("Calculating all node sizes", "", i / (float)nodes.Count);
                }
            }

            EditorUtility.ClearProgressBar();
        }

        public static void CalculateAllNodeNameAndTypeInformation(List<Node> nodes, NodeDependencyLookupContext context)
        {
            bool changed = false;

            for (var i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];

                if (!string.IsNullOrEmpty(node.Name))
                {
                    continue;
                }

                INodeHandler nodeHandler = context.NodeHandlerLookup[node.Type];
                nodeHandler.GetNameAndType(node.Id, out node.Name, out node.ConcreteType);
                changed = true;

                if (i % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("Calculating all node name and type info", "", i / (float)nodes.Count);
                }
            }

            EditorUtility.ClearProgressBar();

            if (!changed)
            {
                return;
            }

            foreach (KeyValuePair<string,INodeHandler> pair in context.NodeHandlerLookup)
            {
                pair.Value.SaveCaches();
            }
        }

        /**
         * Return the dependency lookup for Objects using the ObjectDependencyResolver
         */
        public static void BuildDefaultAssetLookup(NodeDependencyLookupContext stateContext, bool loadFromCache, string savePath)
        {
            ResolverUsageDefinitionList usageDefinitionList = new ResolverUsageDefinitionList();
            usageDefinitionList.Add<AssetDependencyCache, ObjectSerializedDependencyResolver>(loadFromCache, true, false);

            LoadDependencyLookupForCaches(stateContext, usageDefinitionList, false, false, savePath);
        }
    }
}