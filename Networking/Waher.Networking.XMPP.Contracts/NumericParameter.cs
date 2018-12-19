﻿using System;
using System.Text;
using Waher.Content;
using Waher.Content.Xml;
using Waher.Networking.XMPP.Contracts.HumanReadable;

namespace Waher.Networking.XMPP.Contracts
{
	/// <summary>
	/// Numerical contractual parameter
	/// </summary>
	public class NumericalParameter : Parameter
	{
		private double value;

		/// <summary>
		/// Parameter value
		/// </summary>
		public double Value
		{
			get => this.value;
			set => this.value = value;
		}

		/// <summary>
		/// Parameter value.
		/// </summary>
		public override object ObjectValue => this.value;

		/// <summary>
		/// Serializes the parameter, in normalized form.
		/// </summary>
		/// <param name="Xml">XML Output</param>
		public override void Serialize(StringBuilder Xml)
		{
			Xml.Append("<numericalParameter name=\"");
			Xml.Append(XML.Encode(this.Name));
			Xml.Append("\" value=\"");
			Xml.Append(CommonTypes.Encode(this.value));

			if (this.Descriptions is null || this.Descriptions.Length == 0)
				Xml.Append("\"/>");
			else
			{
				Xml.Append("\">");

				foreach (HumanReadableText Description in this.Descriptions)
					Description.Serialize(Xml, "description", false);

				Xml.Append("</numericalParameter>");
			}
		}
	}
}
