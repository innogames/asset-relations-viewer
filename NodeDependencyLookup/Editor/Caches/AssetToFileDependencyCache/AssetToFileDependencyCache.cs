using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class AssetToFileDependencyCache// : IDependencyCache
    {
        public const string Version = "1.10";
        public const string FileName = "AssetToFileDependencyCacheData_" + Version + ".cache";
        
        private Dictionary<string, FileNode> _fileNodesDict = new Dictionary<string, FileNode>();
        private FileNode[] _fileNodes = new FileNode[0];
        
        private CreatedDependencyCache _createdDependencyCache;
        
        private bool _isLoaded;
        
        public void ClearFile(string directory)
        {
            string path = Path.Combine(directory, FileName);
			
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void Initialize(CreatedDependencyCache createdDependencyCache)
        {
            _createdDependencyCache = createdDependencyCache;
        }

        public bool NeedsUpdate(ProgressBase progress)
        {
            string[] assetIds = NodeDependencyLookupUtility.GetAllAssetPathes(progress);
            long[] timeStampsForFiles = NodeDependencyLookupUtility.GetTimeStampsForFiles(assetIds);

            foreach (CreatedResolver resolverUsage in _createdDependencyCache.ResolverUsages)
            {
                IFileDependencyResolver fileDependencyResolver = resolverUsage.Resolver as IFileDependencyResolver;
                //fileDependencyResolver.SetValidGUIDs();
            }

            foreach (CreatedResolver resolverUsage in _createdDependencyCache.ResolverUsages)
            {
                IFileDependencyResolver fileDependencyResolver = resolverUsage.Resolver as IFileDependencyResolver;

                if (GetChangedAssetIdsForResolver(fileDependencyResolver, assetIds, timeStampsForFiles, _fileNodes).Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
        
        private HashSet<string> GetChangedAssetIdsForResolver(IFileDependencyResolver resolver, string[] pathes, long[] timestamps, FileNode[] fileNodes)
        {
            HashSet<string> result = new HashSet<string>();
            Dictionary<string, FileNode> list = RelationLookup.RelationLookupBuilder.ConvertToDictionary(fileNodes);

            for (int i = 0; i < pathes.Length; ++i)
            {
                string path = pathes[i];
                string guid = AssetDatabase.AssetPathToGUID(path);

                // TODO
                /*if (!resolver.IsGuidValid(guid))
                {
                    continue;
                }*/

                if (list.ContainsKey(path))
                {
                    FileNode entry = list[path];
                    entry.IsExisting = true;
                    
                    long timeStamp = timestamps[i];

                    if (entry.TimeStamp != timeStamp)
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

        public bool CanUpdate()
        {
            return true;
        }

        public void Update(ProgressBase progress)
        {
            _fileNodes = GetDependenciesForAssets(_fileNodes, _createdDependencyCache, progress);
        }
        
        private FileNode[] GetDependenciesForAssets(FileNode[] assetNodes,
            CreatedDependencyCache createdDependencyCache, ProgressBase progress)
        {
            string[] pathes = NodeDependencyLookupUtility.GetAllAssetPathes(progress);
            long[] timestamps = NodeDependencyLookupUtility.GetTimeStampsForFiles(pathes);

            foreach (CreatedResolver resolverUsage in createdDependencyCache.ResolverUsages)
            {
                if (!(resolverUsage.Resolver is IFileDependencyResolver))
                {
                    continue;
                }

                IFileDependencyResolver resolver = (IFileDependencyResolver) resolverUsage.Resolver;
				
                HashSet<string> changedAssets = GetChangedAssetIdsForResolver(resolver, pathes, timestamps, assetNodes);
                resolver.Initialize(this, changedAssets, progress);
            }

            Dictionary<string, FileNode> nodeDict = RelationLookup.RelationLookupBuilder.ConvertToDictionary(assetNodes);

            // TODO
            /*foreach (ResolverData resolverData in data)
            {
                GetDependenciesForAssetsInResolver(resolverData.ChangedAssets, resolverData.Resolver, nodeDict, progress);
            }*/

            return nodeDict.Values.ToArray();
        }

        public IResolvedNode[] GetNodes()
        {
            return _fileNodes;
        }

        public string GetHandledNodeType()
        {
            return "File";
        }

        public List<Dependency> GetDependenciesForId(string id)
        {
            return _fileNodesDict[id].Dependencies;
        }

        public void Load(string directory)
        {
            // TODO
        }

        public void Save(string directory)
        {
            // TODO
        }

        public void InitLookup()
        {
            _fileNodesDict.Clear();

            foreach (FileNode node in _fileNodes)
            {
                _fileNodesDict.Add(node.Id, node);
            }
        }

        public Type GetResolverType()
        {
            return typeof(IFileDependencyResolver);
        }
    }
    
    public class FileNode : IResolvedNode
    {
        public string FileId;
        public string Id => FileId;
        public string Type => "File";
        public bool Existing => IsExisting;

        public bool IsExisting;
        public long TimeStamp;
        
        public List<Dependency> Dependencies = new List<Dependency>();
    }

    public interface IFileDependencyResolver : IDependencyResolver
    {
        void Initialize(AssetToFileDependencyCache cache, HashSet<string> changedAssets, ProgressBase progress);
    }

    public class FileDependencyResolver : IFileDependencyResolver
    {
        private static ConnectionType FileType = new ConnectionType(new Color(0.7f, 0.9f, 0.7f), false, true);

        public const string ResolvedType = "File";
        public const string Id = "AssetToFileDependencyResolver";
        
        public string[] GetConnectionTypes()
        {
            return new[] {"File"};
        }

        public string GetId()
        {
            return Id;
        }

        public ConnectionType GetDependencyTypeForId(string typeId)
        {
            return FileType;
        }

        public void Initialize(AssetToFileDependencyCache cache, HashSet<string> changedAssets, ProgressBase progress)
        {
            
        }
    }
}