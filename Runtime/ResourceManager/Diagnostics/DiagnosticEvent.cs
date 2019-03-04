using System;
using System.Text;
using UnityEngine.Serialization;

namespace UnityEngine.ResourceManagement.Diagnostics
{
    /// <summary>
    /// Diagnostic event data.
    /// </summary>
    [Serializable]
    public struct DiagnosticEvent
    {
        [FormerlySerializedAs("m_graph")]
        [SerializeField]
        string m_Graph;  //id of graph definition to use
        [FormerlySerializedAs("m_parent")]
        [SerializeField]
        string m_Parent; //used to nest datagraphs
        [FormerlySerializedAs("m_id")]
        [SerializeField]
        string m_Id;     //id of a set of data streams
        [FormerlySerializedAs("m_stream")]
        [SerializeField]
        int m_Stream;    //data stream
        [FormerlySerializedAs("m_frame")]
        [SerializeField]
        int m_Frame;     //frame of the event
        [FormerlySerializedAs("m_value")]
        [SerializeField]
        int m_Value;      //data value of event
        [FormerlySerializedAs("m_data")]
        [SerializeField]
        byte[] m_Data;   //this is up to the sender/receiver to serialize/deserialize

        /// <summary>
        /// Gets the graph id that this event is intended for
        /// </summary>
        /// <value>The graph Id</value>
        public string Graph { get { return m_Graph; } }
        /// <summary>
        /// Optional id of the parent event.  This is used to structure the tree view of the event viewer
        /// </summary>
        /// <value>Parent Id</value>
        public string Parent { get { return m_Parent; } }
        /// <summary>
        /// The id of this event.  Ids are used to combine multiple events into a single graph
        /// </summary>
        /// <value>Event Id</value>
        public string EventId { get { return m_Id; } }
        /// <summary>
        /// The stream id for the event.  Each graph may display multiple streams of data for the same event Id
        /// </summary>
        /// <value>Stream Id</value>
        public int Stream { get { return m_Stream; } }
        /// <summary>
        /// The frame that the event occurred 
        /// </summary>
        /// <value>Frame number</value>
        public int Frame { get { return m_Frame; } }
        /// <summary>
        /// The value of the event. This value depends on the event type
        /// </summary>
        /// <value>Event value</value>
        public int Value { get { return m_Value; } }
        /// <summary>
        /// Serialized data for the event.  The contents depend on the event type
        /// </summary>
        /// <value>Event data</value>
        public byte[] Data { get { return m_Data; } }

        /// <summary>
        /// DiagnosticEvent constructor
        /// </summary>
        /// <param name="graph">Graph id</param>
        /// <param name="parent">Parent event id</param>
        /// <param name="id">Event id</param>
        /// <param name="stream">Stream index</param>
        /// <param name="frame">Frame number</param>
        /// <param name="value">Event value</param>
        /// <param name="data">Event data</param>
        public DiagnosticEvent(string graph, string parent, string id, int stream, int frame, int value, byte[] data)
        {
            m_Graph = graph;
            m_Parent = parent;
            m_Id = id;
            m_Stream = stream;
            m_Frame = frame;
            m_Value = value;
            m_Data = data;
        }

        /// <summary>
        /// Serializes the event into JSON and then encodes with System.Text.Encoding.ASCII.GetBytes
        /// </summary>
        /// <returns>Byte array containing serialized version of the event</returns>
        internal byte[] Serialize()
        {
            return Encoding.ASCII.GetBytes(JsonUtility.ToJson(this));
        }

        /// <summary>
        /// Deserializes event from a byte array created by the <see cref="Serialize"/> method
        /// </summary>
        /// <returns>Deserialized DiagnosticEvent struct</returns>
        /// <param name="data">Serialized data</param>
        public static DiagnosticEvent Deserialize(byte[] data)
        {
            return JsonUtility.FromJson<DiagnosticEvent>(Encoding.ASCII.GetString(data));
        }
    }
}