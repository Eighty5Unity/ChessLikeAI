using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TreeNodeView))]
public class TreeNodeGUI : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        TreeNodeView treeNodeView = (TreeNodeView)target;

        if(GUILayout.Button("Next Moves"))
        {
            treeNodeView.InstantiateTreeNode();
        }

        if(GUILayout.Button("Show Move"))
        {
            treeNodeView.MoveChessmanView();
        }

        if (GUILayout.Button("Move"))
        {
            treeNodeView.Move();
        }
    }
}
