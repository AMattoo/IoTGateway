﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Model;

namespace Waher.Script.Operators.Comparisons
{
	/// <summary>
	/// Lesser Than Or Equal To.
	/// </summary>
	public class LesserThanOrEqualTo : BinaryOperator 
	{
		/// <summary>
		/// Lesser Than Or Equal To.
		/// </summary>
		/// <param name="Left">Left operand.</param>
		/// <param name="Right">Right operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public LesserThanOrEqualTo(ScriptNode Left, ScriptNode Right, int Start, int Length)
			: base(Left, Right, Start, Length)
		{
		}
	}
}
