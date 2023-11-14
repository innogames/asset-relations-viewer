using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	public class AssetVisualizationNodeData : VisualizationNodeData
	{
		private Object _loadedAsset;
		private Object _loadedMainAsset;
		private bool _assetLoaded;
		private int _assetPreviewRenderTries;

		private const int MAX_ASSET_PREVIEW_RENDER_RETRIES = 100;

		public AssetVisualizationNodeData(Node node)
		{
			Node = node;
		}

		public override Texture2D AssetPreviewTexture => TryGetAssetPreview();

		public Texture2D TryGetAssetPreview()
		{
			if (!_assetLoaded)
			{
				_loadedAsset = NodeDependencyLookupUtility.GetAssetById(Node.Id);
				_loadedMainAsset = NodeDependencyLookupUtility.GetMainAssetById(Node.Id);
				_assetLoaded = true;
			}

			if (_loadedAsset != null && _assetPreview == null &&
			    _assetPreviewRenderTries < MAX_ASSET_PREVIEW_RENDER_RETRIES)
			{
				var previewTexture = AssetPreview.GetAssetPreview(_loadedAsset);

				if (previewTexture == null)
				{
					previewTexture = AssetPreview.GetAssetPreview(_loadedMainAsset);
				}

				if (previewTexture != null)
				{
					var copyTexture = new Texture2D(previewTexture.width, previewTexture.height, previewTexture.format,
						false);
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

		public override Texture2D ThumbNailTexture => GetThumbnail();

		private Texture2D GetThumbnail()
		{
			if (_thumbNail == null)
			{
				var path = AssetDatabase.GUIDToAssetPath(NodeDependencyLookupUtility.GetGuidFromAssetId(Node.Id));
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