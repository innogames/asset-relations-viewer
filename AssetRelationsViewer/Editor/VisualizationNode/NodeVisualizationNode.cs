using System;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// VisualizationNode to display a <see cref="Node"/>> in the TreeView
	/// </summary>
	public class NodeVisualizationNode : VisualizationNodeBase
	{
		public ITypeHandler TypeHandler;
		public VisualizationNodeData NodeData;
		public string Key;
		public int Hash;

		public bool HasNonFilteredChildren = false;
		public bool Filtered;

		public void SetKey(string value)
		{
			Key = value;
			Hash = value.GetHashCode();
		}

		public override string GetSortingKey(RelationType relationType, bool sortBySize)
		{
			if (sortBySize)
			{
				return NodeData.Node.OwnSize.Size.ToString("000000000000000000000");
			}

			return NodeData.GetSortingKey();
		}

		public override EnclosedBounds GetBoundsOwn(NodeDisplayData displayData)
		{
			float height = displayData.AssetPreviewSize;

			if (displayData.ShowAdditionalInformation)
			{
				height = Math.Max(48, height);
			}

			float width = displayData.AssetPreviewSize + displayData.NodeWidth;
			height = displayData.NodeSpaceY + Math.Max(height, PathNode.NodeHeight);
			return new EnclosedBounds(0, 0, (int) width, (int) height);
		}

		public override void Draw(int depth, RelationType relationType, INodeDisplayDataProvider displayDataProvider,
			ISelectionChanger selectionChanger, NodeDisplayData displayData, ViewAreaData viewAreaData)
		{
			Profiler.BeginSample("VisualizationNode::Draw()");
			var position = GetPosition(viewAreaData);

			var rectColor = depth == 0 ? ARVStyles.NodeBackGroundColorOwn : ARVStyles.NodeBackGroundColor;

			var isMissing = NodeData.IsMissing;

			if (NodeData.IsEditorAsset)
			{
				rectColor.b += 0.05f;
			}

			if (isMissing)
			{
				rectColor = new Color(0.8f, 0.07f, 0.02f, 1.0f);
			}

			int assetPreviewSize = displayData.AssetPreviewSize;
			EditorGUI.DrawRect(new Rect(position.x + assetPreviewSize, position.y, displayData.NodeWidth, 16),
				rectColor);

			if (NodeData.IsPackedToApp && displayData.HighlightPackagedAssets)
			{
				EditorGUI.DrawRect(new Rect(position.x + assetPreviewSize, position.y + 16, displayData.NodeWidth, 1),
					ARVStyles.PackageToAppColor);
			}

			DrawPreviewTexture(position.x, position.y, displayData);

			var style = new GUIStyle();
			var textColor = Color.white;

			if (depth > 0)
			{
				var typeId = GetRelations(NodeDependencyLookupUtility.InvertRelationType(relationType))[0].Datas[0]
					.Type;
				textColor = displayDataProvider.GetConnectionColorForType(typeId);
			}

			textColor *= ARVStyles.TextColorMod;

			style.normal.textColor = textColor;
			style.clipping = TextClipping.Clip;
			var contributesToTreeSize = NodeData.Node.OwnSize.ContributesToTreeSize;
			var fullTypeText = $"[{NodeData.Node.ConcreteType}]";
			var typeText = $"[{GetNameFromFullName(NodeData.Node.ConcreteType)}]";
			var name = isMissing ? "Missing!!!" : TypeHandler.GetNodeDisplayName(NodeData.Node);
			var fullname = isMissing ? "Missing!!!" : NodeData.Node.Name;
			var tooltip = fullTypeText + " " + fullname +
			              $"\nContributes to tree size: {contributesToTreeSize.ToString()}";
			GUI.Label(new Rect(position.x + assetPreviewSize, position.y, displayData.NodeWidth - 32, assetPreviewSize),
				new GUIContent(name, tooltip), style);

			if (displayData.ShowAdditionalInformation)
			{
				if (NodeData.HierarchySize == -1)
				{
					displayDataProvider.EnqueueTreeSizeCalculationForNode(NodeData);
				}

				GUI.Label(new Rect(position.x + assetPreviewSize, position.y + 16, 200, 16), typeText);

				var treeSizeAmountText = (NodeData.HierarchySize / 1024).ToString();
				var threeSizeText = NodeData.HierarchySize >= 0 ? $"{treeSizeAmountText}kb" : "calc...";
				var sizeAmountText = (NodeData.Node.OwnSize.Size / 1024).ToString();

				var text = $"{sizeAmountText}kb | Tree: {threeSizeText}";
				GUI.Label(new Rect(position.x + assetPreviewSize, position.y + 32, 200, 16), text);
			}

			var cutData = GetCutData(relationType, false);

			if (cutData != null)
			{
				var x = relationType == RelationType.DEPENDENCY ? Bounds.Rect.xMax : Bounds.Rect.xMin - 16;
				var cutTooltip = "Connections hidden due to reasons:";

				foreach (var entry in cutData.Entries)
				{
					cutTooltip += $"\n{entry.CutReason} : {entry.Count}";
				}

				style.fontSize = 20;
				style.normal.textColor = new Color(0.7f, 0.7f, 0.8f, 1);
				GUI.Label(new Rect(x, position.y - 5, 20, 20), new GUIContent("+", cutTooltip), style);
			}

			DrawIsFilteredOverlay(position, displayData);

			if (GUI.Button(new Rect(position.x + displayData.NodeWidth + assetPreviewSize - 16, position.y, 16, 16),
				    ">"))
			{
				selectionChanger.ChangeSelection(NodeData.Node.Id, NodeData.Node.Type);
			}

			if (GUI.Button(new Rect(position.x + displayData.NodeWidth + assetPreviewSize - 32, position.y, 16, 16),
				    "s"))
			{
				NodeData.TypeHandler.SelectInEditor(NodeData.Node.Id);
			}

			Profiler.EndSample();
		}

		public override bool HasNoneFilteredChildren(RelationType relationType)
		{
			return HasNonFilteredChildren;
		}

		public override bool IsFiltered(RelationType relationType)
		{
			return Filtered;
		}

		private string GetNameFromFullName(string fullName)
		{
			var lastIndex = fullName.LastIndexOf('.');

			if (lastIndex != -1)
			{
				return fullName.Substring(lastIndex + 1);
			}

			return fullName;
		}

		private void DrawIsFilteredOverlay(Vector2 position, NodeDisplayData displayData)
		{
			if (Filtered)
			{
				var b = GetBoundsOwn(displayData).Rect;
				var rect = new Rect(b.x + position.x, b.y + position.y, b.width, b.height);

				EditorGUI.DrawRect(rect, ARVStyles.NodeFilteredOverlayColor);
			}
		}

		/// <summary>
		/// Draws the preview texture for the asset. Tries to use the actual Previewtexture but uses the Thumbnail if no preview could be rendered
		/// </summary>
		private void DrawPreviewTexture(float pX, float pY, NodeDisplayData displayData)
		{
			int assetPreviewSize = displayData.AssetPreviewSize;

			var texture = NodeData.ThumbNailTexture;

			if (displayData.ShowAssetPreview)
			{
				var assetPreview = NodeData.AssetPreviewTexture;

				if (assetPreview != null)
				{
					texture = assetPreview;
				}
			}

			var position = new Rect(pX, pY, assetPreviewSize, assetPreviewSize);

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