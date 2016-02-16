﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Sets;
using Waher.Script.Abstraction.Elements;

namespace Waher.Script.Objects
{
	/// <summary>
	/// Double-valued number.
	/// </summary>
	public sealed class DoubleNumber : FieldElement
	{
		private static readonly DoubleNumbers associatedField = new DoubleNumbers();

		private double value;

		/// <summary>
		/// Double-valued number.
		/// </summary>
		/// <param name="Value">Double value.</param>
		public DoubleNumber(double Value)
		{
			this.value = Value;
		}

		/// <summary>
		/// Double value.
		/// </summary>
		public double Value
		{
			get { return this.value; }
		}

		/// <summary>
		/// <see cref="Object.ToString()"/>
		/// </summary>
		public override string ToString()
		{
			return this.value.ToString().Replace(System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator, ".");
		}

		/// <summary>
		/// Associated Field.
		/// </summary>
		public override Field AssociatedField
		{
			get { return associatedField; }
		}

		/// <summary>
		/// Associated object value.
		/// </summary>
		public override object AssociatedObjectValue
		{
			get { return this.value; }
		}

		/// <summary>
		/// Tries to multiply an element to the current element.
		/// </summary>
		/// <param name="Element">Element to multiply.</param>
		/// <returns>Result, if understood, null otherwise.</returns>
		public override RingElement Multiply(CommutativeRingElement Element)
		{
			DoubleNumber E = Element as DoubleNumber;
			if (E == null)
				return null;
			else
				return new DoubleNumber(this.value * E.value);
		}

		/// <summary>
		/// Inverts the element, if possible.
		/// </summary>
		/// <returns>Inverted element, or null if not possible.</returns>
		public override RingElement Invert()
		{
			return new DoubleNumber(1.0 / this.value);
		}

		/// <summary>
		/// Tries to add an element to the current element.
		/// </summary>
		/// <param name="Element">Element to add.</param>
		/// <returns>Result, if understood, null otherwise.</returns>
		public override AbelianGroupElement Add(AbelianGroupElement Element)
		{
			DoubleNumber E = Element as DoubleNumber;
			if (E == null)
				return null;
			else
				return new DoubleNumber(this.value + E.value);
		}

		/// Negates the element.
		/// </summary>
		/// <returns>Negation of current element.</returns>
		public override GroupElement Negate()
		{
			return new DoubleNumber(-this.value);
		}

		/// <summary>
		/// <see cref="Object.Equals"/>
		/// </summary>
		public override bool Equals(object obj)
		{
			DoubleNumber E = obj as DoubleNumber;
			if (E == null)
				return false;
			else
				return this.value == E.value;
		}

		/// <summary>
		/// <see cref="Object.GetHashCode"/>
		/// </summary>
		public override int GetHashCode()
		{
			return this.value.GetHashCode();
		}
	}
}
