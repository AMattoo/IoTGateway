﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Waher.Persistence.Filters
{
	/// <summary>
	/// This filter selects objects that have a named field not equal to a given value.
	/// </summary>
	public class FilterFieldNotEqualTo : FilterFieldValue
	{
		/// <summary>
		/// This filter selects objects that have a named field not equal to a given value.
		/// </summary>
		/// <param name="FieldName">Field Name.</param>
		/// <param name="Value">Value.</param>
		public FilterFieldNotEqualTo(string FieldName, object Value)
			: base(FieldName, Value)
		{
		}
	}
}
