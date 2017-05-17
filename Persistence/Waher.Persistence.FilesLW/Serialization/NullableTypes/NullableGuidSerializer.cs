﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Waher.Persistence.Files.Serialization.NullableTypes
{
	public class NullableGuidSerializer : NullableValueTypeSerializer
	{
		public NullableGuidSerializer()
		{
		}

		public override Type ValueType
		{
			get
			{
				return typeof(Guid?);
			}
		}

		public override object Deserialize(BinaryDeserializer Reader, uint? DataType, bool Embedded)
		{
			if (!DataType.HasValue)
				DataType = Reader.ReadBits(6);

			switch (DataType.Value)
			{
				case ObjectSerializer.TYPE_GUID: return (Guid?)Reader.ReadGuid();
				case ObjectSerializer.TYPE_STRING: return (Guid?)Guid.Parse(Reader.ReadString());
				case ObjectSerializer.TYPE_NULL: return null;
				default: throw new Exception("Expected a nullable Guid value.");
			}
		}

		public override void Serialize(BinarySerializer Writer, bool WriteTypeCode, bool Embedded, object Value)
		{
			Guid? Value2 = (Guid?)Value;

			if (WriteTypeCode)
			{
				if (!Value2.HasValue)
				{
					Writer.WriteBits(ObjectSerializer.TYPE_NULL, 6);
					return;
				}
				else
					Writer.WriteBits(ObjectSerializer.TYPE_GUID, 6);
			}
			else if (!Value2.HasValue)
				throw new NullReferenceException("Value cannot be null.");

			Writer.Write(Value2.Value);
		}

	}
}
