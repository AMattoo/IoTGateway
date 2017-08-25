﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
#if NETSTANDARD1_5
using System.Security.Cryptography;
#endif
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using Waher.Runtime.Cache;
using Waher.Runtime.Inventory;
using Waher.Persistence.Serialization;
using Waher.Persistence.Files.Serialization;
using Waher.Persistence.Files.Serialization.ReferenceTypes;
using Waher.Persistence.Files.Serialization.ValueTypes;
using Waher.Persistence.Filters;

namespace Waher.Persistence.Files
{
	/// <summary>
	/// Delegate for embedded object value setter methods. Is used when loading embedded objects.
	/// </summary>
	/// <param name="EmbeddedObject">Embedded object.</param>
	public delegate void EmbeddedObjectSetter(object EmbeddedObject);

	/// <summary>
	/// Index regeneration options.
	/// </summary>
	public enum RegenerationOptions
	{
		/// <summary>
		/// Don't regenerate index.
		/// </summary>
		DontRegenerate,

		/// <summary>
		/// Regenerate index if index file not found.
		/// </summary>
		RegenerateIfFileNotFound,

		/// <summary>
		/// Regenerate index if index object not instantiated.
		/// </summary>
		RegenerateIfIndexNotInstantiated,

		/// <summary>
		/// Regenerate file.
		/// </summary>
		Regenerate
	}

	/// <summary>
	/// Persists objects into binary files.
	/// </summary>
	public class FilesProvider : IDisposable, IDatabaseProvider
	{
		private Dictionary<Type, IObjectSerializer> serializers;
		private Dictionary<string, Dictionary<string, ulong>> codeByFieldByCollection = new Dictionary<string, Dictionary<string, ulong>>();
		private Dictionary<string, Dictionary<ulong, string>> fieldByCodeByCollection = new Dictionary<string, Dictionary<ulong, string>>();
		private Dictionary<string, ObjectBTreeFile> files = new Dictionary<string, ObjectBTreeFile>();
		private Dictionary<string, StringDictionary> nameFiles = new Dictionary<string, StringDictionary>();
		private StringDictionary master;
		private Cache<long, byte[]> blocks;
		private object synchObj = new object();
		private object synchObjNrFiles = new object();

		private Encoding encoding;
		private string id;
		private string defaultCollectionName;
		private string folder;
		private int blockSize;
		private int blobBlockSize;
		private int timeoutMilliseconds;
		private int nrFiles = 0;
		private bool debug;
#if NETSTANDARD1_5
		private bool encrypted;
#endif

		#region Constructors

		/// <summary>
		/// Persists objects into binary files.
		/// </summary>
		/// <param name="Folder">Folder to store data files.</param>
		/// <param name="DefaultCollectionName">Default collection name.</param>
		/// <param name="BlockSize">Size of a block in the B-tree. The size must be a power of two, and should be at least the same
		/// size as a sector on the storage device. Smaller block sizes (2, 4 kB) are suitable for online transaction processing, where
		/// a lot of updates to the database occurs. Larger block sizes (8, 16, 32 kB) are suitable for decision support systems.
		/// The block sizes also limit the size of objects stored directly in the file. Objects larger than
		/// <param name="BlocksInCache">Maximum number of blocks in in-memory cache. This cache is used by all files governed by the
		/// database provider. The cache does not contain BLOB blocks.</param>
		/// <param name="BlobBlockSize">Size of a block in the BLOB file. The size must be a power of two. The BLOB file will consist
		/// of a doubly linked list of blocks of this size.</param>
		/// <param name="Encoding">Encoding to use for text properties.</param>
		/// <param name="TimeoutMilliseconds">Timeout, in milliseconds, to wait for access to the database layer.</param>
#if NETSTANDARD1_5
		/// <param name="Encrypted">If the files should be encrypted or not.</param>
#endif
		public FilesProvider(string Folder, string DefaultCollectionName, int BlockSize, int BlocksInCache, int BlobBlockSize,
#if NETSTANDARD1_5
			Encoding Encoding, int TimeoutMilliseconds, bool Encrypted)
			: this(Folder, DefaultCollectionName, BlockSize, BlocksInCache, BlobBlockSize, Encoding, TimeoutMilliseconds, Encrypted, false)
#else
			Encoding Encoding, int TimeoutMilliseconds)
			: this(Folder, DefaultCollectionName, BlockSize, BlocksInCache, BlobBlockSize, Encoding, TimeoutMilliseconds, false)
#endif
		{
		}

		/// <summary>
		/// Persists objects into binary files.
		/// </summary>
		/// <param name="Folder">Folder to store data files.</param>
		/// <param name="DefaultCollectionName">Default collection name.</param>
		/// <param name="BlockSize">Size of a block in the B-tree. The size must be a power of two, and should be at least the same
		/// size as a sector on the storage device. Smaller block sizes (2, 4 kB) are suitable for online transaction processing, where
		/// a lot of updates to the database occurs. Larger block sizes (8, 16, 32 kB) are suitable for decision support systems.
		/// The block sizes also limit the size of objects stored directly in the file. Objects larger than
		/// <param name="BlocksInCache">Maximum number of blocks in in-memory cache. This cache is used by all files governed by the
		/// database provider. The cache does not contain BLOB blocks.</param>
		/// <param name="BlobBlockSize">Size of a block in the BLOB file. The size must be a power of two. The BLOB file will consist
		/// of a doubly linked list of blocks of this size.</param>
		/// <param name="Encoding">Encoding to use for text properties.</param>
		/// <param name="TimeoutMilliseconds">Timeout, in milliseconds, to wait for access to the database layer.</param>
#if NETSTANDARD1_5
		/// <param name="Encrypted">If the files should be encrypted or not.</param>
#endif
		/// <param name="Debug">If the provider is run in debug mode.</param>
		public FilesProvider(string Folder, string DefaultCollectionName, int BlockSize, int BlocksInCache, int BlobBlockSize,
#if NETSTANDARD1_5
			Encoding Encoding, int TimeoutMilliseconds, bool Encrypted, bool Debug)
#else
			Encoding Encoding, int TimeoutMilliseconds, bool Debug)
