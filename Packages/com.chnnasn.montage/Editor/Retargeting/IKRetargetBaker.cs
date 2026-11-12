using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChnnAsn.Montage.Editor
{
    internal static class IKRetargetBaker
    {
        private sealed class TransformCurves
        {
            public readonly string Path;
            public readonly AnimationCurve PositionX = new AnimationCurve();
            public readonly AnimationCurve PositionY = new AnimationCurve();
            public readonly AnimationCurve PositionZ = new AnimationCurve();
            public readonly AnimationCurve RotationX = new AnimationCurve();
            public readonly AnimationCurve RotationY = new AnimationCurve();
            public readonly AnimationCurve RotationZ = new AnimationCurve();
            public readonly AnimationCurve RotationW = new AnimationCurve();

            public TransformCurves(string path)
            {
                Path = path;
            }

            public void AddKey(float time, Transform transform)
            {
                var position = transform.localPosition;
                var rotation = transform.localRotation;
                PositionX.AddKey(time, position.x);
                PositionY.AddKey(time, position.y);
                PositionZ.AddKey(time, position.z);
                RotationX.AddKey(time, rotation.x);
                RotationY.AddKey(time, rotation.y);
                RotationZ.AddKey(time, rotation.z);
                RotationW.AddKey(time, rotation.w);
            }

            public void WriteTo(AnimationClip clip)
            {
                SetCurve(clip, Path, "m_LocalPosition.x", PositionX);
                SetCurve(clip, Path, "m_LocalPosition.y", PositionY);
                SetCurve(clip, Path, "m_LocalPosition.z", PositionZ);
                SetCurve(clip, Path, "m_LocalRotation.x", RotationX);
                SetCurve(clip, Path, "m_LocalRotation.y", RotationY);
                SetCurve(clip, Path, "m_LocalRotation.z", RotationZ);
                SetCurve(clip, Path, "m_LocalRotation.w", RotationW);
            }

            private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), property),
                    curve);
            }
        }

        public static AnimationClip BakeToMemory(
            IKRetargeterAsset retargeter,
            GameObject targetCharacter,
            AnimationClip sourceClip,
            float frameRate)
        {
            if (targetCharacter == null)
                throw new ArgumentNullException(nameof(targetCharacter));
            if (sourceClip == null)
                throw new ArgumentNullException(nameof(sourceClip));

            frameRate = Mathf.Clamp(frameRate, 1f, 240f);
            var instance = UnityEngine.Object.Instantiate(targetCharacter);
            instance.name = targetCharacter.name + " (Retarget Bake)";
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;

                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.enabled = true;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var curves = new List<TransformCurves>(transforms.Length);
                for (var i = 0; i < transforms.Length; i++)
                    curves.Add(new TransformCurves(AnimationUtility.CalculateTransformPath(transforms[i], instance.transform)));

                var originalRootPosition = instance.transform.localPosition;
                var duration = Mathf.Max(0f, sourceClip.length);
                var frameCount = Mathf.Max(1, Mathf.CeilToInt(duration * frameRate));
                for (var frame = 0; frame <= frameCount; frame++)
                {
                    var time = frame == frameCount ? duration : frame / frameRate;
                    sourceClip.SampleAnimation(instance, time);
                    if (retargeter != null)
                    {
                        var sampledRoot = instance.transform.localPosition;
                        instance.transform.localPosition = originalRootPosition +
                            (sampledRoot - originalRootPosition) * retargeter.RootTranslationScale;
                        if (animator != null)
                            retargeter.ApplyPoseCorrections(animator);
                    }

                    for (var i = 0; i < transforms.Length; i++)
                        curves[i].AddKey(time, transforms[i]);
                }

                var output = new AnimationClip
                {
                    name = sourceClip.name + "_Retargeted",
                    frameRate = frameRate,
                    legacy = false
                };
                for (var i = 0; i < curves.Count; i++)
                    curves[i].WriteTo(output);
                output.EnsureQuaternionContinuity();
                return output;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        public static AnimationClip BakeToAsset(
            IKRetargeterAsset retargeter,
            GameObject targetCharacter,
            AnimationClip sourceClip,
            float frameRate,
            string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("An output asset path is required.", nameof(outputPath));

            outputPath = AssetDatabase.GenerateUniqueAssetPath(outputPath);
            var output = BakeToMemory(retargeter, targetCharacter, sourceClip, frameRate);
            AssetDatabase.CreateAsset(output, outputPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(outputPath);
            Selection.activeObject = output;
            EditorGUIUtility.PingObject(output);
            return output;
        }
    }
}
