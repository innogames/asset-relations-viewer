
namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// A dependency from one node to the other
	/// </summary>
	public class Dependency
	{
		public readonly string NodeType;
		public readonly string Id;
		public readonly string Key;
		public readonly string DependencyType;
		public readonly PathSegment[] PathSegments;

		public Dependency(string id, string dependencyType, string nodeType, PathSegment[] pathSegments)
		{
			Id = id;
			DependencyType = dependencyType;
			PathSegments = pathSegments;
			NodeType = nodeType;
			Key = NodeDependencyLookupUtility.GetNodeKey(id, nodeType);
		}
	}
}