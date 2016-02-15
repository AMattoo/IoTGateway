﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Model;

namespace Waher.Script.Operators.Assignments
{
	/// <summary>
	/// Dynamic Index Assignment operator.
	/// </summary>
	public class DynamicIndexAssignment : UnaryOperator 
	{
		DynamicIndex dynamicIndex;

		/// <summary>
		/// Dynamic Index Assignment operator.
		/// </summary>
		/// <param name="DynamicIndex">Dynamic Index</param>
		/// <param name="Operand">Operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public DynamicIndexAssignment(DynamicIndex DynamicIndex, ScriptNode Operand, int Start, int Length)
			: base(Operand, Start, Length)
		{
			this.dynamicIndex = DynamicIndex;
		}

		/// <summary>
		/// Dynamic Index.
		/// </summary>
		public DynamicIndex DynamicIndex
		{
			get { return this.dynamicIndex; }
		}

	}
}
