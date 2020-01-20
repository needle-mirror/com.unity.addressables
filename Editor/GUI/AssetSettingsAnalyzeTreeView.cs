using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    class AssetSettingsAnalyzeTreeView : TreeView
    {
        private int m_CurrentDepth;

        internal AssetSettingsAnalyzeTreeView(TreeViewState state)
            : base(state)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;

            Reload();
        }
        
        private List<AnalyzeRuleContainerTreeViewItem> GatherAllInheritRuleContainers(TreeViewItem baseContainer)
        {
            List<AnalyzeRuleContainerTreeViewItem> retValue = new List<AnalyzeRuleContainerTreeViewItem>();
            if (!baseContainer.hasChildren)
                return new List<AnalyzeRuleContainerTreeViewItem>();

            foreach (var child in baseContainer.children)
            {
                if (child is AnalyzeRuleContainerTreeViewItem)
                {
                    retValue.AddRange(GatherAllInheritRuleContainers(child as AnalyzeRuleContainerTreeViewItem));
                    retValue.Add(child as AnalyzeRuleContainerTreeViewItem);
                }
            }

            return retValue;
        }

        private void PerformActionForEntireRuleSelection(Action<AnalyzeRuleContainerTreeViewItem> action)
        {
            List<AnalyzeRuleContainerTreeViewItem> activeSelection = (from id in GetSelection()
                let selection = FindItem(id, rootItem)
                where selection is AnalyzeRuleContainerTreeViewItem
                select selection as AnalyzeRuleContainerTreeViewItem).ToList();

            List<AnalyzeRuleContainerTreeViewItem> inheritSelection = new List<AnalyzeRuleContainerTreeViewItem>();
            foreach (var selected in activeSelection)
                inheritSelection.AddRange(GatherAllInheritRuleContainers(selected));

            List<AnalyzeRuleContainerTreeViewItem> entireSelection = activeSelection.Union(inheritSelection).ToList();

            foreach (AnalyzeRuleContainerTreeViewItem ruleContainer in entireSelection)
            {
                if (ruleContainer.analyzeRule != null)
                {
                    action(ruleContainer);
                }
            }
        }

        public void RunAllSelectedRules()
        {
            PerformActionForEntireRuleSelection((ruleContainer) =>
            {
                var results = AnalyzeSystem.RefreshAnalysis(ruleContainer.analyzeRule);

                BuildResults(ruleContainer, results);
                Reload();
                UpdateSelections(GetSelection());
            });
        }

        public void FixAllSelectedRules()
        {
            PerformActionForEntireRuleSelection((ruleContainer) =>
            {
                AnalyzeSystem.FixIssues(ruleContainer.analyzeRule);
                var results = AnalyzeSystem.RefreshAnalysis(ruleContainer.analyzeRule);

                BuildResults(ruleContainer, results);
                Reload();
                UpdateSelections(GetSelection());
            });
        }

        public void ClearAllSelectedRules()
        {
            PerformActionForEntireRuleSelection((ruleContainer) =>
            {
                AnalyzeSystem.ClearAnalysis(ruleContainer.analyzeRule);

                BuildResults(ruleContainer, new List<AnalyzeRule.AnalyzeResult>());
                Reload();
                UpdateSelections(GetSelection());
            });
        }

        public void RevertAllSelectedRules()
        {
            //TODO
        }

        public bool SelectionContainsFixableRule { get; private set; }
        public bool SelectionContainsRuleContainer { get; private set; }

        public bool SelectionContainsErrors { get; private set; }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            UpdateSelections(selectedIds);
        }

        void UpdateSelections(IList<int> selectedIds)
        {
            var allSelectedRuleContainers = (from id in selectedIds
                let ruleContainer = FindItem(id, rootItem) as AnalyzeRuleContainerTreeViewItem
                where ruleContainer != null
                select ruleContainer);

            List<AnalyzeRuleContainerTreeViewItem> allRuleContainers = new List<AnalyzeRuleContainerTreeViewItem>();
            foreach (var ruleContainer in allSelectedRuleContainers)
            {
                allRuleContainers.AddRange(GatherAllInheritRuleContainers(ruleContainer));
                allRuleContainers.Add(ruleContainer);
            }

            allRuleContainers = allRuleContainers.Distinct().ToList();

            SelectionContainsErrors = (from container in allRuleContainers
                                       from child in container.children
                                       where child is AnalyzeResultsTreeViewItem && (child as AnalyzeResultsTreeViewItem).IsError
                                       select child).Any();

            SelectionContainsRuleContainer = allRuleContainers.Any();

            SelectionContainsFixableRule = (from container in allRuleContainers
                where container.analyzeRule.CanFix
                select container).Any();
        }

        protected override void ContextClicked()
        {
            if (SelectionContainsRuleContainer)
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Run Analyze Rule"), false, RunAllSelectedRules);
                menu.AddItem(new GUIContent("Clear Analyze Results"), false, ClearAllSelectedRules);

                if (SelectionContainsFixableRule && SelectionContainsErrors)
                    menu.AddItem(new GUIContent("Fix Analyze Rule"), false, FixAllSelectedRules);
                else
                    menu.AddDisabledItem(new GUIContent("Fix Analyze Rule"));

                //TODO
                //menu.AddItem(new GUIContent("Revert Analyze Rule"), false, RevertAllSelectedRules);

                menu.ShowAsContext();
                Repaint();
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            m_CurrentDepth = 0;
            var root = new TreeViewItem(-1, -1);
            root.children = new List<TreeViewItem>();

            string baseName = "Analyze Rules";
            string fixableRules = "Fixable Rules";
            string unfixableRules = "Unfixable Rules";

            AnalyzeRuleContainerTreeViewItem baseViewItem = new AnalyzeRuleContainerTreeViewItem(baseName.GetHashCode(), m_CurrentDepth, baseName);
            baseViewItem.children = new List<TreeViewItem>();
            baseViewItem.analyzeRule.CanFix = true;

            root.AddChild(baseViewItem);

            m_CurrentDepth++;

            var fixable = new AnalyzeRuleContainerTreeViewItem(fixableRules.GetHashCode(), m_CurrentDepth, fixableRules);
            var unfixable = new AnalyzeRuleContainerTreeViewItem(unfixableRules.GetHashCode(), m_CurrentDepth, unfixableRules);

            fixable.analyzeRule.CanFix = true;
            unfixable.analyzeRule.CanFix = false;

            baseViewItem.AddChild(fixable);
            baseViewItem.AddChild(unfixable);

            m_CurrentDepth++;

            for (int i = 0; i < AnalyzeSystem.Rules.Count; i++)
            {
                AnalyzeRuleContainerTreeViewItem ruleContainer = new AnalyzeRuleContainerTreeViewItem(
                    AnalyzeSystem.Rules[i].ruleName.GetHashCode(), m_CurrentDepth, AnalyzeSystem.Rules[i]);

                if(ruleContainer.analyzeRule.CanFix)
                    fixable.AddChild(ruleContainer);
                else
                    unfixable.AddChild(ruleContainer);

            }

            m_CurrentDepth++;

            int index = 0;
            var ruleContainers = GatherAllInheritRuleContainers(baseViewItem);
            foreach (var ruleContainer in ruleContainers)
            {

                if (ruleContainer != null && AnalyzeSystem.AnalyzeData.Data.ContainsKey(ruleContainer.analyzeRule.ruleName))
                {
                    EditorUtility.DisplayProgressBar("Calculating Results for " + ruleContainer.displayName, "", (index / ruleContainers.Count) % 100);
                    BuildResults(ruleContainer, AnalyzeSystem.AnalyzeData.Data[ruleContainer.analyzeRule.ruleName]);
                }

                index++;
            }

            EditorUtility.ClearProgressBar();
            return root;
        }

        private readonly Dictionary<int, TreeViewItem> hashToTreeViewItems = new Dictionary<int, TreeViewItem>();
        void BuildResults(TreeViewItem root, List<AnalyzeRule.AnalyzeResult> ruleResults)
        {
            hashToTreeViewItems.Clear();
            LinkedList<TreeViewItem> treeViewItems = new LinkedList<TreeViewItem>();

            hashToTreeViewItems.Add(root.id, root);
            float index = 0;


            //preprocess nodes
            foreach (var result in ruleResults)
            {
                var resPath = result.resultName.Split(AnalyzeRule.kDelimiter);
                string name = string.Empty;

                for (int i = 0; i < resPath.Length; i++)
                {
                    int parentHash = name.GetHashCode();
                    if (string.IsNullOrEmpty(name))
                        parentHash = root.id;
                    name += resPath[i];
                    int hash = name.GetHashCode();

                    if (hash == root.id)
                        treeViewItems.AddLast(root);
                    else
                    {
                        AnalyzeResultsTreeViewItem item = new AnalyzeResultsTreeViewItem(hash, i + m_CurrentDepth, resPath[i], parentHash, result.severity);
                        item.children = new List<TreeViewItem>();
                        treeViewItems.AddLast(item);  
                    }
                }

                index++;
            }

            //create dictionary
            foreach (var item in treeViewItems)
            {
                if (item != null) 
                {
                    if (!hashToTreeViewItems.ContainsKey(item.id))
                        hashToTreeViewItems.Add(item.id, item);
                }
            }

            //Build results tree
            index = 0;
            foreach (var hash in hashToTreeViewItems.Keys)
            {
                EditorUtility.DisplayProgressBar("Building Results Tree.", hashToTreeViewItems[hash].displayName, (index / hashToTreeViewItems.Keys.Count) % 100);

                TreeViewItem item;
                if (hashToTreeViewItems.TryGetValue(hash, out item))
                {
                    if ((item as AnalyzeResultsTreeViewItem) != null && hashToTreeViewItems.ContainsKey((item as AnalyzeResultsTreeViewItem).parentHash))
                    {
                        var parent = hashToTreeViewItems[(item as AnalyzeResultsTreeViewItem).parentHash];

                        if (parent.id != item.id)
                            parent.AddChild(item);
                    }
                }

                index++;
            }

            EditorUtility.ClearProgressBar();

            List<TreeViewItem> allTreeViewItems = new List<TreeViewItem>();
            allTreeViewItems.Add(root);
            allTreeViewItems.AddRange(root.children);

            foreach (var node in allTreeViewItems)
                (node as AnalyzeTreeViewItemBase)?.AddIssueCountToName();

            EditorUtility.SetDirty(AnalyzeSystem.AnalyzeData);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as AnalyzeResultsTreeViewItem;
            if (item != null && item.severity != MessageType.None)
            {
                Texture2D icon = null;
                switch (item.severity)
                {
                    case MessageType.Info:
                        icon = GetInfoIcon();
                        break;
                    case MessageType.Warning:
                        icon = GetWarningIcon();
                        break;
                    case MessageType.Error:
                        icon = GetErrorIcon();
                        break;
                }

                UnityEngine.GUI.Label(
                    new Rect(args.rowRect.x + baseIndent, args.rowRect.y, args.rowRect.width - baseIndent,
                        args.rowRect.height), new GUIContent(icon, string.Empty));
            }

            base.RowGUI(args);
        }

        Texture2D m_ErrorIcon;
        Texture2D m_WarningIcon;
        Texture2D m_InfoIcon;

        Texture2D GetErrorIcon()
        {
            if (m_ErrorIcon == null)
                FindMessageIcons();
            return m_ErrorIcon;
        }

        Texture2D GetWarningIcon()
        {
            if (m_WarningIcon == null)
                FindMessageIcons();
            return m_WarningIcon;
        }

        Texture2D GetInfoIcon()
        {
            if (m_InfoIcon == null)
                FindMessageIcons();
            return m_InfoIcon;
        }

        void FindMessageIcons()
        {
            m_ErrorIcon = EditorGUIUtility.FindTexture("console.errorIcon");
            m_WarningIcon = EditorGUIUtility.FindTexture("console.warnicon");
            m_InfoIcon = EditorGUIUtility.FindTexture("console.infoIcon");
        }
    }

    class AnalyzeTreeViewItemBase : TreeViewItem
    {
        private string baseDisplayName;
        private string currentDisplayName;

        public override string displayName
        {
            get { return currentDisplayName; }
            set { baseDisplayName = value; }

        }

        public AnalyzeTreeViewItemBase(int id, int depth, string displayName) : base(id, depth,
            displayName)
        {
            currentDisplayName = baseDisplayName = displayName;
        }

        public int AddIssueCountToName()
        {
            int issueCount = 0;
            if (children != null)
            {
                foreach (var child in children)
                {
                    var analyzeNode = child as AnalyzeResultsTreeViewItem;
                    if (analyzeNode != null)
                        issueCount += analyzeNode.AddIssueCountToName();
                }
            }

            if (issueCount == 0)
                return 1;

            currentDisplayName = baseDisplayName + " (" + issueCount + ")";
            return issueCount;
        }
    }

    class AnalyzeResultsTreeViewItem : AnalyzeTreeViewItemBase
    {
        public MessageType severity { get; set; }
        public int parentHash { get; set; }

        public bool IsError
        {
            get { return !displayName.Contains("No issues found"); }
        }

        public AnalyzeResultsTreeViewItem(int id, int depth, string displayName, int parent, MessageType type) : base(id, depth,
            displayName)
        {
            severity = type;
            parentHash = parent;
        }

    }

    class AnalyzeRuleContainerTreeViewItem : AnalyzeTreeViewItemBase
    {
        internal AnalyzeRule analyzeRule;

        public AnalyzeRuleContainerTreeViewItem(int id, int depth, AnalyzeRule rule) : base(id, depth, rule.ruleName)
        {
            analyzeRule = rule;
            children = new List<TreeViewItem>();
        }

        public AnalyzeRuleContainerTreeViewItem(int id, int depth, string displayName) : base(id, depth, displayName)
        {
            analyzeRule = new AnalyzeRule();
            children = new List<TreeViewItem>();
        }
    }
}