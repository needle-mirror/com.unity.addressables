using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.TestTools;

namespace UnityEngine.ResourceManagement.Tests
{
    public class ResourceManagerTests
    {
        Action<IAsyncOperation, Exception> m_PrevHandler;
        class MockProvider : IResourceProvider, IUpdateReceiver
        {
            public string _ProviderId = "MockProvider";
            public ProviderBehaviourFlags _BehaviourFlags = ProviderBehaviourFlags.None;
            
            public bool NeedUpdateResult = true;
            public int UpdateCount = 0;
            public bool NeedsUpdate { get { return NeedUpdateResult; } }

            public string ProviderId { get { return _ProviderId; } }

            public ProviderBehaviourFlags BehaviourFlags { get { return _BehaviourFlags; } }

            public Func<IResourceLocation, Type, IList<object>, IAsyncOperation> ProviderCallback;

            public Func<Type, IResourceLocation, bool> CanProvideCallback = (x, y) => true;

            public void Update(float unscaledDeltaTime)
            {
                UpdateCount++;
            }

            public IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps) where TObject : class
            {
                if (ProviderCallback != null)
                {
                    return (IAsyncOperation<TObject>)ProviderCallback(location, typeof(TObject), deps);
                }
                throw new NotImplementedException();
            }

            public bool CanProvide<TObject>(IResourceLocation location) where TObject : class
            {
                if(ProviderId == location.ProviderId)
                    return CanProvideCallback(typeof(TObject), location);
                return false;
            }

            public bool Release(IResourceLocation location, object asset)
            {
                throw new NotImplementedException();
            }

            public bool Initialize(string id, string data)
            {
                throw new NotImplementedException();
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_PrevHandler = ResourceManager.ExceptionHandler;
            ResourceManager.ExceptionHandler = null;
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            ResourceManager.ExceptionHandler = m_PrevHandler;
        }

        ResourceManager m_ResourceManager;
        [SetUp]
        public void Setup()
        {
            m_ResourceManager = new ResourceManager();
            m_ResourceManager.CallbackHooksEnabled = false; // default for tests. disabled callback hooks. we will call update manually
        }

        [TearDown]
        public void TearDown()
        {
            m_ResourceManager.Dispose();
        }

        [Test]
        public void WhenProviderImplementsIReceiverUpdate_AndNeedsUpdate_UpdateIsCalledWhileInProviderList()
        {
            MockProvider provider = new MockProvider();
            m_ResourceManager.ResourceProviders.Add(provider);
            m_ResourceManager.Update(0.0f);
            Assert.AreEqual(1, provider.UpdateCount);

            // Update isn't called after removing provider
            m_ResourceManager.ResourceProviders.Remove(provider);
            m_ResourceManager.Update(0.0f);
            Assert.AreEqual(1, provider.UpdateCount);
        }

        [Test]
        public void WhenProviderImplementsIReceiverUpdate_AndDoesNotNeedsUpdate_UpdateNotCalled()
        {
            MockProvider provider = new MockProvider();
            provider.NeedUpdateResult = false;
            m_ResourceManager.ResourceProviders.Add(provider);
            m_ResourceManager.Update(0.0f);
            Assert.AreEqual(0, provider.UpdateCount);
        }

        [UnityTest]
        public IEnumerator WhenResourceManagerCallbackHooksAreEnabled_ResourceManagerUpdatesProvidersAndCleansUp()
        {
            int beforeGOCount = GameObject.FindObjectsOfType<MonoBehaviourCallbackHooks>().Length;
            MockProvider provider = new MockProvider();
            m_ResourceManager.CallbackHooksEnabled = true;
            m_ResourceManager.ResourceProviders.Add(provider);
            yield return null;
            Assert.AreEqual(1, provider.UpdateCount);
            Assert.AreEqual(beforeGOCount+1, GameObject.FindObjectsOfType<MonoBehaviourCallbackHooks>().Length);
            m_ResourceManager.Dispose();
            yield return null;
            Assert.AreEqual(beforeGOCount, GameObject.FindObjectsOfType<MonoBehaviourCallbackHooks>().Length);
        }

        [Test]
        public void ProvideResource_WhenDependencyFailsToLoad_AndProviderCannotLoadWithFailedDependencies_ProvideNotCalled()
        {
            MockProvider provider = new MockProvider();
            provider.ProviderCallback = (x, y, z) => { throw new Exception("This Should Not Have Been Called"); };
            m_ResourceManager.ResourceProviders.Add(provider);
            ResourceLocationBase locDep = new ResourceLocationBase("depasset", "depasset", "unkonwn");
            ResourceLocationBase locRoot = new ResourceLocationBase("rootasset", "rootasset", provider.ProviderId, locDep);
            IAsyncOperation<object> op = m_ResourceManager.ProvideResource<object>(locRoot);
            DelayedActionManager.Wait(0, 1.0f);
            Assert.AreEqual(AsyncOperationStatus.Failed, op.Status);
        }

        [Test]
        public void ProvideResource_WhenDependencyFailsToLoad_AndProviderCanLoadWithFailedDependencies_ProviderStillProvides()
        {
            MockProvider provider = new MockProvider();
            provider._BehaviourFlags = ProviderBehaviourFlags.CanProvideWithFailedDependencies;
            provider.ProviderCallback = (loc, type, deps) =>
            {
                var op2 = new CompletedOperation<object>();
                op2.Start(loc, loc, 5);
                return op2;
            };
            m_ResourceManager.ResourceProviders.Add(provider);
            ResourceLocationBase locDep = new ResourceLocationBase("depasset", "depasset", "unkonwn");
            ResourceLocationBase locRoot = new ResourceLocationBase("rootasset", "rootasset", provider.ProviderId, locDep);
            IAsyncOperation<object> op = m_ResourceManager.ProvideResource<object>(locRoot);
            DelayedActionManager.Wait(0, 1.0f);
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.AreEqual(5, op.Result);
        }
    }
}