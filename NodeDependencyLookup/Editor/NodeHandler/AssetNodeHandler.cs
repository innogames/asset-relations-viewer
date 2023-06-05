using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class AssetNodeType
	{
		public const string Name = "Asset";
	}

	public class SerializedNodeData
	{
		public string Id;
		public string Name;
		public string Type;
		public int Size;
		public long TimeStamp;
	}

	/**
	 * NodeHandler for assets
	 */
	public class AssetNodeHandler : INodeHandler
	{
		private readonly Dictionary<string, SerializedNodeData> _cachedNodeDataLookup = new Dictionary<string, SerializedNodeData>();
		private Dictionary<string, long> cachedTimeStamps = new Dictionary<string, long>();
		private Dictionary<string, Object> idToAssetLookup = new Dictionary<string, Object>();

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
					Node.NodeSize ownNodeSize = NodeDependencyLookupUtility.GetNodeSize(dependency.Node, stateContext);
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

		public void InitNodeDataInformation()
		{
			LoadNodeDataCache();
		}

		public void SaveCaches()
		{
			SaveNodeDataCache();
			idToAssetLookup.Clear();
			cachedTimeStamps.Clear();
		}

		private string GetCachePath()
		{
			string version = "2.2";
			string buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
			return Path.Combine(NodeDependencyLookupUtility.DEFAULT_CACHE_PATH, $"AssetNodeHandlerCache_{buildTarget}_{version}.cache");
		}

		private void LoadNodeDataCache()
		{
			_cachedNodeDataLookup.Clear();

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

				_cachedNodeDataLookup.Add(id, new SerializedNodeData{Id = id, Type = type, Name = name, Size = -1, TimeStamp = timeStamp});
			}
		}

		private void SaveNodeDataCache()
		{
			if (_cachedNodeDataLookup.Count == 0)
			{
				return;
			}

			int offset = 0;
			byte[] bytes = new byte[512 * 1024];

			CacheSerializerUtils.EncodeLong(_cachedNodeDataLookup.Count, ref bytes, ref offset);

			foreach (var pair in _cachedNodeDataLookup)
			{
				CacheSerializerUtils.EncodeString(pair.Value.Id, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(pair.Value.Type, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(pair.Value.Name, ref bytes, ref offset);
				CacheSerializerUtils.EncodeLong(pair.Value.TimeStamp, ref bytes, ref offset);

				bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
			}

			File.WriteAllBytes(GetCachePath(), bytes);
		}

		public Node CreateNode(string id, string type, bool update)
		{
			string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(id);
			string path = AssetDatabase.GUIDToAssetPath(guid);

			bool wasCached = _cachedNodeDataLookup.TryGetValue(id, out SerializedNodeData cachedValue);
			long timeStamp = 0;
			bool timeStampChanged = false;

			if (update)
			{
				if (cachedTimeStamps.TryGetValue(guid, out long value))
				{
					timeStamp = value;
				}
				else
				{
					if (string.IsNullOrEmpty(path))
					{
						return new Node(id, type, "Deleted", NodeDependencyCacheConstants.UnknownNodeType, 0);
					}

					timeStamp = NodeDependencyLookupUtility.GetTimeStampForPath(path);
					cachedTimeStamps.Add(guid, timeStamp);
				}

				timeStampChanged = !wasCached || cachedValue.TimeStamp != timeStamp;
			}
			else if(wasCached)
			{
				timeStamp = cachedValue.TimeStamp;
			}

			if (wasCached && !timeStampChanged)
			{
				return new Node(id, type, cachedValue.Name, cachedValue.Type, cachedValue.TimeStamp);
			}

			GetNameAndType(guid, id, out string name, out string concreteType);
			SerializedNodeData cachedSerializedNodeData = new SerializedNodeData{Id = id, Name = name, Type = concreteType, Size = -1, TimeStamp = timeStamp};

			if (!wasCached)
			{
				_cachedNodeDataLookup.Add(id, cachedSerializedNodeData);
			}
			else
			{
				_cachedNodeDataLookup[id] = cachedSerializedNodeData;
			}

			return new Node(id, type, name, concreteType, timeStamp);
		}

		private void GetNameAndType(string guid, string id, out string name, out string type)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);

			if (!idToAssetLookup.ContainsKey(id))
			{
				NodeDependencyLookupUtility.AddAllAssetsOfId(id, idToAssetLookup);
			}

			idToAssetLookup.TryGetValue(id, out Object asset);

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
