// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using Mono.Cecil;
using SmartAuditor.Editor;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// A method node in an upward call tree: the root is the method where a finding occurs; each
    /// child is a direct caller (<see cref="CallCrawler"/> builds the chain).
    /// </summary>
    public sealed class CallTreeNode : DependencyNode
    {
        /// <summary>
        /// Assembly name.
        /// </summary>
        public readonly string AssemblyName;

        /// <summary>
        /// Full name of the type, including namespace.
        /// </summary>
        public readonly string TypeFullName;

        /// <summary>
        /// Canonical method identity used as the call-graph lookup key (see <see cref="MemberReferenceExtension.FastFullName"/>).
        /// </summary>
        public readonly string MethodFullName;

        /// <summary>
        /// User-friendly name of the type.
        /// </summary>
        public readonly string PrettyTypeName;

        /// <summary>
        /// User-friendly name of the method.
        /// </summary>
        public readonly string PrettyMethodName;

        /// <summary>
        /// Creates a call-tree node for the given method.
        /// </summary>
        /// <param name="methodReference">Method metadata to represent, or null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="methodReference"/> is null.</exception>
        public CallTreeNode(MethodReference methodReference)
        {
            if (methodReference == null)
            {
                throw new ArgumentNullException(nameof(methodReference));
            }

            MethodFullName = methodReference.FastFullName();
            AssemblyName = methodReference.Module?.Name ?? string.Empty;

            var declaringType = methodReference.DeclaringType;
            TypeFullName = declaringType?.FullName ?? string.Empty;

            if (declaringType != null)
            {
                (PrettyTypeName, PrettyMethodName) = CreatePrettyNames(methodReference, declaringType, MethodFullName);
            }
            else
            {
                PrettyMethodName = "(anonymous)";
            }

            PerfCriticalContext = false;
        }

        static (string prettyTypeName, string prettyMethodName) CreatePrettyNames(
            MethodReference methodReference,
            TypeReference declaringType,
            string methodFullName)
        {
            var declaringTypeFullName = declaringType.FullName;
            var prettyMethodName = "(anonymous)";

            // check if it's a coroutine or other compiler-generated nested type
            if (declaringTypeFullName.IndexOf("/<", StringComparison.Ordinal) >= 0)
            {
                var methodStartIndex = declaringTypeFullName.IndexOf("<", StringComparison.Ordinal) + 1;
                if (methodStartIndex > 0)
                {
                    var length = declaringTypeFullName.IndexOf(">", StringComparison.Ordinal) - methodStartIndex;
                    var prettyTypeName = declaringTypeFullName.Substring(
                        0, declaringTypeFullName.IndexOf("/", StringComparison.Ordinal));
                    if (length > 0)
                    {
                        prettyMethodName = declaringTypeFullName.Substring(methodStartIndex, length);
                    }
                    else
                    {
                        // handle example: System.Int32 DelegateTest/<>c::<Update>b__1_0()
                        methodStartIndex = methodFullName.LastIndexOf("<", StringComparison.Ordinal) + 1;
                        if (methodStartIndex > 0)
                        {
                            length = methodFullName.LastIndexOf(">", StringComparison.Ordinal) - methodStartIndex;
                            prettyMethodName = methodFullName.Substring(methodStartIndex, length) + ".(anonymous)";
                        }
                    }

                    return (prettyTypeName, prettyMethodName);
                }

                // for some reason, some generated types don't have the same syntax
                return (declaringTypeFullName, prettyMethodName);
            }

            return (declaringType.Name, methodReference.Name);
        }

        public override string GetName()
        {
            return MethodFullName;
        }

        public override string GetPrettyName()
        {
            if (string.IsNullOrEmpty(PrettyTypeName))
            {
                return MethodFullName;
            }

            return $"{PrettyTypeName}.{PrettyMethodName}";
        }
    }
}
