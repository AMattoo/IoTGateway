﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Model;
using Waher.Script.Operators.Matrices;

namespace Waher.Script.Operators.Assignments
{
	/// <summary>
	/// Matrix Index Assignment operator.
	/// </summary>
	public class MatrixIndexAssignment : UnaryOperator 
	{
		MatrixIndex matrixIndex;

		/// <summary>
		/// Matrix Index Assignment operator.
		/// </summary>
		/// <param name="MatrixIndex">Matrix Index</param>
		/// <param name="Operand">Operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public MatrixIndexAssignment(MatrixIndex MatrixIndex, ScriptNode Operand, int Start, int Length)
			: base(Operand, Start, Length)
		{
			this.matrixIndex = MatrixIndex;
		}

		/// <summary>
		/// Matrix Index.
		/// </summary>
		public MatrixIndex MatrixIndex
		{
			get { return this.matrixIndex; }
		}

	}
}
