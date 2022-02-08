using System.Collections.Generic;
using System.Threading;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	public class NodeSizeThread
	{
		private readonly Stack<VisualizationNodeData> stack = new Stack<VisualizationNodeData>();
		private readonly AssetRelationsViewerWindow viewerWindow;
		private readonly HashSet<string> traversedNodes = new HashSet<string>();
		private Thread thread;

		public NodeSizeThread(AssetRelationsViewerWindow window)
		{
			viewerWindow = window;
		}

		public void Start() 
		{
			thread = new Thread(ThreadProc);
			thread.Name = "HierarchySizeThread";
			thread.Start();
		}

		public void Kill()
		{
			thread?.Abort();
			stack.Clear();
		}

		public void EnqueueNodeData(VisualizationNodeData nodeData)
		{
			viewerWindow.CalculateOwnSizeForNode(nodeData);
			stack.Push(nodeData);
		}
		
		private void ThreadProc() {
			while(true)
			{
				while (stack.Count > 0)
				{
					viewerWindow.CalculateTreeSizeForNode(stack.Pop(), traversedNodes);
				}
                    
				Thread.Sleep(25);
			}
		}
	}
}
