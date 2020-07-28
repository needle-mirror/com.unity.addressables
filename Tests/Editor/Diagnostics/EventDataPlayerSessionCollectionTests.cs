using NUnit.Framework;
using UnityEditor.AddressableAssets.Diagnostics.Data;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEditor.AddressableAssets.Tests.Diagnostics
{
    public class EventDataPlayerSessionCollectionTests
    {
        [Test]
        public void EventDataPlayerSessionCollection_RecordEvent_ReturnsFalseOnNullEventHandler()
        {
            EventDataPlayerSessionCollection edpsc = new EventDataPlayerSessionCollection(null);
            
            Assert.IsFalse(edpsc.RecordEvent(new DiagnosticEvent()), "RecordEvent should return null if m_OnRecordEvent is null");
        }

        [Test]
        public void EventDataPlayerSessionCollection_GetPlayerSession_ProperlyCreatesWhenCreateIsTrue()
        {
            EventDataPlayerSessionCollection edpsc = new EventDataPlayerSessionCollection((DiagnosticEvent x) => true);
            EventDataPlayerSession edps = edpsc.GetPlayerSession(0, true);
            
            Assert.NotNull(edps, "New EventDataPlayerSession should have been created.");
            Assert.AreEqual("Player 0", edps.EventName);
        }

        [Test]
        public void EventDataPlayerSessionCollection_GetPlayerSession_ReturnsNullOnNoIdMatch()
        {
            EventDataPlayerSessionCollection edpsc = new EventDataPlayerSessionCollection((DiagnosticEvent x) => true);
            EventDataPlayerSession edps = edpsc.GetPlayerSession(0, false);
            
            Assert.IsNull(edps, "New EventDataPlayerSession should not be created when create = false and there is no id match. ");
        }

        [Test]
        public void EventDataPlayerSessionCollect_GetSessionByIndex_ReturnsNullOnInvalidInput()
        {
            EventDataPlayerSessionCollection edpsc = new EventDataPlayerSessionCollection((DiagnosticEvent x) => true);
            Assert.IsNull(edpsc.GetSessionByIndex(0), "Trying to request a session with a nonexistent index should return null. ");
            
            edpsc.AddSession("test session", 0);
            Assert.IsNull(edpsc.GetSessionByIndex(2), "Trying to request a session with a nonexistent index should return null. ");
            Assert.NotNull(edpsc.GetSessionByIndex(0), "Session not returned properly on valid index. ");
        }
    }
}
