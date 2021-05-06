using System.Collections.Generic;
using System.IO;
using System.Linq;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
   public class PathVisualizationNode : VisualizationNodeBase
	{
		public PathNode PathNode;
		public HashSet<PathNode> TargetNodes = new HashSet<PathNode>();

		public override string GetSortingKey(RelationType relationType)
		{
			string sortingKey = GetRelationArray(relationType)[0].VNode.GetSortingKey(relationType);
			return sortingKey;
		}

		public override EnclosedBounds GetBoundsOwn(NodeDisplayData displayData)
		{
			int count = GetPathNodeCount();
			int width = PathNode.Width + 16;
			return new EnclosedBounds(0, count * -8 + 6, width, count * 8 + 10);
		}

		public override void Draw(int depth, RelationType relationType, ITypeColorProvider colorProvider, ISelectionChanger selectionChanger,
			NodeDisplayData displayData, ViewAreaData viewAreaData)
		{
			float offset = GetPositionOffset(viewAreaData);
			PathNode.DrawPathNodes(PosX, PosY + offset, PathNode, colorProvider);
			DrawPathNodeConnections(PathNode, TargetNodes, colorProvider, offset);
		}

		public override void CalculateCachedDataInternal()
		{
			TargetNodes.Clear();
			GeneratePathNodeTree();
			PathNode.CalculatePositionData(0, 0, TargetNodes);
		}

		private void DrawPathNodeConnections(PathNode rootNode, HashSet<PathNode> targetNodes, ITypeColorProvider colorProvider, float yOffset)
		{
			foreach (PathNode tn in targetNodes)
			{
				Color color = colorProvider.GetConnectionColorForType(tn.DependencyType);
				int offset = -32;
				int endX = PosX + Bounds.Width;
				AssetRelationsViewerWindow.DrawConnection(tn.PosX + tn.Width + PosX - 16, tn.PosY + PosY + yOffset, endX + offset, tn.PosY + PosY + yOffset, color);
				AssetRelationsViewerWindow.DrawConnection(endX + offset, tn.PosY + PosY + yOffset, endX, rootNode.PosY + PosY + yOffset, color);
			}
		}

		private int GetPathNodeCount()
		{
			int pathCount = 0;

			foreach (VisualizationConnection connection in GetRelations(RelationType.DEPENDENCY, true, true))
			{
				for (var i = 0; i < connection.Datas.Count; i++)
				{
					if (connection.Datas[i].PathSegments.Length > 0)
					{
						pathCount++;
					}
				}
			}
			
			return pathCount;
		}
		
		private void GeneratePathNodeTree()
		{
			PathNode = new PathNode(string.Empty, PathSegmentType.GameObject, "Root");
			
			foreach (VisualizationConnection connection in GetRelations(RelationType.DEPENDENCY, true, true))
			{
				foreach (VisualizationConnection.Data data in connection.Datas)
				{
					PathNode.AddPath(PathNode, data.PathSegments.Reverse().ToArray(), data.Type);
				}
			}
			
			PathNode.CalculateNodeHeight(PathNode);
		}
	}
}