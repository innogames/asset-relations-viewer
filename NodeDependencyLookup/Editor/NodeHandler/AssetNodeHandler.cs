﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public static class AssetNodeType
	{
		public const string Name = "Asset";
	}

	public class SerializedNodeData
	{
		public string Id;
		public string Name;
		public string Type;
		public long TimeStamp;
	}

	/// <summary>
	/// NodeHandler for assets
	/// </summary>
	[UsedImplicitly]
	public class AssetNodeHandler : INodeHandler
	{
		private class NameAndType
		{
			public string Name;
			public string Type;
		}

		private class FileData
		{
			public readonly Dictionary<string, NameAndType> Assets = new Dictionary<string, NameAndType>();
		}

		private readonly Dictionary<string, SerializedNodeData> _cachedNodeDataLookup =
			new Dictionary<string, SerializedNodeData>();

		private readonly Dictionary<string, long> _cachedTimeStamps = new Dictionary<string, long>(64 * 1024);
		private readonly Dictionary<string, FileData> _fileDataMapping = new Dictionary<string, FileData>();
		private readonly List<AssetListEntry> _assetList = new List<AssetListEntry>(1024);

		public string GetHandledNodeType() => AssetNodeType.Name;

		public void InitializeOwnFileSize(Node node, NodeDependencyLookupContext context, bool updateNodeData)
		{
			// nothing to do
		}

		public void CalculateOwnFileSizeParallel(Node node, NodeDependencyLookupContext context, bool updateNodeData)
		{
			// nothing to do
		}

		public void CalculateOwnFileDependencies(Node node, NodeDependencyLookupContext context,
			HashSet<Node> calculatedNodes)
		{
			foreach (var dependency in node.Dependencies)
			{
				if (dependency.DependencyType != AssetToFileDependency.Name)
				{
					continue;
				}

				NodeDependencyLookupUtility.UpdateOwnFileSizeDependenciesForNode(dependency.Node, context,
					calculatedNodes);
				var ownNodeSize = dependency.Node.OwnSize;
				ownNodeSize.ContributesToTreeSize = false;

				node.OwnSize = ownNodeSize;
				return;
			}

			node.OwnSize = new Node.NodeSize { Size = 0, ContributesToTreeSize = false };
		}

		public bool IsNodePackedToApp(Node node, bool alwaysExcluded = false)
		{
			if (alwaysExcluded)
			{
				return !IsNodeEditorOnly(node.Id, node.Type);
			}

			var path = AssetDatabase.GUIDToAssetPath(NodeDependencyLookupUtility.GetGuidFromAssetId(node.Id));
			return IsSceneAndPacked(path) || IsInResources(path) ||
				node.Id.StartsWith("0000000", StringComparison.Ordinal);
		}

		public bool IsNodeEditorOnly(string id, string type)
		{
			var path = AssetDatabase.GUIDToAssetPath(NodeDependencyLookupUtility.GetGuidFromAssetId(id));
			return path.Contains("/Editor/");
		}

		public void InitNodeCreation()
		{
			LoadNodeDataCache();
		}

		public void CalculatePrecalculatableAsyncDataWhileCacheExecution(Node node, List<Task> taskList)
		{
			// Nothing to do
		}

		public void SaveCaches()
		{
			SaveNodeDataCache();
			_cachedTimeStamps.Clear();
		}

		private string GetCachePath()
		{
			var version = "3.0";
			var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
			return Path.Combine(NodeDependencyLookupUtility.DEFAULT_CACHE_PATH,
				$"AssetNodeHandlerCache_{buildTarget}_{version}.cache");
		}

		private void LoadNodeDataCache()
		{
			_cachedNodeDataLookup.Clear();

			var cachePath = GetCachePath();
			var offset = 0;

			var bytes = File.Exists(cachePath) ? File.ReadAllBytes(cachePath) : new byte[16 * 1024];
			var length = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

			for (var i = 0; i < length; ++i)
			{
				var id = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				var type = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				var name = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				var timeStamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

				_cachedNodeDataLookup.Add(id,
					new SerializedNodeData { Id = id, Type = type, Name = name, TimeStamp = timeStamp });
			}
		}

		private void SaveNodeDataCache()
		{
			if (_cachedNodeDataLookup.Count == 0)
			{
				return;
			}

			var cachePath = GetCachePath();
			var offset = 0;
			var bytes = new byte[512 * 1024];

			CacheSerializerUtils.EncodeLong(_cachedNodeDataLookup.Count, ref bytes, ref offset);

			foreach (var pair in _cachedNodeDataLookup)
			{
				CacheSerializerUtils.EncodeString(pair.Value.Id, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(pair.Value.Type, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(pair.Value.Name, ref bytes, ref offset);
				CacheSerializerUtils.EncodeLong(pair.Value.TimeStamp, ref bytes, ref offset);

				bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
			}

			File.WriteAllBytes(cachePath, bytes);
		}

		public Node CreateNode(string id, string type, bool update, out bool wasCached)
		{
			var guid = NodeDependencyLookupUtility.GetGuidFromAssetId(id);
			var path = AssetDatabase.GUIDToAssetPath(guid);

			wasCached = _cachedNodeDataLookup.TryGetValue(id, out var cachedValue);
			var timeStamp = 0L;
			var timeStampChanged = false;

			if (update)
			{
				if (string.IsNullOrEmpty(path))
				{
					return new Node(id, type, "Deleted", NodeDependencyCacheConstants.UnknownNodeType);
				}

				if (!_cachedTimeStamps.TryGetValue(guid, out timeStamp))
				{
					timeStamp = NodeDependencyLookupUtility.GetTimeStampForPath(path);
					_cachedTimeStamps.Add(guid, timeStamp);
				}

				timeStampChanged = !wasCached || cachedValue.TimeStamp != timeStamp;
			}
			else if (wasCached)
			{
				timeStamp = cachedValue.TimeStamp;
			}

			if (wasCached && !timeStampChanged)
			{
				return new Node(id, type, cachedValue.Name, cachedValue.Type);
			}

			GetNameAndType(path, id, out var name, out var concreteType);
			var cachedSerializedNodeData = new SerializedNodeData
				{ Id = id, Name = name, Type = concreteType, TimeStamp = timeStamp };

			if (!wasCached)
			{
				_cachedNodeDataLookup.Add(id, cachedSerializedNodeData);
			}
			else
			{
				_cachedNodeDataLookup[id] = cachedSerializedNodeData;
			}

			return new Node(id, type, name, concreteType);
		}

		private void GetNameAndType(string path, string assetId, out string name, out string type)
		{
			_assetList.Clear();
			name = NodeDependencyCacheConstants.UnknownNodeType;
			type = NodeDependencyCacheConstants.UnknownNodeType;

			var guid = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

			if (!_fileDataMapping.ContainsKey(guid))
			{
				NodeDependencyLookupUtility.AddAssetsOfPathToList(_assetList, path);
				var assetData = new FileData();

				foreach (var entry in _assetList)
				{
					GetNameAndTypeForAsset(entry.Asset, entry.AssetId, path, out name, out type);
					assetData.Assets.Add(entry.AssetId, new NameAndType { Name = name, Type = type });
				}

				_fileDataMapping.Add(guid, assetData);
			}

			if (_fileDataMapping.TryGetValue(guid, out var value) &&
			    value.Assets.TryGetValue(assetId, out var nameAndType))
			{
				name = nameAndType.Name;
				type = nameAndType.Type;
			}
		}

		private void GetNameAndTypeForAsset(Object asset, string id, string path, out string name, out string type)
		{
			if (asset != null)
			{
				name = asset.name;

				if (string.IsNullOrEmpty(name))
				{
					name = $"Unnamed {asset.GetType().FullName}";
				}

				type = asset.GetType().FullName;
				return;
			}

			if (!string.IsNullOrEmpty(path))
			{
				name = path;
				type = Directory.Exists(path) ? "Folder" : "Unknown";
				return;
			}

			name = id;
			type = "Unknown";
		}

		private bool IsSceneAndPacked(string path)
		{
			if (Path.GetExtension(path).Equals(".unity", StringComparison.Ordinal))
			{
				return EditorBuildSettings.scenes.Any(scene =>
					scene.enabled && scene.path.Equals(path, StringComparison.Ordinal));
			}

			return false;
		}

		private bool IsInResources(string path) => path.Contains("/Resources/");
	}
}
