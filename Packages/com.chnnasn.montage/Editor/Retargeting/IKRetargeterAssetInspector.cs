using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChnnAsn.Montage.Editor
{
    [CustomEditor(typeof(IKRetargeterAsset))]
    internal sealed class IKRetargeterAssetInspector : UnityEditor.Editor
    {
        private readonly List<string> validationErrors = new List<string>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var asset = (IKRetargeterAsset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Humanoid Chains"))
                {
                    Undo.RecordObject(asset, "Reset Humanoid Retarget Chains");
                    asset.ResetToHumanoidDefaults();
                    EditorUtility.SetDirty(asset);
                }

                if (GUILayout.Button("Open Retargeter"))
                    IKRetargeterWindow.Open(asset);
            }

            validationErrors.Clear();
            asset.Validate(validationErrors);
            if (validationErrors.Count == 0)
                EditorGUILayout.HelpBox("Retargeter configuration is valid.", MessageType.Info);
            else
                for (var i = 0; i < validationErrors.Count; i++)
                    EditorGUILayout.HelpBox(validationErrors[i], MessageType.Warning);
        }
    }
}
