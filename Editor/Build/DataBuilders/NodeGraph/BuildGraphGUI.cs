using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace UnityEditor.AddressableAssets.GraphBuild
{
    class BuildGraphGUI : ScriptableObject, ISearchWindowProvider, IDataBuilderGUI
    {
        public class BuildGraphView : GraphView
        {
            public BuildGraphView()
            {
                SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());
                this.AddManipulator(new FreehandSelector());
                //   this.AddManipulator(new ClickSelector());
                name = "theView";
                persistenceKey = "theView";

                Insert(0, new GridBackground());

                focusIndex = 0;
            }

            public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
            {
                return ports.ToList().Where(nap =>
                    nap.direction != startPort.direction &&
                    nap.node != startPort.node)
                    .ToList();
            }
        }

        public GraphView graphView { get; private set; }
        public NodeGraphDataBuilder graphData { get; private set; }
        //public BuildGraphBlackboard graphBlackboard { get; private set; }
        public IDataBuilderContext context;

        internal void Init(NodeGraphDataBuilder data, IDataBuilderContext iContext)
        {
            context = iContext;
            graphData = data;
        }

        public void ShowGUI(VisualElement container)
        {
            graphView = new BuildGraphView();
            container.Add(graphView);

            graphView.StretchToParentSize();
            graphView.graphViewChanged = GraphViewChanged;
            graphView.nodeCreationRequest += OnRequestNodeCreation;
            graphView.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
            graphView.RegisterCallback<DragPerformEvent>(OnDragPerformEvent);

            Reload();
        }

        public void UpdateGUI(Rect rect)
        {
            var r = graphView.CalculateRectToFitAll(graphView.parent);
            graphView.layout = r;
        }

        public void HideGUI()
        {
            graphView.parent.Remove(graphView);
        }

        void OnDragUpdatedEvent(DragUpdatedEvent e)
        {
            var selection = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;

            if (selection != null && (selection.OfType<BlackboardField>().Count() >= 0))
            {
                DragAndDrop.visualMode = e.ctrlKey ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Move;
            }
        }

        void OnDragPerformEvent(DragPerformEvent e)
        {
            var selection = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;

            if (selection == null)
            {
                return;
            }

            IEnumerable<BlackboardField> fields = selection.OfType<BlackboardField>();

            if (fields.Count() == 0)
                return;

            Vector2 localPos = (e.currentTarget as VisualElement).ChangeCoordinatesTo(graphView.contentViewContainer, e.localMousePosition);

            foreach (BlackboardField field in fields)
            {
                var dataNode = graphData.CreateNode(field.userData as Type, field.text);
                dataNode.position = localPos;
                AddVisualNode(dataNode);
                localPos += new Vector2(0, 25);
            }
        }

        protected void OnRequestNodeCreation(NodeCreationContext iContext)
        {
            SearchWindow.Open(new SearchWindowContext(iContext.screenMousePosition), this);
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext iContext)
        {
            var tree = new List<SearchTreeEntry>();

            tree.Add(new SearchTreeGroupEntry(new GUIContent("Create Node")));
            foreach(var t in NodeTypes)
                tree.Add(new SearchTreeEntry(new GUIContent(t.Name)) { level = 1, userData = t });

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext iContext)
        {
            if (entry is SearchTreeGroupEntry)
                return false;
            var dataNode = graphData.CreateNode(entry.userData as Type, PrettyType(entry.userData as Type));
            dataNode.position = graphView.WorldToLocal(iContext.screenMousePosition - graphView.contentRect.position);
            AddVisualNode(dataNode);
            return true;
        }

        GraphViewChange GraphViewChanged(GraphViewChange graphViewChange)
        {
            if (graphViewChange.elementsToRemove != null)
            {
                foreach (GraphElement element in graphViewChange.elementsToRemove)
                {
                    if (element is Node)
                        graphData.RemoveNode((Hash128)element.userData);
                    else if (element is Edge)
                        graphData.RemoveLink((Hash128)element.userData);
                }
            }

            if (graphViewChange.edgesToCreate != null)
            {
                foreach (Edge edge in graphViewChange.edgesToCreate)
                {
                    var linkId = graphData.CreateLink( (Hash128)edge.output.node.userData, new PortIdentifier { node = (Hash128)edge.input.node.userData, name = edge.input.portName });
                    edge.persistenceKey = linkId.ToString();
                    edge.userData = linkId.id;
                }
            }

            if (graphViewChange.movedElements != null)
            {
                foreach (GraphElement element in graphViewChange.movedElements)
                {
                    if (element is Node)
                    {
                        graphData.GetNode((Hash128)element.userData).position = element.GetPosition().position;
                    }
                }
            }

            return graphViewChange;
        }

        void AddVisualNode(BuildNode node)
        {
            var nodeUI = new BuildGraphViewNode(this, node);
            graphView.AddElement(nodeUI);
        }

        void CreateEdge(BuildLink link)
        {
            var inputNode = graphView.Q(link.target.node.ToString());
            var inputPort = inputNode.Q<Port>(link.target.name);

            var outputNode = graphView.Q(link.source.ToString());
            var outputPort = outputNode.Q<Port>("Output");

            var edge = outputPort.ConnectTo(inputPort);
            edge.persistenceKey = link.id.ToString();
            edge.userData = link.id;

            graphView.AddElement(edge);
        }


        public void Reload()
        {
            if (graphView == null)
                return;

            if (graphData == null)
                return;

            foreach (var node in graphData.nodes)
                AddVisualNode(node.Value);

            foreach (var link in graphData.links)
                CreateEdge(link.Value);

            var miniMap = new MiniMap();
            miniMap.SetPosition(new Rect(0, 372, 200, 176));
            graphView.Add(miniMap);
        }

        static string PrettyType(Type t)
        {
            var n = t.Name;
            var i = n.LastIndexOf('.');
            if (i > 0)
                n = n.Substring(i + 1);
            if (!t.IsGenericType)
                return n;
            i = n.IndexOf('`');
            if (i > 0)
            {
                n = n.Substring(0, i) + "<";
                var args = t.GetGenericArguments();
                for (int c = 0; c < args.Length; c++)
                {
                    var argN = PrettyType(args[c]);
                    n += argN;
                    if (c < args.Length - 1)
                        n += ", ";
                }
                n += ">";
            }
            return n;
        }


        class BuildGraphViewNode : Node
        {
            BuildGraphGUI m_GraphWindow;
            public BuildGraphViewNode(BuildGraphGUI window, BuildNode node)
            {
                m_GraphWindow = window;
                title = node.name + " (" + PrettyType(node.OutputType) + ")";

                capabilities |= Capabilities.Movable;
                persistenceKey = node.id.ToString();
                name = node.id.ToString();
                userData = node.id;
                SetPosition(new Rect(node.position, Vector2.zero));

                foreach (var i in node.Inputs)
                {
                    var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, i.type);
                    inputPort.name = i.name;// + " (" + PrettyType(i.type) + ")";
                    inputPort.portName = i.name;
                    inputContainer.Add(inputPort);
                }


                if (node.OutputType != null)
                {
                    var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, node.OutputType);
                    outputPort.name = "Output";
                    outputPort.portName = "Output";

                    outputContainer.Add(outputPort);
                }
            }

            public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
            {
                if (evt.target is BuildGraphViewNode && m_GraphWindow.context != null)
                {
                    var node = m_GraphWindow.graphData.GetNode((Hash128)(evt.target as BuildGraphViewNode).userData);
#if UNITY_2018_3_OR_NEWER
                    evt.menu.AppendAction("Evaluate", a => Debug.LogFormat("Result {0}", m_GraphWindow.graphData.EvaluateNode(node.id, m_GraphWindow.context)), a=> DropdownMenu.MenuAction.StatusFlags.Normal);
#else
                    evt.menu.AppendAction("Evaluate", a => Debug.LogFormat("Result {0}", m_GraphWindow.graphData.EvaluateNode(node.id, m_GraphWindow.context)),
                        ContextualMenu.MenuAction.AlwaysEnabled);
#endif
                }

                base.BuildContextualMenu(evt);
            }
        }
        static List<Type> s_Types;
        public static List<Type> NodeTypes
        {
            get
            {
                if (s_Types == null)
                {
                    s_Types = new List<Type>();
                    try
                    {
                        var interfaceType = typeof(IBuildNodeProcessor);
                        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                        {
#if NET_4_6
                            foreach (var t in a.ExportedTypes)
#else
                            foreach (var t in a.GetExportedTypes())
#endif
                            {
                                if (t != interfaceType && interfaceType.IsAssignableFrom(t) && !t.IsAbstract)
                                    s_Types.Add(t);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                return s_Types;
            }
        }

    }
}