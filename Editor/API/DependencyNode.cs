// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Represents a node in a dependency tree attached to a report item.
    /// </summary>
    /// <remarks>
    /// Subsystems use this type for different graph shapes. Edge direction is not uniform:
    /// <list type="bullet">
    /// <item><description>Call trees (<see cref="CodeAnalysis.CallTreeNode"/>): the root is the method where a finding occurs; each child is a direct caller — the tree walks upward through the call stack.</description></item>
    /// <item><description>Asset dependency trees (<see cref="Core.AssetDependencyNode"/>): nodes represent assets; children point toward the Resources asset that pulled them in.</description></item>
    /// </list>
    /// When any descendant is performance-critical, <see cref="PerfCriticalContext"/> is propagated to ancestors via <see cref="AddChild"/> and <see cref="AddChildren"/>.
    /// </remarks>
    public abstract class DependencyNode
    {
        List<DependencyNode> m_Children = new List<DependencyNode>(1);

        /// <summary>
        /// The location represented by this node.
        /// </summary>
        public Location Location { get; set; }

        /// <summary>
        /// Whether this node forms part of a performance-critical context.
        /// </summary>
        /// <remarks>
        /// For code call trees, this is true when the call stack includes a known hot-path entry point
        /// (for example, a MonoBehaviour.Update()). The flag is propagated to ancestor nodes when children are added.
        /// </remarks>
        public bool PerfCriticalContext { get; set; }

        /// <summary>
        /// This node's name.
        /// </summary>
        public string Name => GetName();

        /// <summary>
        /// A prettified, UI-friendly version of this node's name.
        /// </summary>
        public string PrettyName => GetPrettyName();

        /// <summary>
        /// Whether this node has at least one child.
        /// </summary>
        public bool HasChildren => m_Children.Count > 0;

        /// <summary>
        /// Gets the number of children that this node has.
        /// </summary>
        public int ChildCount => m_Children.Count;

        /// <summary>
        /// Adds a child to this node.
        /// </summary>
        /// <param name="child">The node to add as a child of this one.</param>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
        public void AddChild(DependencyNode child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child.PerfCriticalContext)
                PerfCriticalContext = true;

            m_Children.Add(child);
        }

        /// <summary>
        /// Adds multiple children to this node.
        /// </summary>
        /// <param name="children">Nodes to add as children of this one.</param>
        /// <exception cref="ArgumentNullException"><paramref name="children"/> or one of its elements is null.</exception>
        public void AddChildren(DependencyNode[] children)
        {
            if (children == null)
                throw new ArgumentNullException(nameof(children));

            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null)
                    throw new ArgumentNullException(nameof(children));

                if (child.PerfCriticalContext)
                    PerfCriticalContext = true;
            }

            m_Children.AddRange(children);
        }

        /// <summary>
        /// Gets a child node with the specified index.
        /// </summary>
        /// <param name="index">The index into the node's child list (defaults to 0).</param>
        /// <returns>The child node with the given index.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the child list bounds.</exception>
        public DependencyNode GetChild(int index = 0)
        {
            if ((uint)index >= (uint)m_Children.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return m_Children[index];
        }

        /// <summary>
        /// Gets the node's "raw" name.
        /// </summary>
        /// <returns>The node's name.</returns>
        public abstract string GetName();

        /// <summary>
        /// Gets the node's "pretty" name, suitable for UI display.
        /// </summary>
        /// <returns>The node's prettified name.</returns>
        public abstract string GetPrettyName();
    }
}
