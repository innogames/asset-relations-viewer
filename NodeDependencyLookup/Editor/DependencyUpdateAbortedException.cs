using System;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Exception that is thrown if the Dependency Cache update got aborted by the user
	/// </summary>
	public class DependencyUpdateAbortedException : Exception
	{
		public DependencyUpdateAbortedException()
			: base("Dependency search got aborted. Cache didn't get updated!")
		{
			EditorUtility.ClearProgressBar();
		}
	}
}