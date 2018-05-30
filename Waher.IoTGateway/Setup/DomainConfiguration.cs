﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Waher.Networking.HTTP;
using Waher.Persistence;
using Waher.Persistence.Attributes;
using Waher.Security;
using Waher.Security.ACME;
using Waher.Security.PKCS;

namespace Waher.IoTGateway.Setup
{
	/// <summary>
	/// Domain Configuration
	/// </summary>
	public class DomainConfiguration : SystemMultiStepConfiguration
	{
		private static DomainConfiguration instance = null;

		private string[] alternativeDomains = null;
		private byte[] certificate = null;
		private string domain = string.Empty;
		private string acmeDirectory = string.Empty;
		private string contactEMail = string.Empty;
		private string urlToS = string.Empty;
		private string password = string.Empty;
		private bool useDomainName = false;
		private bool useEncryption = true;
		private bool customCA = false;
		private bool acceptToS = false;

		private HttpResource testNames = null;
		private HttpResource testName = null;
		private HttpResource testCA = null;
		private HttpResource acmeChallenge = null;
		private string challenge = string.Empty;
		private string token = string.Empty;

		/// <summary>
		/// Current instance of configuration.
		/// </summary>
		public static DomainConfiguration Instance => instance;

		/// <summary>
		/// Principal domain name
		/// </summary>
		[DefaultValueStringEmpty]
		public string Domain
		{
			get { return this.domain; }
			set { this.domain = value; }
		}

		/// <summary>
		/// Alternative domain names
		/// </summary>
		[DefaultValueNull]
		public string[] AlternativeDomains
		{
			get { return this.alternativeDomains; }
			set { this.alternativeDomains = value; }
		}

		/// <summary>
		/// If the server uses a domain name.
		/// </summary>
		[DefaultValue(false)]
		public bool UseDomainName
		{
			get { return this.useDomainName; }
			set { this.useDomainName = value; }
		}

		/// <summary>
		/// If the server uses server-side encryption.
		/// </summary>
		[DefaultValue(true)]
		public bool UseEncryption
		{
			get { return this.useEncryption; }
			set { this.useEncryption = value; }
		}

		/// <summary>
		/// If a custom Certificate Authority is to be used
		/// </summary>
		[DefaultValue(false)]
		public bool CustomCA
		{
			get { return this.customCA; }
			set { this.customCA = value; }
		}

		/// <summary>
		/// If a custom Certificate Authority is to be used, this property holds the URL to their ACME directory.
		/// </summary>
		[DefaultValueStringEmpty]
		public string AcmeDirectory
		{
			get { return this.acmeDirectory; }
			set { this.acmeDirectory = value; }
		}

		/// <summary>
		/// Contact e-mail address
		/// </summary>
		[DefaultValueStringEmpty]
		public string ContactEMail
		{
			get { return this.contactEMail; }
			set { this.contactEMail = value; }
		}

		/// <summary>
		/// CA Terms of Service
		/// </summary>
		[DefaultValueStringEmpty]
		public string UrlToS
		{
			get { return this.urlToS; }
			set { this.urlToS = value; }
		}

		/// <summary>
		/// If the CA Terms of Service has been accepted.
		/// </summary>
		[DefaultValue(false)]
		public bool AcceptToS
		{
			get { return this.acceptToS; }
			set { this.acceptToS = value; }
		}

		/// <summary>
		/// Certificate password
		/// </summary>
		[DefaultValueStringEmpty]
		public string Password
		{
			get { return this.password; }
			set { this.password = value; }
		}

		/// <summary>
		/// Certificate
		/// </summary>
		[DefaultValueNull]
		public byte[] Certificate
		{
			get { return this.certificate; }
			set { this.certificate = value; }
		}

		/// <summary>
		/// If the CA has a Terms of Service.
		/// </summary>
		public bool HasToS => !string.IsNullOrEmpty(this.urlToS);

