using System;
using System.Collections.Generic;
using System.Linq;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// During cache update this class stores the current state of the <see cref="IDependencyResolver"/>
	/// </summary>
	public class CreatedResolver
	{
		public CreatedResolver(IDependencyResolver resolver)
		{
			Resolver = resolver;
		}

		public bool IsActive;
		public List<string> DependencyTypes = new List<string>();
		public readonly IDependencyResolver Resolver;
	}
}