#endif
		{
			ObjectBTreeFile.CheckBlockSizes(BlockSize, BlobBlockSize);

			if (TimeoutMilliseconds <= 0)
				throw new ArgumentException("The timeout must be positive.", nameof(TimeoutMilliseconds));

			this.id = Guid.NewGuid().ToString().Replace("-", string.Empty);
			this.defaultCollectionName = DefaultCollectionName;
			this.folder = Path.GetFullPath(Folder);
			this.debug = Debug;
			this.blockSize = BlockSize;
			this.blobBlockSize = BlobBlockSize;
			this.encoding = Encoding;
			this.timeoutMilliseconds = TimeoutMilliseconds;
			this.serializers = new Dictionary<Type, Serialization.IObjectSerializer>();
#if NETSTANDARD1_5
			this.encrypted = Encrypted;
#endif

			if (!string.IsNullOrEmpty(this.folder) && this.folder[this.folder.Length - 1] != Path.DirectorySeparatorChar)
				this.folder += Path.DirectorySeparatorChar;

			IObjectSerializer S;
			TypeInfo TI;

			foreach (Type T in Types.GetTypesImplementingInterface(typeof(IObjectSerializer)))
			{
				TI = T.GetTypeInfo();
				if (TI.IsAbstract)
					continue;

				try
				{
					S = (IObjectSerializer)Activator.CreateInstance(T);
				}
				catch (Exception)
				{
					continue;
				}

				this.serializers[S.ValueType] = S;
			}

			GenericObjectSerializer GenericObjectSerializer = new GenericObjectSerializer(this);

			this.serializers[typeof(GenericObject)] = GenericObjectSerializer;
			this.serializers[typeof(object)] = GenericObjectSerializer;

			this.blocks = new Cache<long, byte[]>(BlocksInCache, TimeSpan.MaxValue, new TimeSpan(0, 1, 0, 0, 0));

			this.master = new StringDictionary(this.folder + "Files.master", string.Empty, string.Empty, this, false);

			this.GetFile(this.defaultCollectionName).Wait();

			this.LoadConfiguration().Wait();
		}

		private static readonly char[] CRLF = new char[] { '\r', '\n' };

		#endregion

		#region Properties

		/// <summary>
		/// Default collection name.
		/// </summary>
		public string DefaultCollectionName
		{
			get { return this.defaultCollectionName; }
		}

		/// <summary>
		/// Base folder of where files will be stored.
		/// </summary>
		public string Folder
		{
			get { return this.folder; }
		}

		/// <summary>
		/// An ID of the files provider. It's unique, and constant during the life-time of the FilesProvider class.
		/// </summary>
		public string Id
		{
			get { return this.id; }
		}

		/// <summary>
		/// Size of a block in the B-tree. The size must be a power of two, and should be at least the same
		/// size as a sector on the storage device. Smaller block sizes (2, 4 kB) are suitable for online transaction processing, where
		/// a lot of updates to the database occurs. Larger block sizes (8, 16, 32 kB) are suitable for decision support systems.
		/// The block sizes also limit the size of objects stored directly in the file. Objects larger than
		/// <see cref="InlineObjectSizeLimit"/> will be persisted as BLOBs, with the bulk of the object stored as separate files. 
		/// Smallest block size = 1024, largest block size = 65536.
		/// </summary>
		public int BlockSize { get { return this.blockSize; } }

		/// <summary>
		/// Size of a block in the BLOB file. The size must be a power of two. The BLOB file will consist
		/// of a doubly linked list of blocks of this size.
		/// </summary>
		public int BlobBlockSize { get { return this.blobBlockSize; } }

		/// <summary>
		/// Encoding to use for text properties.
		/// </summary>
		public Encoding Encoding { get { return this.encoding; } }

		/// <summary>
		/// Timeout, in milliseconds, for database operations.
		/// </summary>
		public int TimeoutMilliseconds
		{
			get { return this.timeoutMilliseconds; }
		}

#if NETSTANDARD1_5
		/// <summary>
		/// If the files should be encrypted or not.
		/// </summary>
		public bool Encrypted
		{
			get { return this.encrypted; }
		}
