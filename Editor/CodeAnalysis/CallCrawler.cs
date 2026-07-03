using System;
using System.Collections.Generic;
using System.Threading;
using Mono.Cecil;
using UnityEngine.Profiling;

namespace SmartAuditor.Editor.CodeAnalysis
{
    public class CallInfo
    {
        public MethodReference Callee { get; }
        public MethodReference Caller { get; }
        public Location Location { get; }
        public bool IsPerfCriticalContext { get; }
        public CallTreeNode Hierarchy { get; set; }

        public CallInfo(
            MethodReference callee,
            MethodReference caller,
            Location location,
            bool isPerfCriticalContext)
        {
            Callee = callee;
            Caller = caller;
            Location = location;
            IsPerfCriticalContext = isPerfCriticalContext;
        }

        public override bool Equals(object obj)
        {
            var other = obj as CallInfo;
            if (other == null)
            {
                return false;
            }

            return other.Callee == Callee &&
                other.Caller == Caller;
        }

        public override int GetHashCode()
        {
            return Callee.GetHashCode()
                + Caller.GetHashCode();
        }
    }

    public class CallCrawler
    {
        const int k_MaxDepth = 10;

        // key: callee name, value: lists of all callers
        readonly Dictionary<string, List<CallInfo>> m_BucketedCalls =
            new Dictionary<string, List<CallInfo>>();

        public void Add(CallInfo callInfo)
        {
            var key = callInfo.Callee.FastFullName();
            List<CallInfo> calls;
            if (!m_BucketedCalls.TryGetValue(key, out calls))
            {
                calls = new List<CallInfo>();
                m_BucketedCalls.Add(key, calls);
            }
            calls.Add(callInfo);
        }

        public bool BuildCallHierarchies(List<ReportItem> issues, IProgress progress = null, CancellationToken cancellationToken = default)
        {
            if (issues.Count > 0)
            {
                Profiler.BeginSample("CallCrawler.BuildCallHierarchies");

                progress?.Start("Building Call Hierarchies", string.Empty, issues.Count);

                foreach (var issue in issues)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (progress != null)
                            progress.Clear();
                        Profiler.EndSample();
                        return false;
                    }
                    progress?.Advance(GetCallHierarchyAdvanceDescription(issue));

                    const int depth = 0;
                    var root = issue.Dependencies;
                    BuildHierarchy(root as CallTreeNode, depth, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (progress != null)
                            progress.Clear();
                        Profiler.EndSample();
                        return false;
                    }

                    // temp fix for null location (code analysis was unable to get sequence point)
                    if (issue.Location == null)
                        issue.Location = root.Location;
                }
                if (progress != null)
                    progress.Clear();

                Profiler.EndSample();
            }

            return true;
        }

        static string GetCallHierarchyAdvanceDescription(ReportItem issue)
        {
            if (issue.Dependencies is CallTreeNode callTree)
                return callTree.GetPrettyName();

            if (!string.IsNullOrEmpty(issue.Description))
                return issue.Description;

            return issue.RelativePath;
        }

        void BuildHierarchy(CallTreeNode callee, int depth, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || callee == null)
                return;

            // this check should be removed. Instead, the deep callstacks should be built on-demand
            if (depth++ == k_MaxDepth)
                return;

            // let's find all callers with matching callee
            List<CallInfo> callPairs;
            if (m_BucketedCalls.TryGetValue(callee.MethodFullName, out callPairs))
            {
                var childrenCount = callPairs.Count;
                var children = new DependencyNode[childrenCount];

                for (var i = 0; i < childrenCount; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var call = callPairs[i];
                    if (call.Hierarchy != null)
                    {
                        // use previously built hierarchy
                        children[i] = call.Hierarchy;
                        continue;
                    }

                    var callerName = call.Caller.FastFullName();
                    var hierarchy = new CallTreeNode(call.Caller)
                    {
                        Location = call.Location, PerfCriticalContext = call.IsPerfCriticalContext
                    };

                    // stop recursion, if applicable (note that this only prevents recursion when a method calls itself)
                    if (!callerName.Equals(callee.MethodFullName))
                        BuildHierarchy(hierarchy, depth, cancellationToken);

                    children[i] = hierarchy;
                    call.Hierarchy = hierarchy;
                }
                callee.AddChildren(children);
            }
        }

        /// <summary>
        /// Enumerate every recorded call site whose callee matches <paramref name="calleeFilter"/>.
        /// </summary>
        /// <remarks>
        /// All entries in a bucket share the same callee key, so the filter is evaluated once
        /// per bucket and matched buckets are then yielded in full.
        /// </remarks>
        /// <param name="calleeFilter">Predicate applied to the bucket's callee.</param>
        public IEnumerable<CallInfo> EnumerateCalls(Func<MethodReference, bool> calleeFilter)
        {
            if (calleeFilter == null)
            {
                yield break;
            }

            foreach (var bucket in m_BucketedCalls.Values)
            {
                if (bucket.Count == 0)
                {
                    continue;
                }
                if (!calleeFilter(bucket[0].Callee))
                {
                    continue;
                }
                foreach (var call in bucket)
                {
                    yield return call;
                }
            }
        }

        /// <summary>
        /// Walk the caller chain backwards from <paramref name="startMethod"/> and return the
        /// first ancestor that satisfies <paramref name="isRoot"/>. The starting method itself
        /// is not tested; callers should test it explicitly if that case matters.
        /// </summary>
        /// <remarks>
        /// Bounded by <see cref="k_MaxDepth"/>. A visited set prevents revisiting methods
        /// already explored on this walk so cycles and shared sub-graphs do not cause
        /// repeated work.
        /// </remarks>
        /// <param name="startMethod">Method whose callers are walked.</param>
        /// <param name="isRoot">Predicate identifying the entry point we are looking for.</param>
        /// <param name="matchedRoot">Set to the first matching caller when the method returns true.</param>
        public bool TryFindRootAncestor(
            MethodReference startMethod,
            Func<MethodReference, bool> isRoot,
            out MethodReference matchedRoot)
        {
            matchedRoot = null;
            if (startMethod == null || isRoot == null)
            {
                return false;
            }

            var visited = new HashSet<string>();
            return TryFindRootAncestorRecursive(
                startMethod.FastFullName(),
                isRoot,
                depth: 0,
                visited,
                out matchedRoot);
        }

        bool TryFindRootAncestorRecursive(
            string calleeFullName,
            Func<MethodReference, bool> isRoot,
            int depth,
            HashSet<string> visited,
            out MethodReference matchedRoot)
        {
            matchedRoot = null;
            if (depth >= k_MaxDepth)
            {
                return false;
            }
            if (!visited.Add(calleeFullName))
            {
                return false;
            }
            if (!m_BucketedCalls.TryGetValue(calleeFullName, out var callPairs))
            {
                return false;
            }

            foreach (var call in callPairs)
            {
                if (isRoot(call.Caller))
                {
                    matchedRoot = call.Caller;
                    return true;
                }
            }

            foreach (var call in callPairs)
            {
                if (TryFindRootAncestorRecursive(
                    call.Caller.FastFullName(),
                    isRoot,
                    depth + 1,
                    visited,
                    out matchedRoot))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
