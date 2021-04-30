using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// To not always calculate this information each frame we store data which required quite long per asset to generate
	/// </summary>
	public abstract class VisualizationNodeData
	{
		protected Texture2D _assetPreview = null;
		protected Texture2D _thumbNail = null;

		public string Name;
		
		public string Id;
		public string Type;

		public ITypeHandler TypeHandler;
		public INodeHandler NodeHandler;
			
		public int HierarchySize = 0;
		public int OwnSize = 0;
		public bool IsEditorAsset = false;
		public bool IsPackedToApp = false;
		public bool IsMissing = false;

		public abstract Texture2D AssetPreviewTexture { get; }
		public abstract Texture2D ThumbNailTexture { get; }
	}

	public class AssetVisualizationNodeData : VisualizationNodeData
	{
		private Object _loadedAsset;
		private Object _loadedMainAsset;
		private bool _assetLoaded;
		private int _assetPreviewRenderTries;

		private const int MAX_ASSET_PREVIEW_RENDER_RETRIES = 100;

		public AssetVisualizationNodeData(string id, string type)
		{
			Id = id;
			Type = type;
		}

		public override Texture2D AssetPreviewTexture
		{
			get { return TryGetAssetPreview(); }
		}

		public Texture2D TryGetAssetPreview()
		{
			if (!_assetLoaded)
			{
				_loadedAsset = NodeDependencyLookupUtility.GetAssetById(Id);
				_loadedMainAsset = NodeDependencyLookupUtility.GetMainAssetById(Id);
				_assetLoaded = true;
			}
			
			if (_loadedAsset != null && _assetPreview == null && _assetPreviewRenderTries < MAX_ASSET_PREVIEW_RENDER_RETRIES)
			{
				Texture2D previewTexture = AssetPreview.GetAssetPreview(_loadedAsset);

				if (previewTexture == null)
				{
					previewTexture = AssetPreview.GetAssetPreview(_loadedMainAsset);
				}

				if (previewTexture != null)
				{
					Texture2D copyTexture = new Texture2D(previewTexture.width, previewTexture.height, previewTexture.format, false);
					Graphics.CopyTexture(previewTexture, copyTexture);

					_assetPreview = copyTexture;
				}
				else
				{
					_assetPreviewRenderTries++;
				}
			}

			return _assetPreview;
		}

		public override Texture2D ThumbNailTexture
		{
		
			get { return GetThumbnail(); }
		}

		private Texture2D GetThumbnail()
		{
			if (_thumbNail == null)
			{
				string path = AssetDatabase.GUIDToAssetPath(Id);
				_thumbNail = AssetDatabase.GetCachedIcon(path) as Texture2D;

				if (_thumbNail == null)
				{
					_thumbNail = AssetPreview.GetMiniTypeThumbnail(AssetDatabase.GetMainAssetTypeAtPath(path));
				}

				if (_thumbNail == null)
				{
					_thumbNail = AssetPreview.GetMiniTypeThumbnail(typeof(Object));
				}
			}

			return _thumbNail;
		}
	}
}