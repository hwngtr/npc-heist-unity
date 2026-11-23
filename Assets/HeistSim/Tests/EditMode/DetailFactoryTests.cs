using System.Collections.Generic;
using HeistSim.Market.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Checks that the composed market detail is complete and renderable for every data-driven kind.</summary>
    public sealed class DetailFactoryTests
    {
        private readonly List<GameObject> _built = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _built)
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            _built.Clear();
        }

        [Test]
        public void EveryFixtureKindHasThreeOrMoreParts()
        {
            foreach (var kind in MarketData.Kinds.Keys)
            {
                var root = Track(DetailFactory.BuildFixture(kind));
                Assert.That(ChildCount(root), Is.GreaterThanOrEqualTo(3), kind + " part count");
                AssertRenderable(root, kind);
            }
        }

        [TestCase("gondola-shelf")]
        [TestCase("shopping-cart")]
        [TestCase("checkout-lane")]
        [TestCase("freezer-chest")]
        public void NamedFixtureKindsHaveSixOrMoreParts(string kind)
        {
            var root = Track(DetailFactory.BuildFixture(kind));
            Assert.That(ChildCount(root), Is.GreaterThanOrEqualTo(6), kind + " part count");
            AssertRenderable(root, kind);
        }

        [TestCase("player")]
        [TestCase("guard")]
        [TestCase("staff")]
        public void ActorRolesHaveFourOrMoreParts(string role)
        {
            var root = Track(DetailFactory.BuildActor(role));
            Assert.That(ChildCount(root), Is.GreaterThanOrEqualTo(4), role + " part count");
            AssertRenderable(root, role);
        }

        [Test]
        public void LootAndCameraPartsAreRenderable()
        {
            foreach (var loot in MarketData.Loot)
            {
                var root = Track(DetailFactory.BuildLoot(loot.Kind));
                Assert.That(ChildCount(root), Is.GreaterThanOrEqualTo(1), loot.Id + " part count");
                AssertRenderable(root, loot.Id);
            }

            var camera = Track(DetailFactory.BuildCamera());
            Assert.That(ChildCount(camera), Is.GreaterThanOrEqualTo(3), "camera part count");
            AssertRenderable(camera, "camera");
        }

        private GameObject Track(GameObject root)
        {
            _built.Add(root);
            return root;
        }

        private static int ChildCount(GameObject root)
        {
            var count = 0;
            foreach (Transform child in root.transform)
            {
                count++;
            }

            return count;
        }

        private static void AssertRenderable(GameObject root, string label)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters.Length, Is.EqualTo(ChildCount(root)), label + " mesh filter count");
            foreach (var filter in filters)
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, label + " missing mesh on " + filter.name);
                var renderer = filter.GetComponent<MeshRenderer>();
                Assert.That(renderer, Is.Not.Null, label + " missing renderer on " + filter.name);
                Assert.That(renderer.sharedMaterial, Is.Not.Null, label + " missing material on " + filter.name);
                Assert.That(renderer.sharedMaterial.shader, Is.Not.Null, label + " null shader on " + filter.name);
            }
        }
    }
}
