namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class ResolverProgress : ProgressBase
	{
		private int m_count;
		private int m_amount;
		private float m_complexity = 0;
		
		public ResolverProgress(ProgressBase parent, int amount, float complexityMultiplicator) : base(parent)
		{
			m_complexity = amount * complexityMultiplicator;
			m_amount = amount;
		}

		public void IncreaseProgress()
		{
			m_count++;
		}

		protected override float GetProgress()
		{
			if (m_amount == 0)
				return 1.0f;
			
			return (float)m_count / m_amount;
		}

		protected override float GetComplexity()
		{
			return m_complexity;
		}
	}
}