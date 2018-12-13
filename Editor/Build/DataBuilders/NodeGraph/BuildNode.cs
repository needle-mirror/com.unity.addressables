using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GraphBuild
{
    [Serializable]
    struct PortDescription
    {
        public string name;
        public string processorAssembly;
        public string processorType;

        public Type type
        {
            get { return Assembly.Load(processorAssembly).GetType(processorType); }
        }

        public PortDescription(Type t, string n)
        {
            name = n;
            processorType = t.FullName;
            processorAssembly = t.Assembly.FullName;
        }

        public override int GetHashCode()
        {
            return processorType.GetHashCode() * 9176 + processorAssembly.GetHashCode() * 9176 + name.GetHashCode();
        }
    }

    [Serializable]
    struct PortIdentifier
    {
        public string name;
        public Hash128 node;
    }

    [Serializable]
    class BuildNode
    {
        public Hash128 id;
        public string name;
        public Vector2 position;
        public string processorAssembly;
        public string processorType;
        Type m_ProcessorType;
        List<PortDescription> m_TypeInputMap;

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public BuildNode() { }

        public BuildNode(Type t)
        {
            processorAssembly = t.Assembly.FullName;
            processorType = t.FullName;
        }

        public Type ProcessorType
        {
            get
            {
                if (m_ProcessorType == null)
                    m_ProcessorType = Assembly.Load(processorAssembly).GetType(processorType);
                return m_ProcessorType;
            }
        }

        IBuildNodeProcessor CreateProcessor()
        {
            var instance = Activator.CreateInstance(ProcessorType) as IBuildNodeProcessor;
            if (instance == null)
                Debug.LogErrorFormat("Unable to create type {0} from assembly {1}.", processorType, processorAssembly);
            return instance;
        }

        static List<PortDescription> ExtractInputs(Type t)
        {
            var ports = new List<PortDescription>();
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                if (f.IsDefined(typeof(InjectInputAttribute), false))
                    ports.Add(new PortDescription(f.FieldType, f.Name));
            return ports;
        }

        public ICollection<PortDescription> Inputs
        {
            get
            {
                if (m_TypeInputMap == null)
                    m_TypeInputMap = ExtractInputs(ProcessorType);
                return m_TypeInputMap;
            }
        }

        public Type OutputType
        {
            get
            {
                try
                {
                    Type oType = null;
                    var baseType = ProcessorType.BaseType;
                    while (oType == null && baseType != null && baseType.BaseType != null)
                    {
                        if (baseType.IsGenericType)
                            oType = baseType.GetGenericArguments()[0];
                        baseType = baseType.BaseType;
                    }

                    return oType;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return null;
                }
            }
        }

        public object Evaluate(NodeGraphDataBuilder graph, IDataBuilderContext context)
        {
            if (Inputs.Count == 0)
            {
                return CreateProcessor().Evaluate(this, null, context);
            }

            var inputs = new List<object>();
            foreach (var input in Inputs)
            foreach (var inputNode in graph.GetInputNodes(id, input.name))
                inputs.Add(graph.EvaluateNode(inputNode, context));

            return CreateProcessor().Evaluate(this, inputs, context);
        }
    }
}
