﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Waher.Persistence.Attributes;

namespace Waher.Persistence.Files.Test.Classes
{
	[TypeName(TypeNameSerialization.None)]
	public class Simple
	{
		[ObjectId]
		public Guid ObjectId;
		public bool Boolean1;
		public bool Boolean2;
		public byte Byte;
		public short Short;
		public int Int;
		public long Long;
		public sbyte SByte;
		public ushort UShort;
		public uint UInt;
		public ulong ULong;
		public char Char;
		public decimal Decimal;
		public double Double;
		public float Single;
		public string String;
		public string ShortString;
		public DateTime DateTime;
		public TimeSpan TimeSpan;
		public Guid Guid;
		public NormalEnum NormalEnum;
		public FlagsEnum FlagsEnum;

		public Simple()
		{
		}

		public override string ToString()
		{
			return this.ObjectId.ToString();
		}
	}
}
