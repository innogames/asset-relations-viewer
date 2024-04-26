using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public static class FileNodeType
	{
		public const string Name = "File";
	}

	/// <summary>
	/// NodeHandler for files
	/// </summary>
	[UsedImplicitly]
	public class FileNodeHandler : INodeHandler
	{
		private class CachedData
		{
			public string Id;
			public int Size;
			public long TimeStamp;
		}

		private readonly Dictionary<Node, string> nodeToArtifactPathLookup = new Dictionary<Node, string>();

		private readonly MethodInfo getPreviewTextureMethod;
		private readonly MethodInfo getAudioSizeMethod;
		private readonly MethodInfo getStorageMemorySizeMethod;

		private readonly string audioClipTypeName = typeof(AudioClip).FullName;
		private readonly string spriteTypeName = typeof(Sprite).FullName;
		private readonly string spriteAtlasTypeName = typeof(SpriteAtlas).FullName;

		private readonly ConcurrentDictionary<string, CachedData> cachedSizeLookup = new ConcurrentDictionary<string, CachedData>();

		public FileNodeHandler()
		{
			var spriteAtlasExtensionsType = typeof(SpriteAtlasExtensions);
			getPreviewTextureMethod =
				spriteAtlasExtensionsType.GetMethod("GetPreviewTextures", BindingFlags.Static | BindingFlags.NonPublic);

			var unityAssembly = Assembly.Load("UnityEditor.dll");
			var textureUtilType = unityAssembly.GetType("UnityEditor.TextureUtil");
			getStorageMemorySizeMethod = textureUtilType.GetMethod("GetStorageMemorySizeLong",
				BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);

			var audioUtilType = unityAssembly.GetType("UnityEditor.AudioImporter");
			getAudioSizeMethod = audioUtilType.GetMethod("get_compSize",
				BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public string GetHandledNodeType() => FileNodeType.Name;

		private static long? GetCompressedSize(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return 0;
			}

			using (var memoryStream = new MemoryStream())
			{
				var fileInfo = new FileInfo(path);
				if (!fileInfo.Exists)
				{
					return null;
				}

				using (var compressionStream = new GZipStream(memoryStream, CompressionMode.Compress, true))

				using (var originalFileStream = fileInfo.OpenRead())
				{
					originalFileStream.CopyTo(compressionStream);
				}

				return memoryStream.Position;
			}
		}

		public void InitializeOwnFileSize(Node node, NodeDependencyLookupContext stateContext, bool updateNodeData)
		{
			var isSpriteOfSpriteAtlas = IsSpriteOfSpriteAtlas(node);
			var contributesToTreeSize = !isSpriteOfSpriteAtlas;

			var size = GetSpriteAtlasSize(node) + GetAudioClipSize(node);

			var guid = NodeDependencyLookupUtility.GetGuidFromAssetId(node.Id);
			nodeToArtifactPathLookup.Add(node, NodeDependencyLookupUtility.GetLibraryFullPath(guid));

			node.OwnSize.Size = size;
			node.OwnSize.ContributesToTreeSize = contributesToTreeSize;
		}

		public void CalculateOwnFileSize(Node node, NodeDependencyLookupContext stateContext, bool updateNodeData)
		{
			var id = node.Id;
			var packedAssetSize = 0;
			var wasCached = cachedSizeLookup.TryGetValue(id, out var cachedValue);

			long timeStamp = 0;
			var timeStampChanged = false;

			if (updateNodeData)
			{
				timeStamp = NodeDependencyLookupUtility.GetTimeStampForPath(node.Name);
				timeStampChanged = (!wasCached || cachedValue.TimeStamp != timeStamp) && updateNodeData;
			}

			if (wasCached && !timeStampChanged)
			{
				packedAssetSize = cachedValue.Size;
			}
			else
			{
				var compressedSize = GetCompressedSize(nodeToArtifactPathLookup[node]);

				if (compressedSize != null)
				{
					packedAssetSize = (int)compressedSize.Value;
				}
			}

			var cachedData = new CachedData { Id = id, Size = packedAssetSize, TimeStamp = timeStamp };

			if (updateNodeData)
			{
				if (wasCached)
				{
					cachedSizeLookup[id] = cachedData;
				}
				else
				{
					cachedSizeLookup.TryAdd(id, cachedData);
				}
			}

			node.OwnSize.Size += packedAssetSize;
		}

		public void CalculateOwnFileDependencies(Node node, NodeDependencyLookupContext context,
			HashSet<Node> calculatedNodes)
		{
			// nothing to do
		}

		private int GetSpriteAtlasSize(Node node)
		{
			var size = 0;

			foreach (var connection in node.Referencers)
			{
				if (connection.Node.ConcreteType == spriteAtlasTypeName)
				{
					var guid = NodeDependencyLookupUtility.GetGuidFromAssetId(connection.Node.Id);
					var path = AssetDatabase.GUIDToAssetPath(guid);
					var spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);

					if (spriteAtlas == null)
					{
						continue;
					}

					var previewTextures =
						getPreviewTextureMethod.Invoke(null, new object[] { spriteAtlas }) as Texture2D[];

					foreach (var previewTexture in previewTextures)
					{
						size += Convert.ToInt32(
							getStorageMemorySizeMethod.Invoke(null, new object[] { previewTexture }));
					}
				}
			}

			return size;
		}

		private int GetAudioClipSize(Node node)
		{
			foreach (var connection in node.Referencers)
			{
				if (connection.Node.ConcreteType == audioClipTypeName)
				{
					var guid = NodeDependencyLookupUtility.GetGuidFromAssetId(connection.Node.Id);
					var path = AssetDatabase.GUIDToAssetPath(guid);
					var importer = AssetImporter.GetAtPath(path) as AudioImporter;
					return (int)getAudioSizeMethod.Invoke(importer, new object[] { });
				}
			}

			return 0;
		}

		private bool IsSpriteOfSpriteAtlas(Node node)
		{
			foreach (var connection in node.Referencers)
			{
				if (connection.Node.ConcreteType == spriteTypeName)
				{
					foreach (var cconnection in connection.Node.Referencers)
					{
						if (cconnection.Node.ConcreteType == spriteAtlasTypeName)
						{
							return true;
						}
					}

					return false;
				}
			}

			return false;
		}

		public bool IsNodePackedToApp(Node node, bool alwaysExcluded)
		{
			if (alwaysExcluded)
			{
				return !IsNodeEditorOnly(node.Id, node.Type);
			}

			var path = AssetDatabase.GUIDToAssetPath(node.Id);
			return IsSceneAndPacked(path) || IsInResources(path) || node.Id.StartsWith("0000000", StringComparison.Ordinal);
		}

		public bool IsNodeEditorOnly(string id, string type)
		{
			var path = AssetDatabase.GUIDToAssetPath(id);
			return path.Contains("/Editor/");
		}

		public Node CreateNode(string id, string type, bool update, out bool wasCached)
		{
			wasCached = false;
			return new Node(id, type, AssetDatabase.GUIDToAssetPath(id), "File");
		}

		private string GetCachePath()
		{
			var version = "2.0";
			var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();

			return Path.Combine(NodeDependencyLookupUtility.DEFAULT_CACHE_PATH,
				$"FileNodeHandlerCache_{buildTarget}_{version}.cache");
		}

		public void InitNodeCreation()
		{
			cachedSizeLookup.Clear();

			var cachePath = GetCachePath();
			var offset = 0;
			var bytes = File.Exists(cachePath) ? File.ReadAllBytes(cachePath) : new byte[16 * 1024];
			var length = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

			for (var i = 0; i < length; ++i)
			{
				var id = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				var size = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);
				var timeStamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

				cachedSizeLookup.TryAdd(id, new CachedData { Id = id, Size = (int)size, TimeStamp = timeStamp });
			}
		}

		public void SaveCaches()
		{
			if (cachedSizeLookup.Count == 0)
			{
				return;
			}

			var offset = 0;
			var bytes = new byte[512 * 1024];

			CacheSerializerUtils.EncodeLong(cachedSizeLookup.Count, ref bytes, ref offset);

			foreach (var pair in cachedSizeLookup)
			{
				CacheSerializerUtils.EncodeString(pair.Value.Id, ref bytes, ref offset);
				CacheSerializerUtils.EncodeLong(pair.Value.Size, ref bytes, ref offset);
				CacheSerializerUtils.EncodeLong(pair.Value.TimeStamp, ref bytes, ref offset);

				bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
			}

			File.WriteAllBytes(GetCachePath(), bytes);

			cachedSizeLookup.Clear();
			nodeToArtifactPathLookup.Clear();
		}

		private bool IsSceneAndPacked(string path)
		{
			if (Path.GetExtension(path).Equals(".unity", StringComparison.Ordinal))
			{
				return EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path.Equals(path, StringComparison.Ordinal));
			}

			return false;
		}

		private bool IsInResources(string path) => path.Contains("/Resources/");
	}
}
