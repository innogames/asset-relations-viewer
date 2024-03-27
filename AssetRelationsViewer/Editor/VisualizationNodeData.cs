using System;
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

		public Node Node;
		public ITypeHandler TypeHandler;

		public int HierarchySize = -1;
		public bool IsEditorAsset = false;
		public bool IsPackedToApp = false;
		public bool IsMissing = false;

		public abstract Texture2D AssetPreviewTexture { get; }
		public abstract Texture2D ThumbNailTexture { get; }

		public string GetSortingKey()
		{
			return TypeHandler.GetSortingKey(Node.Name);
		}
	}
}