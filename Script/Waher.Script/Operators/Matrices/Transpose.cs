﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Model;

namespace Waher.Script.Operators.Matrices
{
	/// <summary>
	/// Transpose operator.
	/// </summary>
	public class Transpose : UnaryOperator 
	{
		/// <summary>
		/// Transpose operator.
		/// </summary>
		/// <param name="Operand">Operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public Transpose(ScriptNode Operand, int Start, int Length)
			: base(Operand, Start, Length)
		{
		}
	}
}
