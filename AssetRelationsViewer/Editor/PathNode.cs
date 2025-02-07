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

		private readonly string _name;
		private readonly int _textLength;
		private readonly PathSegmentType _type;
		private readonly List<PathNode> _children = new List<PathNode>();
		private readonly NodeVisualizationNode _targetNode;
		private int _height;
		private int _childrenHeight;
		public readonly string DependencyType;

		public float PosX;
		public float PosY;
		public int Width;

		public PathNode(string name, PathSegmentType type, string dependencyType)
		{
			_name = name;
			_type = type;

			DependencyType = dependencyType;
			_textLength = GetTextLength(name, GUI.skin.font);
		}

		public static void AddPath(PathNode node, PathSegment[] segments, string type)
		{
			var currentNode = node;

			foreach (var segment in segments)
			{
				var name = GetPathSegmentName(segment);

				if (currentNode._children.All(n => n._name != name))
				{
					currentNode._children.Add(new PathNode(name, segment.Type, type));
				}

				currentNode = currentNode._children.Find(n => n._name == name && n._targetNode == null);
			}
		}

		private static string GetPathSegmentName(PathSegment segment)
		{
			var name = segment.Name.Replace(".Array.data", "");

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
			var totalHeight = _childrenHeight;
			var currentHeight = 0;

			PosX = px;
			PosY = py;

			var width = _textLength + 16;
			var maxChildWidth = 0;

			if (_children.Count == 0)
			{
				targetNodes.Add(this);
			}

			foreach (var nodeChild in _children)
			{
				var childHeight = nodeChild._height;
				var offset = currentHeight - (int) ((totalHeight - childHeight) * 0.5);

				var eX = px + width;
				var eY = py + offset;

				nodeChild.CalculatePositionData(eX, eY, targetNodes);
				maxChildWidth = maxChildWidth > nodeChild.Width ? maxChildWidth : nodeChild.Width;

				currentHeight += nodeChild._height;
			}

			Width = width + maxChildWidth;
		}

		private static void DrawPathNodeRec(float px, float py, PathNode node, INodeDisplayDataProvider colorProvider)
		{
			DrawPathSegment(node.PosX + px, node.PosY + py, node);

			foreach (var child in node._children)
			{
				DrawPathNodeRec(px, py, child, colorProvider);
				AssetRelationsViewerWindow.DrawConnection(node.PosX + px + node._textLength, node.PosY + py,
					child.PosX + px, child.PosY + py, colorProvider.GetConnectionColorForType(child.DependencyType));
			}
		}

		public static int CalculateNodeHeight(PathNode node)
		{
			var height = 0;

			foreach (var nodeChild in node._children)
			{
				height += CalculateNodeHeight(nodeChild);
			}

			node._childrenHeight = height;
			node._height = Math.Max(NodeHeight, height);
			return node._height;
		}

		private static void DrawPathSegment(float px, float py, PathNode node)
		{
			var color = Color.black;

			switch (node._type)
			{
				case PathSegmentType.GameObject:
					color = new Color(0.1f, 0.1f, 0.2f, 0.7f);
					break;
				case PathSegmentType.Component:
					color = new Color(0.1f, 0.4f, 0.2f, 0.7f);
					break;
				case PathSegmentType.Property:
					color = new Color(0.3f, 0.2f, 0.2f, 0.7f);
					break;
				case PathSegmentType.Unknown:
					color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
					break;
			}

			EditorGUI.DrawRect(new Rect(px, py, node._textLength, NodeHeight - 2), color);
			EditorGUI.LabelField(new Rect(px, py, node._textLength + 6, NodeHeight), node._name);
		}

		private static int GetTextLength(string text, Font font)
		{
			var length = 0;
			CharacterInfo info; 
			
			font.RequestCharactersInTexture(text);

			foreach (var c in text)
			{
				font.GetCharacterInfo(c, out info);
				length += info.advance;
			}

			return length;
		}
	}
}