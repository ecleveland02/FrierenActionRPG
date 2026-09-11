using System;
using Frieren.Core.Services;
using NUnit.Framework;

namespace Frieren.Tests.EditMode
{
    public sealed class ServiceLocatorTests
    {
        private sealed class SampleService
        {
            public int Value;
        }

        private sealed class OtherService
        {
        }

        [SetUp]
        public void SetUp() => ServiceLocator.Clear();

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        [Test]
        public void Get_ReturnsRegisteredInstance()
        {
            var service = new SampleService { Value = 7 };
            ServiceLocator.Register(service);

            Assert.AreSame(service, ServiceLocator.Get<SampleService>());
        }

        [Test]
        public void Get_ThrowsWhenNotRegistered()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<SampleService>());
        }

        [Test]
        public void TryGet_ReturnsFalseWhenNotRegistered()
        {
            Assert.IsFalse(ServiceLocator.TryGet(out SampleService service));
            Assert.IsNull(service);
        }

        [Test]
        public void Register_ReplacesPreviousInstanceOfSameType()
        {
            var first = new SampleService { Value = 1 };
            var second = new SampleService { Value = 2 };

            ServiceLocator.Register(first);
            ServiceLocator.Register(second);

            Assert.AreSame(second, ServiceLocator.Get<SampleService>());
        }

        [Test]
        public void Register_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ServiceLocator.Register<SampleService>(null));
        }

        [Test]
        public void Unregister_RemovesOnlyTheRequestedType()
        {
            ServiceLocator.Register(new SampleService());
            ServiceLocator.Register(new OtherService());

            Assert.IsTrue(ServiceLocator.Unregister<SampleService>());
            Assert.IsFalse(ServiceLocator.IsRegistered<SampleService>());
            Assert.IsTrue(ServiceLocator.IsRegistered<OtherService>());
        }
    }
}