		/// <summary>
		/// Resource to be redirected to, to perform the configuration.
		/// </summary>
		public override string Resource => "/Settings/Domain.md";

		/// <summary>
		/// Priority of the setting. Configurations are sorted in ascending order.
		/// </summary>
		public override int Priority => 100;

		/// <summary>
		/// Is called during startup to configure the system.
		/// </summary>
		public override Task ConfigureSystem()
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Sets the static instance of the configuration.
		/// </summary>
		/// <param name="Configuration">Configuration object</param>
		public override void SetStaticInstance(ISystemConfiguration Configuration)
		{
			instance = Configuration as DomainConfiguration;
		}

		/// <summary>
		/// Waits for the user to provide configuration.
		/// </summary>
		/// <param name="WebServer">Current Web Server object.</param>
		public override Task SetupConfiguration(HttpServer WebServer)
		{
			Task Result = base.SetupConfiguration(WebServer);

			this.testNames = WebServer.Register("/Settings/TestDomainNames", null, this.TestDomainNames, true, false, true);
			this.testName = WebServer.Register("/Settings/TestDomainName", this.TestDomainName, true, false, true);
			this.testCA = WebServer.Register("/Settings/TestCA", null, this.TestCA, true, false, true);

			this.RegisterAcmeChallenge(WebServer);

			return Result;
		}

		internal void RegisterAcmeChallenge(HttpServer WebServer)
		{
			this.acmeChallenge = WebServer.Register("/.well-known/acme-challenge", this.AcmeChallenge, true, true, true);
		}

		internal void UnregisterAcmeChallenge(HttpServer WebServer)
		{
			if (this.acmeChallenge != null)
			{
				WebServer.Unregister(this.acmeChallenge);
				this.acmeChallenge = null;
			}
		}

		/// <summary>
		/// Cleans up after configuration has been performed.
		/// </summary>
		/// <param name="WebServer">Current Web Server object.</param>
		public override Task CleanupAfterConfiguration(HttpServer WebServer)
		{
			Task Result = base.CleanupAfterConfiguration(WebServer);

			if (this.testNames != null)
			{
				WebServer.Unregister(this.testNames);
				this.testNames = null;
			}

			if (this.testName != null)
			{
				WebServer.Unregister(this.testName);
				this.testName = null;
			}

			if (this.testCA != null)
			{
				WebServer.Unregister(this.testCA);
				this.testCA = null;
			}

			this.UnregisterAcmeChallenge(WebServer);

			return Result;
		}

		private void TestDomainNames(HttpRequest Request, HttpResponse Response)
		{
			if (!Request.HasData)
				throw new BadRequestException();

			object Obj = Request.DecodeData();
			if (!(Obj is Dictionary<string, object> Parameters))
				throw new BadRequestException();

			if (!Parameters.TryGetValue("domainName", out Obj) || !(Obj is string DomainName))
				throw new BadRequestException();

			List<string> AlternativeNames = new List<string>();
			int Index = 0;

			while (Parameters.TryGetValue("altDomainName" + Index.ToString(), out Obj) && Obj is string AltDomainName && !string.IsNullOrEmpty(AltDomainName))
				AlternativeNames.Add(AltDomainName);

			if (Parameters.TryGetValue("altDomainName", out Obj) && Obj is string AltDomainName2 && !string.IsNullOrEmpty(AltDomainName2))
				AlternativeNames.Add(AltDomainName2);

			string TabID = Request.Header["X-TabID"];
			if (string.IsNullOrEmpty(TabID))
				throw new BadRequestException();

			this.domain = DomainName;
			this.alternativeDomains = AlternativeNames.Count == 0 ? null : AlternativeNames.ToArray();
			this.useDomainName = true;

			Response.StatusCode = 200;

			this.Test(TabID);
		}

		private void TestDomainName(HttpRequest Request, HttpResponse Response)
		{
			Response.StatusCode = 200;
			Response.ContentType = "text/plain";
			Response.Write(this.token);
		}

