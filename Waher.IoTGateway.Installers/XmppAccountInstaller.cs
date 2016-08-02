﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Waher.IoTGateway.Installers
{
	[RunInstaller(true)]
	public partial class XmppAccountInstaller : System.Configuration.Install.Installer
	{
		public XmppAccountInstaller()
		{
		}

		protected override void OnAfterInstall(IDictionary savedState)
		{
			base.OnAfterInstall(savedState);

			XmppAccountForm Form = new XmppAccountForm();
			if (Form.ShowDialog() != DialogResult.OK)
				throw new Exception("Account creation failed.");
		}

	}
}
