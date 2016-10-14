﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Waher.Persistence.Files.Serialization;
using Waher.Persistence.Files.Test.Classes;

namespace Waher.Persistence.Files.Test
{
	[TestFixture]
	public class ObjectSerializationTests
	{
		private FilesProvider provider;

		[TestFixtureSetUp]
		public void TestFixtureSetUp()
		{
			this.provider = new FilesProvider("Data", "Default");
		}

		[TestFixtureTearDown]
		public void TestFixtureTearDown()
		{
			this.provider.Dispose();
			this.provider = null;
		}

		[Test]
		public void Test_01_SimpleObject()
		{
			Simple Obj = new Simple();

			Obj.Boolean1 = true;
			Obj.Boolean2 = false;
			Obj.Byte = 15;
			Obj.Short = -1234;
			Obj.Int = -23456789;
			Obj.Long = -345456456456456345;
			Obj.SByte = -45;
			Obj.UShort = 23456;
			Obj.UInt = 334534564;
			Obj.ULong = 4345345345345345;
			Obj.Char = '☀';
			Obj.Decimal = 12345.6789M;
			Obj.Double = 12345.6789;
			Obj.Single = 12345.6789f;
			Obj.String = "Today, there will be a lot of ☀.";
			Obj.DateTime = DateTime.Now;
			Obj.TimeSpan = Obj.DateTime.TimeOfDay;
			Obj.Guid = Guid.NewGuid();
			Obj.NormalEnum = NormalEnum.Option3;
			Obj.FlagsEnum = FlagsEnum.Option1 | FlagsEnum.Option4;

			Assert.IsTrue(Obj.ObjectId.Equals(Guid.Empty));

			IObjectSerializer S = this.provider.GetObjectSerializer(typeof(Simple));
			BinarySerializer Writer = new BinarySerializer(Encoding.UTF8);

			S.Serialize(Writer, false, false, Obj);

			Assert.IsFalse(Obj.ObjectId.Equals(Guid.Empty));

			byte[] Data = Writer.GetSerialization();
			int i, c = Data.Length;

			for (i = 0; i < c; i++)
			{
				if ((i & 15) == 0)
					Console.Out.WriteLine();
				else
					Console.Out.Write(' ');

				Console.Out.Write(Data[i].ToString("x2"));
			}

			BinaryDeserializer Reader = new BinaryDeserializer(Encoding.UTF8, Data);

			Simple Obj2 = (Simple)S.Deserialize(Reader, ObjectSerializer.TYPE_OBJECT, false);

			Assert.AreEqual(Obj.Boolean1, Obj2.Boolean1);
			Assert.AreEqual(Obj.Boolean2, Obj2.Boolean2);
			Assert.AreEqual(Obj.Byte, Obj2.Byte);
			Assert.AreEqual(Obj.Short, Obj2.Short);
			Assert.AreEqual(Obj.Int, Obj2.Int);
			Assert.AreEqual(Obj.Long, Obj2.Long);
			Assert.AreEqual(Obj.SByte, Obj2.SByte);
			Assert.AreEqual(Obj.UShort, Obj2.UShort);
			Assert.AreEqual(Obj.UInt, Obj2.UInt);
			Assert.AreEqual(Obj.ULong, Obj2.ULong);
			Assert.AreEqual(Obj.Char, Obj2.Char);
			Assert.AreEqual(Obj.Decimal, Obj2.Decimal);
			Assert.AreEqual(Obj.Double, Obj2.Double);
			Assert.AreEqual(Obj.Single, Obj2.Single);
			Assert.AreEqual(Obj.String, Obj2.String);
			Assert.AreEqual(Obj.DateTime, Obj2.DateTime);
			Assert.AreEqual(Obj.TimeSpan, Obj2.TimeSpan);
			Assert.AreEqual(Obj.Guid, Obj2.Guid);
			Assert.AreEqual(Obj.NormalEnum, Obj2.NormalEnum);
			Assert.AreEqual(Obj.FlagsEnum, Obj2.FlagsEnum);
			Assert.AreEqual(Obj.ObjectId, Obj2.ObjectId);
		}

