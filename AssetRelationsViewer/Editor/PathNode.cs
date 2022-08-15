using System;
using System.Collections.Generic;
using System.Linq;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// UI element for displaying the part of the Hierarchy and Property path
	/// </summary>
	public class PathNode
	{
		public static int NodeHeight = 16;

		public string Name = string.Empty;
		public int TextLength;
		public PathSegmentType Type;
		public List<PathNode> Children = new List<PathNode>();
		public int Height;
		public int ChildrenHeight;
		public string DependencyType;
		public VisualizationNode TargetNode;

		public float PosX;
		public float PosY;

		public int Width;

		public PathNode(string name, PathSegmentType type, string dependencyType)
		{
			Name = name;
			Type = type;
			DependencyType = dependencyType;
			TextLength = GetTextLength(name, GUI.skin.font);
		}

		public static void AddPath(PathNode node, PathSegment[] segments, string type)
		{
			PathNode currentNode = node;
			
			foreach (PathSegment segment in segments)
			{
				string name = GetPathSegmentName(segment);

				if (!currentNode.Children.Any(n => n.Name == name))
				{
					currentNode.Children.Add(new PathNode(name, segment.Type, type));
				}

				currentNode = currentNode.Children.Find(n => n.Name == name && n.TargetNode == null);
			}
		}

		public static string GetPathSegmentName(PathSegment segment)
		{
			string name = segment.Name.Replace(".Array.data", "");

			if (segment.Type == PathSegmentType.Property)
			{
				return ObjectNames.NicifyVariableName(name);
			}
			
			return name;
		}

		public static void DrawPathNodes(float px, float py, PathNode node,
			INodeDisplayDataProvider colorProvider)
		{
			DrawPathNodeRec(px, py, node, colorProvider);
		}

		public void CalculatePositionData(int px, int py, HashSet<PathNode> targetNodes)
		{
			int totalHeight = ChildrenHeight;
			int currentHeight = 0;

			PosX = px;
			PosY = py;

			int width = TextLength + 16;
			int maxChildWidth = 0;

			if (Children.Count == 0)
			{
				targetNodes.Add(this);
			}

			foreach (PathNode nodeChild in Children)
			{
				int childHeight = nodeChild.Height;
				int offset = currentHeight - (int)((totalHeight - childHeight) * 0.5);

				int eX = px + width;
				int eY = py + offset;

				nodeChild.CalculatePositionData(eX, eY, targetNodes);
				maxChildWidth = maxChildWidth > nodeChild.Width ? maxChildWidth : nodeChild.Width;

				currentHeight += nodeChild.Height;
			}

			Width = width + maxChildWidth;
		}

		public static void DrawPathNodeRec(float px, float py, PathNode node, INodeDisplayDataProvider colorProvider)
		{
			DrawPathSegment(node.PosX + px, node.PosY + py, node);

			foreach (PathNode child in node.Children)
			{
				DrawPathNodeRec(px, py, child, colorProvider);
				AssetRelationsViewerWindow.DrawConnection(node.PosX + px + node.TextLength, node.PosY + py, child.PosX + px, child.PosY + py, colorProvider.GetDependencyType(child.DependencyType).Colour);
			}
		}

		public static int CalculateNodeHeight(PathNode node)
		{
			int height = 0;

			foreach (PathNode nodeChild in node.Children)
			{
				height += CalculateNodeHeight(nodeChild);
			}

			node.ChildrenHeight = height;
			node.Height = Math.Max(NodeHeight, height);
			return node.Height;
		}

		public static void DrawPathSegment(float px, float py, PathNode node)
		{
			Color color = Color.black;

			switch (node.Type)
			{
				case PathSegmentType.GameObject: color = new Color(0.1f, 0.1f, 0.2f, 0.7f); break;
				case PathSegmentType.Component: color = new Color(0.1f, 0.4f, 0.2f, 0.7f); break;
				case PathSegmentType.Property: color = new Color(0.3f, 0.2f, 0.2f, 0.7f); break;
				case PathSegmentType.Unknown: color = new Color(0.3f, 0.3f, 0.3f, 0.7f); break;
			}

			EditorGUI.DrawRect(new Rect(px, py, node.TextLength, NodeHeight - 2), color);
			EditorGUI.LabelField(new Rect(px, py, node.TextLength + 6, NodeHeight), node.Name);
		}

		public static int GetTextLength(string text, Font font)
		{
			int length = 0;
			CharacterInfo info;

			foreach (char c in text)
			{
				font.GetCharacterInfo(c, out info);
				length += info.advance;
			}

			return length;
		}
	}
}
