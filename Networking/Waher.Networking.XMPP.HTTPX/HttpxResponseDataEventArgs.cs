﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Waher.Networking.HTTP;

namespace Waher.Networking.XMPP.HTTPX
{
	/// <summary>
	/// Event handler for HTTPX response data events.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="e">Event arguments.</param>
	public delegate void HttpxResponseDataEventHandler(object Sender, HttpxResponseDataEventArgs e);

	/// <summary>
	/// Event arguments for HTTPX data responses.
	/// </summary>
	public class HttpxResponseDataEventArgs : EventArgs
	{
		private HttpxResponseEventArgs response;
		private byte[] data;
		private bool last;

		/// <summary>
		/// Event arguments for HTTPX data responses.
		/// </summary>
		/// <param name="Response">Response event arguments.</param>
		/// <param name="Data">Data response, possibly partial.</param>
		/// <param name="Last">If it is the last data block.</param>
		public HttpxResponseDataEventArgs(HttpxResponseEventArgs Response, byte[] Data, bool Last)
			: base()
		{
			this.response = Response;
			this.data = Data;
			this.last = Last;
		}

		/// <summary>
		/// HTTPX Response.
		/// </summary>
		public HttpxResponseEventArgs HttpxResponse
		{
			get { return this.response; }
		}

		/// <summary>
		/// Data response, possibly partial.
		/// </summary>
		public byte[] Data
		{
			get { return this.data; }
		}

		/// <summary>
		/// If it is the last data block.
		/// </summary>
		public bool Last
		{
			get { return this.last; }
		}

	}
}
