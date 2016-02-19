﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Model;
using Waher.Script.Objects;

namespace Waher.Script.Operators.Comparisons
{
	/// <summary>
	/// Element-Wise Equal To.
	/// </summary>
	public class EqualToElementWise : BinaryScalarOperator 
	{
		/// <summary>
		/// Element-Wise Equal To.
		/// </summary>
		/// <param name="Left">Left operand.</param>
		/// <param name="Right">Right operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public EqualToElementWise(ScriptNode Left, ScriptNode Right, int Start, int Length)
			: base(Left, Right, Start, Length)
		{
		}

        /// <summary>
        /// Evaluates the operator on scalar operands.
        /// </summary>
        /// <param name="Left">Left value.</param>
        /// <param name="Right">Right value.</param>
        /// <returns>Result</returns>
        public override IElement EvaluateScalar(IElement Left, IElement Right)
        {
            if (Left.Equals(Right))
                return BooleanValue.True;
            else
                return BooleanValue.False;
        }
    }
}
