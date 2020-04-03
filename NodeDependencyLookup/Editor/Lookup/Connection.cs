
namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// A relation can be either of a node is a dependency of another one or if the node is references by another node
	/// </summary>
	public enum RelationType
	{
		DEPENDENCY,
		REFERENCER
	}

	/// <summary>
	/// The connection between two nodes in the resolved dependency tree structure
	/// </summary>
	public class Connection
	{
		public readonly Node Node;
		public readonly string Type;
		public readonly PathSegment[] PathSegments;

		public Connection(Node node, string connectionType, PathSegment[] pathSegments)
		{
			Node = node;
			Type = connectionType;
			PathSegments = pathSegments;
		}
	}
}