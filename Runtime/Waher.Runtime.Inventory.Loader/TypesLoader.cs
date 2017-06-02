﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Waher.Events;
using Waher.Runtime.Inventory;

namespace Waher.Runtime.Inventory.Loader
{
	public static class TypesLoader
	{
		/// <summary>
		/// Initializes the inventory engine, registering types and interfaces available in <paramref name="Assemblies"/>.
		/// </summary>
		public static void Initialize()
		{
			Initialize(string.Empty);
		}

		/// <summary>
		/// Initializes the inventory engine, registering types and interfaces available in <paramref name="Assemblies"/>.
		/// </summary>
		/// <param name="Folder">Name of folder containing assemblies to load, if they are not already loaded.</param>
		public static void Initialize(string Folder)
		{
			if (string.IsNullOrEmpty(Folder))
				Folder = Path.GetDirectoryName(typeof(TypesLoader).GetTypeInfo().Assembly.Location);

			string[] DllFiles = Directory.GetFiles(Folder, "*.dll", SearchOption.TopDirectoryOnly);
			Dictionary<AssemblyName, Assembly> LoadedAssemblies = new Dictionary<AssemblyName, Assembly>();
			Dictionary<AssemblyName, bool> ReferencedAssemblies = new Dictionary<AssemblyName, bool>();

			foreach (string DllFile in DllFiles)
			{
				try
				{
					Assembly A = AssemblyLoadContext.Default.LoadFromAssemblyPath(DllFile);
					LoadedAssemblies[A.GetName()] = A;

					foreach (AssemblyName AN in A.GetReferencedAssemblies())
						ReferencedAssemblies[AN] = true;
				}
				catch (Exception ex)
				{
					Log.Critical(ex);
				}
			}

			do
			{
				AssemblyName[] References = new AssemblyName[ReferencedAssemblies.Count];
				ReferencedAssemblies.Keys.CopyTo(References, 0);
				ReferencedAssemblies.Clear();

				foreach (AssemblyName AN in References)
				{
					if (LoadedAssemblies.ContainsKey(AN))
						continue;

					Assembly A = AssemblyLoadContext.Default.LoadFromAssemblyName(AN);
					LoadedAssemblies[A.GetName()] = A;

					foreach (AssemblyName AN2 in A.GetReferencedAssemblies())
						ReferencedAssemblies[AN2] = true;
				}
			}
			while (ReferencedAssemblies.Count > 0);

			Assembly[] Assemblies = new Assembly[LoadedAssemblies.Count];
			LoadedAssemblies.Values.CopyTo(Assemblies, 0);

			Types.Initialize(Assemblies);
		}
	}
}
