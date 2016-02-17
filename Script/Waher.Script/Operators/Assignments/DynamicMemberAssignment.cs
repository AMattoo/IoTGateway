﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Model;
using Waher.Script.Operators.Membership;

namespace Waher.Script.Operators.Assignments
{
	/// <summary>
	/// Dynamic member Assignment operator.
	/// </summary>
	public class DynamicMemberAssignment : UnaryOperator 
	{
		DynamicMember namedMember;

		/// <summary>
		/// Dynamic member Assignment operator.
		/// </summary>
		/// <param name="DynamicMember">Dynamic member</param>
		/// <param name="Operand">Operand.</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public DynamicMemberAssignment(DynamicMember DynamicMember, ScriptNode Operand, int Start, int Length)
			: base(Operand, Start, Length)
		{
			this.namedMember = DynamicMember;
		}

		/// <summary>
		/// Dynamic member.
		/// </summary>
		public DynamicMember DynamicMember
		{
			get { return this.namedMember; }
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
