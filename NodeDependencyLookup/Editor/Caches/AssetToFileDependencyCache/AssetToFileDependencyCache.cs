using System;
using System.Collections.Generic;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class AssetToFileDependencyCache : IDependencyCache
    {
        public void ClearFile(string directory)
        {
            // TODO
        }

        public void Initialize(CreatedDependencyCache createdDependencyCache)
        {
            // TODO
        }

        public bool NeedsUpdate(ProgressBase progress)
        {
            return true;
        }

        public bool CanUpdate()
        {
            return true;
        }

        public void Update(ProgressBase progress)
        {
            
        }

        private IResolvedNode[] Nodes = new IResolvedNode[0];

        public IResolvedNode[] GetNodes()
        {
            return Nodes;
        }

        public string GetHandledNodeType()
        {
            return "File";
        }

        public List<Dependency> GetDependenciesForId(string id)
        {
            throw new NotImplementedException();
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
            // TODO
        }

        public Type GetResolverType()
        {
            return typeof(IFileDependencyResolver);
        }
    }

    public interface IFileDependencyResolver : IDependencyResolver
    {
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
    }
}