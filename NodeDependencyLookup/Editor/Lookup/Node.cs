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
		/// <summary>
		/// Size related information of the node
		/// </summary>
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
		public readonly string Name;
		public readonly string ConcreteType;

		public NodeSize OwnSize = new NodeSize {Size = -1};

		internal bool CompressedSizeCalculationStarted;

		public Node(string id, string type, string name, string concreteType)
		{
			Id = id;
			Type = type;
			Name = name;
			ConcreteType = concreteType;
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