using Frieren.Characters;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    public sealed class ResourcePoolTests
    {
        [Test]
        public void StartsFullByDefault()
        {
            var pool = new ResourcePool(100f);

            Assert.AreEqual(100f, pool.Current);
            Assert.IsTrue(pool.IsFull);
            Assert.IsFalse(pool.IsDepleted);
        }

        [Test]
        public void StartingValueIsClampedToTheMaximum()
        {
            Assert.AreEqual(50f, new ResourcePool(50f, 999f).Current);
            Assert.AreEqual(0f, new ResourcePool(50f, -10f).Current);
        }

        [Test]
        public void NegativeMaximumBecomesZero()
        {
            var pool = new ResourcePool(-20f);

            Assert.AreEqual(0f, pool.Max);
            Assert.AreEqual(0f, pool.Normalized, "A pool with no maximum must not divide by zero.");
        }

        [Test]
        public void RemoveReportsWhatActuallyCameOut()
        {
            var pool = new ResourcePool(100f, 30f);

            Assert.AreEqual(30f, pool.Remove(80f), 0.001f, "Cannot remove more than is there.");
            Assert.AreEqual(0f, pool.Current);
            Assert.IsTrue(pool.IsDepleted);
        }

        [Test]
        public void AddReportsWhatActuallyWentIn()
        {
            var pool = new ResourcePool(100f, 90f);

            Assert.AreEqual(10f, pool.Add(50f), 0.001f, "Cannot exceed the maximum.");
            Assert.IsTrue(pool.IsFull);
        }

        [Test]
        public void NonPositiveAmountsAreIgnored()
        {
            var pool = new ResourcePool(100f, 50f);

            Assert.AreEqual(0f, pool.Add(0f));
            Assert.AreEqual(0f, pool.Add(-10f));
            Assert.AreEqual(0f, pool.Remove(0f));
            Assert.AreEqual(0f, pool.Remove(-10f), "Removing a negative amount must not become healing.");
            Assert.AreEqual(50f, pool.Current);
        }

        [Test]
        public void TryRemoveIsAllOrNothing()
        {
            var pool = new ResourcePool(100f, 30f);

            Assert.IsFalse(pool.TryRemove(31f));
            Assert.AreEqual(30f, pool.Current, "A refused withdrawal must not take a partial amount.");

            Assert.IsTrue(pool.TryRemove(30f));
            Assert.AreEqual(0f, pool.Current);
        }

        [Test]
        public void TryRemoveExactlyTheRemainingAmountSucceeds()
        {
            var pool = new ResourcePool(100f, 25f);

            Assert.IsTrue(pool.TryRemove(25f));
        }

        [Test]
        public void RaisingTheMaximumWithoutScalingDoesNotHeal()
        {
            var pool = new ResourcePool(100f, 40f);

            pool.SetMax(200f);

            Assert.AreEqual(40f, pool.Current, 0.001f, "Permanent progression must not refill the pool.");
            Assert.AreEqual(200f, pool.Max);
        }

        [Test]
        public void RaisingTheMaximumWithScalingKeepsTheProportion()
        {
            var pool = new ResourcePool(100f, 50f);

            pool.SetMax(200f, scaleCurrent: true);

            Assert.AreEqual(100f, pool.Current, 0.001f, "A character at half health stays at half health.");
            Assert.AreEqual(0.5f, pool.Normalized, 0.001f);
        }

        [Test]
        public void LoweringTheMaximumClampsTheCurrentValue()
        {
            var pool = new ResourcePool(100f, 90f);

            pool.SetMax(50f);

            Assert.AreEqual(50f, pool.Current, 0.001f);
        }

        [Test]
        public void ScalingFromAZeroMaximumDoesNotProduceNaN()
        {
            var pool = new ResourcePool(0f);

            pool.SetMax(100f, scaleCurrent: true);

            Assert.AreEqual(0f, pool.Current, 0.001f);
            Assert.IsFalse(float.IsNaN(pool.Current));
        }

        [Test]
        public void FillAndEmptyGoToTheBounds()
        {
            var pool = new ResourcePool(100f, 40f);

            pool.Empty();
            Assert.IsTrue(pool.IsDepleted);

            pool.Fill();
            Assert.IsTrue(pool.IsFull);
        }

        [Test]
        public void SetCurrentIsClamped()
        {
            var pool = new ResourcePool(100f);

            pool.SetCurrent(500f);
            Assert.AreEqual(100f, pool.Current);

            pool.SetCurrent(-500f);
            Assert.AreEqual(0f, pool.Current);
        }
    }
}
