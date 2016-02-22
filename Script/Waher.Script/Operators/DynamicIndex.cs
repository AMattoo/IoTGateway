﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Model;

namespace Waher.Script.Operators
{
	/// <summary>
	/// Dynamic index operator
	/// </summary>
	public class DynamicIndex : UnaryOperator 
	{
		/// <summary>
		/// Index
		/// </summary>
		protected ElementList index;

		/// <summary>
		/// Dynamic index operator
		/// </summary>
		/// <param name="Left">Left operand.</param>
		/// <param name="Right">Right operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public DynamicIndex(ScriptNode Left, ElementList Right, int Start, int Length)
			: base(Left, Start, Length)
		{
			this.index = Right;
		}

		/// <summary>
		/// Index operand.
		/// </summary>
		public ElementList IndexOperand
		{
			get { return this.index; }
		}

		/// <summary>
		/// Evaluates the node, using the variables provided in the <paramref name="Variables"/> collection.
		/// </summary>
		/// <param name="Variables">Variables collection.</param>
		/// <returns>Result.</returns>
		public override IElement Evaluate(Variables Variables)
		{
            throw new NotImplementedException();
		}

	}
}
