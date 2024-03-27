using System;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Type of the PathSegment
	/// For now I only indicated that GameObject, Component and Property are necessary.
	/// </summary>
	[Serializable]
	public enum PathSegmentType
	{
		GameObject = 0,
		Component = 1,
		Property = 2,
		Unknown = 3
	}

	/// <summary>
	/// A PathSegment is a part of the Hierarchy/Property path
	/// It just has the name and which type it is
	/// </summary>
	[Serializable]
	public class PathSegment
	{
		public PathSegment(string name, PathSegmentType type)
		{
			Name = name;
			Type = type;
		}

		public string Name;
		public PathSegmentType Type;
	}
}