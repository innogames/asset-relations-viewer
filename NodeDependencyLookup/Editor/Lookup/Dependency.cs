
namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// A dependency from one node to the other
	/// </summary>
	public class Dependency
	{
		public string NodeType;
		public string Id;
		public string DependencyType;
		public PathSegment[] PathSegments;

		public Dependency(string id, string dependencyType, string nodeType, PathSegment[] pathSegments)
		{
			Id = id;
			DependencyType = dependencyType;
			PathSegments = pathSegments;
			NodeType = nodeType;
		}
	}
}