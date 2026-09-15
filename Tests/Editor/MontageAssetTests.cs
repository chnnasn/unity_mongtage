using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ChnnAsn.Montage.Tests
{
    public sealed class MontageAssetTests
    {
        [Test]
        public void EmptyAssetReportsValidationErrors()
        {
            var asset = ScriptableObject.CreateInstance<MontageAsset>();
            var errors = new List<string>();

            asset.Validate(errors);

            Assert.That(errors, Is.Not.Empty);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void DefaultOptionsUseExpectedSentinels()
        {
            var options = MontagePlayOptions.Default;

            Assert.That(options.PlayRate, Is.EqualTo(1f));
            Assert.That(options.BlendIn, Is.EqualTo(-1f));
        }
    }
}
