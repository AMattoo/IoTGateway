﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Elements;

namespace Waher.Script.Abstraction.Sets
{
	/// <summary>
	/// Base class for all types of groups.
	/// </summary>
	public abstract class Group : SemiGroup
	{
		/// <summary>
		/// Base class for all types of groups.
		/// </summary>
		public Group()
			: base()
		{
		}

		/// <summary>
		/// Subtracts the right group element from the left one: Left+(-Right)
		/// </summary>
		/// <param name="Left">Left element.</param>
		/// <param name="Right">Right element.</param>
		/// <returns>Result, if understood, null otherwise.</returns>
		public virtual GroupElement RightSubtract(GroupElement Left, GroupElement Right)
		{
			return this.Add(Left, Right.Negate()) as GroupElement;
		}

		/// <summary>
		/// Subtracts the left group element from the right one: (-Left)+Right.
		/// </summary>
		/// <param name="Left">Left element.</param>
		/// <param name="Right">Right element.</param>
		/// <returns>Result, if understood, null otherwise.</returns>
		public virtual GroupElement LeftSubtract(GroupElement Left, GroupElement Right)
		{
			return this.Add(Left.Negate(), Right) as GroupElement;
		}

		/// <summary>
		/// If the group + operator is commutative or not.
		/// </summary>
		public abstract bool IsAbelian
		{
			get;
		}

		/// <summary>
		/// Returns the additive identity of the group.
		/// </summary>
		public abstract GroupElement AdditiveIdentity
		{
			get;
		}

	}
}
