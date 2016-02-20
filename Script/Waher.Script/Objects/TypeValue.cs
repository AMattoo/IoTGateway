﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Sets;
using Waher.Script.Abstraction.Elements;

namespace Waher.Script.Objects
{
	/// <summary>
	/// Type value.
	/// </summary>
	public sealed class TypeValue : Element
	{
        private static readonly TypeValues associatedSet = new TypeValues();

        private Type value;

		/// <summary>
		/// Type value.
		/// </summary>
		/// <param name="Value">Type value.</param>
		public TypeValue(Type Value)
		{
			this.value = Value;
		}

		/// <summary>
		/// Type value.
		/// </summary>
		public Type Value
		{
			get { return this.value; }
		}

		/// <summary>
		/// <see cref="Type.ToString()"/>
		/// </summary>
		public override string ToString()
		{
			return this.value.FullName;
		}

		/// <summary>
		/// Associated Set.
		/// </summary>
		public override ISet AssociatedSet
		{
			get { return associatedSet; }
		}

		/// <summary>
		/// Associated Type value.
		/// </summary>
		public override object AssociatedObjectValue
		{
			get { return this.value; }
		}

		/// <summary>
		/// <see cref="Type.Equals"/>
		/// </summary>
		public override bool Equals(object obj)
		{
			TypeValue E = obj as TypeValue;
			if (E == null)
				return false;
			else
				return this.value.Equals(E.value);
		}

		/// <summary>
		/// <see cref="Type.GetHashCode"/>
		/// </summary>
		public override int GetHashCode()
		{
			if (this.value == null)
				return 0;
			else
				return this.value.GetHashCode();
		}

    }
}
