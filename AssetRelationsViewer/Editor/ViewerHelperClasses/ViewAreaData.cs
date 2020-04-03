using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class ViewAreaData
    {
        public Rect ViewArea;
        public Vector2 ScrollPosition;
        public EnclosedBounds Bounds = new EnclosedBounds();
			
        public void Update(Rect _windowData)
        {
            ViewArea.x = (int)ScrollPosition.x + Bounds.MinX;
            ViewArea.y = (int)ScrollPosition.y + Bounds.MinY;

            ViewArea.width = _windowData.width;
            ViewArea.height = _windowData.height;
        }
			
        public void UpdateAreaSize(VisualizationNode node, Rect _windowData)
        {
            EnclosedBounds oldArea = new EnclosedBounds();
            Bounds.CopyTo(oldArea);
            EnclosedBounds bounds = node.TreeBounds;

            int edge = 1000;

            Bounds.Set(bounds.MinX - edge, bounds.MinY - edge, bounds.MaxX + edge, bounds.MaxY + edge);
            Bounds.Enclose(new EnclosedBounds(-(int)_windowData.width / 2, -(int)_windowData.height / 2, (int)_windowData.width / 2, (int)_windowData.height / 2));

            if (!oldArea.IsInvalid)
            {
                ScrollPosition.x += (Bounds.Width - oldArea.Width) / 2;
                ScrollPosition.y += (Bounds.Height - oldArea.Height) / 2;
            }
        }
			
        /// <summary>
        /// Function to check if a node is actually in view
        /// </summary>
        public bool IsRectInDrawArea(Rect rect, Color color)
        {
            //EditorGUI.DrawRect(rect, color);
            return ViewArea.Overlaps(rect);
        }
    }
}