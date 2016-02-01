﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Networking.Sniffers
{
	/// <summary>
	/// How binary data is to be presented.
	/// </summary>
	public enum BinaryPresentationMethod
	{
		/// <summary>
		/// Has hexadecimal strings.
		/// </summary>
		Hexadecimal,

		/// <summary>
		/// Has base64 strings.
		/// </summary>
		Base64,

		/// <summary>
		/// Has simple byte counts.
		/// </summary>
		ByteCount
	}

	/// <summary>
	/// Outputs sniffed data to <see cref="Console.Out"/>.
	/// </summary>
	public class ConsoleOutSniffer : ISniffer
	{
		private const int TabWidth = 8;
		private BinaryPresentationMethod binaryPresentationMethod;

		/// <summary>
		/// Outputs sniffed data to <see cref="Console.Out"/>.
		/// </summary>
		/// <param name="BinaryPresentationMethod">How binary data is to be presented.</param>
		public ConsoleOutSniffer(BinaryPresentationMethod BinaryPresentationMethod)
		{
			this.binaryPresentationMethod = BinaryPresentationMethod;
		}

		public void TransmitText(string Text)
		{
			this.Output(Text, ConsoleColor.Black, ConsoleColor.White);
		}

		public void ReceiveText(string Text)
		{
			this.Output(Text, ConsoleColor.White, ConsoleColor.DarkBlue);
		}

		public void TransmitBinary(byte[] Data)
		{
			this.BinaryOutput(Data, ConsoleColor.Black, ConsoleColor.White);
		}

		public void ReceiveBinary(byte[] Data)
		{
			this.BinaryOutput(Data, ConsoleColor.White, ConsoleColor.DarkBlue);
		}

		private void BinaryOutput(byte[] Data, ConsoleColor Fg, ConsoleColor Bg)
		{
			switch (this.binaryPresentationMethod)
			{
				case BinaryPresentationMethod.Hexadecimal:
					StringBuilder Row = new StringBuilder();
					int i = 0;

					foreach (byte b in Data)
					{
						if (i > 0)
							Row.Append(' ');

						Row.Append(b.ToString("X2"));

						i = (i + 1) & 31;
						if (i == 0)
						{
							this.Output(Row.ToString(), Fg, Bg);
							Row.Clear();
						}
					}

					if (i != 0)
						this.Output(Row.ToString(), Fg, Bg);
					break;

				case BinaryPresentationMethod.Base64:
					this.Output(System.Convert.ToBase64String(Data), Fg, Bg);
					break;

				case BinaryPresentationMethod.ByteCount:
					this.Output("<" + Data.Length.ToString() + " bytes>", Fg, Bg);
					break;
			}
		}

		public void Information(string Comment)
		{
			this.Output(Comment, ConsoleColor.Yellow, ConsoleColor.DarkGreen);
		}

		public void Warning(string Warning)
		{
			this.Output(Warning, ConsoleColor.Black, ConsoleColor.Yellow);
		}

		public void Error(string Error)
		{
			this.Output(Error, ConsoleColor.Yellow, ConsoleColor.Red);
		}

		public void Exception(string Exception)
		{
			this.Output(Exception, ConsoleColor.White, ConsoleColor.DarkRed);
		}

		private void Output(string s, ConsoleColor Fg, ConsoleColor Bg)
		{
			ConsoleColor FgBak = Console.ForegroundColor;
			ConsoleColor BgBak = Console.BackgroundColor;

			Console.ForegroundColor = Fg;
			Console.BackgroundColor = Bg;

			try
			{
				int w = Console.WindowWidth;
				int i;

				if (s.IndexOf('\t') >= 0)
				{
					StringBuilder sb = new StringBuilder();
					string[] Parts = s.Split('\t');
					bool First = true;

					foreach (string Part in Parts)
					{
						if (First)
							First = false;
						else
						{
							i = Console.CursorLeft % TabWidth;
							sb.Append(new string(' ', TabWidth - i));
						}

						sb.Append(Part);
					}

					s = sb.ToString();
				}

				i = s.Length % w;

				if (i > 0)
					s += new string(' ', w - i);

				Console.Out.Write(s);
			}
			catch (Exception)
			{
				Console.Out.WriteLine(s);
			}

			Console.ForegroundColor = Fg;
			Console.BackgroundColor = Bg;
		}

	}
}
