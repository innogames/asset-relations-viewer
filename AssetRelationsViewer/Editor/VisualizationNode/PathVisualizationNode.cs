using System.Collections.Generic;
using System.Linq;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// VisualizationNode to for displaying a path (GameObject -> Component -> MemberVariable) between connections
	/// </summary>
	public class PathVisualizationNode : VisualizationNodeBase
	{
		private PathNode _pathNode;
		private readonly HashSet<PathNode> _targetNodes = new HashSet<PathNode>();

		public override string GetSortingKey(RelationType relationType, bool sortBySize)
		{
			var sortingKey = GetRelationArray(relationType)[0].VNode.GetSortingKey(relationType, sortBySize);
			return sortingKey;
		}

		public override EnclosedBounds GetBoundsOwn(NodeDisplayData displayData)
		{
			var count = GetPathNodeCount();
			var width = _pathNode.Width + 16;
			return new EnclosedBounds(0, count * -8 + 6, width, count * 8 + 10);
		}

		public override void Draw(int depth, RelationType relationType, INodeDisplayDataProvider displayDataProvider,
			ISelectionChanger selectionChanger,
			NodeDisplayData displayData, ViewAreaData viewAreaData)
		{
			var offset = GetPositionOffset(viewAreaData);
			PathNode.DrawPathNodes(PosX, PosY + offset, _pathNode, displayDataProvider);
			DrawPathNodeConnections(_pathNode, _targetNodes, displayDataProvider, offset);
		}

		public override void CalculateCachedDataInternal()
		{
			_targetNodes.Clear();
			GeneratePathNodeTree();
			_pathNode.CalculatePositionData(0, 0, _targetNodes);
		}

		public override bool HasNoneFilteredChildren(RelationType relationType)
		{
			return GetRelationArray(relationType)[0].VNode.HasNoneFilteredChildren(relationType);
		}

		public override bool IsFiltered(RelationType relationType)
		{
			return GetRelationArray(relationType)[0].VNode.IsFiltered(relationType);
		}

		private void DrawPathNodeConnections(PathNode rootNode, HashSet<PathNode> targetNodes,
			INodeDisplayDataProvider colorProvider, float yOffset)
		{
			foreach (var tn in targetNodes)
			{
				var color = colorProvider.GetConnectionColorForType(tn.DependencyType);
				var offset = -32;
				var endX = PosX + Bounds.Width;
				AssetRelationsViewerWindow.DrawConnection(tn.PosX + tn.Width + PosX - 16, tn.PosY + PosY + yOffset,
					endX + offset, tn.PosY + PosY + yOffset, color);
				AssetRelationsViewerWindow.DrawConnection(endX + offset, tn.PosY + PosY + yOffset, endX,
					rootNode.PosY + PosY + yOffset, color);
			}
		}

		private int GetPathNodeCount()
		{
			var pathCount = 0;

			foreach (var connection in GetRelations(RelationType.DEPENDENCY, true, true))
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
			_pathNode = new PathNode(string.Empty, PathSegmentType.GameObject, "Root");

			foreach (var connection in GetRelations(RelationType.DEPENDENCY, true, true))
			{
				foreach (var data in connection.Datas)
				{
					PathNode.AddPath(_pathNode, data.PathSegments.Reverse().ToArray(), data.Type);
				}
			}

			PathNode.CalculateNodeHeight(_pathNode);
		}
	}
}