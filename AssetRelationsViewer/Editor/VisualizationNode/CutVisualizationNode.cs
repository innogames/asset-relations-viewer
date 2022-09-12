using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class CutVisualizationNode : VisualizationNodeBase
    {
        private string countString;
        private int textLength;
        private string cutReasonString;
        
        public CutVisualizationNode(int c, AssetRelationsViewerWindow.CutReason reason)
        {
            countString = c.ToString();
            textLength = GetTextLength(countString, GUI.skin.font);
            cutReasonString = GetCutReasonString(reason);
        }

        private string GetCutReasonString(AssetRelationsViewerWindow.CutReason reason)
        {
            cutReasonString = "";

            if ((reason & AssetRelationsViewerWindow.CutReason.NodeDepth) != 0)
            {
                cutReasonString += "Max Node Depth Reached \n";
            }
            
            if ((reason & AssetRelationsViewerWindow.CutReason.HierarchyShown) != 0)
            {
                cutReasonString += "Node hierarchy already shown \n";
            }
            
            if ((reason & AssetRelationsViewerWindow.CutReason.NodeShown) != 0)
            {
                cutReasonString += "Node already shown \n";
            }
            
            if ((reason & AssetRelationsViewerWindow.CutReason.NodeLimitReached) != 0)
            {
                cutReasonString += "Tree too big. Total node limit reached \n";
            }

            return cutReasonString;
        }

        public override string GetSortingKey(RelationType relationType)
        {
            return relationType.ToString();
        }

        public override EnclosedBounds GetBoundsOwn(NodeDisplayData displayData)
        {
            int width = 16;
            return new EnclosedBounds(0, 1 * -8 + 6, width, 1 * 8 + 10);
        }

        public override bool IsAlignable => false;
        public override int NodeDistanceWidth => 16;

        public override void Draw(int depth, RelationType relationType, INodeDisplayDataProvider displayDataProvider,
            ISelectionChanger selectionChanger, NodeDisplayData displayData, ViewAreaData viewAreaData)
        {
            string typeId = GetRelations(NodeDependencyLookupUtility.InvertRelationType(relationType))[0].Datas[0].Type;

            DependencyType dependencyType = displayDataProvider.GetDependencyType(typeId);
            Color textColor = dependencyType.Colour;
            
            GUIStyle style = new GUIStyle();
            style.normal.textColor = textColor;
            style.clipping = TextClipping.Overflow;
            style.alignment = TextAnchor.LowerRight;
            
            Vector2 position = GetPosition(viewAreaData);
            string typeName = dependencyType.Name;
            string toolTip = $"{typeName} hidden due to: \n{cutReasonString}";
            EditorGUI.DrawRect(new Rect(position.x, position.y, 16, 16), new Color(0.5f, 0.5f, 0.5f, 0.1f));
            EditorGUI.LabelField(new Rect(position.x, position.y, 16, 16), new GUIContent(countString, toolTip), style);

            int offset = relationType == RelationType.DEPENDENCY ? -8 : 8 + 16;
            EditorGUI.LabelField(new Rect(position.x + offset, position.y, 2, 16), "/", style);
        }
        
        public static int GetTextLength(string text, Font font)
        {
            int length = 0;
            CharacterInfo info;

            foreach (char c in text)
            {
                font.GetCharacterInfo(c, out info);
                length += info.advance;
            }

            return length;
        }
    }
}