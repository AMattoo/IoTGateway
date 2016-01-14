﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Content;

namespace Waher.Networking.XMPP.DataForms.DataTypes
{
	/// <summary>
	/// Decimal Data Type (xs:decimal)
	/// </summary>
	public class DecimalDataType : DataType
	{
		/// <summary>
		/// Decimal Data Type (xs:decimal)
		/// </summary>
		/// <param name="TypeName">Type Name</param>
		public DecimalDataType(string DataType)
			: base(DataType)
		{
		}

		/// <summary>
		/// <see cref="DataType.Parse"/>
		/// </summary>
		internal override object Parse(string Value)
		{
			decimal Result;
				
			if (CommonTypes.TryParse(Value, out Result))
				return Result;
			else
				return null;
		}
	}
}
