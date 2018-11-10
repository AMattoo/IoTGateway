﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Networking.HTTP
{
	/// <summary>
	/// The client did not produce a request within the time that the server was prepared to wait. The client MAY repeat the request without 
	/// modifications at any later time. 
	/// </summary>
	public class RequestTimeoutException : HttpException
	{
		private const int Code = 408;
		private const string Msg = "Request Timeout";

		/// <summary>
		/// The client did not produce a request within the time that the server was prepared to wait. The client MAY repeat the request without 
		/// modifications at any later time. 
		/// </summary>
		public RequestTimeoutException()
			: base(Code, Msg)
		{
		}

		/// <summary>
		/// The client did not produce a request within the time that the server was prepared to wait. The client MAY repeat the request without 
		/// modifications at any later time. 
		/// </summary>
		/// <param name="ContentObject">Any content object to return. The object will be encoded before being sent.</param>
		public RequestTimeoutException(object ContentObject)
			: base(Code, Msg, ContentObject)
		{
		}

		/// <summary>
		/// The client did not produce a request within the time that the server was prepared to wait. The client MAY repeat the request without 
		/// modifications at any later time. 
		/// </summary>
		/// <param name="Content">Any encoded content to return.</param>
		/// <param name="ContentType">The content type of <paramref name="Content"/>, if provided.</param>
		public RequestTimeoutException(byte[] Content, string ContentType)
			: base(Code, Msg, Content, ContentType)
		{
		}
	}
}
