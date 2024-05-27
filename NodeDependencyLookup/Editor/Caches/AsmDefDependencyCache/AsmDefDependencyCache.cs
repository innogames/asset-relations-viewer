using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.AsmDefDependencyCache
{
	public static class AsmdefToAsmdefDependency
	{
		public const string Name = "AsmDefToAsmDef";
	}

	[UsedImplicitly]
	public class AsmDefDependencyCache : IDependencyCache
	{
		private const string Version = "4.0.0";
		private const string FileName = "AsmDefDependencyCacheData_" + Version + ".cache";

		[UsedImplicitly]
		private class AsmDefJson
		{
			public string name;
			public string[] references;
		}

		[UsedImplicitly]
		private class AsmRefJson
		{
			public string reference;
		}

		private GenericDependencyMappingNode[] _nodes = Array.Empty<GenericDependencyMappingNode>();

		private List<GenericDependencyMappingNode> _nodeList = new List<GenericDependencyMappingNode>();

		private readonly Dictionary<string, GenericDependencyMappingNode> _lookup =
			new Dictionary<string, GenericDependencyMappingNode>();

		private CreatedDependencyCache _createdDependencyCache;

		private Dictionary<string, string> _nameToFileMapping = new Dictionary<string, string>();

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		public bool CanUpdate() => true;

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			yield return null;
		}

		private Dictionary<string, string> GenerateAsmDefFileMapping()
		{
			var nameToFilePathMapping = new Dictionary<string, string>();
			var guids = AssetDatabase.FindAssets("t:asmdef");

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var asmdef = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
				var asmDefJson = JsonUtility.FromJson<AsmDefJson>(asmdef.text);
				var asmDefName = asmDefJson.name;
				nameToFilePathMapping.Add(asmDefName, path);
			}

			return nameToFilePathMapping;
		}

		private bool TryGetRefPathFromGUID(string reference, Dictionary<string, string> nameToFileMapping,
			out string refPath)
		{
			if (reference.StartsWith("GUID:", StringComparison.Ordinal))
			{
				refPath = AssetDatabase.GUIDToAssetPath(reference.Substring("GUID:".Length));
			}
			else if (!nameToFileMapping.TryGetValue(reference, out refPath))
			{
				return false;
			}

			if (string.IsNullOrEmpty(refPath))
			{
				return false;
			}

			return true;
		}

		private void AddAsmDef(TextAsset asset, List<GenericDependencyMappingNode> nodes,
			Dictionary<string, string> nameToFileMapping)
		{
			var g = 0;
			var asmDefJson = JsonUtility.FromJson<AsmDefJson>(asset.text);

			var node = new GenericDependencyMappingNode(NodeDependencyLookupUtility.GetAssetIdForAsset(asset),
				AssetNodeType.Name);

			if (asmDefJson.references == null)
			{
				asmDefJson.references = Array.Empty<string>();
			}

			foreach (var reference in asmDefJson.references)
			{
				if (!TryGetRefPathFromGUID(reference, nameToFileMapping, out var refPath))
				{
					continue;
				}

				var refAsmDef = AssetDatabase.LoadAssetAtPath<TextAsset>(refPath);

				if (refAsmDef == null)
				{
					Debug.LogWarning($"No Assembly Definition loaded for {refPath}");
					continue;
				}

				var assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(refAsmDef);
				var componentName = "Ref " + g++;

				node.Dependencies.Add(new Dependency(assetId, AsmdefToAsmdefDependency.Name, AssetNodeType.Name,
					new[] { new PathSegment(componentName, PathSegmentType.Property) }));
			}

			nodes.Add(node);
			_lookup.Add(node.Id, node);
		}

		private void AddAsmRef(TextAsset asset, List<GenericDependencyMappingNode> nodes,
			Dictionary<string, string> nameToFileMapping)
		{
			var asmRefJson = JsonUtility.FromJson<AsmRefJson>(asset.text);

			var node = new GenericDependencyMappingNode(NodeDependencyLookupUtility.GetAssetIdForAsset(asset),
				AssetNodeType.Name);

			if (!TryGetRefPathFromGUID(asmRefJson.reference, nameToFileMapping, out var refPath))
			{
				return;
			}

			var asmRef = AssetDatabase.LoadAssetAtPath<TextAsset>(refPath);

			if (asmRef == null)
			{
				Debug.LogWarning($"No Assembly Definition loaded for {refPath}");
				return;
			}

			var assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(asmRef);
			var componentName = "Ref";

			node.Dependencies.Add(new Dependency(assetId, AsmdefToAsmdefDependency.Name, AssetNodeType.Name,
				new[] { new PathSegment(componentName, PathSegmentType.Property) }));

			nodes.Add(node);
			_lookup.Add(node.Id, node);
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (var node in _nodes)
			{
				nodes.Add(node);
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AsmDefDependencyResolver.Id,
				    AsmdefToAsmdefDependency.Name))
			{
				return _lookup[id].Dependencies;
			}

			return new List<Dependency>();
		}

		public void Load(string directory)
		{
			_nodeList = CacheSerializerUtils.LoadGenericMapping(Path.Combine(directory, FileName)).ToList();
			_nodes = _nodeList.ToArray();

			foreach (var node in _nodes)
			{
				_lookup.Add(node.Id, node);
			}
		}

		public void Save(string directory)
		{
			CacheSerializerUtils.SaveGenericMapping(directory, FileName, _nodeList.ToArray());
		}

		public void InitLookup()
		{
		}

		public void PreAssetUpdate()
		{
			_nodeList.Clear();
			_lookup.Clear();
			_nameToFileMapping = GenerateAsmDefFileMapping();
		}

		public void PostAssetUpdate()
		{
			_nodes = _nodeList.ToArray();
		}

		public List<string> GetChangedAssetPaths(string[] allPaths, long[] pathTimestamps)
		{
			return allPaths.Where(path =>
				{
					var ext = Path.GetExtension(path);
					return ext.Equals(".asmdef", StringComparison.OrdinalIgnoreCase) ||
						ext.Equals(".asmref", StringComparison.OrdinalIgnoreCase);
				})
				.ToList();
		}

		public List<IDependencyMappingNode> UpdateAssetsForPath(string path, long timeStamp,
			List<AssetListEntry> assetEntries)
		{
			var result = new List<IDependencyMappingNode>();
			var entry = assetEntries[0];
			var entryAsset = entry.Asset as TextAsset;
			var extension = Path.GetExtension(path);

			if (extension.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
			{
				AddAsmDef(entryAsset, _nodeList, _nameToFileMapping);
			}

			if (extension.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase))
			{
				AddAsmRef(entryAsset, _nodeList, _nameToFileMapping);
			}

			return result;
		}

		public Type GetResolverType() => typeof(IAsmDefDependencyResolver);

		public interface IAsmDefDependencyResolver : IDependencyResolver
		{
		}

		public class AsmDefDependencyResolver : IAsmDefDependencyResolver
		{
			private const string ConnectionTypeDescription = "Dependencies between AssemblyDefinitions";

			private static readonly DependencyType asmdefDependencyType = new DependencyType("AsmDef->AsmDef",
				new Color(0.9f, 0.9f, 0.5f), false, true, ConnectionTypeDescription);

			public const string Id = "AsmdefDependencyResolver";

			public string[] GetDependencyTypes()
			{
				return new[] { AsmdefToAsmdefDependency.Name };
			}

			public string GetId() => Id;

			public DependencyType GetDependencyTypeForId(string typeId) => asmdefDependencyType;
		}
	}
}
