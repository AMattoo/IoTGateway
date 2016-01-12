﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Content.Markdown.Model.BlockElements
{
	/// <summary>
	/// Represents a bullet list in a markdown document.
	/// </summary>
	public class BulletList : MarkdownElementChildren
	{
		/// <summary>
		/// Represents a bullet list in a markdown document.
		/// </summary>
		/// <param name="Document">Markdown document.</param>
		/// <param name="Children">Child elements.</param>
		public BulletList(MarkdownDocument Document, IEnumerable<MarkdownElement> Children)
			: base(Document, Children)
		{
		}

		/// <summary>
		/// Represents a bullet list in a markdown document.
		/// </summary>
		/// <param name="Document">Markdown document.</param>
		/// <param name="Children">Child elements.</param>
		public BulletList(MarkdownDocument Document, params MarkdownElement[] Children)
			: base(Document, Children)
		{
		}

		/// <summary>
		/// Generates HTML for the markdown element.
		/// </summary>
		/// <param name="Output">HTML will be output here.</param>
		public override void GenerateHTML(StringBuilder Output)
		{
			Output.AppendLine("<ul>");

			foreach (MarkdownElement E in this.Children)
				E.GenerateHTML(Output);

			Output.AppendLine("</ul>");
		}

		/// <summary>
		/// Generates plain text for the markdown element.
		/// </summary>
		/// <param name="Output">Plain text will be output here.</param>
		public override void GeneratePlainText(StringBuilder Output)
		{
			StringBuilder sb = new StringBuilder();
			string s;
			string s2 = Environment.NewLine + Environment.NewLine;
			bool LastIsParagraph = false;

			s = Output.ToString();
			if (!s.EndsWith(Environment.NewLine) && !string.IsNullOrEmpty(s))
				Output.AppendLine();

			foreach (MarkdownElement E in this.Children)
			{
				E.GeneratePlainText(sb);
				s = sb.ToString();
				sb.Clear();
				Output.Append(s);

				LastIsParagraph = s.EndsWith(s2);
			}

			if (!LastIsParagraph)
				Output.AppendLine();
		}
	}
}
