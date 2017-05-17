﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Waher.Persistence.Files.Serialization.ValueTypes
{
	public class TimeSpanSerializer : ValueTypeSerializer
	{
		public TimeSpanSerializer()
		{
		}

		public override Type ValueType
		{
			get
			{
				return typeof(TimeSpan);
			}
		}

		public override object Deserialize(BinaryDeserializer Reader, uint? DataType, bool Embedded)
		{
			if (!DataType.HasValue)
				DataType = Reader.ReadBits(6);

			switch (DataType.Value)
			{
				case ObjectSerializer.TYPE_TIMESPAN: return Reader.ReadTimeSpan();
				case ObjectSerializer.TYPE_STRING: return TimeSpan.Parse(Reader.ReadString());
				case ObjectSerializer.TYPE_MIN: return TimeSpan.MinValue;
				case ObjectSerializer.TYPE_MAX: return TimeSpan.MaxValue;
				case ObjectSerializer.TYPE_NULL: return null;
				default: throw new Exception("Expected a TimeSpan value.");
			}
		}

		public override void Serialize(BinarySerializer Writer, bool WriteTypeCode, bool Embedded, object Value)
		{
			if (WriteTypeCode)
				Writer.WriteBits(ObjectSerializer.TYPE_TIMESPAN, 6);

			Writer.Write((TimeSpan)Value);
		}

	}
}
