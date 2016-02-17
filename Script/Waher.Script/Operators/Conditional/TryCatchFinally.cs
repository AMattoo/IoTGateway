﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Model;

namespace Waher.Script.Operators.Conditional
{
	/// <summary>
	/// Try-Catch-Finally operator.
	/// </summary>
	public class TryCatchFinally : TernaryOperator
	{
		/// <summary>
		/// Try-Catch-Finally operator.
		/// </summary>
		/// <param name="Statement">Statement.</param>
		/// <param name="CatchStatement">Catch statement.</param>
		/// <param name="FinallyStatement">Finally statement.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public TryCatchFinally(ScriptNode Statement, ScriptNode CatchStatement, ScriptNode FinallyStatement, int Start, int Length)
			: base(Statement, CatchStatement, FinallyStatement, Start, Length)
		{
		}

		/// <summary>
		/// Evaluates the node, using the variables provided in the <paramref name="Variables"/> collection.
		/// </summary>
		/// <param name="Variables">Variables collection.</param>
		/// <returns>Result.</returns>
		public override IElement Evaluate(Variables Variables)
		{
			throw new NotImplementedException();	// TODO: Implement
		}
	}
}
