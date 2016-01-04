﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Things.SensorData
{
	/// <summary>
	/// Base class for all sensor data fields.
	/// </summary>
	public abstract class Field
	{
		private ThingReference thing;
		private DateTime timestamp;
		private LocalizationStep[] stringIdSteps;
		private FieldType type;
		private FieldQoS qos;
		private string name;
		private string module;
		private bool writable;

		/// <summary>
		/// Base class for all sensor data fields.
		/// </summary>
		/// <param name="Thing">Reference to the thing to which the field belongs.</param>
		/// <param name="Timestamp">Timestamp of field value.</param>
		/// <param name="Name">Field Name.</param>
		/// <param name="Type">Field Type flags.</param>
		/// <param name="QoS">Quality of Service flags.</param>
		/// <param name="Writable">If the field is writable, i.e. corresponds to a control parameter.</param>
		/// <param name="Module">Language Module for localization purposes.</param>
		/// <param name="StringIdSteps">String ID steps.</param>
		public Field(ThingReference Thing, DateTime Timestamp, string Name, FieldType Type, FieldQoS QoS, bool Writable, string Module, 
			params LocalizationStep[] StringIdSteps)
		{
			this.thing = Thing;
			this.timestamp = Timestamp;
			this.name = Name;
			this.type = Type;
			this.qos = QoS;
			this.writable = Writable;
			this.module = Module;
			this.stringIdSteps = StringIdSteps;
		}

		/// <summary>
		/// Base class for all sensor data fields.
		/// </summary>
		/// <param name="Thing">Reference to the thing to which the field belongs.</param>
		/// <param name="Timestamp">Timestamp of field value.</param>
		/// <param name="Name">Field Name.</param>
		/// <param name="Type">Field Type flags.</param>
		/// <param name="QoS">Quality of Service flags.</param>
		/// <param name="Writable">If the field is writable, i.e. corresponds to a control parameter.</param>
		/// <param name="Module">Language Module for localization purposes.</param>
		/// <param name="StringIdSteps">String ID steps.</param>
		public Field(ThingReference Thing, DateTime Timestamp, string Name, FieldType Type, FieldQoS QoS, bool Writable, string Module, params int[] StringIds)
		{
			this.thing = Thing;
			this.timestamp = Timestamp;
			this.name = Name;
			this.type = Type;
			this.qos = QoS;
			this.writable = Writable;
			this.module = Module;
			this.stringIdSteps = Convert(StringIds);
		}

		/// <summary>
		/// Base class for all sensor data fields.
		/// </summary>
		/// <param name="Thing">Reference to the thing to which the field belongs.</param>
		/// <param name="Timestamp">Timestamp of field value.</param>
		/// <param name="Name">Field Name.</param>
		/// <param name="Type">Field Type flags.</param>
		/// <param name="QoS">Quality of Service flags.</param>
		/// <param name="Module">Language Module for localization purposes.</param>
		/// <param name="StringIdSteps">String ID steps.</param>
		public Field(ThingReference Thing, DateTime Timestamp, string Name, FieldType Type, FieldQoS QoS, string Module, params LocalizationStep[] StringIdSteps)
		{
			this.thing = Thing;
			this.timestamp = Timestamp;
			this.name = Name;
			this.type = Type;
			this.qos = QoS;
			this.writable = false;
			this.module = Module;
			this.stringIdSteps = StringIdSteps;
		}

		/// <summary>
		/// Base class for all sensor data fields.
		/// </summary>
		/// <param name="Thing">Reference to the thing to which the field belongs.</param>
		/// <param name="Timestamp">Timestamp of field value.</param>
		/// <param name="Name">Field Name.</param>
		/// <param name="Type">Field Type flags.</param>
		/// <param name="QoS">Quality of Service flags.</param>
		/// <param name="Module">Language Module for localization purposes.</param>
		/// <param name="StringIdSteps">String ID steps.</param>
		public Field(ThingReference Thing, DateTime Timestamp, string Name, FieldType Type, FieldQoS QoS, string Module, params int[] StringIds)
		{
			this.thing = Thing;
			this.timestamp = Timestamp;
			this.name = Name;
			this.type = Type;
			this.qos = QoS;
			this.writable = false;
			this.module = Module;
			this.stringIdSteps = Convert(StringIds);
		}

		/// <summary>
		/// Base class for all sensor data fields.
		/// </summary>
		/// <param name="Thing">Reference to the thing to which the field belongs.</param>
		/// <param name="Timestamp">Timestamp of field value.</param>
		/// <param name="Name">Field Name.</param>
		/// <param name="Type">Field Type flags.</param>
		/// <param name="QoS">Quality of Service flags.</param>
		/// <param name="Writable">If the field is writable, i.e. corresponds to a control parameter.</param>
		public Field(ThingReference Thing, DateTime Timestamp, string Name, FieldType Type, FieldQoS QoS, bool Writable)
		{
			this.thing = Thing;
			this.timestamp = Timestamp;
			this.name = Name;
			this.type = Type;
			this.qos = QoS;
			this.writable = Writable;
			this.module = string.Empty;
			this.stringIdSteps = null;
		}

		/// <summary>
		/// Base class for all sensor data fields.
		/// </summary>
		/// <param name="Thing">Reference to the thing to which the field belongs.</param>
		/// <param name="Timestamp">Timestamp of field value.</param>
		/// <param name="Name">Field Name.</param>
		/// <param name="Type">Field Type flags.</param>
		/// <param name="QoS">Quality of Service flags.</param>
		public Field(ThingReference Thing, DateTime Timestamp, string Name, FieldType Type, FieldQoS QoS)
		{
			this.thing = Thing;
			this.timestamp = Timestamp;
			this.name = Name;
			this.type = Type;
			this.qos = QoS;
			this.writable = false;
			this.module = string.Empty;
			this.stringIdSteps = null;
		}

		private static LocalizationStep[] Convert(int[] StringIds)
		{
			int i, c = StringIds.Length;
			LocalizationStep[] Result = new LocalizationStep[c];

			for (i = 0; i < c; i++)
				Result[i] = new LocalizationStep(StringIds[i]);

			return Result;
		}

		/// <summary>
		/// Reference to the thing to which the field belongs.
		/// </summary>
		public ThingReference Thing { get { return this.thing; } }

		/// <summary>
		/// Timestamp of field value.
		/// </summary>
		public DateTime Timestamp { get { return this.timestamp; } }

		/// <summary>
		/// Array of Language String ID steps. List can be null. Strings can be null if no seeds or modules are used.
		/// 
		/// Localization algorithm is defined in:
		/// http://xmpp.org/extensions/xep-0323.html#localization
		/// </summary>
		public LocalizationStep[] StringIdSteps { get { return this.stringIdSteps; } }

		/// <summary>
		/// Field Type flags.
		/// </summary>
		public FieldType Type { get { return this.type; } }

		/// <summary>
		/// Field Quality of Service flags.
		/// </summary>
		public FieldQoS QoS { get { return this.qos; } }

		/// <summary>
		/// Unlocalized field name.
		/// </summary>
		public string Name { get { return this.name; } }

		/// <summary>
		/// Default language module, if explicit language modules are not specified in the language steps.
		/// </summary>
		public string Module { get { return this.module; } }

		/// <summary>
		/// If the field corresponds to a control parameter with the same name.
		/// </summary>
		public bool Writable { get { return this.writable; } }

		/// <summary>
		/// <see cref="Object.ToString()"/>
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			bool First = true;

			sb.Append(this.name);
			sb.Append(": ");
			sb.Append(this.ValueString);
			sb.Append(" (");

			if (this.writable)
				this.Append(sb, "Writable", ref First);

			if (this.type.HasFlag(FieldType.Computed))
				this.Append(sb, "Computed", ref First);

			if (this.type.HasFlag(FieldType.Identity))
				this.Append(sb, "Identity", ref First);

			if (this.type.HasFlag(FieldType.Momentary))
				this.Append(sb, "Momentary", ref First);

			if (this.type.HasFlag(FieldType.Peak))
				this.Append(sb, "Peak", ref First);

			if (this.type.HasFlag(FieldType.Status))
				this.Append(sb, "Status", ref First);

			if (this.type.HasFlag(FieldType.HistoricalSecond))
				this.Append(sb, "HistoricalSecond", ref First);

			if (this.type.HasFlag(FieldType.HistoricalMinute))
				this.Append(sb, "HistoricalMinute", ref First);

			if (this.type.HasFlag(FieldType.HistoricalHour))
				this.Append(sb, "HistoricalHour", ref First);

			if (this.type.HasFlag(FieldType.HistoricalDay))
				this.Append(sb, "HistoricalDay", ref First);

			if (this.type.HasFlag(FieldType.HistoricalWeek))
				this.Append(sb, "HistoricalWeek", ref First);

			if (this.type.HasFlag(FieldType.HistoricalMonth))
				this.Append(sb, "HistoricalMonth", ref First);

			if (this.type.HasFlag(FieldType.HistoricalQuarter))
				this.Append(sb, "HistoricalQuarter", ref First);

			if (this.type.HasFlag(FieldType.HistoricalYear))
				this.Append(sb, "HistoricalYear", ref First);

			if (this.type.HasFlag(FieldType.HistoricalQuarter))
				this.Append(sb, "HistoricalQuarter", ref First);

			if (this.type.HasFlag(FieldType.HistoricalOther))
				this.Append(sb, "HistoricalOther", ref First);

			if (this.qos.HasFlag(FieldQoS.Missing))
				this.Append(sb, "Missing", ref First);

			if (this.qos.HasFlag(FieldQoS.InProgress))
				this.Append(sb, "InProgress", ref First);

			if (this.qos.HasFlag(FieldQoS.AutomaticEstimate))
				this.Append(sb, "AutomaticEstimate", ref First);

			if (this.qos.HasFlag(FieldQoS.ManualEstimate))
				this.Append(sb, "ManualEstimate", ref First);

			if (this.qos.HasFlag(FieldQoS.ManualReadout))
				this.Append(sb, "ManualReadout", ref First);

			if (this.qos.HasFlag(FieldQoS.AutomaticReadout))
				this.Append(sb, "AutomaticReadout", ref First);

			if (this.qos.HasFlag(FieldQoS.TimeOffset))
				this.Append(sb, "TimeOffset", ref First);

			if (this.qos.HasFlag(FieldQoS.Warning))
				this.Append(sb, "Warning", ref First);

			if (this.qos.HasFlag(FieldQoS.Error))
				this.Append(sb, "Error", ref First);

			if (this.qos.HasFlag(FieldQoS.Signed))
				this.Append(sb, "Signed", ref First);

			if (this.qos.HasFlag(FieldQoS.Invoiced))
				this.Append(sb, "Invoiced", ref First);

			if (this.qos.HasFlag(FieldQoS.EndOfSeries))
				this.Append(sb, "EndOfSeries", ref First);

			if (this.qos.HasFlag(FieldQoS.PowerFailure))
				this.Append(sb, "PowerFailure", ref First);

			if (this.qos.HasFlag(FieldQoS.InvoiceConfirmed))
				this.Append(sb, "InvoiceConfirmed", ref First);

			sb.Append(")");

			return sb.ToString();
		}

		private void Append(StringBuilder sb, string s, ref bool First)
		{
			if (First)
				First = false;
			else
				sb.Append(", ");

			sb.Append(s);
		}

		/// <summary>
		/// String representation of field value.
		/// </summary>
		public abstract string ValueString
		{
			get;
		}

		/// <summary>
		/// Provides a string identifying the data type of the field. Should conform to field value data types specified in XEP-0323, if possible:
		/// http://xmpp.org/extensions/xep-0323.html#fieldvaluetypes
		/// </summary>
		public abstract string FieldDataTypeName
		{
			get;
		}
	}
}
