using System;
using System.Collections.Generic;

namespace AutoGroupGenerator
{
    /// <summary>
    /// Represents a subset of a larger <see cref="DependencyGraph"/> instance.
    /// </summary>
    [Serializable]
    public class Subgraph
    {

        #region Fields
#pragma warning disable UAC1009
        /// <summary>
        /// Nodes contained in this subgraph.
        /// </summary>
        public HashSet<AssetNode> Nodes = new HashSet<AssetNode>();

        /// <summary>
        /// Source nodes that lead into this subgraph.
        /// </summary>
        public HashSet<AssetNode> Sources = new HashSet<AssetNode>();
#pragma warning restore UAC1009

        /// <summary>
        /// Hash representing the sources set.
        /// </summary>
        public int HashOfSources;
        #endregion
    }
}
