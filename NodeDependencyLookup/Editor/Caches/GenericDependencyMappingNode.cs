using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class GenericDependencyMappingNode : IResolvedNode
	{
		public string NodeId;
		public List<Dependency> Dependencies = new List<Dependency>();
		public bool IsExisting = true;
		public string TypeName = String.Empty;
		public string Id => NodeId;
		public string Type => TypeName;
		public bool Existing => IsExisting;
	}
}
