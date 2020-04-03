
namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// A dependency from one node to the other
	/// </summary>
	public class Dependency
	{
		public string NodeType;
		public string Id;
		public string ConnectionType;
		public PathSegment[] PathSegments;

		public Dependency(string id, string connectionType, string nodeType, PathSegment[] pathSegments)
		{
			Id = id;
			ConnectionType = connectionType;
			PathSegments = pathSegments;
			NodeType = nodeType;
		}
	}
}