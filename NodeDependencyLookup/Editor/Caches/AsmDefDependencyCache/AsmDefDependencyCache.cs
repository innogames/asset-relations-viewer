using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.AsmDefDependencyCache
{
    public class AsmdefToAsmdefDependency
    {
        public const string Name = "AsmDefToAsmDef";
    }
    
    public class AsmDefDependencyCache : IDependencyCache
    {
        private IDependencyMappingNode[] Nodes = new IDependencyMappingNode[0];
        private Dictionary<string, GenericDependencyMappingNode> _lookup = new Dictionary<string, GenericDependencyMappingNode>();
        private CreatedDependencyCache _createdDependencyCache;
        
        public void ClearFile(string directory)
        {
            // not needed
        }

        public void Initialize(CreatedDependencyCache createdDependencyCache)
        {
            _createdDependencyCache = createdDependencyCache;
        }

        public bool NeedsUpdate()
        {
            return true;
        }

        public bool CanUpdate()
        {
            return true;
        }

        private class AsmDefJson
        {
            public string name = String.Empty;
            public string[] references = Array.Empty<string>();
        }

        public void Update()
        {
            _lookup.Clear();

            List<IDependencyMappingNode> nodes = new List<IDependencyMappingNode>();
            
            string[] guids = AssetDatabase.FindAssets("t:asmdef");

            Dictionary<string, string> nameToFileMapping = new Dictionary<string, string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssemblyDefinitionAsset asmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
                nameToFileMapping.Add(JsonUtility.FromJson<AsmDefJson>(asmdef.text).name, path);
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                AssemblyDefinitionAsset asmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
                AsmDefJson asmDefJson = JsonUtility.FromJson<AsmDefJson>(asmdef.text);
                
                GenericDependencyMappingNode node = new GenericDependencyMappingNode();
                node.NodeId = NodeDependencyLookupUtility.GetAssetIdForAsset(asmdef);
                node.NodeType = AssetNodeType.Name;

                int g = 0;

                foreach (string reference in asmDefJson.references)
                {
                    bool isGuid = Guid.TryParse(reference, out Guid _);

                    string refPath = null;
                    
                    if (isGuid)
                    {
                        refPath = AssetDatabase.GUIDToAssetPath(reference);
                    }
                    else
                    {
                        if(!nameToFileMapping.TryGetValue(reference, out refPath))
                        {
                            continue;
                        }
                    }

                    AssemblyDefinitionAsset refAsmDef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(refPath);

                    string assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(refAsmDef);
                    string componentName = "Ref " + g++;
                    
                    node.Dependencies.Add(new Dependency(assetId, AsmdefToAsmdefDependency.Name, AssetNodeType.Name, new []{new PathSegment(componentName, PathSegmentType.Property), }));
                }
                
                nodes.Add(node);
                _lookup.Add(node.Id, node);
            }
            
            Nodes = nodes.ToArray();
        }

        public void AddExistingNodes(List<IDependencyMappingNode> nodes)
        {
            foreach (IDependencyMappingNode node in Nodes)
            {
                if (node.Existing)
                {
                    nodes.Add(node);
                }
            }
        }

        public List<Dependency> GetDependenciesForId(string id)
        {
            if(NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AsmDefDependencyResolver.Id, AsmdefToAsmdefDependency.Name))	
            {
                return _lookup[id].Dependencies;
            }
			
            return new List<Dependency>();
        }

        public void Load(string directory)
        {
        }

        public void Save(string directory)
        {
        }

        public void InitLookup()
        {
        }

        public Type GetResolverType()
        {
            return typeof(IAsmDefDependencyResolver);
        }

        public interface IAsmDefDependencyResolver : IDependencyResolver
        {
        }
        
        public class AsmDefDependencyResolver : IAsmDefDependencyResolver
        {
            private const string ConnectionTypeDescription = "Dependencies between AssemblyDefinitions";
            private static DependencyType asmdefDependencyType = new DependencyType("AsmDef->AsmDef", new Color(0.9f, 0.9f, 0.5f), false, true, ConnectionTypeDescription);
            public const string Id = "AsmdefDependencyResolver";
            
            public string[] GetDependencyTypes()
            {
                return new[] {AsmdefToAsmdefDependency.Name};
            }

            public string GetId()
            {
                return Id;
            }

            public DependencyType GetDependencyTypeForId(string typeId)
            {
                return asmdefDependencyType;
            }
        }
    }
}