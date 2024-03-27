using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// MappingNode to map the dependency of one node to another
	/// </summary>
	public class GenericDependencyMappingNode : IDependencyMappingNode
	{
		public string NodeId { get; }
		public readonly string NodeType;
		public List<Dependency> Dependencies = new List<Dependency>();

		public string Id => NodeId;
		public string Type => NodeType;
		public string Key { get; }

		public GenericDependencyMappingNode(string id, string type)
		{
			NodeId = id;
			NodeType = type;
			Key = NodeDependencyLookupUtility.GetNodeKey(id, type);
		}
	}
}