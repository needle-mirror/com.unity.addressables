using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.TestTools;

namespace AddressableAssetsIntegrationTests
{
    internal abstract partial class AddressablesIntegrationTests : IPrebuildSetup
    {
        private static bool m_handlerCalled = false;
        
        [UnityTest]
        public IEnumerator CustomExceptionHandler()
        {
            yield return Init();
            
            var prevHandler = ResourceManager.ExceptionHandler;
            AssetReference ar = new AssetReference();
            ResourceManager.ExceptionHandler = CustomLogException;
            var op = ar.InstantiateAsync();
            ThrowFakeException();
            Assert.IsTrue(m_handlerCalled);
            ResourceManager.ExceptionHandler = prevHandler;
        }

        static public void CustomLogException(AsyncOperationHandle op, Exception ex)
        {
            m_handlerCalled = true;
        }

        static void ThrowFakeException()
        {
            ResourceManager.ExceptionHandler(new AsyncOperationHandle(), new Exception());
        }
    }
}