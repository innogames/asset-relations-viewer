using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// Settings for how to display <see cref="NodeVisualizationNode"/> in the TreeView
	/// </summary>
	public class NodeDisplayData
	{
		public int NodeWidth = 16 * 16;
		public int NodeSpaceX = 16 * 8;
		public int NodeSpaceY = 8;

		public PrefValueInt AssetPreviewSize = new PrefValueInt("AssetPreviewSize", 16, 16, 128);
		public PrefValueBool HighlightPackagedAssets = new PrefValueBool("HighlightPackagedAssets", false);
		public PrefValueBool ShowAdditionalInformation = new PrefValueBool("ShowAdditionalInformation", false);
		public PrefValueBool ShowAssetPreview = new PrefValueBool("ShowAssetPreview", false);
	}
}