		[Test]
		public void Test_02_Nullable1()
		{
			Classes.Nullable Obj = new Classes.Nullable();

			Obj.Boolean1 = true;
			Obj.Byte = 15;
			Obj.Int = -23456789;
			Obj.SByte = -45;
			Obj.UInt = 334534564;
			Obj.Char = '☀';
			Obj.Double = 12345.6789;
			Obj.String = "Today, there will be a lot of ☀.";
			Obj.TimeSpan = DateTime.Now.TimeOfDay;
			Obj.NormalEnum = NormalEnum.Option3;

			IObjectSerializer S = this.provider.GetObjectSerializer(typeof(Classes.Nullable));
			BinarySerializer Writer = new BinarySerializer(Encoding.UTF8);

			S.Serialize(Writer, false, false, Obj);

			byte[] Data = Writer.GetSerialization();
			int i, c = Data.Length;

			for (i = 0; i < c; i++)
			{
				if ((i & 15) == 0)
					Console.Out.WriteLine();
				else
					Console.Out.Write(' ');

				Console.Out.Write(Data[i].ToString("x2"));
			}

			BinaryDeserializer Reader = new BinaryDeserializer(Encoding.UTF8, Data);

			Classes.Nullable Obj2 = (Classes.Nullable)S.Deserialize(Reader, ObjectSerializer.TYPE_OBJECT, false);

			Assert.AreEqual(Obj.Boolean1, Obj2.Boolean1);
			Assert.AreEqual(Obj.Boolean2, Obj2.Boolean2);
			Assert.AreEqual(Obj.Byte, Obj2.Byte);
			Assert.AreEqual(Obj.Short, Obj2.Short);
			Assert.AreEqual(Obj.Int, Obj2.Int);
			Assert.AreEqual(Obj.Long, Obj2.Long);
			Assert.AreEqual(Obj.SByte, Obj2.SByte);
			Assert.AreEqual(Obj.UShort, Obj2.UShort);
			Assert.AreEqual(Obj.UInt, Obj2.UInt);
			Assert.AreEqual(Obj.ULong, Obj2.ULong);
			Assert.AreEqual(Obj.Char, Obj2.Char);
			Assert.AreEqual(Obj.Decimal, Obj2.Decimal);
			Assert.AreEqual(Obj.Double, Obj2.Double);
			Assert.AreEqual(Obj.Single, Obj2.Single);
			Assert.AreEqual(Obj.String, Obj2.String);
			Assert.AreEqual(Obj.DateTime, Obj2.DateTime);
			Assert.AreEqual(Obj.TimeSpan, Obj2.TimeSpan);
			Assert.AreEqual(Obj.Guid, Obj2.Guid);
			Assert.AreEqual(Obj.NormalEnum, Obj2.NormalEnum);
			Assert.AreEqual(Obj.FlagsEnum, Obj2.FlagsEnum);
		}

