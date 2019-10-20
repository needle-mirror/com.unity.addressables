using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AddressableAssetsIntegrationTests
{
    internal class ManualPercentCompleteOperation : AsyncOperationBase<GameObject>
    {
        public ManualPercentCompleteOperation(float percentComplete)
        {
            m_PercentComplete = percentComplete;
        }

        public float m_PercentComplete;

        protected override void Execute()
        {
            Complete(new GameObject(), true, "");
        }

        protected override float Progress => m_PercentComplete;
    }
}