using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class GenericDependencyMappingNode : IDependencyMappingNode
	{
		public string NodeId { get; private set; }
		public string NodeType = string.Empty;
		private string KeyValue;

		public List<Dependency> Dependencies = new List<Dependency>();

		public string Id => NodeId;
		public string Type => NodeType;
		public string Key => KeyValue;

		public GenericDependencyMappingNode(string id, string type)
		{
			NodeId = id;
			NodeType = type;
			KeyValue = NodeDependencyLookupUtility.GetNodeKey(id, type);
		}
	}
}