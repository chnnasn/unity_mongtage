using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChnnAsn.Montage.Editor
{
    [CustomEditor(typeof(MontageAsset))]
    internal sealed class MontageAssetInspector : UnityEditor.Editor
    {
        private readonly List<string> errors = new List<string>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var asset = (MontageAsset)target;
            errors.Clear();
            asset.Validate(errors);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Calculated Duration", $"{asset.Duration:0.###} s");
            if (errors.Count == 0)
                EditorGUILayout.HelpBox("Montage data is valid.", MessageType.Info);
            else
                for (var i = 0; i < errors.Count; i++)
                    EditorGUILayout.HelpBox(errors[i], MessageType.Error);

            if (GUILayout.Button("Open Montage Overview"))
                MontageEditorWindow.Open(asset);
        }
    }
}
