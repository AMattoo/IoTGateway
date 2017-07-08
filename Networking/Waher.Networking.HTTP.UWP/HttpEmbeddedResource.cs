﻿using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using Waher.Content;
using Waher.Security;

namespace Waher.Networking.HTTP
{
	/// <summary>
	/// Publishes an embedded resource through HTTP GET.
	/// </summary>
	public class HttpEmbeddedResource : HttpResource, IHttpGetMethod
	{
		private const int BufferSize = 32768;

		private HttpAuthenticationScheme[] authenticationSchemes;
		private string etag = null;
		private string embeddedResourceName;
		private string contentType;
		private Assembly assembly;

		/// <summary>
		/// Publishes an embedded resource through HTTP GET.
		/// </summary>
		/// <param name="ResourceName">Name of resource.</param>
		/// <param name="EmbeddedResourceName">Resource name of embedded resource.</param>
		/// <param name="Assembly">Assembly containing the embedded resource.</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public HttpEmbeddedResource(string ResourceName, string EmbeddedResourceName, Assembly Assembly,
			params HttpAuthenticationScheme[] AuthenticationSchemes)
			: this(ResourceName, EmbeddedResourceName, Assembly, InternetContent.GetContentType(Path.GetExtension(ResourceName)), AuthenticationSchemes)
		{
		}

		/// <summary>
		/// Publishes an embedded resource through HTTP GET.
		/// </summary>
		/// <param name="ResourceName">Name of resource.</param>
		/// <param name="EmbeddedResourceName">Resource name of embedded resource.</param>
		/// <param name="Assembly">Assembly containing the embedded resource.</param>
		/// <param name="ContentType">Internet Content Type to use when the embedded resource is requested.</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public HttpEmbeddedResource(string ResourceName, string EmbeddedResourceName, Assembly Assembly, string ContentType,
			params HttpAuthenticationScheme[] AuthenticationSchemes)
			: base(ResourceName)
		{
			this.embeddedResourceName = EmbeddedResourceName;
			this.assembly = Assembly;
			this.contentType = ContentType;
			this.authenticationSchemes = AuthenticationSchemes;
		}

		/// <summary>
		/// If the resource is synchronous (i.e. returns a response in the method handler), or if it is asynchronous
		/// (i.e. sends the response from another thread).
		/// </summary>
		public override bool Synchronous
		{
			get { return true; }
		}

		/// <summary>
		/// If the resource handles sub-paths.
		/// </summary>
		public override bool HandlesSubPaths
		{
			get { return false; }
		}

		/// <summary>
		/// If the resource uses user sessions.
		/// </summary>
		public override bool UserSessions
		{
			get { return false; }
		}

		/// <summary>
		/// If the GET method is allowed.
		/// </summary>
		public bool AllowsGET
		{
			get
			{
				return true;
			}
		}

		/// <summary>
		/// Any authentication schemes used to authenticate users before access is granted to the corresponding resource.
		/// </summary>
		/// <param name="Request">Current request</param>
		public override HttpAuthenticationScheme[] GetAuthenticationSchemes(HttpRequest Request)
		{
			return this.authenticationSchemes;
		}

		/// <summary>
		/// Executes the GET method on the resource.
		/// </summary>
		/// <param name="Request">HTTP Request</param>
		/// <param name="Response">HTTP Response</param>
		/// <exception cref="HttpException">If an error occurred when processing the method.</exception>
		public void GET(HttpRequest Request, HttpResponse Response)
		{
			using (Stream f = this.assembly.GetManifestResourceStream(this.embeddedResourceName))
			{
				if (f == null)
					throw new NotFoundException();

				if (this.etag == null)
				{
					this.etag = Hashes.ComputeSHA1HashString(f);
					f.Position = 0;
				}

				if (Request.Header.IfNoneMatch != null && Request.Header.IfNoneMatch.Value == this.etag)
					throw new NotModifiedException();

				Response.SetHeader("ETag", "\"" + this.etag + "\"");

				long l = f.Length;
				long Pos = 0;
				int Size = (int)Math.Min(BufferSize, l);
				byte[] Buffer = new byte[Size];
				int i;

				Response.ContentType = this.contentType;
				Response.ContentLength = l;

				if (!Response.OnlyHeader)
				{
					while (Pos < l)
					{
						i = f.Read(Buffer, 0, Size);
						if (i <= 0)
							throw new Exception("Unexpected end of stream.");

						Response.Write(Buffer, 0, i);
						Pos += i;
					}
				}
			}
		}
	}
}
