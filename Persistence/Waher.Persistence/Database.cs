﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Waher.Persistence.Filters;

namespace Waher.Persistence
{
	/// <summary>
	/// Static interface for database persistence. In order to work, a database provider has to be assigned to it. This is
	/// ideally done as one of the first steps in the startup of an application.
	/// </summary>
    public static class Database
    {
		private static IDatabaseProvider provider = null;

		/// <summary>
		/// Registers a database provider for use from the static <see cref="Database"/> class, 
		/// throughout the lifetime of the application.
		/// 
		/// Note: Only one database provider can be registered.
		/// </summary>
		/// <param name="DatabaseProvider">Database provider to use.</param>
		public static void Register(IDatabaseProvider DatabaseProvider)
		{
			if (provider != null)
				throw new Exception("A database provider is already registered.");

			provider = DatabaseProvider;
		}

		/// <summary>
		/// Registered database provider.
		/// </summary>
		public static IDatabaseProvider Provider
		{
			get
			{
				if (provider != null)
					return provider;
				else
					throw new Exception("A database provider has not been registered.");
			}
		}

		/// <summary>
		/// Inserts an object into the default collection of the database.
		/// </summary>
		/// <param name="Object">Object to insert.</param>
		public static Task Insert(object Object)
		{
			return Provider.Insert(Object);
		}

		/// <summary>
		/// Inserts a set of objects into the default collection of the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public static Task Insert(params object[] Objects)
		{
			return Provider.Insert(Objects);
		}

		/// <summary>
		/// Inserts a set of objects into the default collection of the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public static Task Insert(IEnumerable<object> Objects)
		{
			return Provider.Insert(Objects);
		}

		/// <summary>
		/// Finds objects of a given class <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Class defining how to deserialize objects found.</typeparam>
		/// <param name="SortOrder">Sort order. Each string represents a field name. By default, sort order is ascending.
		/// If descending sort order is desired, prefix the field name by a hyphen (minus) sign.</param>
		/// <returns>Objects found.</returns>
		public static Task<IEnumerable<T>> Find<T>(params string[] SortOrder)
		{
			return Provider.Find<T>(0, int.MaxValue, SortOrder);
		}

		/// <summary>
		/// Finds objects of a given class <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Class defining how to deserialize objects found.</typeparam>
		/// <param name="Filter">Optional filter. Can be null.</param>
		/// <param name="SortOrder">Sort order. Each string represents a field name. By default, sort order is ascending.
		/// If descending sort order is desired, prefix the field name by a hyphen (minus) sign.</param>
		/// <returns>Objects found.</returns>
		public static Task<IEnumerable<T>> Find<T>(Filter Filter, params string[] SortOrder)
		{
			return Provider.Find<T>(0, int.MaxValue, Filter, SortOrder);
		}

		/// <summary>
		/// Finds objects of a given class <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Class defining how to deserialize objects found.</typeparam>
		/// <param name="Offset">Result offset.</param>
		/// <param name="MaxCount">Maximum number of objects to return.</param>
		/// <param name="SortOrder">Sort order. Each string represents a field name. By default, sort order is ascending.
		/// If descending sort order is desired, prefix the field name by a hyphen (minus) sign.</param>
		/// <returns>Objects found.</returns>
		public static Task<IEnumerable<T>> Find<T>(int Offset, int MaxCount, params string[] SortOrder)
		{
			return Provider.Find<T>(Offset, MaxCount, SortOrder);
		}

		/// <summary>
		/// Finds objects of a given class <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Class defining how to deserialize objects found.</typeparam>
		/// <param name="Offset">Result offset.</param>
		/// <param name="MaxCount">Maximum number of objects to return.</param>
		/// <param name="Filter">Optional filter. Can be null.</param>
		/// <param name="SortOrder">Sort order. Each string represents a field name. By default, sort order is ascending.
		/// If descending sort order is desired, prefix the field name by a hyphen (minus) sign.</param>
		/// <returns>Objects found.</returns>
		public static Task<IEnumerable<T>> Find<T>(int Offset, int MaxCount, Filter Filter, params string[] SortOrder)
		{
			return Provider.Find<T>(Offset, MaxCount, Filter, SortOrder);
		}

		/// <summary>
		/// Finds the first object of a given class <typeparamref name="T"/> and deletes the rest.
		/// </summary>
		/// <typeparam name="T">Class defining how to deserialize objects found.</typeparam>
		/// <param name="Timeout">Timeout, in milliseconds.</param>
		/// <param name="SortOrder">Sort order. Each string represents a field name. By default, sort order is ascending.
		/// If descending sort order is desired, prefix the field name by a hyphen (minus) sign.</param>
		/// <returns>Objects found.</returns>
		///	<exception cref="TimeoutException">Thrown if a response is not returned from the database within the given number of milliseconds.</exception>
		public async static Task<T> FindFirstDeleteRest<T>(int Timeout, params string[] SortOrder)
		{
			return await FirstDeleteRest<T>(Timeout, await Provider.Find<T>(0, int.MaxValue, SortOrder));
		}

		private static async Task<T> FirstDeleteRest<T>(int Timeout, IEnumerable<T> Set)
		{
			T Result = default(T);
			bool First = true;

			foreach (T Obj in Set)
			{
				if (First)
				{
					First = false;
					Result = Obj;
				}
				else
					await Database.Delete(Obj);
			}

			return Result;
		}

		/// <summary>
		/// Finds objects of a given class <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Class defining how to deserialize objects found.</typeparam>
		/// <param name="Timeout">Timeout, in milliseconds.</param>
		/// <param name="Filter">Optional filter. Can be null.</param>
		/// <param name="SortOrder">Sort order. Each string represents a field name. By default, sort order is ascending.
		/// If descending sort order is desired, prefix the field name by a hyphen (minus) sign.</param>
		/// <returns>Objects found.</returns>
		///	<exception cref="TimeoutException">Thrown if a response is not returned from the database within the given number of milliseconds.</exception>
		public static async Task<T> FindFirstDeleteRest<T>(int Timeout, Filter Filter, params string[] SortOrder)
		{
			return await FirstDeleteRest<T>(Timeout, await Provider.Find<T>(0, int.MaxValue, Filter, SortOrder));
		}

		/// <summary>
		/// Updates an object in the database.
		/// </summary>
		/// <param name="Object">Object to insert.</param>
		public static Task Update(object Object)
		{
			return Provider.Update(Object);
		}

		/// <summary>
		/// Updates a collection of objects in the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public static Task Update(params object[] Objects)
		{
			return Provider.Update(Objects);
		}

		/// <summary>
		/// Updates a collection of objects in the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public static Task Update(IEnumerable<object> Objects)
		{
			return Provider.Update(Objects);
		}

		/// <summary>
		/// Deletes an object in the database.
		/// </summary>
		/// <param name="Object">Object to insert.</param>
		public static Task Delete(object Object)
		{
			return Provider.Delete(Object);
		}

		/// <summary>
		/// Deletes a collection of objects in the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public static Task Delete(params object[] Objects)
		{
			return Provider.Delete(Objects);
		}

		/// <summary>
		/// Deletes a collection of objects in the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public static Task Delete(IEnumerable<object> Objects)
		{
			return Provider.Delete(Objects);
		}

	}
}
