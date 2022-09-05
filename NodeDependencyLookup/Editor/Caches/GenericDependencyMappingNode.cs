using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class GenericDependencyMappingNode : IDependencyMappingNode
	{
		public string NodeId;
		public List<Dependency> Dependencies = new List<Dependency>();
		public string NodeType = String.Empty;
		public string Id => NodeId;
		public string Type => NodeType;
	}
}
