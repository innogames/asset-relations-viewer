using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Node that has both dependencies and referencers
	/// This is the node for the structure that will be used for the relation lookup
	/// </summary>
	public class Node
	{
		public struct NodeSize
		{
			public int Size;
			public bool ContributesToTreeSize;
		}
		
		public readonly List<Connection> Dependencies = new List<Connection>();
		public readonly List<Connection> Referencers = new List<Connection>();

		public readonly string Id;
		public readonly string Type;
		public readonly string Key;

		public string Name = String.Empty;
		public string ConcreteType = String.Empty;

		public NodeSize OwnSize = new NodeSize{Size = -1};

		public Node(string id, string type)
		{
			Id = id;
			Type = type;
			Key = NodeDependencyLookupUtility.GetNodeKey(id, type);
		}

		public List<Connection> GetRelations(RelationType type)
		{
			switch (type)
			{
				case RelationType.DEPENDENCY: return Dependencies;
				case RelationType.REFERENCER: return Referencers;
			}

			return null;
		}

		public void ResetRelationInformation()
		{
			Dependencies.Clear();
			Referencers.Clear();
		}
	}
}