		private async void Test(string TabID)
		{
			try
			{
				if (!string.IsNullOrEmpty(this.domain))
				{
					if (!await this.Test(TabID, this.domain))
					{
						ClientEvents.PushEvent(new string[] { TabID }, "NameNotValid", this.domain, false);
						return;
					}
				}

				if (this.alternativeDomains != null)
				{
					foreach (string AltDomainName in this.alternativeDomains)
					{
						if (!await this.Test(TabID, AltDomainName))
						{
							ClientEvents.PushEvent(new string[] { TabID }, "NameNotValid", AltDomainName, false);
							return;
						}
					}
				}

				if (this.Step < 1)
					this.Step = 1;

				await Database.Update(this);

				ClientEvents.PushEvent(new string[] { TabID }, "NamesOK", string.Empty, false);
			}
			catch (Exception ex)
			{
				ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", ex.Message, false);
			}
		}

		private async Task<bool> Test(string TabID, string DomainName)
		{
			ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Testing " + DomainName + "...", false);

			this.token = Hashes.BinaryToString(Gateway.NextBytes(32));

			using (HttpClient HttpClient = new HttpClient()
			{
				Timeout = TimeSpan.FromMilliseconds(10000)
			})
			{
				try
				{
					HttpResponseMessage Response = await HttpClient.GetAsync("http://" + DomainName + "/Settings/TestDomainName");
					if (!Response.IsSuccessStatusCode)
					{
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Domain name does not point to this machine.", false);
						return false;
					}

					Stream Stream = await Response.Content.ReadAsStreamAsync(); // Regardless of status code, we check for XML content.
					byte[] Bin = await Response.Content.ReadAsByteArrayAsync();
					string Token = Encoding.ASCII.GetString(Bin);

					if (Token != this.token)
					{
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Unexpected response returned. Domain name does not point to this machine.", false);
						return false;
					}
				}
				catch (TimeoutException)
				{
					ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Time-out. Check that the domain name points to this machine.", false);
					return false;
				}
				catch (Exception ex)
				{
					ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Unable to validate domain name: " + ex.Message, false);
					return false;
				}
			}

			ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Domain name valid.", false);

			return true;
		}

		private void TestCA(HttpRequest Request, HttpResponse Response)
		{
			if (!Request.HasData)
				throw new BadRequestException();

			object Obj = Request.DecodeData();
			if (!(Obj is Dictionary<string, object> Parameters))
				throw new BadRequestException();

			if (!Parameters.TryGetValue("useEncryption", out Obj) || !(Obj is bool UseEncryption))
				throw new BadRequestException();

			if (!Parameters.TryGetValue("customCA", out Obj) || !(Obj is bool CustomCA))
				throw new BadRequestException();

			if (!Parameters.TryGetValue("acmeDirectory", out Obj) || !(Obj is string AcmeDirectory))
				throw new BadRequestException();

			if (!Parameters.TryGetValue("contactEMail", out Obj) || !(Obj is string ContactEMail))
				throw new BadRequestException();

			if (!Parameters.TryGetValue("acceptToS", out Obj) || !(Obj is bool AcceptToS))
				throw new BadRequestException();

			if (!Parameters.TryGetValue("domainName", out Obj) || !(Obj is string DomainName))
				throw new BadRequestException();

			List<string> AlternativeNames = new List<string>();
			int Index = 0;

			while (Parameters.TryGetValue("altDomainName" + Index.ToString(), out Obj) && Obj is string AltDomainName && !string.IsNullOrEmpty(AltDomainName))
				AlternativeNames.Add(AltDomainName);

			if (Parameters.TryGetValue("altDomainName", out Obj) && Obj is string AltDomainName2 && !string.IsNullOrEmpty(AltDomainName2))
				AlternativeNames.Add(AltDomainName2);

			string TabID = Request.Header["X-TabID"];
			if (string.IsNullOrEmpty(TabID))
				throw new BadRequestException();

			this.domain = DomainName;
			this.alternativeDomains = AlternativeNames.Count == 0 ? null : AlternativeNames.ToArray();
			this.useDomainName = true;
			this.useEncryption = UseEncryption;
			this.customCA = CustomCA;
			this.acmeDirectory = AcmeDirectory;
			this.contactEMail = ContactEMail;
			this.acceptToS = AcceptToS;

			Response.StatusCode = 200;

			Task T = this.CreateCertificate(TabID);
		}

