using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * Helper class to calculate the current progress of the dependency search
	 */
	public class ProgressBase
	{
		private ProgressBase m_parent;
		private Action<string, string, float> m_progressFunc = null;
		private List<ProgressBase> m_children = new List<ProgressBase>();

		public ProgressBase(ProgressBase parent = null)
		{
			m_parent = parent;

			if (parent != null)
			{
				parent.m_children.Add(this);
			}
		}

		/**
		 * Sets the action that is executed when the progress.
		 * This for example could be the display of the progress bar
		 */
		public void SetProgressFunction(Action<string, string, float> progressFunc)
		{
			m_progressFunc = progressFunc;
		}

		public void UpdateProgress(string headline, string message)
		{
			if (m_progressFunc != null)
			{
				m_progressFunc(headline, message, GetProgress());
			}

			if (m_parent != null)
			{
				m_parent.UpdateProgress(headline, message);
			}
		}

		/**
		 * Returns the current progress normalized between 0 and 1
		 */
		protected virtual float GetProgress()
		{
			float totalComplexity = 0;
			float totalprogress = 0;

			foreach (ProgressBase child in m_children)
			{
				float complexity = child.GetComplexity();
				float progress = child.GetProgress();

				totalComplexity += complexity;
				totalprogress += complexity * progress;
			}
			
			return totalprogress / totalComplexity;
		}

		/**
		 * Different resolvers can have a different complexity to execute per asset.
		 * For example it takes a lot longer to execute the Asset Resolver with paths that crawls though the SerializedProperties compared to the Resolver that uses the AssetDatabase.GetDependencies() function
		 */
		protected virtual float GetComplexity()
		{
			float totalComplexity = 0;

			foreach (ProgressBase child in m_children)
			{
				totalComplexity += child.GetComplexity();
			}
			
			return totalComplexity;
		}
	}
}