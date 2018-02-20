﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Content.Html.Elements
{
	/// <summary>
	/// SUP element
	/// </summary>
    public class Sup : HtmlElement
    {
		/// <summary>
		/// SUP element
		/// </summary>
		/// <param name="Parent">Parent element. Can be null for root elements.</param>
		public Sup(HtmlElement Parent)
			: base(Parent, "SUP")
		{
		}
    }
}
