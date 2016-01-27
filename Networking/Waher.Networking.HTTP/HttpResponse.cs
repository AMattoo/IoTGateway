﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using Waher.Content;
using Waher.Networking.HTTP.TransferEncodings;

namespace Waher.Networking.HTTP
{
	/// <summary>
	/// Sends a HTTP response back to a client.
	/// </summary>
	public class HttpResponse : TextWriter
	{
		private Dictionary<string, string> customHeaders = null;
		private Encoding encoding = Encoding.UTF8;
		private DateTimeOffset date = DateTimeOffset.Now;
		private DateTimeOffset? expires = null;
		private string server = null;
		private string contentLanguage = null;
		private string contentType = null;
		private string statusMessage = "OK";
		private long? contentLength = null;
		private int statusCode = 200;
		private bool responseSent = false;
		private bool onlyHeader = false;

		private NetworkStream responseStream;
		private TransferEncoding transferEncoding = null;

		/// <summary>
		/// Sends a HTTP response back to a client.
		/// </summary>
		/// <param name="ResponseStream">Underlying response stream.</param>
		public HttpResponse(NetworkStream ResponseStream)
			: base()
		{
			this.responseStream = ResponseStream;
		}

		private void AssertHeaderOpen()
		{
			if (this.transferEncoding != null)
				throw new ArgumentException("Response header written, and cannot be changed.");
		}

		/// <summary>
		/// The Date general-header field represents the date and time at which the message was originated.
		/// </summary>
		public DateTimeOffset Date
		{
			get
			{
				return this.date;
			}

			set
			{
				this.AssertHeaderOpen();
				this.date = value;
			}
		}

		/// <summary>
		/// The Expires entity-header field gives the date/time after which the response is considered stale.
		/// </summary>
		public DateTimeOffset? Expires
		{
			get
			{
				return this.expires;
			}

			set
			{
				this.AssertHeaderOpen();
				this.expires = value;
			}
		}

		/// <summary>
		/// The Server response-header field contains information about the software used by the origin server to handle the request.
		/// </summary>
		public string Server
		{
			get
			{
				return this.server;
			}

			set
			{
				this.AssertHeaderOpen();
				this.server = value;
			}
		}

		/// <summary>
		/// The Content-Language entity-header field describes the natural language(s) of the intended audience for the enclosed entity.
		/// </summary>
		public string ContentLanguage
		{
			get
			{
				return this.contentLanguage;
			}

			set
			{
				this.AssertHeaderOpen();
				this.contentLanguage = value;
			}
		}

		/// <summary>
		/// The Content-Type entity-header field indicates the media type of the entity-body sent to the recipient or, in the case of the HEAD method, 
		/// the media type that would have been sent had the request been a GET. 
		/// </summary>
		public string ContentType
		{
			get
			{
				return this.contentType;
			}

			set
			{
				this.AssertHeaderOpen();
				this.contentType = value;
			}
		}

		/// <summary>
		/// The Content-Length entity-header field indicates the size of the entity-body, in decimal number of OCTETs, sent to the recipient or, 
		/// in the case of the HEAD method, the size of the entity-body that would have been sent had the request been a GET. 
		/// </summary>
		public long? ContentLength
		{
			get
			{
				return this.contentLength;
			}

			set
			{
				this.AssertHeaderOpen();
				this.contentLength = value;
			}
		}

		/// <summary>
		/// HTTP Status code.
		/// </summary>
		public int StatusCode
		{
			get
			{
				return this.statusCode;
			}

			set
			{
				this.AssertHeaderOpen();
				this.statusCode = value;
			}
		}

		/// <summary>
		/// HTTP Status Message.
		/// </summary>
		public string StatusMessage
		{
			get
			{
				return this.statusMessage;
			}

			set
			{
				this.AssertHeaderOpen();
				this.statusMessage = value;
			}
		}

		/// <summary>
		/// If only the header is of interest.
		/// </summary>
		public bool OnlyHeader
		{
			get
			{
				return this.onlyHeader;
			}

			internal set
			{
				this.AssertHeaderOpen();
				this.onlyHeader = value;
			}
		}

