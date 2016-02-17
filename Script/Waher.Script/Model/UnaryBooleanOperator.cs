﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Abstraction.Sets;
using Waher.Script.Exceptions;
using Waher.Script.Objects;

namespace Waher.Script.Model
{
	/// <summary>
	/// Base class for unary boolean operators.
	/// </summary>
	public abstract class UnaryBooleanOperator : UnaryScalarOperator
	{
		/// <summary>
		/// Base class for binary boolean operators.
		/// </summary>
		/// <param name="Operand">Operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public UnaryBooleanOperator(ScriptNode Operand, int Start, int Length)
			: base(Operand, Start, Length)
		{
		}

		/// <summary>
		/// Evaluates the node, using the variables provided in the <paramref name="Variables"/> collection.
		/// </summary>
		/// <param name="Variables">Variables collection.</param>
		/// <returns>Result.</returns>
		public override IElement Evaluate(Variables Variables)
		{
			IElement Op = this.op.Evaluate(Variables);
			BooleanValue BOp = Op as BooleanValue;

			if (BOp != null)
				return this.Evaluate(BOp.Value);
			else
				return this.Evaluate(Op);
		}

		/// <summary>
		/// Evaluates the operator on scalar operands.
		/// </summary>
		/// <param name="Operand">Operand.</param>
		/// <returns>Result</returns>
		public override IElement EvaluateScalar(IElement Operand)
		{
			BooleanValue BOp = Operand as BooleanValue;

			if (BOp != null)
				return this.Evaluate(BOp.Value);
			else
				throw new ScriptRuntimeException("Scalar operands must be boolean values.", this);
		}

		/// <summary>
		/// Evaluates the boolean operator.
		/// </summary>
		/// <param name="Operand">Operand.</param>
		/// <returns>Result</returns>
		public abstract IElement Evaluate(bool Operand);

	}
}
