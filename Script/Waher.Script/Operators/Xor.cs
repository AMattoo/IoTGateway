﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Model;

namespace Waher.Script.Operators
{
	/// <summary>
	/// Unspecified Exclusive Or.
	/// </summary>
	public class Xor : BinaryOperator 
	{
		/// <summary>
		/// Unspecified Exclusive Or.
		/// </summary>
		/// <param name="Left">Left operand.</param>
		/// <param name="Right">Right operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public Xor(ScriptNode Left, ScriptNode Right, int Start, int Length)
			: base(Left, Right, Start, Length)
		{
		}
	}
}
