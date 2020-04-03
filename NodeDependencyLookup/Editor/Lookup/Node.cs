using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Node that has both dependencies and referencers
	/// This is the node for the structure that will be used for the relation lookup
	/// </summary>
	public class Node
	{
		public readonly List<Connection> Dependencies = new List<Connection>();
		public readonly List<Connection> Referencers = new List<Connection>();

		public string Id;
		public string Type;

		public List<Connection> GetRelations(RelationType type)
		{
			switch (type)
			{
				case RelationType.DEPENDENCY: return Dependencies;
				case RelationType.REFERENCER: return Referencers;
			}

			return null;
		}
	}
}