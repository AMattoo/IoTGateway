﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Networking.XMPP.Contracts.Search
{
	/// <summary>
	/// Abstract base class for search filters relating to the roles of contracts.
	/// </summary>
	public abstract class FilterRole : SearchFilter
	{
		/// <summary>
		/// Abstract base class for search filters relating to the roles of contracts.
		/// </summary>
		/// <param name="Operands">Operands</param>
		public FilterRole(params SearchFilterOperand[] Operands)
				: base(Operands)
		{
		}

		/// <summary>
		/// Local XML element name of filter.
		/// </summary>
		public override string ElementName => "role";
	}
}
