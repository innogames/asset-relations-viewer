using System;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class VisualizationNode : VisualizationNodeBase
	{
		public VisualizationNodeData NodeData;
		public string Key;
		public int Hash;

		public void SetKey(string value)
		{
			Key = value;
			Hash = value.GetHashCode();
		}

		public override string GetSortingKey(RelationType relationType)
		{
			return NodeData.GetSortingKey();
		}

		public override EnclosedBounds GetBoundsOwn(NodeDisplayData displayData)
		{
			float height = displayData.AssetPreviewSize;
				
			if (displayData.ShowSizes)
			{
				height = Math.Max(32, height);
			}
			
			float width = displayData.AssetPreviewSize + displayData.NodeWidth;
			height = displayData.NodeSpaceY + Math.Max(height, PathNode.NodeHeight);
			return new EnclosedBounds(0, 0, (int)width, (int)height);
		}

		public override void Draw(int depth, RelationType relationType, INodeDisplayDataProvider displayDataProvider, 
			ISelectionChanger selectionChanger, NodeDisplayData displayData, ViewAreaData viewAreaData)
		{
			Vector2 position = GetPosition(viewAreaData);
			
			Color rectColor = (depth == 0) ? ARVStyles.NodeBackGroundColorOwn : ARVStyles.NodeBackGroundColor;

			bool isMissing = NodeData.IsMissing;

			if (NodeData.IsEditorAsset)
			{
				rectColor.b += 0.05f;
			}

			if (isMissing)
			{
				rectColor = new Color(0.8f, 0.07f, 0.02f, 1.0f);
			}

			int assetPreviewSize = displayData.AssetPreviewSize;
			EditorGUI.DrawRect(new Rect(position.x + assetPreviewSize, position.y, displayData.NodeWidth, 16), rectColor);

			if (NodeData.IsPackedToApp && displayData.HighlightPackagedAssets)
			{
				EditorGUI.DrawRect(new Rect(position.x + assetPreviewSize, position.y + 16, displayData.NodeWidth, 1), ARVStyles.PackageToAppColor);
			}

			DrawPreviewTexture(position.x, position.y, displayData);

			GUIStyle style = new GUIStyle();
			Color textColor = Color.white;
			
			if (depth > 0)
			{
				string typeId = GetRelations(AssetRelationsViewerWindow.InvertRelationType(relationType))[0].Datas[0].Type; // TODO move
				textColor = displayDataProvider.GetConnectionColorForType(typeId);
			}
				
			textColor *= ARVStyles.TextColorMod;

			style.normal.textColor = textColor;
			style.clipping = TextClipping.Clip;
			string name = isMissing ? "Missing!!!" : NodeData.Name;
			GUI.Label(new Rect(position.x + assetPreviewSize, position.y, displayData.NodeWidth - 32, assetPreviewSize), name, style);
			
			if (displayData.ShowSizes)
			{
				if (NodeData.HierarchySize == -1)
				{
					displayDataProvider.EnqueueTreeSizeCalculationForNode(NodeData);
				}

				string threeSizeText = NodeData.HierarchySize >= 0 ? $"{NodeData.HierarchySize}kb" : "calc...";
				
				string text = $"Size: {NodeData.OwnSize}kb | TreeSize: {threeSizeText}";
				GUI.Label(new Rect(position.x + assetPreviewSize, position.y + 16, 200, 16), text);
			}
			
			DrawIsFilteredOverlay(position, displayData);
			
			if (GUI.Button(new Rect(position.x + displayData.NodeWidth + assetPreviewSize - 16, position.y, 16, 16), ">"))
			{
				selectionChanger.ChangeSelection(NodeData.Id, NodeData.Type);
			}

			if (GUI.Button(new Rect(position.x + displayData.NodeWidth + assetPreviewSize - 32, position.y, 16, 16), "s"))
			{
				NodeData.TypeHandler.SelectInEditor(NodeData.Id);
			}
		}

		public void DrawIsFilteredOverlay(Vector2 position, NodeDisplayData displayData)
		{
			if (IsFiltered)
			{
				Rect b = GetBoundsOwn(displayData).Rect;
				Rect rect = new Rect(b.x + position.x, b.y + position.y, b.width, b.height);
				
				EditorGUI.DrawRect(rect, ARVStyles.NodeFilteredOverlayColor);
			}
		}
		
		/// <summary>
		/// Draws the preview texture for the asset. Tries to use the actual Previewtexture but uses the Thumbnail if no preview could be rendered
		/// </summary>
		private void DrawPreviewTexture(float pX, float pY, NodeDisplayData displayData)
		{
			int assetPreviewSize = displayData.AssetPreviewSize;
			Texture2D texture = NodeData.AssetPreviewTexture != null ? NodeData.AssetPreviewTexture : NodeData.ThumbNailTexture;
			Rect position = new Rect(pX, pY, assetPreviewSize, assetPreviewSize);

			if (texture != null)
			{
				EditorGUI.DrawTextureTransparent(position, texture);
			}
			else
			{
				EditorGUI.DrawRect(position, Color.black);
			}
		}
	}
}