		/// <summary>
		/// Sets a custom header field value.
		/// </summary>
		/// <param name="FieldName">HTTP Header field name.</param>
		/// <param name="Value">Field value.</param>
		public void SetHeader(string FieldName, string Value)
		{
			this.AssertHeaderOpen();

			switch (FieldName.ToLower())
			{
				case "date":
					DateTimeOffset DTO;
					if (CommonTypes.TryParseRfc822(Value, out DTO))
						this.date = DTO;
					else
						throw new ArgumentException("Value does not conform to RFC 822.", "Value");
					break;

				case "expires":
					if (CommonTypes.TryParseRfc822(Value, out DTO))
						this.expires = DTO;
					else
						throw new ArgumentException("Value does not conform to RFC 822.", "Value");
					break;

				case "server":
					this.server = Value;
					break;

				case "content-language":
					this.contentLanguage = Value;
					break;

				case "content-type":
					this.contentType = Value;
					break;

				case "content-length":
					this.contentLength = long.Parse(Value);
					break;

				default:
					if (this.customHeaders == null)
						this.customHeaders = new Dictionary<string, string>();

					this.customHeaders[FieldName] = Value;
					break;
			}
		}

		/// <summary>
		/// Gets the System.Text.Encoding in which the output is written.
		/// </summary>
		public override Encoding Encoding
		{
			get { return this.encoding; }
		}

		/// <summary>
		/// Closes the current StreamWriter object and the underlying stream.
		/// </summary>
		/// <exception cref="EncoderFallbackException">The current encoding does not support displaying half of a Unicode surrogate pair.</exception>
		public override void Close()
		{
			// TODO
		}

		/// <summary>
		/// Releases the unmanaged resources used by the System.IO.StreamWriter and optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
		/// <exception cref="System.Text.EncoderFallbackException">The current encoding does not support displaying half of a Unicode surrogate pair.</exception>
		protected override void Dispose(bool disposing)
		{
			// TODO
		}

		/// <summary>
		/// Clears all buffers for the current writer and causes any buffered data to be written to the underlying stream.
		/// </summary>
		/// <exception cref="System.ObjectDisposedException">The current writer is closed.</exception>
		/// <exception cref="System.IO.IOException">An I/O error has occurred.</exception>
		/// <exception cref="System.Text.EncoderFallbackException">The current encoding does not support displaying half of a Unicode surrogate pair.</exception>
		public override void Flush()
		{
			// TODO
		}

		/// <summary>
		/// If the header has been sent.
		/// </summary>
		public bool HeaderSent
		{
			get { return this.transferEncoding != null; }
		}

		/// <summary>
		/// Sends the response back to the client. If the resource is synchronous, there's no need to call this method. Only asynchronous
		/// resources need to call this method explicitly.
		/// </summary>
		public void SendResponse()
		{
			if (!this.responseSent)
			{
				this.responseSent = true;

				if (this.transferEncoding == null)
					this.StartSendResponse(false);
				else
					this.transferEncoding.Flush();
			}
		}

		private void StartSendResponse(bool ExpectContent)
		{
			if (this.transferEncoding == null)
			{
				StringBuilder Output = new StringBuilder();

				Output.Append("HTTP/1.1 ");
				Output.Append(this.statusCode.ToString());
				Output.Append(' ');
				Output.Append(this.statusMessage);

				Output.Append("\r\nDate: ");
				Output.Append(CommonTypes.EncodeRfc822(this.date));

				if (this.expires.HasValue)
				{
					Output.Append("\r\nExpires: ");
					Output.Append(CommonTypes.EncodeRfc822(this.expires.Value));
				}

				Output.Append("\r\nServer: ");
				if (string.IsNullOrEmpty(this.server))
					Output.Append("Waher.Networking.HTTP");
				else
					Output.Append(this.server + " (Waher.Networking.HTTP)");

				if (!string.IsNullOrEmpty(this.contentLanguage))
				{
					Output.Append("\r\nContent-Language: ");
					Output.Append(this.contentLanguage);
				}

				if (!string.IsNullOrEmpty(this.contentType))
				{
					Output.Append("\r\nContent-Type: ");
					Output.Append(this.contentType);

					if (this.contentType.StartsWith("text/") && !this.contentType.Contains("charset="))
					{
						Output.Append("; charset=");
						Output.Append(this.encoding.WebName);
					}
				}

				if (this.contentLength.HasValue)
				{
					Output.Append("\r\nContent-Length: ");
					Output.Append(this.contentLength.Value.ToString());

					this.transferEncoding = new ContentLengthEncoding(this.responseStream, this.contentLength.Value);
				}
				else if (ExpectContent)
				{
					Output.Append("\r\nTransfer-Encoding: chunked");
					this.transferEncoding = new ChunkedTransferEncoding(this.responseStream);
				}
				else
					this.transferEncoding = new ContentLengthEncoding(this.responseStream, 0);

				if (this.customHeaders != null)
				{
					foreach (KeyValuePair<string, string> P in this.customHeaders)
					{
						Output.Append("\r\n");
						Output.Append(P.Key);
						Output.Append(": ");
						Output.Append(P.Value);
					}
				}

				// TODO: Encode non-ASCII characters in HTTP headers.

				Output.Append("\r\n\r\n");

				string Header = Output.ToString();
				byte[] HeaderBin = Encoding.ASCII.GetBytes(Header);

				this.responseStream.Write(HeaderBin, 0, HeaderBin.Length);
			}
		}

