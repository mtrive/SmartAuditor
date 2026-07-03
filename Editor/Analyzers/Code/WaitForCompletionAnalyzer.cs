// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
	sealed class WaitForCompletionAnalyzer : CodeAnalyzer
	{
		internal const string ADR0013 = nameof(ADR0013);

		static readonly Descriptor WaitForCompletionDescriptor = new Descriptor
		(
            ADR0013,
			"Addressables: WaitForCompletion Blocks the Calling Thread",
			Impact.Performance,
			"<b>AsyncOperationHandle.WaitForCompletion</b> blocks the calling thread until the async operation finishes. When called on the main thread it stalls the frame and produces a visible hitch on first access.",
			"Await the handle's <b>Task</b>, yield on the handle in a coroutine, or attach a <b>Completed</b> callback. Reserve <b>WaitForCompletion</b> for editor tooling or initialization code where blocking is intentional."
		)
		{
			MessageFormat = "'{0}' blocks the calling thread until completion",
			DefaultSeverity = Severity.Moderate
		};

		readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };
		public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

		public override void Analyze(InstructionAnalysisContext context)
		{
			var callee = context.Instruction.Operand as MethodReference;
			if (callee == null)
				return;

			// We want to catch both the AsyncOperationHandle extension method and any instance variants
			// present in Resource Management/Addressables packages.
			if (callee.Name != "WaitForCompletion")
				return;

			var declaringType = callee.DeclaringType;
			if (declaringType == null)
				return;

			var fullName = declaringType.FullName;
			// Common namespaces for Addressables/Resource Management APIs
			if (!fullName.StartsWith("UnityEngine.ResourceManagement"))
			{
				// Some versions surface extension methods from Addressables namespace
				if (!fullName.StartsWith("UnityEngine.AddressableAssets"))
					return;
			}

			var diagnostic = Diagnostic.Create(AnalysisCategory.Code, WaitForCompletionDescriptor.Id, callee.Name);
			context.ReportIssue(diagnostic);
		}
	}
}


