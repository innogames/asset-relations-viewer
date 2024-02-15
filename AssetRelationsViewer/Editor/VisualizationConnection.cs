using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// Connection between two <see cref="VisualizationNodeBase"/>s.
	/// </summary>
	public class VisualizationConnection
	{
		public class Data
		{
			public Data(string type, PathSegment[] pathSegments, bool hardRef)
			{
				Type = type;
				PathSegments = pathSegments;
				IsHardRef = hardRef;
			}

			public readonly string Type;
			public PathSegment[] PathSegments;
			public bool IsHardRef;
		}

		public VisualizationConnection(List<Data> datas, VisualizationNodeBase node, bool isRecursion)
		{
			Datas = datas;
			VNode = node;
			IsRecursion = isRecursion;
		}

		public static bool HasPathSegments(List<Data> datas)
		{
			foreach (var data in datas)
			{
				if (data.PathSegments.Length > 0)
					return true;
			}

			return false;
		}

		public readonly VisualizationNodeBase VNode;
		public readonly List<Data> Datas;
		public readonly bool IsRecursion;
	}
}