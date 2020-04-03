using System;
using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public abstract class VisualizationNodeBase
	{
		public List<VisualizationConnection> Dependencies = new List<VisualizationConnection>();
		public List<VisualizationConnection> Referencers = new List<VisualizationConnection>();
		
		protected int PosX = Int32.MaxValue;
		protected int PosY = Int32.MaxValue;
		public int ExtendedNodeWidth; // extended with
		
		public EnclosedBounds Bounds = new EnclosedBounds();
		public EnclosedBounds TreeBounds = new EnclosedBounds();
		public bool IsFiltered;
		public bool HasNoneFilteredChildren;

		public abstract string GetSortingKey(RelationType relationType);
		
		public abstract EnclosedBounds GetBoundsOwn(NodeDisplayData displayData);
		
		public abstract void Draw(int depth, RelationType relationType, ITypeColorProvider colorProvider,
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
		
		public static Vector2 GetPositionInternal(float posX, float posY, Rect viewArea, EnclosedBounds bounds, EnclosedBounds treeBounds)
		{
			float positionOffset = GetPositionOffsetInternal(posY, viewArea, bounds, treeBounds);

			return new Vector2(posX, posY + positionOffset);
		}

		public static float GetPositionOffsetInternal(float posY, Rect viewArea, EnclosedBounds bounds, EnclosedBounds treeBounds)
		{
			float overallOffset = 270; // this is just a "random" number which I had to apply, I dont know why this offset exists 

			float effect = Mathf.Clamp01(treeBounds.Height / viewArea.height);
			
			float lowerDist = -viewArea.yMin + treeBounds.MaxY - overallOffset;
			float upperDist = viewArea.yMax - treeBounds.MinY - overallOffset;
			
			float lowerInterp = Mathf.Clamp01(upperDist / treeBounds.Height);
			float upperInterp = Mathf.Clamp01(lowerDist / treeBounds.Height);

			float totalInterp = lowerInterp + upperInterp;

			lowerInterp /= totalInterp;
			upperInterp /= totalInterp;

			float lower = treeBounds.MaxY - bounds.MaxY + posY;
			float upper = treeBounds.MinY - bounds.MinY + posY;

			float newY = lower * lowerInterp + upper * upperInterp;
			newY = newY * effect + posY * (1.0f - effect);

			return newY - posY;
		}

		public void InvalidatePositionData()
		{
			PosX = Int32.MaxValue;
			PosY = Int32.MaxValue;
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

			foreach (VisualizationConnection childConnection in GetRelations(connectionType))
			{
				childConnection.VNode.CalculateBounds(displayData, connectionType);
			}
		}
		
		public void CalculateXData(float pX, RelationType connectionType, NodeDisplayData displayData)
		{
			PosX = (int)pX;
			PosY = 0;
			Bounds.Shift((int)pX, 0);
			TreeBounds.Shift((int)pX, 0);
			
			int offsetX = connectionType == RelationType.DEPENDENCY ? ExtendedNodeWidth + displayData.NodeSpaceX: -displayData.NodeSpaceX;
			
			foreach (VisualizationConnection childConnection in GetRelations(connectionType))
			{
				int cOffset = connectionType == RelationType.REFERENCER ? -childConnection.VNode.ExtendedNodeWidth : 0;
				childConnection.VNode.CalculateXData(pX + offsetX + cOffset, connectionType, displayData);
			}
		}

		public void CalculateYData(RelationType connectionType)
		{
			List<VisualizationConnection> connections = GetRelations(connectionType);
			int[] offsets = new int[connections.Count];
			int totalHeight = 0;
			
			foreach (VisualizationConnection childConnection in connections)
			{
				childConnection.VNode.CalculateYData(connectionType);
			}
			
			for (var i = 0; i < connections.Count - 1; i++)
			{
				int w1 = connections[i].VNode.TreeBounds.MaxY;
				int w2 = connections[i + 1].VNode.TreeBounds.MinY;
				int height = w1 - w2;
				offsets[i + 1] = height + totalHeight;
				totalHeight += height;
			}
			
			for (var i = 0; i < connections.Count; i++)
			{
				VisualizationNodeBase childNode = connections[i].VNode;
				childNode.ShiftY(offsets[i] - totalHeight / 2, connectionType);
				TreeBounds.Enclose(childNode.TreeBounds);
			}
		}

		private void ShiftY(int y, RelationType connectionType)
		{
			PosY += y;
			Bounds.Shift(0, y);
			TreeBounds.Shift(0, y);
			
			foreach (VisualizationConnection childConnection in GetRelations(connectionType))
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
				case RelationType.DEPENDENCY: return Dependencies;
				case RelationType.REFERENCER: return Referencers;
			}

			return null;
		}

		public List<VisualizationConnection> GetRelations(RelationType type, bool nonRecursive = true, bool recursive = false)
		{
			List<VisualizationConnection> result = new List<VisualizationConnection>();

			foreach (VisualizationConnection connection in GetRelationArray(type))
			{
				if((connection.IsRecursion && recursive) || (!connection.IsRecursion && nonRecursive))
					result.Add(connection);
			}
			
			return result;
		}

		public void SetRelations(List<VisualizationConnection> nodes, RelationType type)
		{
			switch (type)
			{
				case RelationType.DEPENDENCY: Dependencies = nodes; break;
				case RelationType.REFERENCER: Referencers = nodes; break;
			}
		}
	}
}