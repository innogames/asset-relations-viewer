using System.Collections.Generic;
using System.Threading;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// Thread to calculate the hierarchy size of nodes in the background
	/// </summary>
	public class NodeSizeThread
	{
		private readonly Stack<VisualizationNodeData> _stack = new Stack<VisualizationNodeData>();
		private readonly HashSet<string> _traversedNodes = new HashSet<string>();
		private Thread _thread;
		private NodeDependencyLookupContext _context;

		public NodeSizeThread(NodeDependencyLookupContext context)
		{
			_context = context;
		}

		public void Start()
		{
			_thread = new Thread(ThreadProc);
			_thread.Name = "HierarchySizeThread";
			_thread.Start();
		}

		public void Kill()
		{
			_thread?.Abort();
			_stack.Clear();
		}

		public void EnqueueNodeData(VisualizationNodeData nodeData)
		{
			_stack.Push(nodeData);
		}

		private void ThreadProc()
		{
			HashSet<Node> flattedHierarchy = new HashSet<Node>();

			while(true)
			{
				while (_stack.Count > 0)
				{
					VisualizationNodeData visualizationNodeData = _stack.Pop();
					if (visualizationNodeData != null)
					{
						visualizationNodeData.HierarchySize = NodeDependencyLookupUtility.GetTreeSize(visualizationNodeData.Node, _context, flattedHierarchy);
					}
				}

				Thread.Sleep(5);
			}
		}
	}
}
