using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    [Serializable]
    internal class EventDataPlayerSessionCollection
    {
        List<EventDataPlayerSession> m_playerSessions = new List<EventDataPlayerSession>();
        Action<EventDataPlayerSession, DiagnosticEvent, bool> m_onEvent;
        Func<DiagnosticEvent, bool> m_onRecordEvent;

        public EventDataPlayerSessionCollection(Action<EventDataPlayerSession, DiagnosticEvent, bool> onEvent, Func<DiagnosticEvent, bool> onRecordEvent)
        {
            m_onEvent = onEvent;
            m_onRecordEvent = onRecordEvent;
        }

        bool RecordEvent(DiagnosticEvent e)
        {
            if (m_onRecordEvent != null)
                return m_onRecordEvent(e);
            return false;
        }

        public void ProcessEvent(DiagnosticEvent diagnosticEvent, int sessionId)
        {
            var session = GetPlayerSession(sessionId, true);
            bool entryCreated = false;
            session.AddSample(diagnosticEvent, RecordEvent(diagnosticEvent), ref entryCreated);
            m_onEvent(session, diagnosticEvent, entryCreated);
        }

        public EventDataPlayerSession GetSessionByIndex(int index)
        {
            if (m_playerSessions.Count == 0 || m_playerSessions.Count <= index)
                return null;

            return m_playerSessions[index];
        }

        public EventDataPlayerSession GetPlayerSession(int playerId, bool create)
        {
            foreach (var c in m_playerSessions)
                if (c.PlayerId == playerId)
                    return c;
            if (create)
            {
                var c = new EventDataPlayerSession("Player " + playerId, playerId);
                m_playerSessions.Add(c);
                return c;
            }
            return null;
        }

        public string[] GetConnectionNames()
        {
            string[] names = new string[m_playerSessions.Count];// + 1];
            for (int i = 0; i < m_playerSessions.Count; i++)
                names[i] = m_playerSessions[i].Name;
            return names;
        }

        public void AddSession(string name, int id)
        {
            m_playerSessions.Add(new EventDataPlayerSession(name, id));
        }
    }
}