#endif
		/// <summary>
		/// If the provider is run in debug mode.
		/// </summary>
		public bool Debug
		{
			get { return this.debug; }
		}

		#endregion

		#region IDisposable

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			if (this.master != null)
			{
				this.master.Dispose();
				this.master = null;
			}

			if (this.files != null)
			{
				foreach (ObjectBTreeFile File in this.files.Values)
					File.Dispose();

				this.files.Clear();
			}

			if (this.nameFiles != null)
			{
				foreach (StringDictionary File in this.nameFiles.Values)
					File.Dispose();

				this.nameFiles.Clear();
			}

			if (this.blocks != null)
			{
				this.blocks.Dispose();
				this.blocks = null;
			}

			if (this.serializers != null)
			{
				IDisposable d;

				foreach (IObjectSerializer Serializer in this.serializers.Values)
				{
					d = Serializer as IDisposable;
					if (d != null)
					{
						try
						{
							d.Dispose();
						}
						catch (Exception)
						{
							// Ignore
						}
					}
				}

				this.serializers.Clear();
				this.serializers = null;
			}
		}

		#endregion

		#region Types

		/// <summary>
		/// Returns the type name corresponding to a given field data type code.
		/// </summary>
		/// <param name="FieldDataType">Field data type code.</param>
		/// <returns>Corresponding data type name.</returns>
		public static string GetFieldDataTypeName(uint FieldDataType)
		{
			return GetFieldDataType(FieldDataType).FullName;
		}

		/// <summary>
		/// Returns the type corresponding to a given field data type code.
		/// </summary>
		/// <param name="FieldDataTypeCode">Field data type code.</param>
		/// <returns>Corresponding data type.</returns>
		public static Type GetFieldDataType(uint FieldDataTypeCode)
		{
			switch (FieldDataTypeCode)
			{
				case ObjectSerializer.TYPE_BOOLEAN: return typeof(bool);
				case ObjectSerializer.TYPE_BYTE: return typeof(byte);
				case ObjectSerializer.TYPE_INT16: return typeof(short);
				case ObjectSerializer.TYPE_INT32: return typeof(int);
				case ObjectSerializer.TYPE_INT64: return typeof(long);
				case ObjectSerializer.TYPE_SBYTE: return typeof(sbyte);
				case ObjectSerializer.TYPE_UINT16: return typeof(ushort);
				case ObjectSerializer.TYPE_UINT32: return typeof(uint);
				case ObjectSerializer.TYPE_UINT64: return typeof(ulong);
				case ObjectSerializer.TYPE_DECIMAL: return typeof(decimal);
				case ObjectSerializer.TYPE_DOUBLE: return typeof(double);
				case ObjectSerializer.TYPE_SINGLE: return typeof(float);
				case ObjectSerializer.TYPE_DATETIME: return typeof(DateTime);
				case ObjectSerializer.TYPE_TIMESPAN: return typeof(TimeSpan);
				case ObjectSerializer.TYPE_CHAR: return typeof(char);
				case ObjectSerializer.TYPE_STRING: return typeof(string);
				case ObjectSerializer.TYPE_ENUM: return typeof(Enum);
				case ObjectSerializer.TYPE_BYTEARRAY: return typeof(byte[]);
				case ObjectSerializer.TYPE_GUID: return typeof(Guid);
				case ObjectSerializer.TYPE_ARRAY: return typeof(Array);
				case ObjectSerializer.TYPE_OBJECT: return typeof(object);
				default: throw new Exception("Unrecognized field code: " + FieldDataTypeCode.ToString());
			}
		}

		/// <summary>
		/// Returns the type code corresponding to a given field data type.
		/// </summary>
		/// <param name="Value">Field data value.</param>
		/// <returns>Corresponding data type code.</returns>
		public static uint GetFieldDataTypeCode(object Value)
		{
			if (Value == null)
				return ObjectSerializer.TYPE_NULL;
			else
				return GetFieldDataTypeCode(Value.GetType());
		}

		/// <summary>
		/// Returns the type code corresponding to a given field data type.
		/// </summary>
		/// <param name="FieldDataType">Field data type.</param>
		/// <returns>Corresponding data type code.</returns>
		public static uint GetFieldDataTypeCode(Type FieldDataType)
		{
			TypeInfo TI = FieldDataType.GetTypeInfo();
			if (TI.IsEnum)
			{
				if (TI.IsDefined(typeof(FlagsAttribute), false))
					return ObjectSerializer.TYPE_INT32;
				else
					return ObjectSerializer.TYPE_ENUM;
			}

			if (FieldDataType == typeof(bool))
				return ObjectSerializer.TYPE_BOOLEAN;
			else if (FieldDataType == typeof(byte))
				return ObjectSerializer.TYPE_BYTE;
			else if (FieldDataType == typeof(short))
				return ObjectSerializer.TYPE_INT16;
			else if (FieldDataType == typeof(int))
				return ObjectSerializer.TYPE_INT32;
			else if (FieldDataType == typeof(long))
				return ObjectSerializer.TYPE_INT64;
			else if (FieldDataType == typeof(sbyte))
				return ObjectSerializer.TYPE_SBYTE;
			else if (FieldDataType == typeof(ushort))
				return ObjectSerializer.TYPE_UINT16;
			else if (FieldDataType == typeof(uint))
				return ObjectSerializer.TYPE_UINT32;
			else if (FieldDataType == typeof(ulong))
				return ObjectSerializer.TYPE_UINT64;
			else if (FieldDataType == typeof(decimal))
				return ObjectSerializer.TYPE_DECIMAL;
			else if (FieldDataType == typeof(double))
				return ObjectSerializer.TYPE_DOUBLE;
			else if (FieldDataType == typeof(float))
				return ObjectSerializer.TYPE_SINGLE;
			else if (FieldDataType == typeof(DateTime))
				return ObjectSerializer.TYPE_DATETIME;
			else if (FieldDataType == typeof(char))
				return ObjectSerializer.TYPE_CHAR;
			else if (FieldDataType == typeof(string))
				return ObjectSerializer.TYPE_STRING;
			else if (FieldDataType == typeof(TimeSpan))
				return ObjectSerializer.TYPE_TIMESPAN;
			else if (FieldDataType == typeof(byte[]))
				return ObjectSerializer.TYPE_BYTEARRAY;
			else if (FieldDataType == typeof(Guid))
				return ObjectSerializer.TYPE_GUID;
			else if (TI.IsArray)
				return ObjectSerializer.TYPE_ARRAY;
			else if (FieldDataType == typeof(void))
				return ObjectSerializer.TYPE_NULL;
			else
				return ObjectSerializer.TYPE_OBJECT;
		}

		/// <summary>
		/// Gets the object serializer corresponding to a specific type.
		/// </summary>
		/// <param name="Type">Type of object to serialize.</param>
		/// <returns>Object Serializer</returns>
		public IObjectSerializer GetObjectSerializer(Type Type)
		{
			IObjectSerializer Result;
			TypeInfo TI = Type.GetTypeInfo();

			lock (this.synchObj)
			{
				if (this.serializers.TryGetValue(Type, out Result))
					return Result;

				if (TI.IsEnum)
					Result = new EnumSerializer(Type);
				else if (Type.IsArray)
				{
					Type ElementType = Type.GetElementType();
					Type T = Types.GetType(typeof(ByteArraySerializer).FullName.Replace("ByteArray", "Array"));
					Type SerializerType = T.MakeGenericType(new Type[] { ElementType });
					Result = (IObjectSerializer)Activator.CreateInstance(SerializerType, this);
				}
				else if (TI.IsGenericType)
				{
					Type GT = Type.GetGenericTypeDefinition();
					if (GT == typeof(Nullable<>))
					{
						Type NullableType = Type.GenericTypeArguments[0];

						if (NullableType.GetTypeInfo().IsEnum)
							Result = new Serialization.NullableTypes.NullableEnumSerializer(NullableType);
						else
							Result = null;
					}
					else
						Result = null;
				}
				else
					Result = null;

				if (Result != null)
				{
					this.serializers[Type] = Result;
					return Result;
				}
			}

			try
			{
				Result = new ObjectSerializer(Type, this, this.debug);

				lock (this.synchObj)
				{
					this.serializers[Type] = Result;
				}
			}
			catch (Exception ex)
			{
				lock (this.synchObj)
				{
					if (this.serializers.TryGetValue(Type, out Result))
						return Result;
				}

				ExceptionDispatchInfo.Capture(ex).Throw();
			}

			return Result;
		}

		/// <summary>
		/// Gets the object serializer corresponding to a specific object.
		/// </summary>
		/// <param name="Object">Object to serialize</param>
		/// <returns>Object Serializer</returns>
		public ObjectSerializer GetObjectSerializerEx(object Object)
		{
			return this.GetObjectSerializerEx(Object.GetType());
		}

		/// <summary>
		/// Gets the object serializer corresponding to a specific object.
		/// </summary>
		/// <param name="Type">Type of object to serialize.</param>
		/// <returns>Object Serializer</returns>
		public ObjectSerializer GetObjectSerializerEx(Type Type)
		{
			ObjectSerializer Serializer = this.GetObjectSerializer(Type) as ObjectSerializer;
			if (Serializer == null)
				throw new Exception("Objects of type " + Type.FullName + " must be embedded.");

			return Serializer;
		}

		internal static void Wait(Task Task, int TimeoutMilliseconds)
		{
			if (!Task.Wait(TimeoutMilliseconds))
				throw TimeoutException(null);
		}

		internal static TimeoutException TimeoutException(string Trace)
		{
			StringBuilder sb = new StringBuilder();
			string s;

			sb.Append("Unable to get access to underlying database.");

			if (Trace != null)
			{
				sb.AppendLine();
				sb.AppendLine();
				sb.AppendLine("Database locked from:");
				sb.AppendLine();

				foreach (string Frame in Trace.Split(CRLF, StringSplitOptions.RemoveEmptyEntries))
				{
					s = Frame.TrimStart();
					if (s.Contains(" System.Runtime.CompilerServices") || s.Contains(" System.Threading"))
						continue;

					sb.AppendLine(s);
				}
			}

			return new TimeoutException(sb.ToString());
		}

		#endregion

		#region Fields

		/// <summary>
		/// Gets the code for a specific field in a collection.
		/// </summary>
		/// <param name="Collection">Name of collection.</param>
		/// <param name="FieldName">Name of field.</param>
		/// <returns>Field code.</returns>
		public ulong GetFieldCode(string Collection, string FieldName)
		{
			if (string.IsNullOrEmpty(Collection))
				Collection = this.defaultCollectionName;

			Dictionary<string, ulong> List;
			Dictionary<ulong, string> List2;
			StringDictionary Names;
			ulong Result;

			lock (this.synchObj)
			{
				if (this.codeByFieldByCollection.TryGetValue(Collection, out List))
				{
					if (List.TryGetValue(FieldName, out Result))
						return Result;

					List2 = this.fieldByCodeByCollection[Collection];

					Result = (uint)List.Count + 1;

					List[FieldName] = Result;
					List2[Result] = FieldName;
				}
				else
				{
					Result = 0;
					List = null;
					List2 = null;
				}
			}

			if (List == null)
			{
				Task<ulong> Task = this.GetFieldCodeAsync(Collection, FieldName);
				FilesProvider.Wait(Task, this.timeoutMilliseconds);
				return Task.Result;
			}
			else
			{
				lock (this.files)
				{
					Names = this.nameFiles[Collection];
				}

				Task Task = Names.AddAsync(FieldName, Result, true);    // Add asynchronously
				return Result;
			}
		}

		/// <summary>
		/// Gets the code for a specific field in a collection.
		/// </summary>
		/// <param name="Collection">Name of collection.</param>
		/// <param name="FieldName">Name of field.</param>
		/// <returns>Field code.</returns>
		public async Task<ulong> GetFieldCodeAsync(string Collection, string FieldName)
		{
			if (string.IsNullOrEmpty(Collection))
				Collection = this.defaultCollectionName;

			Dictionary<string, ulong> List;
			Dictionary<ulong, string> List2;
			StringDictionary Names;
			ulong Result;

			lock (this.synchObj)
			{
				if (this.codeByFieldByCollection.TryGetValue(Collection, out List))
				{
					if (List.TryGetValue(FieldName, out Result))
						return Result;

					List2 = this.fieldByCodeByCollection[Collection];

					Result = (uint)List.Count + 1;

					List[FieldName] = Result;
					List2[Result] = FieldName;
				}
				else
				{
					Result = 0;
					List = null;
					List2 = null;
				}
			}

			if (List == null)
			{
				await this.GetFile(Collection);
				return await this.GetFieldCodeAsync(Collection, FieldName);
			}
			else
			{
				lock (this.files)
				{
					Names = this.nameFiles[Collection];
				}

				Task Task = Names.AddAsync(FieldName, Result, true);    // Add asynchronously
				return Result;
			}
		}

		/// <summary>
		/// Gets the name of a field in a collection, given its code.
		/// </summary>
		/// <param name="Collection">Name of collection.</param>
		/// <param name="FieldCode">Field code.</param>
		/// <returns>Field name.</returns>
		/// <exception cref="ArgumentException">If the collection or field code are not known.</exception>
		public string GetFieldName(string Collection, ulong FieldCode)
		{
			Task<string> Result = this.GetFieldNameAsync(Collection, FieldCode);
			FilesProvider.Wait(Result, this.timeoutMilliseconds);
			return Result.Result;
		}

		/// <summary>
		/// Gets the name of a field in a collection, given its code.
		/// </summary>
		/// <param name="Collection">Name of collection.</param>
		/// <param name="FieldCode">Field code.</param>
		/// <returns>Field name.</returns>
		/// <exception cref="ArgumentException">If the collection or field code are not known.</exception>
		public async Task<string> GetFieldNameAsync(string Collection, ulong FieldCode)
		{
			if (string.IsNullOrEmpty(Collection))
				Collection = this.defaultCollectionName;

			Dictionary<ulong, string> List2;
			string Result;

			lock (this.synchObj)
			{
				if (this.fieldByCodeByCollection.TryGetValue(Collection, out List2))
				{
					if (List2.TryGetValue(FieldCode, out Result))
						return Result;
					else
						throw new ArgumentException("Field code unknown: " + FieldCode.ToString(), nameof(FieldCode));
				}
				else
				{
					Result = null;
					List2 = null;
				}
			}

			if (List2 == null)
			{
				await this.GetFile(Collection);
				Result = await this.GetFieldNameAsync(Collection, FieldCode);
			}

			return Result;
		}

		#endregion

		#region Blocks

		/// <summary>
		/// Removes all blocks pertaining to a specific file.
		/// </summary>
		/// <param name="FileId">Internal file ID.</param>
		internal void RemoveBlocks(int FileId)
		{
			long Min = this.GetBlockKey(FileId, 0);
			long Max = this.GetBlockKey(FileId, uint.MaxValue);

			foreach (long Key in this.blocks.GetKeys())
			{
				if (Key >= Min && Key <= Max)
					this.blocks.Remove(Key);
			}
		}

		/// <summary>
		/// Removes a particular block from the cache.
		/// </summary>
		/// <param name="FileId">Internal file ID.</param>
		/// <param name="BlockIndex">Block index.</param>
		internal void RemoveBlock(int FileId, uint BlockIndex)
		{
			this.blocks.Remove(this.GetBlockKey(FileId, BlockIndex));
		}

		/// <summary>
		/// Calculates a block key value.
		/// </summary>
		/// <param name="FileId">Internal file ID.</param>
		/// <param name="BlockIndex">Block index.</param>
		/// <returns>Key value.</returns>
		private long GetBlockKey(int FileId, uint BlockIndex)
		{
			long Key = FileId;
			Key <<= 32;
			Key += BlockIndex;

			return Key;
		}

		/// <summary>
		/// Tries to get a cached block.
		/// </summary>
		/// <param name="FileId">Internal file ID.</param>
		/// <param name="BlockIndex">Block index.</param>
		/// <param name="Block">Cached block, if found.</param>
		/// <returns>If block was found in cache.</returns>
		internal bool TryGetBlock(int FileId, uint BlockIndex, out byte[] Block)
		{
			return this.blocks.TryGetValue(this.GetBlockKey(FileId, BlockIndex), out Block);
		}

		/// <summary>
		/// Adds a block to the cache.
		/// </summary>
		/// <param name="FileId">Internal file ID.</param>
		/// <param name="BlockIndex">Block index.</param>
		/// <param name="Block">Block.</param>
		internal void AddBlockToCache(int FileId, uint BlockIndex, byte[] Block)
		{
			this.blocks.Add(this.GetBlockKey(FileId, BlockIndex), Block);
		}

		#endregion

		#region Files

		/// <summary>
		/// Gets a new file ID.
		/// </summary>
		/// <returns></returns>
		internal int GetNewFileId()
		{
			lock (this.synchObjNrFiles)
			{
				return this.nrFiles++;
			}
		}

		/// <summary>
		/// Gets the BTree file corresponding to a named collection.
		/// </summary>
		/// <param name="CollectionName">Name of collection.</param>
		/// <returns>BTree file corresponding to the given collection.</returns>
		public async Task<ObjectBTreeFile> GetFile(string CollectionName)
		{
			ObjectBTreeFile File;

			if (string.IsNullOrEmpty(CollectionName))
				CollectionName = this.defaultCollectionName;

			string s = this.GetFileName(CollectionName);
			KeyValuePair<string, object>[] Strings;
			StringDictionary Names;

			lock (this.files)
			{
				if (this.files.TryGetValue(CollectionName, out File))
					return File;

				File = new ObjectBTreeFile(s + ".btree", CollectionName, s + ".blob", this.blockSize, this.blobBlockSize,
#if NETSTANDARD1_5
					this, this.encoding, this.timeoutMilliseconds, this.encrypted, this.debug);
#else
					this, this.encoding, this.timeoutMilliseconds, this.debug);
#endif

				this.files[CollectionName] = File;

				Names = new StringDictionary(s + ".names", string.Empty, CollectionName, this, false);
				this.nameFiles[CollectionName] = Names;
			}

			Strings = await Names.ToArrayAsync();

			lock (this.synchObj)
			{
				Dictionary<string, ulong> List = new Dictionary<string, ulong>();
				this.codeByFieldByCollection[CollectionName] = List;

				Dictionary<ulong, string> List2 = new Dictionary<ulong, string>();
				this.fieldByCodeByCollection[CollectionName] = List2;

				foreach (KeyValuePair<string, object> P in Strings)
				{
					List[P.Key] = (ulong)P.Value;
					List2[(ulong)P.Value] = P.Key;
				}
			}

			StringBuilder sb = new StringBuilder();

			sb.AppendLine("Collection");
			sb.AppendLine(CollectionName);

			KeyValuePair<bool, KeyValuePair<string, object>> P2 = await this.master.TryGetValueAsync(File.FileName);
			string s2 = sb.ToString();

			if (!P2.Key || !(P2.Value.Value is string) || ((string)P2.Value.Value) != s2)
				await this.master.AddAsync(File.FileName, s2, true);

			await this.GetFieldCodeAsync(null, CollectionName);

			return File;
		}

		/// <summary>
		/// Tries to get the names dictionary for a given collection.
		/// </summary>
		/// <param name="CollectionName">Collection name.</param>
		/// <param name="Names">Names dictionary, if found.</param>
		/// <returns>If a names dictionary was found for the given collection.</returns>
		public bool TryGetNamesFile(string CollectionName, out StringDictionary Names)
		{
			lock (this.files)
			{
				return this.nameFiles.TryGetValue(CollectionName, out Names);
			}
		}

		/// <summary>
		/// Gets an index file.
		/// </summary>
		/// <param name="File">Object file.</param>
		/// <param name="RegenerationOptions">Index regeneration options.</param>
		/// <param name="FieldNames">Indexed fields.</param>
		/// <returns>Index file.</returns>
		public async Task<IndexBTreeFile> GetIndexFile(ObjectBTreeFile File, RegenerationOptions RegenerationOptions, params string[] FieldNames)
		{
			IndexBTreeFile IndexFile;
			IndexBTreeFile[] Indices = File.Indices;
			string[] Fields;
			int i, c;
			bool Regenerate = (RegenerationOptions == RegenerationOptions.Regenerate);

			foreach (IndexBTreeFile I in Indices)
			{
				if ((c = (Fields = I.FieldNames).Length) != FieldNames.Length)
					continue;

				for (i = 0; i < c; i++)
				{
					if (Fields[i] != FieldNames[i])
						break;
				}

				if (i < c)
					continue;

				if (Regenerate)
					await I.Regenerate();

				return I;
			}

			if (RegenerationOptions == RegenerationOptions.RegenerateIfIndexNotInstantiated)
				Regenerate = true;

			StringBuilder sb = new StringBuilder();

#if NETSTANDARD1_5
			byte[] Hash;

			foreach (string FieldName in FieldNames)
			{
				sb.Append('.');
				sb.Append(FieldName);
			}

			using (SHA1 Sha1 = SHA1.Create())
			{
				Hash = Sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
			}

			sb.Clear();

			sb.Append(File.FileName);
			sb.Append('.');

			foreach (byte b in Hash)
				sb.Append(b.ToString("x2"));
#else
			sb.Append(File.FileName);

			foreach (string FieldName in FieldNames)
			{
				sb.Append('.');
				sb.Append(FieldName);
			}
#endif
			sb.Append(".index");

			string s = sb.ToString();
			bool Exists = System.IO.File.Exists(s);

			if (!Exists && RegenerationOptions == RegenerationOptions.RegenerateIfFileNotFound)
				Regenerate = true;

			lock (this.synchObj)
			{
				IndexFile = new IndexBTreeFile(s, File, this, FieldNames);
			}

			await File.AddIndex(IndexFile, Regenerate);

			sb.Clear();

			sb.AppendLine("Index");
			sb.AppendLine(File.CollectionName);

			foreach (string FieldName in FieldNames)
				sb.AppendLine(FieldName);

			if (s.StartsWith(this.folder))
				s = s.Substring(this.folder.Length);

			KeyValuePair<bool, KeyValuePair<string, object>> P = await this.master.TryGetValueAsync(s);
			string s2 = sb.ToString();

			if (!P.Key || !(P.Value.Value is string) || ((string)P.Value.Value) != s2)
				await this.master.AddAsync(s, s2, true);

			return IndexFile;
		}

		/// <summary>
		/// Closes files related to a collection.
		/// </summary>
		/// <param name="CollectionName">Collection.</param>
		/// <returns>If a collection with the given name was found and closed.</returns>
		public bool CloseFile(string CollectionName)
		{
			ObjectBTreeFile File;
			StringDictionary Names;

			if (string.IsNullOrEmpty(CollectionName))
				CollectionName = this.defaultCollectionName;

			lock (this.files)
			{
				if (!this.files.TryGetValue(CollectionName, out File))
					return false;

				this.files.Remove(CollectionName);

				Names = this.nameFiles[CollectionName];
				this.nameFiles.Remove(CollectionName);
			}

			int FileId = File.Id;
			int NamesId = Names.DictionaryFile.Id;

			File.Dispose();
			Names.Dispose();

			lock (this.synchObj)
			{
				this.codeByFieldByCollection.Remove(CollectionName);
				this.fieldByCodeByCollection.Remove(CollectionName);
			}

			this.RemoveBlocks(FileId);
			this.RemoveBlocks(NamesId);

			return true;
		}

		/// <summary>
		/// Gets the file name root that corresponds to a given collection.
		/// </summary>
		/// <param name="CollectionName">Collection name.</param>
		/// <returns>File name root.</returns>
		public string GetFileName(string CollectionName)
		{
			string s = CollectionName;
			char[] ch = null;
			int i = 0;

			while ((i = s.IndexOfAny(forbiddenCharacters, i)) >= 0)
			{
				if (ch == null)
					ch = s.ToCharArray();

				ch[i] = '_';
			}

			if (ch != null)
				s = new string(ch);

			s = this.folder + s;

			return s;
		}

		private static readonly char[] forbiddenCharacters = Path.GetInvalidFileNameChars();

		/// <summary>
		/// Loads the configuration from the master file.
		/// </summary>
		/// <returns>Task object</returns>
		private async Task LoadConfiguration()
		{
			foreach (KeyValuePair<string, object> P in this.master)
			{
				string s = P.Value.ToString();
				string[] Rows = s.Split(CRLF, StringSplitOptions.RemoveEmptyEntries);

				switch (Rows[0])
				{
					case "Collection":
						if (Rows.Length < 2)
							break;

						string CollectionName = Rows[1];
						ObjectBTreeFile File = await this.GetFile(CollectionName);
						break;

					case "Index":
						if (Rows.Length < 3)
							break;

						CollectionName = Rows[1];
						string[] FieldNames = new string[Rows.Length - 2];
						Array.Copy(Rows, 2, FieldNames, 0, Rows.Length - 2);

						File = await this.GetFile(CollectionName);
						await this.GetIndexFile(File, RegenerationOptions.RegenerateIfFileNotFound, FieldNames);
						break;
				}
			}
		}

		#endregion

		#region Objects

		/// <summary>
		/// Loads an object given its Object ID <paramref name="ObjectId"/> and its base type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Base type.</typeparam>
		/// <param name="ObjectId">Object ID</param>
		/// <returns>Loaded object.</returns>
		public Task<T> LoadObject<T>(object ObjectId)
		{
			Guid OID;

			if (ObjectId is Guid)
				OID = (Guid)ObjectId;
			else if (ObjectId is string)
				OID = new Guid((string)ObjectId);
			else if (ObjectId is byte[])
				OID = new Guid((byte[])ObjectId);
			else
				throw new NotSupportedException("Unsupported type for Object ID: " + ObjectId.GetType().FullName);

			return this.LoadObject<T>(OID);
		}

		/// <summary>
		/// Loads an object given its Object ID <paramref name="ObjectId"/> and its base type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Base type.</typeparam>
		/// <param name="ObjectId">Object ID</param>
		/// <returns>Loaded object.</returns>
		public Task<T> LoadObject<T>(Guid ObjectId)
		{
			return this.LoadObject<T>(ObjectId, null);
		}

		/// <summary>
		/// Loads an object given its Object ID <paramref name="ObjectId"/> and its base type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Base type.</typeparam>
		/// <param name="ObjectId">Object ID</param>
		/// <param name="EmbeddedSetter">Setter method, used to set an embedded property during delayed loading.</param>
		/// <returns>Loaded object.</returns>
		public async Task<T> LoadObject<T>(Guid ObjectId, EmbeddedObjectSetter EmbeddedSetter)
		{
			ObjectSerializer Serializer = this.GetObjectSerializerEx(typeof(T));
			ObjectBTreeFile File = await this.GetFile(Serializer.CollectionName);

			if (EmbeddedSetter != null)
			{
				if (await File.TryLock(0))
				{
					try
					{
						return (T)await File.LoadObjectLocked(ObjectId, Serializer);
					}
					finally
					{
						await File.Release();
					}
				}
				else
				{
					File.QueueForLoad(ObjectId, Serializer, EmbeddedSetter);
					return default(T);
				}
			}
			else
				return (T)await File.LoadObject(ObjectId, Serializer);
		}

		/// <summary>
		/// Loads an object given its Object ID <paramref name="ObjectId"/> and its base type <typeparamref name="T"/>.
		/// </summary>
		/// <param name="T">Base type.</param>
		/// <param name="ObjectId">Object ID</param>
		/// <param name="EmbeddedSetter">Setter method, used to set an embedded property during delayed loading.</param>
		/// <returns>Loaded object.</returns>
		public async Task<object> LoadObject(Type T, Guid ObjectId, EmbeddedObjectSetter EmbeddedSetter)
		{
			ObjectSerializer Serializer = this.GetObjectSerializerEx(T);
			ObjectBTreeFile File = await this.GetFile(Serializer.CollectionName);

			if (EmbeddedSetter != null)
			{
				if (await File.TryLock(0))
				{
					try
					{
						return await File.LoadObjectLocked(ObjectId, Serializer);
					}
					finally
					{
						await File.Release();
					}
				}
				else
				{
					File.QueueForLoad(ObjectId, Serializer, EmbeddedSetter);
					return null;
				}
			}
			else
				return await File.LoadObject(ObjectId, Serializer);
		}

		/// <summary>
		/// Gets the Object ID for a given object.
		/// </summary>
		/// <param name="Value">Object reference.</param>
		/// <param name="InsertIfNotFound">Insert object into database with new Object ID, if no Object ID is set.</param>
		/// <returns>Object ID for <paramref name="Value"/>.</returns>
		/// <exception cref="NotSupportedException">Thrown, if the corresponding class does not have an Object ID property, 
		/// or if the corresponding property type is not supported.</exception>
		public Task<Guid> GetObjectId(object Value, bool InsertIfNotFound)
		{
			ObjectSerializer Serializer = this.GetObjectSerializerEx(Value);
			return Serializer.GetObjectId(Value, InsertIfNotFound);
		}

		#endregion

		#region IDatabaseProvider

		/// <summary>
		/// Inserts an object into the database.
		/// </summary>
		/// <param name="Object">Object to insert.</param>
		public async Task Insert(object Object)
		{
			ObjectSerializer Serializer = this.GetObjectSerializerEx(Object);
			ObjectBTreeFile File = await this.GetFile(Serializer.CollectionName);
			await File.SaveNewObject(Object, Serializer);
		}

		/// <summary>
		/// Inserts a collection of objects into the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public async Task Insert(params object[] Objects)
		{
			foreach (object Object in Objects)
				await this.Insert(Object);
		}

		/// <summary>
		/// Inserts a collection of objects into the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public async Task Insert(IEnumerable<object> Objects)
		{
			foreach (object Object in Objects)
				await this.Insert(Object);
		}

		/// <summary>
		/// Finds objects of a given class <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">Class defining how to deserialize objects found.</typeparam>
		/// <param name="Offset">Result offset.</param>
		/// <param name="MaxCount">Maximum number of objects to return.</param>
		/// <param name="SortOrder">Sort order.</param>
		/// <param name="SortOrder">Sort order. Each string represents a field name. By default, sort order is ascending.
		/// If descending sort order is desired, prefix the field name by a hyphen (minus) sign.</param>
		/// <returns>Objects found.</returns>
		public async Task<IEnumerable<T>> Find<T>(int Offset, int MaxCount, params string[] SortOrder)
		{
			ObjectSerializer Serializer = this.GetObjectSerializerEx(typeof(T));
			ObjectBTreeFile File = await this.GetFile(Serializer.CollectionName);
			using (ICursor<T> ResultSet = await File.Find<T>(Offset, MaxCount, null, true, SortOrder))
			{
				return await this.LoadAll<T>(ResultSet);
			}
		}

		private async Task<IEnumerable<T>> LoadAll<T>(ICursor<T> ResultSet)
		{
			LinkedList<T> Result = new LinkedList<T>();

			while (await ResultSet.MoveNextAsync())
			{
				if (ResultSet.CurrentTypeCompatible)
					Result.AddLast(ResultSet.Current);
			}

			return Result;
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
		public async Task<IEnumerable<T>> Find<T>(int Offset, int MaxCount, Filter Filter, params string[] SortOrder)
		{
			ObjectSerializer Serializer = this.GetObjectSerializerEx(typeof(T));
			ObjectBTreeFile File = await this.GetFile(Serializer.CollectionName);
			using (ICursor<T> ResultSet = await File.Find<T>(Offset, MaxCount, Filter, true, SortOrder))
			{
				return await this.LoadAll<T>(ResultSet);
			}
		}

		/// <summary>
		/// Updates an object in the database.
		/// </summary>
		/// <param name="Object">Object to insert.</param>
		public async Task Update(object Object)
		{
			GenericObject GenObj = Object as GenericObject;
			if (GenObj == null)
			{
				ObjectSerializer Serializer = this.GetObjectSerializerEx(Object.GetType());
				ObjectBTreeFile File = await this.GetFile(Serializer.CollectionName);
				await File.UpdateObject(Object, Serializer);
			}
			else
			{
				IObjectSerializer Serializer = this.GetObjectSerializer(Object.GetType());
				ObjectBTreeFile File = await this.GetFile(GenObj.CollectionName);
				await File.UpdateObject(GenObj.ObjectId, GenObj, Serializer);
			}
		}

		/// <summary>
		/// Updates a collection of objects in the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public async Task Update(params object[] Objects)
		{
			foreach (object Object in Objects)
				await this.Update(Object);
		}

		/// <summary>
		/// Updates a collection of objects in the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public async Task Update(IEnumerable<object> Objects)
		{
			foreach (object Object in Objects)
				await this.Update(Object);
		}

		/// <summary>
		/// Deletes an object in the database.
		/// </summary>
		/// <param name="Object">Object to insert.</param>
		public async Task Delete(object Object)
		{
			GenericObject GenObj = Object as GenericObject;
			if (GenObj == null)
			{
				ObjectSerializer Serializer = this.GetObjectSerializerEx(Object.GetType());
				ObjectBTreeFile File = await this.GetFile(Serializer.CollectionName);
				await File.DeleteObject(Object, Serializer);
			}
			else
			{
				IObjectSerializer Serializer = this.GetObjectSerializer(Object.GetType());
				ObjectBTreeFile File = await this.GetFile(GenObj.CollectionName);
				await File.DeleteObject(GenObj.ObjectId, Serializer);
			}
		}

		/// <summary>
		/// Deletes a collection of objects in the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public async Task Delete(params object[] Objects)
		{
			foreach (object Object in Objects)
				await this.Delete(Object);
		}

		/// <summary>
		/// Deletes a collection of objects in the database.
		/// </summary>
		/// <param name="Objects">Objects to insert.</param>
		public async Task Delete(IEnumerable<object> Objects)
		{
			foreach (object Object in Objects)
				await this.Delete(Object);
		}

		#endregion

		#region Export

		/// <summary>
		/// Exports the database to XML.
		/// </summary>
		/// <param name="Properties">If object properties should be exported as well.</param>
		/// <returns>Graph XML.</returns>
		public async Task<string> ExportXml(bool Properties)
		{
			StringBuilder Output = new StringBuilder();
			await this.ExportXml(Output, Properties);
			return Output.ToString();
		}

		/// <summary>
		/// Exports the database to XML.
		/// </summary>
		/// <param name="Output">XML Output</param>
		/// <param name="Properties">If object properties should be exported as well.</param>
		/// <returns>Asynchronous task object.</returns>
		public async Task ExportXml(StringBuilder Output, bool Properties)
		{
			XmlWriterSettings Settings = new XmlWriterSettings()
			{
				CloseOutput = false,
				ConformanceLevel = ConformanceLevel.Document,
				Encoding = System.Text.Encoding.UTF8,
				Indent = true,
				IndentChars = "\t",
				NewLineChars = "\r\n",
				NewLineHandling = NewLineHandling.Entitize,
				NewLineOnAttributes = false,
				OmitXmlDeclaration = true
			};

			using (XmlWriter w = XmlWriter.Create(Output, Settings))
			{
				await this.ExportXml(w, Properties);
				w.Flush();
			}
		}

		/// <summary>
		/// Exports the database to XML.
		/// </summary>
		/// <param name="Output">XML Output.</param>
		/// <param name="Properties">If object properties should be exported as well.</param>
		/// <returns>Asynhronous task object.</returns>
		public async Task ExportXml(XmlWriter Output, bool Properties)
		{
			ObjectBTreeFile[] Files;
			int c;

			lock (this.files)
			{
				c = this.files.Count;
				Files = new ObjectBTreeFile[c];
				this.files.Values.CopyTo(Files, 0);
			}

			Output.WriteStartElement("Database", "http://waher.se/Schema/Persistence/Files.xsd");

			foreach (ObjectBTreeFile File in Files)
				await File.ExportGraphXML(Output, Properties);

			Output.WriteEndElement();
		}

		/// <summary>
		/// Performs an export of the entire database.
		/// </summary>
		/// <param name="Output">Database will be output to this interface.</param>
		/// <returns>Task object for synchronization purposes.</returns>
		public async Task Export(IDatabaseExport Output)
		{
			ObjectBTreeFile[] Files;

			lock (this.files)
			{
				Files = new ObjectBTreeFile[this.files.Count];
				this.files.Values.CopyTo(Files, 0);
			}

			Output.StartExport();
			try
			{
				foreach (ObjectBTreeFile File in Files)
				{
					Output.StartCollection(File.CollectionName);
					try
					{
						using (ObjectBTreeFileEnumerator<GenericObject> e = File.GetTypedEnumerator<GenericObject>(true))
						{
							GenericObject Obj;

							while (await e.MoveNextAsync())
							{
								if (e.CurrentTypeCompatible)
								{
									Obj = e.Current;

									Output.StartObject(Obj.ObjectId.ToString(), Obj.TypeName);
									try
									{
										foreach (KeyValuePair<string, object> P in Obj)
											Output.ReportProperty(P.Key, P.Value);
									}
									catch (Exception ex)
									{
										this.ReportException(ex, Output);
									}
									finally
									{
										Output.EndObject();
									}
								}
								else if (e.CurrentObjectId != null)
									Output.ReportError("Unable to load object " + e.CurrentObjectId.ToString() + ".");
							}
						}
					}
					catch (Exception ex)
					{
						this.ReportException(ex, Output);
					}
					finally
					{
						Output.EndCollection();
					}
				}
			}
			catch (Exception ex)
			{
				this.ReportException(ex, Output);
			}
			finally
			{
				Output.EndExport();
			}
		}

		private void ReportException(Exception ex, IDatabaseExport Output)
		{
			AggregateException ex2;

			ex = Waher.Events.Log.UnnestException(ex);

			if ((ex2 = ex as AggregateException) != null)
			{
				foreach (Exception ex3 in ex2.InnerExceptions)
					Output.ReportException(ex3);
			}
			else
				Output.ReportException(ex);
		}

		#endregion

	}
}
