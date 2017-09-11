﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Waher.Events;
using Waher.Runtime.Inventory;

namespace Waher.Runtime.Inventory.Loader
{
	/// <summary>
	/// Static class, loading and initializing assemblies dynamically.
	/// </summary>
	public static class TypesLoader
	{
		/// <summary>
		/// Initializes the inventory engine, registering types and interfaces available in <see cref="Types"/>.
		/// </summary>
		public static void Initialize()
		{
			Initialize(string.Empty);
		}

		/// <summary>
		/// Initializes the inventory engine, registering types and interfaces available in <see cref="Types"/>.
		/// </summary>
		/// <param name="Folder">Name of folder containing assemblies to load, if they are not already loaded.</param>
		public static void Initialize(string Folder)
		{
			if (string.IsNullOrEmpty(Folder))
				Folder = Path.GetDirectoryName(typeof(TypesLoader).GetTypeInfo().Assembly.Location);

			string[] DllFiles = Directory.GetFiles(Folder, "*.dll", SearchOption.TopDirectoryOnly);
			SortedDictionary<string, Assembly> LoadedAssembliesByName = new SortedDictionary<string, Assembly>(StringComparer.CurrentCultureIgnoreCase);
			SortedDictionary<string, Assembly> LoadedAssembliesByLocation = new SortedDictionary<string, Assembly>(StringComparer.CurrentCultureIgnoreCase);
			SortedDictionary<string, AssemblyName> ReferencedAssemblies = new SortedDictionary<string, AssemblyName>(StringComparer.CurrentCultureIgnoreCase);

			foreach (Assembly A in AppDomain.CurrentDomain.GetAssemblies())
			{
				LoadedAssembliesByName[A.FullName] = A;
				LoadedAssembliesByLocation[A.Location] = A;

				foreach (AssemblyName AN in A.GetReferencedAssemblies())
					ReferencedAssemblies[AN.FullName] = AN;
			}

			LoadReferencedAssemblies(LoadedAssembliesByName, LoadedAssembliesByLocation, ReferencedAssemblies);

			foreach (string DllFile in DllFiles)
			{
				if (LoadedAssembliesByLocation.ContainsKey(DllFile))
					continue;

				if (DllFile.StartsWith("api-ms-win-"))
					continue;

				try
				{
					byte[] Bin = File.ReadAllBytes(DllFile);
					Assembly A = AppDomain.CurrentDomain.Load(Bin);
					LoadedAssembliesByName[A.FullName] = A;
					LoadedAssembliesByLocation[A.Location] = A;

					foreach (AssemblyName AN in A.GetReferencedAssemblies())
						ReferencedAssemblies[AN.FullName] = AN;
				}
				catch (Exception ex)
				{
					Log.Critical(ex);
				}
			}

			LoadReferencedAssemblies(LoadedAssembliesByName, LoadedAssembliesByLocation, ReferencedAssemblies);

			Assembly[] Assemblies = new Assembly[LoadedAssembliesByName.Count];
			LoadedAssembliesByName.Values.CopyTo(Assemblies, 0);

			Types.Initialize(Assemblies);
		}

		private static void LoadReferencedAssemblies(SortedDictionary<string, Assembly> LoadedAssembliesByName,
			SortedDictionary<string, Assembly> LoadedAssembliesByLocation,
			SortedDictionary<string, AssemblyName> ReferencedAssemblies)
		{
			do
			{
				AssemblyName[] References = new AssemblyName[ReferencedAssemblies.Count];
				ReferencedAssemblies.Values.CopyTo(References, 0);
				ReferencedAssemblies.Clear();

				foreach (AssemblyName AN in References)
				{
					if (LoadedAssembliesByName.ContainsKey(AN.FullName))
						continue;

					try
					{
						Assembly A = AppDomain.CurrentDomain.Load(AN);
						LoadedAssembliesByName[A.FullName] = A;
						LoadedAssembliesByLocation[A.Location] = A;

						foreach (AssemblyName AN2 in A.GetReferencedAssemblies())
							ReferencedAssemblies[AN2.FullName] = AN2;
					}
					catch (Exception)
					{
						string s = AN.ToString();

						if (s.StartsWith("System.") ||
							s.StartsWith("SQLitePCLRaw.") ||
							s.StartsWith("SkiaSharp") ||
							s.StartsWith("Gma.QrCodeNet.Encoding") ||
							s.StartsWith("Esent.Interop"))
						{
							continue;
						}

						Log.Error("Unable to load assembly " + s + ".");
					}
				}
			}
			while (ReferencedAssemblies.Count > 0);
		}
	}
}
