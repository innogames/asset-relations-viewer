using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
    /// Settings for how to display nodes
    /// </summary>
    public class NodeDisplayData
    {
        public Color NodeColorOwn = new Color(0.60f, 0.25f, 0.2f);
        public Color NodeColor = new Color(0.1f, 0.1f, 0.15f);
		
        public int NodeWidth = 16 * 16;
        public int NodeSpaceX = 16 * 8;
        public int NodeSpaceY = 8;
		
        public PrefValueInt AssetPreviewSize = new PrefValueInt("AssetPreviewSize", 16, 16, 128);
        public PrefValueBool HighlightPackagedAssets = new PrefValueBool("HighlightPackagedAssets", false);
        public PrefValueBool ShowSizes = new PrefValueBool("ShowAdditionalInformation", false);
    }
}