		[Test]
		public void Test_03_Nullable2()
		{
			Classes.Nullable Obj = new Classes.Nullable();

			Obj.Boolean2 = false;
			Obj.Short = -1234;
			Obj.Long = -345456456456456345;
			Obj.UShort = 23456;
			Obj.ULong = 4345345345345345;
			Obj.Decimal = 12345.6789M;
			Obj.Single = 12345.6789f;
			Obj.DateTime = DateTime.Now;
			Obj.Guid = Guid.NewGuid();
			Obj.FlagsEnum = FlagsEnum.Option1 | FlagsEnum.Option4;

			IObjectSerializer S = this.provider.GetObjectSerializer(typeof(Classes.Nullable));
			BinarySerializer Writer = new BinarySerializer(Encoding.UTF8);

			S.Serialize(Writer, false, false, Obj);

			byte[] Data = Writer.GetSerialization();
			int i, c = Data.Length;

			for (i = 0; i < c; i++)
			{
				if ((i & 15) == 0)
					Console.Out.WriteLine();
				else
					Console.Out.Write(' ');

				Console.Out.Write(Data[i].ToString("x2"));
			}

			BinaryDeserializer Reader = new BinaryDeserializer(Encoding.UTF8, Data);

			Classes.Nullable Obj2 = (Classes.Nullable)S.Deserialize(Reader, ObjectSerializer.TYPE_OBJECT, false);

			Assert.AreEqual(Obj.Boolean1, Obj2.Boolean1);
			Assert.AreEqual(Obj.Boolean2, Obj2.Boolean2);
			Assert.AreEqual(Obj.Byte, Obj2.Byte);
			Assert.AreEqual(Obj.Short, Obj2.Short);
			Assert.AreEqual(Obj.Int, Obj2.Int);
			Assert.AreEqual(Obj.Long, Obj2.Long);
			Assert.AreEqual(Obj.SByte, Obj2.SByte);
			Assert.AreEqual(Obj.UShort, Obj2.UShort);
			Assert.AreEqual(Obj.UInt, Obj2.UInt);
			Assert.AreEqual(Obj.ULong, Obj2.ULong);
			Assert.AreEqual(Obj.Char, Obj2.Char);
			Assert.AreEqual(Obj.Decimal, Obj2.Decimal);
			Assert.AreEqual(Obj.Double, Obj2.Double);
			Assert.AreEqual(Obj.Single, Obj2.Single);
			Assert.AreEqual(Obj.String, Obj2.String);
			Assert.AreEqual(Obj.DateTime, Obj2.DateTime);
			Assert.AreEqual(Obj.TimeSpan, Obj2.TimeSpan);
			Assert.AreEqual(Obj.Guid, Obj2.Guid);
			Assert.AreEqual(Obj.NormalEnum, Obj2.NormalEnum);
			Assert.AreEqual(Obj.FlagsEnum, Obj2.FlagsEnum);
		}

		[Test]
		public void Test_04_Default1()
		{
			Default Obj = new Default();

			Obj.Boolean1 = true;
			Obj.Boolean2 = false;
			Obj.Byte = 10;
			Obj.Short = -1234;
			Obj.Int = 10;
			Obj.Long = -345456456456456345;
			Obj.SByte = 10;
			Obj.UShort = 23456;
			Obj.UInt = 10;
			Obj.ULong = 4345345345345345;
			Obj.Char = 'x';
			Obj.Decimal = 12345.6789M;
			Obj.Double = 10;
			Obj.Single = 12345.6789f;
			Obj.String = string.Empty;
			Obj.DateTime = DateTime.Now;
			Obj.TimeSpan = TimeSpan.MinValue;
			Obj.Guid = Guid.NewGuid();
			Obj.NormalEnum = NormalEnum.Option1;
			Obj.FlagsEnum = FlagsEnum.Option1 | FlagsEnum.Option4;

			IObjectSerializer S = this.provider.GetObjectSerializer(typeof(Default));
			BinarySerializer Writer = new BinarySerializer(Encoding.UTF8);

			S.Serialize(Writer, false, false, Obj);

			byte[] Data = Writer.GetSerialization();
			int i, c = Data.Length;

			for (i = 0; i < c; i++)
			{
				if ((i & 15) == 0)
					Console.Out.WriteLine();
				else
					Console.Out.Write(' ');

				Console.Out.Write(Data[i].ToString("x2"));
			}

			BinaryDeserializer Reader = new BinaryDeserializer(Encoding.UTF8, Data);

			Default Obj2 = (Default)S.Deserialize(Reader, ObjectSerializer.TYPE_OBJECT, false);

			Assert.AreEqual(Obj.Boolean1, Obj2.Boolean1);
			Assert.AreEqual(Obj.Boolean2, Obj2.Boolean2);
			Assert.AreEqual(Obj.Byte, Obj2.Byte);
			Assert.AreEqual(Obj.Short, Obj2.Short);
			Assert.AreEqual(Obj.Int, Obj2.Int);
			Assert.AreEqual(Obj.Long, Obj2.Long);
			Assert.AreEqual(Obj.SByte, Obj2.SByte);
			Assert.AreEqual(Obj.UShort, Obj2.UShort);
			Assert.AreEqual(Obj.UInt, Obj2.UInt);
			Assert.AreEqual(Obj.ULong, Obj2.ULong);
			Assert.AreEqual(Obj.Char, Obj2.Char);
			Assert.AreEqual(Obj.Decimal, Obj2.Decimal);
			Assert.AreEqual(Obj.Double, Obj2.Double);
			Assert.AreEqual(Obj.Single, Obj2.Single);
			Assert.AreEqual(Obj.String, Obj2.String);
			Assert.AreEqual(Obj.DateTime, Obj2.DateTime);
			Assert.AreEqual(Obj.TimeSpan, Obj2.TimeSpan);
			Assert.AreEqual(Obj.Guid, Obj2.Guid);
			Assert.AreEqual(Obj.NormalEnum, Obj2.NormalEnum);
			Assert.AreEqual(Obj.FlagsEnum, Obj2.FlagsEnum);
		}

