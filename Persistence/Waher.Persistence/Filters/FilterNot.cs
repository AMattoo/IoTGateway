﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Waher.Persistence.Filters
{
	/// <summary>
	/// This filter selects objects that does not conform to the child-filter provided.
	/// </summary>
	public class FilterNot : FilterChild 
	{
		/// <summary>
		/// This filter selects objects that does not conform to the child-filter provided.
		/// </summary>
		/// <param name="Filters">Child filter.</param>
		public FilterNot(Filter Filter)
			: base(Filter)
		{
		}
	}
}
