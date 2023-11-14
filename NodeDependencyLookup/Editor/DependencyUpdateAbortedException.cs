using System;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class DependencyUpdateAbortedException : Exception
	{
		public DependencyUpdateAbortedException()
			: base("Dependency search got aborted. Cache didn't get updated!")
		{
			EditorUtility.ClearProgressBar();
		}
	}
}