		[Test]
		public void Test_05_Default2()
		{
			Default Obj = new Default();

			Obj.Boolean1 = false;
			Obj.Boolean2 = true;
			Obj.Byte = 15;
			Obj.Short = 10;
			Obj.Int = -23456789;
			Obj.Long = 10;
			Obj.SByte = -45;
			Obj.UShort = 10;
			Obj.UInt = 334534564;
			Obj.ULong = 10;
			Obj.Char = '☀';
			Obj.Decimal = 10;
			Obj.Double = 12345.6789;
			Obj.Single = 10;
			Obj.String = "Today, there will be a lot of ☀.";
			Obj.DateTime = DateTime.MinValue;
			Obj.TimeSpan = DateTime.Now.TimeOfDay;
			Obj.Guid = Guid.Empty;
			Obj.NormalEnum = NormalEnum.Option3;
			Obj.FlagsEnum = FlagsEnum.Option1 | FlagsEnum.Option2;

			IObjectSerializer S = this.provider.GetObjectSerializer(typeof(Default));
			BinarySerializer Writer = new BinarySerializer(Encoding.UTF8);

			S.Serialize(Writer, false, false, Obj);

			byte[] Data = Writer.GetSerialization();
			int i, c = Data.Length;

			for (i = 0; i < c; i++)
			{
				if ((i & 15) == 0)
					Console.Out.WriteLine();
				else
					Console.Out.Write(' ');

				Console.Out.Write(Data[i].ToString("x2"));
			}

			BinaryDeserializer Reader = new BinaryDeserializer(Encoding.UTF8, Data);

			Default Obj2 = (Default)S.Deserialize(Reader, ObjectSerializer.TYPE_OBJECT, false);

			Assert.AreEqual(Obj.Boolean1, Obj2.Boolean1);
			Assert.AreEqual(Obj.Boolean2, Obj2.Boolean2);
			Assert.AreEqual(Obj.Byte, Obj2.Byte);
			Assert.AreEqual(Obj.Short, Obj2.Short);
			Assert.AreEqual(Obj.Int, Obj2.Int);
			Assert.AreEqual(Obj.Long, Obj2.Long);
			Assert.AreEqual(Obj.SByte, Obj2.SByte);
			Assert.AreEqual(Obj.UShort, Obj2.UShort);
			Assert.AreEqual(Obj.UInt, Obj2.UInt);
			Assert.AreEqual(Obj.ULong, Obj2.ULong);
			Assert.AreEqual(Obj.Char, Obj2.Char);
			Assert.AreEqual(Obj.Decimal, Obj2.Decimal);
			Assert.AreEqual(Obj.Double, Obj2.Double);
			Assert.AreEqual(Obj.Single, Obj2.Single);
			Assert.AreEqual(Obj.String, Obj2.String);
			Assert.AreEqual(Obj.DateTime, Obj2.DateTime);
			Assert.AreEqual(Obj.TimeSpan, Obj2.TimeSpan);
			Assert.AreEqual(Obj.Guid, Obj2.Guid);
			Assert.AreEqual(Obj.NormalEnum, Obj2.NormalEnum);
			Assert.AreEqual(Obj.FlagsEnum, Obj2.FlagsEnum);
		}

		// TODO: Embedded Arrays (value elements, nullable elements)
		// TODO: Embedded objects (nullable)
		// TODO: Objects, by reference, nullable
		// TODO: Generic object reader/writer (with no type knowledge, for batch operations). If type not found when reading: Return generic object.
		// TODO: Type name serialization
		// TODO: Binary length (to skip block)
		// TODO: Different Object ID field types (Guid, string, byte[])
		// TODO: Collections
	}
}
