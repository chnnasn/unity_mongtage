using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChnnAsn.Montage.Editor
{
    internal static class MontageValidationMenu
    {
        [MenuItem("Tools/Unity Montage/Validate Project")]
        private static void ValidateProject()
        {
            var guids = AssetDatabase.FindAssets("t:MontageAsset");
            var errors = new List<string>();
            var invalidAssets = 0;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<MontageAsset>(path);
                errors.Clear();
                asset.Validate(errors);
                if (errors.Count == 0)
                    continue;
                invalidAssets++;
                for (var j = 0; j < errors.Count; j++)
                    Debug.LogError($"{path}: {errors[j]}", asset);
            }

            if (invalidAssets == 0)
                Debug.Log($"Unity Montage: validated {guids.Length} asset(s); no errors found.");
            else
                Debug.LogError($"Unity Montage: {invalidAssets} of {guids.Length} asset(s) contain errors.");
        }
    }
}
