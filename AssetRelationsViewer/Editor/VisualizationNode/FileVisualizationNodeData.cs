using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class FileVisualizationNodeData : VisualizationNodeData
    {
	    private Object _loadedMainAsset;
    	private bool _assetLoaded;
    	private int _assetPreviewRenderTries;

    	private const int MAX_ASSET_PREVIEW_RENDER_RETRIES = 100;

    	public FileVisualizationNodeData(Node node)
        {
	        Node = node;
        }

    	public override Texture2D AssetPreviewTexture
    	{
    		get { return TryGetAssetPreview(); }
    	}

    	public Texture2D TryGetAssetPreview()
    	{
    		if (!_assetLoaded)
    		{
	            _loadedMainAsset = NodeDependencyLookupUtility.GetMainAssetById(Node.Id);
    			_assetLoaded = true;
    		}

    		if (_loadedMainAsset != null && _assetPreview == null && _assetPreviewRenderTries < MAX_ASSET_PREVIEW_RENDER_RETRIES)
    		{
    			Texture2D previewTexture = AssetPreview.GetAssetPreview(_loadedMainAsset);

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
    			string path = AssetDatabase.GUIDToAssetPath(Node.Id);
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