		/// <summary>
		/// Writes a character to the stream.
		/// </summary>
		/// <param name="value">The character to write to the text stream.</param>
		/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
		/// <exception cref="System.ObjectDisposedException">System.IO.StreamWriter.AutoFlush is true or the System.IO.StreamWriter buffer is full, 
		/// and current writer is closed.</exception>
		/// <exception cref="System.NotSupportedException">System.IO.StreamWriter.AutoFlush is true or the System.IO.StreamWriter buffer
		/// is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the System.IO.StreamWriter 
		/// is at the end the stream.</exception>
		public override void Write(char value)
		{
			if (this.transferEncoding == null)
				this.StartSendResponse(true);

			// TODO
		}

		/// <summary>
		/// Writes a character array to the stream.
		/// </summary>
		/// <param name="buffer">A character array containing the data to write. If buffer is null, nothing is written.</param>
		/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
		/// <exception cref="System.ObjectDisposedException">System.IO.StreamWriter.AutoFlush is true or the System.IO.StreamWriter buffer
		/// is full, and current writer is closed.</exception>
		/// <exception cref="System.NotSupportedException">System.IO.StreamWriter.AutoFlush is true or the System.IO.StreamWriter buffer
		/// is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the System.IO.StreamWriter 
		/// is at the end the stream.</exception>
		public override void Write(char[] buffer)
		{
			if (this.transferEncoding == null)
				this.StartSendResponse(true);

			// TODO
		}

		/// <summary>
		/// Writes a string to the stream.
		/// </summary>
		/// <param name="value">The string to write to the stream. If value is null, nothing is written.</param>
		/// <exception cref="System.ObjectDisposedException">System.IO.StreamWriter.AutoFlush is true or the System.IO.StreamWriter buffer
		/// is full, and current writer is closed.</exception>
		/// <exception cref="System.NotSupportedException">System.IO.StreamWriter.AutoFlush is true or the System.IO.StreamWriter buffer
		/// is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the System.IO.StreamWriter 
		/// is at the end the stream.</exception>
		/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
		public override void Write(string value)
		{
			if (this.transferEncoding == null)
				this.StartSendResponse(true);

			// TODO
		}

		/// <summary>
		/// Writes a subarray of characters to the stream.
		/// </summary>
		/// <param name="buffer">A character array containing the data to write.</param>
		/// <param name="index">The index into buffer at which to begin writing.</param>
		/// <param name="count">The number of characters to read from buffer.</param>
		/// <exception cref="System.ArgumentNullException">buffer is null.</exception>
		/// <exception cref="System.ArgumentException">The buffer length minus index is less than count.</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">index or count is negative.</exception>
		/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
		/// <exception cref="System.ObjectDisposedException">System.IO.StreamWriter.AutoFlush is true or the System.IO.StreamWriter buffer
		/// is full, and current writer is closed.</exception>
		/// <exception cref="System.NotSupportedException">System.IO.StreamWriter.AutoFlush is true or the System.IO.StreamWriter buffer
		/// is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the System.IO.StreamWriter 
		/// is at the end the stream.</exception>
		public override void Write(char[] buffer, int index, int count)
		{
			if (this.transferEncoding == null)
				this.StartSendResponse(true);

			// TODO
		}

	}
}
