using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class AssetNodeType
	{
		public const string Name = "Asset";
	}

	/**
	 * NodeHandler for assets
	 */
	public class AssetNodeHandler : INodeHandler
	{
		private class SerializedTypeAndName
		{
			public string Id;
			public string Name;
			public string Type;
			public long TimeStamp;
		}

		private readonly Dictionary<string, SerializedTypeAndName> _typeAndNameLookup = new Dictionary<string, SerializedTypeAndName>();

		public string GetHandledNodeType()
		{
			return AssetNodeType.Name;
		}

		public Node.NodeSize GetOwnFileSize(Node node, NodeDependencyLookupContext stateContext)
		{
			foreach (Connection dependency in node.Dependencies)
			{
				if (dependency.DependencyType == AssetToFileDependency.Name)
				{
					Node.NodeSize ownNodeSize = NodeDependencyLookupUtility.UpdateNodeSize(dependency.Node, stateContext);
					ownNodeSize.ContributesToTreeSize = false;
					return ownNodeSize;
				}
			}

			return new Node.NodeSize{Size = 0, ContributesToTreeSize = false};
		}

		public bool IsNodePackedToApp(Node node, bool alwaysExcluded = false)
		{
			if (alwaysExcluded)
			{
				return !IsNodeEditorOnly(node.Id, node.Type);
			}

			string path = AssetDatabase.GUIDToAssetPath(NodeDependencyLookupUtility.GetGuidFromAssetId(node.Id));
			return IsSceneAndPacked(path) || IsInResources(path) || node.Id.StartsWith("0000000");
		}

		public bool IsNodeEditorOnly(string id, string type)
		{
			string path = AssetDatabase.GUIDToAssetPath(NodeDependencyLookupUtility.GetGuidFromAssetId(id));
			return path.Contains("/Editor/");
		}

		public long GetChangedTimeStamp(string id)
		{
			return NodeDependencyLookupUtility.GetTimeStampForFileId(id);
		}

		public void SaveCaches()
		{
			SerializeCache();
		}


		private string GetCachePath()
		{
			string version = "1.0";
			return Path.Combine(NodeDependencyLookupUtility.DEFAULT_CACHE_PATH, $"AssetNodeHandlerCache_{version}.cache");
		}

		private void LoadCache()
		{
			_typeAndNameLookup.Clear();

			string cachePath = GetCachePath();

			int offset = 0;
			byte[] bytes;

			bytes = File.Exists(cachePath) ? File.ReadAllBytes(cachePath) : new byte[16 * 1024];

			long length = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

			for (int i = 0; i < length; ++i)
			{
				string id = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				string type = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				string name = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				long timeStamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

				_typeAndNameLookup.Add(id, new SerializedTypeAndName{Id = id, Type = type, Name = name, TimeStamp = timeStamp});
			}
		}

		private void SerializeCache()
		{
			if (_typeAndNameLookup.Count == 0)
			{
				return;
			}

			int offset = 0;
			byte[] bytes = new byte[512 * 1024];

			CacheSerializerUtils.EncodeLong(_typeAndNameLookup.Count, ref bytes, ref offset);

			foreach (var pair in _typeAndNameLookup)
			{
				CacheSerializerUtils.EncodeString(pair.Value.Id, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(pair.Value.Type, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(pair.Value.Name, ref bytes, ref offset);
				CacheSerializerUtils.EncodeLong(pair.Value.TimeStamp, ref bytes, ref offset);

				bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
			}

			File.WriteAllBytes(GetCachePath(), bytes);
		}

		public void GetNameAndType(string id, out string name, out string type)
		{
			if (_typeAndNameLookup.Count == 0)
			{
				LoadCache();
			}

			string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(id);
			string path = AssetDatabase.GUIDToAssetPath(guid);

			if (string.IsNullOrEmpty(path))
			{
				GetNameAndType(guid, id, out name, out type);
				return;
			}

			long timeStamp = File.GetLastWriteTime(path).Millisecond;

			if (_typeAndNameLookup.TryGetValue(id, out SerializedTypeAndName value) && value.TimeStamp == timeStamp)
			{
				name = value.Name;
				type = value.Type;
				return;
			}

			GetNameAndType(guid, id, out name, out type);
			SerializedTypeAndName typeAndName = new SerializedTypeAndName{Id = id, Name = name, Type = type, TimeStamp = timeStamp};

			if (_typeAndNameLookup.ContainsKey(id))
			{
				_typeAndNameLookup[id] = typeAndName;
			}
			else
			{
				_typeAndNameLookup.Add(id, typeAndName);
			}
		}

		private void GetNameAndType(string guid, string id, out string name, out string type)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			Object asset = NodeDependencyLookupUtility.GetAssetById(id);

			if (asset != null)
			{
				name = $"{asset.name}";

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
				type = "Unknown";
			}

			name = id;
			type = "Unknown";
		}

		private bool IsSceneAndPacked(string path)
		{
			if (Path.GetExtension(path).Equals(".unity"))
			{
				return EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path.Equals(path));
			}

			return false;
		}

		private bool IsInResources(string path)
		{
			return path.Contains("/Resources/");
		}
	}
}
