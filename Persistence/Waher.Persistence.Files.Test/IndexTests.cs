﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Waher.Persistence.Filters;
using Waher.Persistence.Files.Test.Classes;
using Waher.Persistence.Files.Serialization;
using Waher.Persistence.Files.Statistics;

namespace Waher.Persistence.Files.Test
{
	[TestFixture]
	public abstract class IndexTests
	{
		internal const string FileName = "Data\\Objects.btree";
		internal const string Index1FileName = "Data\\Objects.btree.index1";
		internal const string Index2FileName = "Data\\Objects.btree.index2";
		internal const string ObjFileName = "Data\\LastObject.bin";
		internal const string ObjIdFileName = "Data\\LastObjectId.bin";
		internal const string BlockSizeFileName = "Data\\BlockSize.bin";
		internal const string Folder = "Data";
		internal const string BlobFileName = "Data\\Objects.blob";
		internal const string CollectionName = "Default";
		internal const int BlocksInCache = 1000;
		internal const int ObjectsToEnumerate = 1000;//0;

		protected ObjectBTreeFile file;
		protected IndexBTreeFile index1;
		protected IndexBTreeFile index2;
		protected FilesProvider provider;
		protected Random gen = new Random();
		protected DateTime start;

		public abstract int BlockSize
		{
			get;
		}

		[SetUp]
		public void SetUp()
		{
			if (File.Exists(FileName + ".bak"))
				File.Delete(FileName + ".bak");

			if (File.Exists(FileName))
			{
				File.Copy(FileName, FileName + ".bak");
				File.Delete(FileName);
			}

			if (File.Exists(BlobFileName + ".bak"))
				File.Delete(BlobFileName + ".bak");

			if (File.Exists(BlobFileName))
			{
				File.Copy(BlobFileName, BlobFileName + ".bak");
				File.Delete(BlobFileName);
			}

			if (File.Exists(Index1FileName + ".bak"))
				File.Delete(Index1FileName + ".bak");

			if (File.Exists(Index1FileName))
			{
				File.Copy(Index1FileName, Index1FileName + ".bak");
				File.Delete(Index1FileName);
			}

			if (File.Exists(Index2FileName + ".bak"))
				File.Delete(Index2FileName + ".bak");

			if (File.Exists(Index2FileName))
			{
				File.Copy(Index2FileName, Index2FileName + ".bak");
				File.Delete(Index2FileName);
			}

			this.provider = new FilesProvider(Folder, CollectionName);
			this.file = new ObjectBTreeFile(FileName, CollectionName, BlobFileName, BlockSize, BlocksInCache, Math.Max(BlockSize / 2, 1024),
				this.provider, Encoding.UTF8, 10000, true);

			this.index1 = new IndexBTreeFile(Index1FileName, BlocksInCache, this.file, this.provider, "Byte", "-DateTime");
			this.file.AddIndex(this.index1, false).Wait();

			this.index2 = new IndexBTreeFile(Index2FileName, BlocksInCache, this.file, this.provider, "ShortString");
			this.file.AddIndex(this.index2, false).Wait();

			this.start = DateTime.Now;
		}

		[TearDown]
		public void TearDown()
		{
			Console.Out.WriteLine("Elapsed time: " + (DateTime.Now - this.start).ToString());

			if (this.file != null)
			{
				this.file.Dispose();
				this.file = null;
			}

			if (this.provider != null)
			{
				this.provider.Dispose();
				this.provider = null;
			}
		}

		public virtual int MaxStringLength
		{
			get { return 100; }
		}

		[Test]
		public async Task Test_01_NormalEnumeration()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
			SortedDictionary<Guid, bool> Objects2 = new SortedDictionary<Guid, bool>();
			GenericObject Prev = null;

			await BTreeTests.ExportXML(this.file, "Data\\BTree.xml");

			foreach (GenericObject Obj in this.index1)
			{
				Objects2[Obj.ObjectId] = true;

				if (Prev != null)
					Assert.Less(this.Index1Compare(Prev, Obj), 0);

				Prev = Obj;
				Assert.IsTrue(Objects.Remove(Obj.ObjectId));
			}

			Assert.AreEqual(0, Objects.Count);