		internal Task<bool> CreateCertificate()
		{
			return CreateCertificate(null);
		}

		internal async Task<bool> CreateCertificate(string TabID)
		{
			try
			{
				string URL = this.customCA ? this.acmeDirectory : "https://acme-v02.api.letsencrypt.org/directory";

				using (AcmeClient Client = new AcmeClient(URL))
				{
					ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Connecting to directory.", false);

					AcmeDirectory AcmeDirectory = await Client.GetDirectory();

					if (AcmeDirectory.ExternalAccountRequired)
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "An external account is required.", false);

					if (AcmeDirectory.TermsOfService != null)
					{
						URL = AcmeDirectory.TermsOfService.ToString();
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Terms of service available on: " + URL, false);
						ClientEvents.PushEvent(new string[] { TabID }, "TermsOfService", URL, false);

						this.urlToS = URL;

						if (!this.acceptToS)
						{
							ClientEvents.PushEvent(new string[] { TabID }, "CertificateError", "You need to accept the terms of service.", false);
							return false;
						}
					}

					if (AcmeDirectory.Website != null)
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Web site available on: " + AcmeDirectory.Website.ToString(), false);

					ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Getting account.", false);

					List<string> Names = new List<string>();

					if (!string.IsNullOrEmpty(this.domain))
						Names.Add(this.domain);

					if (this.alternativeDomains != null)
					{
						foreach (string Name in this.alternativeDomains)
						{
							if (!Names.Contains(Name))
								Names.Add(Name);
						}
					}
					string[] DomainNames = Names.ToArray();

					AcmeAccount Account;

					try
					{
						Account = await Client.GetAccount();

						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Account found.", false);
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Created: " + Account.CreatedAt.ToString(), false);
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Initial IP: " + Account.InitialIp, false);
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Status: " + Account.Status.ToString(), false);

						if (string.IsNullOrEmpty(this.contactEMail))
						{
							if (Account.Contact != null && Account.Contact.Length != 0)
							{
								ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Updating contact URIs in account.", false);
								Account = await Account.Update(new string[0]);
								ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Account updated.", false);
							}
						}
						else
						{
							if (Account.Contact == null || Account.Contact.Length != 1 || Account.Contact[0] != "mailto:" + this.contactEMail)
							{
								ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Updating contact URIs in account.", false);
								Account = await Account.Update(new string[] { "mailto:" + this.contactEMail });
								ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Account updated.", false);
							}
						}
					}
					catch (AcmeAccountDoesNotExistException)
					{
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Account not found.", false);
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Creating account.", false);

						Account = await Client.CreateAccount(string.IsNullOrEmpty(this.contactEMail) ? new string[0] : new string[] { "mailto:" + this.contactEMail },
							this.acceptToS);

						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Account created.", false);
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Status: " + Account.Status.ToString(), false);
					}

					ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Generating new key.", false);
					await Account.NewKey();
					ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "New key generated.", false);

					ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Creating order.", false);
					AcmeOrder Order = await Account.OrderCertificate(DomainNames, null, null);
					ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Order created.", false);

					foreach (AcmeAuthorization Authorization in await Order.GetAuthorizations())
					{
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Processing authorization for " + Authorization.Value, false);

						AcmeChallenge Challenge;
						bool Acknowledged = false;
						int Index = 1;
						int NrChallenges = Authorization.Challenges.Length;

						for (Index = 1; Index <= NrChallenges; Index++)
						{
							Challenge = Authorization.Challenges[Index - 1];

							if (Challenge is AcmeHttpChallenge HttpChallenge)
							{
								this.challenge = "/" + HttpChallenge.Token;
								this.token = HttpChallenge.KeyAuthorization;

								ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Acknowleding challenge.", false);
								Challenge = await HttpChallenge.AcknowledgeChallenge();
								ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Challenge acknowledged: " + Challenge.Status.ToString(), false);

								Acknowledged = true;
							}
						}

						if (!Acknowledged)
						{
							ClientEvents.PushEvent(new string[] { TabID }, "CertificateError", "No automated method found to respond to any of the authorization challenges.", false);
							return false;
						}

						AcmeAuthorization Authorization2 = Authorization;

						do
						{
							ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Waiting to poll authorization status.", false);
							System.Threading.Thread.Sleep(5000);

							ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Polling authorization.", false);
							Authorization2 = await Authorization2.Poll();

							ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Authorization polled: " + Authorization2.Status.ToString(), false);
						}
						while (Authorization2.Status == AcmeAuthorizationStatus.pending);

						if (Authorization2.Status != AcmeAuthorizationStatus.valid)
						{
							switch (Authorization2.Status)
							{
								case AcmeAuthorizationStatus.deactivated:
									throw new Exception("Authorization deactivated.");

								case AcmeAuthorizationStatus.expired:
									throw new Exception("Authorization expired.");

								case AcmeAuthorizationStatus.invalid:
									throw new Exception("Authorization invalid.");

								case AcmeAuthorizationStatus.revoked:
									throw new Exception("Authorization revoked.");

								default:
									throw new Exception("Authorization not validated.");
							}
						}
					}

					using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider(4096))   // TODO: Make configurable
					{
						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Finalizing order.", false);

						SignatureAlgorithm SignAlg = new RsaSha256(RSA);

						Order = await Order.FinalizeOrder(new CertificateRequest(SignAlg)
						{
							CommonName = this.domain,
							SubjectAlternativeNames = DomainNames,
							EMailAddress = this.contactEMail
						});

						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Order finalized: " + Order.Status.ToString(), false);

						if (Order.Status != AcmeOrderStatus.valid)
						{
							switch (Order.Status)
							{
								case AcmeOrderStatus.invalid:
									throw new Exception("Order invalid.");

								default:
									throw new Exception("Unable to validate oder.");
							}
						}

						if (Order.Certificate == null)
							throw new Exception("No certificate URI provided.");

						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Downloading certificate.", false);

						X509Certificate2[] Certificates = await Order.DownloadCertificate();
						X509Certificate2 Certificate = Certificates[0];

						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Adding private key.", false);
						Certificate.PrivateKey = RSA;

						ClientEvents.PushEvent(new string[] { TabID }, "ShowStatus", "Exporting certificate.", false);

						string Password = Hashes.BinaryToString(Gateway.NextBytes(32));
						this.certificate = Certificate.Export(X509ContentType.Pfx, Password);
						this.password = Password;

						if (this.Step < 2)
							this.Step = 2;

						await Database.Update(this);

						ClientEvents.PushEvent(new string[] { TabID }, "CertificateOk", string.Empty, false);

						return true;
					}
				}
			}
			catch (Exception ex)
			{
				ClientEvents.PushEvent(new string[] { TabID }, "CertificateError", "Unable to create certificate: " + ex.Message, false);
				return false;
			}
		}

		private void AcmeChallenge(HttpRequest Request, HttpResponse Response)
		{
			if (Request.SubPath != this.challenge)
				throw new NotFoundException();

			Response.StatusCode = 200;
			Response.ContentType = "application/octet-stream";
			Response.Write(Encoding.ASCII.GetBytes(this.token));
		}

	}
}
