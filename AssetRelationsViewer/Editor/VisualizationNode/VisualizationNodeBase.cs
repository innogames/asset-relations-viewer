using System;
using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// The reason why a node is not shown (cut) in the treeview
	/// </summary>
	public enum CutReason
	{
		DepthReached,
		NodeLimitReached,
		HierarchyAlreadyShown,
		NodeAlreadyShown,
		FilteredOut
	}

	/// <summary>
	/// Indicates why a node connection is not shown (cut) in the tree view
	/// This can contain multiple reasons
	/// </summary>
	public class CutData
	{
		public class Entry
		{
			public int Count;
			public CutReason CutReason;
		}

		public readonly List<Entry> Entries = new List<Entry>();
	}

	/// <summary>
	/// A VisualizationNode is a node to display the <see cref="Node"/> structure of the NodeDependencyCache inside the TreeView
	/// It contains additional information compared the just the <see cref="Node"/>
	/// </summary>
	public abstract class VisualizationNodeBase
	{
		private List<VisualizationConnection> _dependencies = new List<VisualizationConnection>();
		private List<VisualizationConnection> _referencers = new List<VisualizationConnection>();
		private CutData[] _cutDatas = new CutData[2];

		protected int PosX = int.MaxValue;
		protected int PosY = int.MaxValue;
		public int ExtendedNodeWidth;

		public EnclosedBounds Bounds = new EnclosedBounds();
		public EnclosedBounds TreeBounds = new EnclosedBounds();

		public abstract string GetSortingKey(RelationType relationType, bool sortBySize);

		public abstract EnclosedBounds GetBoundsOwn(NodeDisplayData displayData);

		public abstract bool HasNoneFilteredChildren(RelationType relationType);

		public abstract bool IsFiltered(RelationType relationType);

		public abstract void Draw(int depth, RelationType relationType, INodeDisplayDataProvider displayDataProvider,
			ISelectionChanger selectionChanger, NodeDisplayData displayData, ViewAreaData viewAreaData);

		public virtual void CalculateCachedDataInternal()
		{
		}

		public Vector2 GetPosition(ViewAreaData viewAreaData)
		{
			return GetPositionInternal(PosX, PosY, viewAreaData.ViewArea, Bounds, TreeBounds);
		}

		public float GetPositionOffset(ViewAreaData viewAreaData)
		{
			return GetPositionOffsetInternal(PosY, viewAreaData.ViewArea, Bounds, TreeBounds);
		}

		private static Vector2 GetPositionInternal(float posX, float posY, Rect viewArea, EnclosedBounds bounds,
			EnclosedBounds treeBounds)
		{
			var positionOffset = GetPositionOffsetInternal(posY, viewArea, bounds, treeBounds);

			return new Vector2(posX, posY + positionOffset);
		}

		private static float GetPositionOffsetInternal(float posY, Rect viewArea, EnclosedBounds bounds,
			EnclosedBounds treeBounds)
		{
			float overallOffset = 270; // this is just a "random" number which I had to apply, I dont know why this offset exists

			var effect = Mathf.Clamp01(treeBounds.Height / viewArea.height);

			var lowerDist = -viewArea.yMin + treeBounds.MaxY - overallOffset;
			var upperDist = viewArea.yMax - treeBounds.MinY - overallOffset;

			var lowerInterp = Mathf.Clamp01(upperDist / treeBounds.Height);
			var upperInterp = Mathf.Clamp01(lowerDist / treeBounds.Height);

			var totalInterp = lowerInterp + upperInterp;

			lowerInterp /= totalInterp;
			upperInterp /= totalInterp;

			var lower = treeBounds.MaxY - bounds.MaxY + posY;
			var upper = treeBounds.MinY - bounds.MinY + posY;

			var newY = lower * lowerInterp + upper * upperInterp;
			newY = newY * effect + posY * (1.0f - effect);

			return newY - posY;
		}

		public void InvalidatePositionData()
		{
			PosX = int.MaxValue;
			PosY = int.MaxValue;
			Bounds = new EnclosedBounds();
			TreeBounds = new EnclosedBounds();
		}

		public void CalculateBounds(NodeDisplayData displayData, RelationType connectionType)
		{
			CalculateCachedDataInternal();

			if (Bounds.IsInvalid)
			{
				Bounds = GetBoundsOwn(displayData);
				TreeBounds.Enclose(Bounds);
			}

			ExtendedNodeWidth = Bounds.Width;

			foreach (var childConnection in GetRelations(connectionType))
			{
				childConnection.VNode.CalculateBounds(displayData, connectionType);
			}
		}

		public void CalculateXData(float pX, RelationType connectionType, NodeDisplayData displayData)
		{
			PosX = (int) pX;
			PosY = 0;
			Bounds.Shift((int) pX, 0);
			TreeBounds.Shift((int) pX, 0);

			var offsetX = connectionType == RelationType.DEPENDENCY
				? ExtendedNodeWidth + displayData.NodeSpaceX
				: -displayData.NodeSpaceX;

			foreach (var childConnection in GetRelations(connectionType))
			{
				var cOffset = connectionType == RelationType.REFERENCER ? -childConnection.VNode.ExtendedNodeWidth : 0;
				childConnection.VNode.CalculateXData(pX + offsetX + cOffset, connectionType, displayData);
			}
		}

		public void CalculateYData(RelationType connectionType)
		{
			var connections = GetRelations(connectionType);
			var offsets = new int[connections.Count];
			var totalHeight = 0;

			foreach (var childConnection in connections)
			{
				childConnection.VNode.CalculateYData(connectionType);
			}

			for (var i = 0; i < connections.Count - 1; i++)
			{
				var w1 = connections[i].VNode.TreeBounds.MaxY;
				var w2 = connections[i + 1].VNode.TreeBounds.MinY;
				var height = w1 - w2;
				offsets[i + 1] = height + totalHeight;
				totalHeight += height;
			}

			for (var i = 0; i < connections.Count; i++)
			{
				var childNode = connections[i].VNode;
				childNode.ShiftY(offsets[i] - totalHeight / 2, connectionType);
				TreeBounds.Enclose(childNode.TreeBounds);
			}
		}

		private void ShiftY(int y, RelationType connectionType)
		{
			PosY += y;
			Bounds.Shift(0, y);
			TreeBounds.Shift(0, y);

			foreach (var childConnection in GetRelations(connectionType))
			{
				childConnection.VNode.ShiftY(y, connectionType);
			}
		}

		public void AddRelation(RelationType relationType, VisualizationConnection connection)
		{
			GetRelationArray(relationType).Add(connection);
		}

		protected List<VisualizationConnection> GetRelationArray(RelationType relationType)
		{
			switch (relationType)
			{
				case RelationType.DEPENDENCY: return _dependencies;
				case RelationType.REFERENCER: return _referencers;
			}

			return null;
		}

		public CutData GetCutData(RelationType relationType, bool createIfNotExisting)
		{
			var type = (int) relationType;
			var cutData = _cutDatas[type];

			if (createIfNotExisting && cutData == null)
			{
				_cutDatas[type] = new CutData();
			}

			return _cutDatas[type];
		}

		public List<VisualizationConnection> GetRelations(RelationType type, bool nonRecursive = true,
			bool recursive = false)
		{
			var result = new List<VisualizationConnection>();

			foreach (var connection in GetRelationArray(type))
			{
				if ((connection.IsRecursion && recursive) || (!connection.IsRecursion && nonRecursive))
				{
					result.Add(connection);
				}
			}

			return result;
		}

		public void SetRelations(List<VisualizationConnection> nodes, RelationType type)
		{
			switch (type)
			{
				case RelationType.DEPENDENCY:
					_dependencies = nodes;
					break;
				case RelationType.REFERENCER:
					_referencers = nodes;
					break;
			}
		}
	}
}