			Prev = null;
			foreach (GenericObject Obj in this.index2)
			{
				if (Prev != null)
					Assert.Less(this.Index2Compare(Prev, Obj), 0);

				Prev = Obj;
				Assert.IsTrue(Objects2.Remove(Obj.ObjectId));
			}

			Assert.AreEqual(0, Objects2.Count);
		}

		private int Index1Compare(GenericObject Obj1, GenericObject Obj2)
		{
			int i = ((IComparable)Obj1["Byte"]).CompareTo(Obj2["Byte"]);
			if (i != 0)
				return i;

			i = ((IComparable)Obj1["DateTime"]).CompareTo(Obj2["DateTime"]);
			if (i != 0)
				return -i;

			return 0;
		}

		private int Index2Compare(GenericObject Obj1, GenericObject Obj2)
		{
			return ((IComparable)Obj1["ShortString"]).CompareTo(Obj2["ShortString"]);
		}

		[Test]
		public async Task Test_02_TypedEnumeration()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
			SortedDictionary<Guid, bool> Objects2 = new SortedDictionary<Guid, bool>();
			Simple Prev = null;
			Simple Obj;
			ulong Rank = 0;

			using (IndexBTreeFileEnumerator<Simple> e = this.index1.GetTypedEnumerator<Simple>(false))
			{
				while (e.MoveNext())
				{
					Obj = e.Current;
					Objects2[Obj.ObjectId] = true;

					if (Prev != null)
						Assert.Less(this.Index1Compare(Prev, Obj), 0);

					Prev = Obj;
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));

