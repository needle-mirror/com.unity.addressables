using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.GraphBuild
{
 //Disabled for now...   [CreateAssetMenu(fileName = "BuildGraph.asset", menuName = "Build Script/Node Graph")]
    class NodeGraphDataBuilder : ScriptableObject, ISerializationCallbackReceiver, IDataBuilder
    {
        public Dictionary<Hash128, BuildNode> nodes = new Dictionary<Hash128, BuildNode>();
        public Dictionary<Hash128, BuildLink> links = new Dictionary<Hash128, BuildLink>();
        public Dictionary<PortIdentifier, List<Hash128>> linkTable = new Dictionary<PortIdentifier, List<Hash128>>(); //link backwards from input to source nodes
        [FormerlySerializedAs("serializedNodes")]
        [SerializeField]
        List<BuildNode> m_SerializedNodes;
        [FormerlySerializedAs("serializedLinks")]
        [SerializeField]
        List<BuildLink> m_SerializedLinks;

        public string Name
        {
            get
            {
                return name;
            }
        }

        public BuildNode CreateNode(Type type, string n)
        {
            var node = new BuildNode(type) { name = n, id = Hash128.Parse(GUID.Generate().ToString()) };
            nodes.Add(node.id, node);
            Debug.LogFormat("Created node {0} {1} {2}", type.Name, n, node.id);
            EditorUtility.SetDirty(this);
            return node;
        }

        public Hash128 FindNodeWithOutput<T>()
        {
            foreach (var n in nodes)
                if (typeof(T).IsAssignableFrom(n.Value.OutputType))
                    return n.Key;
            return default(Hash128);
        }

        public ICollection<Hash128> FindNodesWithOutput<T>()
        {
            var results = new List<Hash128>();
            foreach (var n in nodes)
                if (typeof(T).IsAssignableFrom(n.Value.OutputType))
                    results.Add(n.Key);
            return results;
        }

        public BuildNode GetNode(Hash128 id)
        {
            return nodes[id];
        }

        public void RemoveNode(Hash128 node)
        {
            nodes.Remove(node);
            EditorUtility.SetDirty(this);
        }

        public void RemoveLink(Hash128 link)
        {
            links.Remove(link);
            EditorUtility.SetDirty(this);
        }

        public BuildLink CreateLink(Hash128 src, PortIdentifier target)
        {
            var link = new BuildLink { source = src, target = target, id = Hash128.Parse(GUID.Generate().ToString()) };
            List<Hash128> tableEntries;
            if (!linkTable.TryGetValue(target, out tableEntries))
                linkTable.Add(target, tableEntries = new List<Hash128>());
            tableEntries.Add(src);
            links.Add(link.id, link);
            Debug.LogFormat("Created link {0} -> {1}.{2}", src, target.node, target.name);

            EditorUtility.SetDirty(this);
            return link;
        }

        public IList<Hash128> GetInputNodes(Hash128 targetNode, string inputName)
        {
            return linkTable[new PortIdentifier { node = targetNode, name = inputName }];
        }

        public object EvaluateNode(Hash128 node, IDataBuilderContext context)
        {
            return nodes[node].Evaluate(this, context);
        }

        public void OnBeforeSerialize()
        {
            m_SerializedLinks = new List<BuildLink>(links.Values);
            m_SerializedNodes = new List<BuildNode>(nodes.Values);
        }

        public void OnAfterDeserialize()
        {
            if (m_SerializedNodes != null)
            {
                foreach (var n in m_SerializedNodes)
                    nodes.Add(n.id, n);
                m_SerializedNodes = null;
            }

            if (m_SerializedLinks != null)
            {
                foreach (var l in m_SerializedLinks)
                {
                    links.Add(l.id, l);

                    List<Hash128> tableEntries;
                    if (!linkTable.TryGetValue(l.target, out tableEntries))
                        linkTable.Add(l.target, tableEntries = new List<Hash128>());
                    tableEntries.Add(l.source);
                }
                m_SerializedLinks = null;
            }
        }

        public bool CanBuildData<T>() where T : IDataBuilderResult
        {
            return FindNodeWithOutput<T>() != default(Hash128);
        }

        public T BuildData<T>(IDataBuilderContext context) where T : IDataBuilderResult
        {
            var nodeId = FindNodeWithOutput<T>();
            return (T)GetNode(nodeId).Evaluate(this, context);
        }

        public IDataBuilderGUI CreateGUI(IDataBuilderContext context)
        {
            var gui = CreateInstance<BuildGraphGUI>();
            gui.Init(this, context);
            return gui;
        }

        public void ClearCachedData()
        {
            
        }
    }


}