					Assert.AreEqual(Rank++, e.CurrentRank);
					Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
				}
			}

			Assert.AreEqual(0, Objects.Count);

			Prev = null;
			Rank = 0;
			using (IndexBTreeFileEnumerator<Simple> e = this.index2.GetTypedEnumerator<Simple>(false))
			{
				while (e.MoveNext())
				{
					Obj = e.Current;
					if (Prev != null)
						Assert.Less(this.Index2Compare(Prev, Obj), 0);

					Prev = Obj;
					Assert.IsTrue(Objects2.Remove(Obj.ObjectId));

					Assert.AreEqual(Rank++, e.CurrentRank);
					Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
				}
			}

			Assert.AreEqual(0, Objects2.Count);
		}

		private int Index1Compare(Simple Obj1, Simple Obj2)
		{
			int i = Obj1.Byte.CompareTo(Obj2.Byte);
			if (i != 0)
				return i;

			i = Obj1.DateTime.CompareTo(Obj2.DateTime);
			if (i != 0)
				return -i;

			return 0;
		}

		private int Index2Compare(Simple Obj1, Simple Obj2)
		{
			return Obj1.ShortString.CompareTo(Obj2.ShortString);
		}

		[Test]
		public async Task Test_03_LockedEnumeration()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
			SortedDictionary<Guid, bool> Objects2 = new SortedDictionary<Guid, bool>();
			Simple Prev = null;
			Simple Obj;
			ulong Rank = 0;

			using (IndexBTreeFileEnumerator<Simple> e = this.index1.GetTypedEnumerator<Simple>(true))
			{
				while (e.MoveNext())
				{
					Obj = e.Current;
					Objects2[Obj.ObjectId] = true;

					if (Prev != null)
						Assert.Less(this.Index1Compare(Prev, Obj), 0);

					Prev = Obj;
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));

					Assert.AreEqual(Rank++, e.CurrentRank);
					Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
				}
			}

			Assert.AreEqual(0, Objects.Count);

			Prev = null;
			Rank = 0;
			using (IndexBTreeFileEnumerator<Simple> e = this.index2.GetTypedEnumerator<Simple>(true))
			{
				while (e.MoveNext())
				{
					Obj = e.Current;
					if (Prev != null)
						Assert.Less(this.Index2Compare(Prev, Obj), 0);

					Prev = Obj;
					Assert.IsTrue(Objects2.Remove(Obj.ObjectId));

					Assert.AreEqual(Rank++, e.CurrentRank);
					Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
				}
			}

			Assert.AreEqual(0, Objects2.Count);
		}

		[Test]
		[ExpectedException]
		public async Task Test_04_UnlockedChangeEnumeration()
		{
			await this.CreateObjects(Math.Min(ObjectsToEnumerate, 1000));
			Simple Obj;

			using (IndexBTreeFileEnumerator<Simple> e = this.index1.GetTypedEnumerator<Simple>(true))
			{
				while (e.MoveNext())
				{
					Obj = e.Current;
					Obj = BTreeTests.CreateSimple(this.MaxStringLength);
					await this.file.SaveNewObject(Obj);
				}
			}
		}

		[Test]
		public async Task Test_05_BackwardsEnumeration()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
			SortedDictionary<Guid, bool> Objects2 = new SortedDictionary<Guid, bool>();
			Simple Prev = null;
			Simple Obj;
			ulong Rank = ObjectsToEnumerate;

			using (IndexBTreeFileEnumerator<Simple> e = this.index1.GetTypedEnumerator<Simple>(true))
			{
				while (e.MovePrevious())
				{
					Obj = e.Current;
					Objects2[Obj.ObjectId] = true;
					if (Prev != null)
						Assert.Greater(this.Index1Compare(Prev, Obj), 0);

					Prev = Obj;
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));

					Assert.AreEqual(--Rank, e.CurrentRank);
					Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
				}
			}

			Assert.AreEqual(0, Objects.Count);

			Prev = null;
			Rank = ObjectsToEnumerate;
			using (IndexBTreeFileEnumerator<Simple> e = this.index2.GetTypedEnumerator<Simple>(true))
			{
				while (e.MovePrevious())
				{
					Obj = e.Current;
					if (Prev != null)
						Assert.Greater(this.Index2Compare(Prev, Obj), 0);

					Prev = Obj;
					Assert.IsTrue(Objects2.Remove(Obj.ObjectId));

					Assert.AreEqual(--Rank, e.CurrentRank);
					Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
				}
			}

			Assert.AreEqual(0, Objects2.Count);
		}

		private async Task<SortedDictionary<Guid, Simple>> CreateObjects(int NrObjects)
		{
			SortedDictionary<Guid, Simple> Result = new SortedDictionary<Guid, Simple>();

			while (NrObjects > 0)
			{
				Simple Obj = BTreeTests.CreateSimple(this.MaxStringLength);
				Guid ObjectId = await this.file.SaveNewObject(Obj);
				Result[ObjectId] = Obj;
				NrObjects--;
			}

			return Result;
		}

		[Test]
		public async Task Test_06_SelectIthObject()
		{
			int c = ObjectsToEnumerate;
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(c);
			SortedDictionary<string, Simple> ObjectsSorted = this.SortObjects(Objects);
			Simple[] Ordered = new Simple[c];
			ObjectsSorted.Values.CopyTo(Ordered, 0);
			Simple Prev = null;
			Simple Obj;
			Random gen = new Random();
			int i, j;

			for (i = 0; i < c; i++)
			{
				j = 0;
				Prev = null;

				if (i < 10 || (gen.Next(0, 2) == 0 && i <= c - 10))
				{
					using (IndexBTreeFileEnumerator<Simple> e = this.index1.GetTypedEnumerator<Simple>(true))
					{
						Assert.IsTrue(await e.GoToObject((uint)i));

						do
						{
							Obj = e.Current;
							if (Prev != null)
								Assert.Less(this.Index1Compare(Prev, Obj), 0);

							Prev = Obj;
							ObjectSerializationTests.AssertEqual(Ordered[i + j], Obj);

							Assert.AreEqual(i + j, e.CurrentRank);
							Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
						}
						while (e.MoveNext() && j++ < 10);
					}
				}
				else
				{
					using (IndexBTreeFileEnumerator<Simple> e = this.index1.GetTypedEnumerator<Simple>(true))
					{
						Assert.IsTrue(await e.GoToObject((uint)i));

						do
						{
							Obj = e.Current;
							if (Prev != null)
								Assert.Greater(this.Index1Compare(Prev, Obj), 0);

							Prev = Obj;
							ObjectSerializationTests.AssertEqual(Ordered[i - j], Obj);

							Assert.AreEqual(i - j, e.CurrentRank);
							Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
						}
						while (e.MovePrevious() && j++ < 10);
					}
				}
			}
		}

		private SortedDictionary<string, Simple> SortObjects(SortedDictionary<Guid, Simple> Objects)
		{
			SortedDictionary<string, Simple> ObjectsSorted = new SortedDictionary<string, Simple>();

			foreach (Simple Obj in Objects.Values)
				ObjectsSorted[Obj.Byte.ToString("D3") + " " + (long.MaxValue - Obj.DateTime.Ticks).ToString()] = Obj;

			return ObjectsSorted;
		}

		[Test]
		public async Task Test_07_RankObject()
		{
			int c = ObjectsToEnumerate;
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(c);
			SortedDictionary<string, Simple> ObjectsSorted = this.SortObjects(Objects);
			Simple[] Ordered = new Simple[c];
			ObjectsSorted.Values.CopyTo(Ordered, 0);
			int i;

			for (i = 0; i < c; i++)
				Assert.AreEqual(i, await this.index1.GetRank(Ordered[i].ObjectId));
		}

		[Test]
		public async Task Test_08_Reset()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
			Simple Prev = null;
			Simple Obj;
			ulong Rank = 0;

			using (IndexBTreeFileEnumerator<Simple> e = this.index1.GetTypedEnumerator<Simple>(false))
			{
				while (e.MoveNext())
				{
					Obj = e.Current;
					if (Prev != null)
						Assert.Less(this.Index1Compare(Prev, Obj), 0);

					Prev = Obj;
					Assert.IsTrue(Objects.ContainsKey(Obj.ObjectId));

					Assert.AreEqual(Rank++, e.CurrentRank);
					Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
				}

				e.Reset();
				Prev = null;

				while (e.MovePrevious())
				{
					Obj = e.Current;
					if (Prev != null)
						Assert.Greater(this.Index1Compare(Prev, Obj), 0);

					Prev = Obj;
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));

					Assert.AreEqual(--Rank, e.CurrentRank);
					Assert.AreEqual(Obj.ObjectId, e.CurrentObjectId);
				}
			}

			Assert.AreEqual(0, Objects.Count);
		}

		[Test]
		public void Test_09_UpdateObjects_1000()
		{
			this.Test_UpdateObjects(1000).Wait();
		}

		[Test]
		public void Test_10_UpdateObjects_10000()
		{
			this.Test_UpdateObjects(10000).Wait();
		}

		[Test]
		public void Test_11_UpdateObjects_100000()
		{
			this.Test_UpdateObjects(100000).Wait();
		}

		private async Task Test_UpdateObjects(int c)
		{
			Dictionary<Guid, Simple> Ordered = new Dictionary<Guid, Simple>();
			Simple[] Objects = new Simple[c];
			Simple Obj;
			int i;

			for (i = 0; i < c; i++)
			{
				Objects[i] = Obj = BTreeTests.CreateSimple(this.MaxStringLength);
				await this.file.SaveNewObject(Obj);
				Ordered[Obj.ObjectId] = Obj;
			}

			await BTreeTests.AssertConsistent(this.file, this.provider, null, null, true);
			Console.Out.WriteLine(await BTreeTests.ExportXML(this.file, "Data\\BTreeBeforeUpdates.xml"));

			for (i = 0; i < c; i++)
			{
				Obj = BTreeTests.CreateSimple(this.MaxStringLength);
				Obj.ObjectId = Objects[i].ObjectId;

				await this.file.UpdateObject(Obj);

				Objects[i] = Obj;

				Obj = await this.file.LoadObject<Simple>(Obj.ObjectId);
				ObjectSerializationTests.AssertEqual(Objects[i], Obj);
			}

			await BTreeTests.AssertConsistent(this.file, this.provider, null, null, true);
			Console.Out.WriteLine(await BTreeTests.ExportXML(this.file, "Data\\BTreeAfterUpdates.xml"));

			for (i = 0; i < c; i++)
			{
				Obj = await this.file.LoadObject<Simple>(Objects[i].ObjectId);
				ObjectSerializationTests.AssertEqual(Objects[i], Obj);
			}

			using (IndexBTreeFileEnumerator<Simple> e = this.index1.GetTypedEnumerator<Simple>(true))
			{
				Obj = null;

				while (e.MoveNext())
				{
					Assert.AreEqual(e.Current.ObjectId, e.CurrentObjectId);
					Assert.IsTrue(Ordered.Remove(e.Current.ObjectId));

					if (Obj != null)
						Assert.Less(this.Index1Compare(Obj, e.Current), 0);

					Obj = e.Current;
				}
			}

			Assert.AreEqual(0, Ordered.Count);
		}

		[Test]
		public async Task Test_12_DeleteObject()
		{
			await this.Test_DeleteObjects(3);
		}

		[Test]
		public async Task Test_13_DeleteObject_100()
		{
			await this.Test_DeleteObjects(100);
		}

		[Test]
		public async Task Test_14_DeleteObject_1000()
		{
			await this.Test_DeleteObjects(1000);
		}

		[Test]
		public async Task Test_15_DeleteObject_10000()
		{
			await this.Test_DeleteObjects(10000);
		}

		[Test]
		public async Task Test_16_DeleteObject_100000()
		{
			await this.Test_DeleteObjects(100000);
		}

		private async Task Test_DeleteObjects(int c)
		{
			Random Gen = new Random();
			Simple[] Objects = new Simple[c];
			Simple Obj;
			int i;

			for (i = 0; i < c; i++)
			{
				Objects[i] = Obj = BTreeTests.CreateSimple(this.MaxStringLength);
				await this.file.SaveNewObject(Obj);
			}

			while (c > 0)
			{
				i = Gen.Next(0, c);

				Obj = Objects[i];
				c--;
				if (i < c)
					Array.Copy(Objects, i + 1, Objects, i, c - i);

				await this.file.DeleteObject(Obj);
			}

			FileStatistics Stat = await BTreeTests.AssertConsistent(this.file, this.provider, null, null, true);

			Assert.AreEqual(0, this.file.Count);
			Assert.AreEqual(0, this.index1.IndexFile.Count);
			Assert.AreEqual(1, Stat.NrBlocks);
			Assert.AreEqual(0, Stat.NrBlobBlocks);
		}

		[Test]
		public async Task Test_17_Clear()
		{
			Simple Obj = BTreeTests.CreateSimple(this.MaxStringLength);
			Guid ObjectId = await this.file.SaveNewObject(Obj);
			Assert.AreNotEqual(Guid.Empty, ObjectId);
			Assert.IsTrue(this.file.Contains(Obj));
			this.file.Clear();
			Assert.IsFalse(this.file.Contains(Obj));
			Assert.AreEqual(0, this.file.Count);
			Assert.AreEqual(0, this.index1.IndexFile.Count);
		}

		[Test]
		public async Task Test_18_FindFirst()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
			int i;

			for (i = 0; i < 256; i++)
			{
				using (IndexBTreeFileEnumerator<Simple> e = await this.index1.FindFirstGreaterOrEqualTo<Simple>(true,
					new KeyValuePair<string, object>("Byte", i)))
				{
					while (e.MoveNext())
					{
						Assert.GreaterOrEqual(e.Current.Byte, i);
						if (e.Current.Byte > i)
							break;
					}

					e.Reset();

					while (e.MovePrevious())
					{
						Assert.Less(e.Current.Byte, i);
						if (e.Current.Byte < i - 1)
							break;
					}
				}
			}
		}

		[Test]
		public async Task Test_19_FindLast()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);
			int i;

			for (i = 0; i < 256; i++)
			{
				using (IndexBTreeFileEnumerator<Simple> e = await this.index1.FindLastLesserOrEqualTo<Simple>(true,
					new KeyValuePair<string, object>("Byte", i)))
				{
					while (e.MovePrevious())
					{
						Assert.LessOrEqual(e.Current.Byte, i);
						if (e.Current.Byte < i)
							break;
					}

					e.Reset();

					while (e.MoveNext())
					{
						Assert.Greater(e.Current.Byte, i);
						if (e.Current.Byte > i + 1)
							break;
					}
				}
			}
		}

		[Test]
		public async Task Test_20_Search_FilterEqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterFieldEqualTo("Byte", 100), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.AreEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.AreNotEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_21_Search_FilterNotEqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterFieldNotEqualTo("Byte", 100), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.AreNotEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.AreEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_22_Search_FilterGreaterThanOrEqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterFieldGreaterOrEqualTo("Byte", 100), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.GreaterOrEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.Less(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_23_Search_FilterLesserThanOrEqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterFieldLesserOrEqualTo("Byte", 100), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.LessOrEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.Greater(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_24_Search_FilterGreaterThan()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterFieldGreaterThan("Byte", 100), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.Greater(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.LessOrEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_25_Search_FilterLesserThan()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterFieldLesserThan("Byte", 100), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.Less(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.GreaterOrEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_26_Search_FilterLikeRegEx()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterFieldLikeRegEx("ShortString", "A.*B.*"), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.IsTrue(Obj.ShortString.StartsWith("A"));
					Assert.IsTrue(Obj.ShortString.Contains("B"));
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.IsFalse(Obj2.ShortString.StartsWith("A") && Obj2.ShortString.Contains("B"));
			}
		}

		[Test]
		public async Task Test_27_Search_FilterLikeRegEx_2()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterFieldLikeRegEx("ShortString", "[AB].*"), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.IsTrue(Obj.ShortString.StartsWith("A") || Obj.ShortString.StartsWith("B"));
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.IsFalse(Obj2.ShortString.StartsWith("A") || Obj2.ShortString.StartsWith("B"));
			}
		}

		[Test]
		public async Task Test_28_Search_FilterNot_EqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterFieldEqualTo("Byte", 100)), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.AreNotEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.AreEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_29_Search_FilterNot_NotEqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterFieldNotEqualTo("Byte", 100)), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.AreEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.AreNotEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_30_Search_FilterNot_GreaterThanOrEqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterFieldGreaterOrEqualTo("Byte", 100)), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.Less(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.GreaterOrEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_31_Search_FilterNot_LesserThanOrEqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterFieldLesserOrEqualTo("Byte", 100)), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.GreaterOrEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.LessOrEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_32_Search_FilterNot_GreaterThan()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterFieldGreaterThan("Byte", 100)), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.LessOrEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.Greater(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_33_Search_FilterNot_LesserThan()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterFieldLesserThan("Byte", 100)), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.GreaterOrEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.Less(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_34_Search_FilterNot_LikeRegEx()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterFieldLikeRegEx("ShortString", "A.*B.*")), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.IsFalse(Obj.ShortString.StartsWith("A") && Obj.ShortString.Contains("B"));
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.IsTrue(Obj2.ShortString.StartsWith("A") && Obj2.ShortString.Contains("B"));
			}
		}

		[Test]
		public async Task Test_35_Search_FilterNot_LikeRegEx_2()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterFieldLikeRegEx("ShortString", "[AB].*")), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.IsFalse(Obj.ShortString.StartsWith("A") || Obj.ShortString.StartsWith("B"));
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.IsTrue(Obj2.ShortString.StartsWith("A") || Obj2.ShortString.StartsWith("B"));
			}
		}

		[Test]
		public async Task Test_36_Search_FilterNot_Not_EqualTo()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterNot(new FilterNot(new FilterFieldEqualTo("Byte", 100))), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.AreEqual(Obj.Byte, 100);
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.AreNotEqual(Obj2.Byte, 100);
			}
		}

		[Test]
		public async Task Test_37_Search_FilterOr()
		{
			SortedDictionary<Guid, Simple> Objects = await this.CreateObjects(ObjectsToEnumerate);

			using (ICursor<Simple> Cursor = await this.file.Find<Simple>(0, 0, new FilterOr(
				new FilterFieldEqualTo("Byte", 100),
				new FilterFieldEqualTo("Byte", 200),
				new FilterFieldLikeRegEx("ShortString", "A.*")), true))
			{
				Simple Obj;

				while (await Cursor.MoveNextAsync())
				{
					Obj = Cursor.Current;
					Assert.NotNull(Obj);
					Assert.IsTrue(Obj.Byte == 100 || Obj.Byte == 200 || Obj.ShortString.StartsWith("A"));
					Assert.IsTrue(Objects.Remove(Obj.ObjectId));
				}

				foreach (Simple Obj2 in Objects.Values)
					Assert.IsFalse(Obj2.Byte == 100 || Obj2.Byte == 200 || Obj2.ShortString.StartsWith("A"));
			}
		}

		// TODO: And (fields, regex with index, regex without index, open and closed ranges, multi-level ranges, conflicting/complementary ranges)
		// TODO: Noralization
		// TODO: Avoid multiple full file enumerations when evaluating normalized UNION
		// TODO: Offset
		// TODO: MaxCount
		// TODO: SortOrder
		// TODO: Searching on default values
		// TODO: Saving new objects with subobjects saved by reference.

	}
}
