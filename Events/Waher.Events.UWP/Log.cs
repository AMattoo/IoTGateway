﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Waher.Events
{
	/// <summary>
	/// Static class managing the application event log. Applications and services log events on this static class,
	/// and it distributes them to registered event sinks.
	/// </summary>
	public static class Log
	{
		private static IEventSink[] staticSinks = new IEventSink[0];
		private static List<IEventSink> dynamicSinks = new List<IEventSink>();

		/// <summary>
		/// Registers an event sink with the event log.
		/// </summary>
		/// <param name="EventSink">Event Sink.</param>
		public static void Register(IEventSink EventSink)
		{
			lock (dynamicSinks)
			{
				dynamicSinks.Add(EventSink);
				staticSinks = dynamicSinks.ToArray();
			}
		}

		/// <summary>
		/// Unregisters an event sink from the event log.
		/// </summary>
		/// <param name="EventSink">Event Sink.</param>
		/// <returns>If the sink was found and removed.</returns>
		public static bool Unregister(IEventSink EventSink)
		{
			lock (dynamicSinks)
			{
				if (dynamicSinks.Remove(EventSink))
				{
					staticSinks = dynamicSinks.ToArray();
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Registered sinks.
		/// </summary>
		public static IEventSink[] Sinks
		{
			get
			{
				return staticSinks;
			}
		}

		/// <summary>
		/// Logs an event. It will be distributed to registered event sinks.
		/// </summary>
		/// <param name="Event">Event to log.</param>
		public static void Event(Event Event)
		{
			Task.Factory.StartNew(DistributeEvent, Event);
		}

		private static void DistributeEvent(object State)
		{
			Event Event = (Event)State;

			foreach (IEventSink EventSink in staticSinks)
			{
				if (!Event.Avoid(EventSink))
				{
					try
					{
						EventSink.Queue(Event);

						if (hasReportedErrors)
						{
							lock (reportedErrors)
							{
								if (reportedErrors.Remove(EventSink))
									hasReportedErrors = reportedErrors.Count > 0;
							}
						}
					}
					catch (Exception ex)
					{
						bool b;

						lock (reportedErrors)
						{
							if (reportedErrors.TryGetValue(EventSink, out b) && b)
								continue;

							reportedErrors[EventSink] = true;
							hasReportedErrors = true;
						}

						Event Event2 = new Event(EventType.Critical, ex, EventSink.ObjectID);

						if (Event.ToAvoid != null)
						{
							foreach (IEventSink EventSink2 in Event.ToAvoid)
								Event2.Avoid(EventSink2);
						}

						Event2.Avoid(EventSink);

						Log.Event(Event2);
					}
				}
			}
		}

		private static Dictionary<IEventSink, bool> reportedErrors = new Dictionary<IEventSink, bool>();
		private static bool hasReportedErrors = false;

		#region Debug

		/// <summary>
		/// Logs a debug event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="StackTrace">Stack Trace of event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Debug(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, string StackTrace, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Debug, Message, Object, Actor, EventId, Level, Facility, Module, StackTrace, Tags));
		}

		/// <summary>
		/// Logs a debug event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Debug(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Debug, Message, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs a debug event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Debug(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Debug, Message, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs a debug event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Debug(string Message, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Debug, Message, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs a debug event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Debug(string Message, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Debug, Message, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs a debug event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Debug(string Message, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Debug, Message, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs a debug event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Debug(string Message, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Debug, Message, Object, Tags));
		}

		/// <summary>
		/// Logs a debug event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Debug(string Message, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Debug, Message, Tags));
		}

		#endregion

		#region Informational

		/// <summary>
		/// Logs an informational event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="StackTrace">Stack Trace of event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Informational(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, string StackTrace, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Informational, Message, Object, Actor, EventId, Level, Facility, Module, StackTrace, Tags));
		}

		/// <summary>
		/// Logs an informational event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Informational(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Informational, Message, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs an informational event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Informational(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Informational, Message, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs an informational event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Informational(string Message, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Informational, Message, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs an informational event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Informational(string Message, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Informational, Message, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs an informational event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Informational(string Message, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Informational, Message, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs an informational event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Informational(string Message, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Informational, Message, Object, Tags));
		}

		/// <summary>
		/// Logs an informational event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Informational(string Message, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Informational, Message, Tags));
		}

		#endregion

		#region Notice

		/// <summary>
		/// Logs a notice event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="StackTrace">Stack Trace of event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Notice(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, string StackTrace, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Notice, Message, Object, Actor, EventId, Level, Facility, Module, StackTrace, Tags));
		}

		/// <summary>
		/// Logs a notice event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Notice(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Notice, Message, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs a notice event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Notice(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Notice, Message, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs a notice event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Notice(string Message, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Notice, Message, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs a notice event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Notice(string Message, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Notice, Message, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs a notice event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Notice(string Message, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Notice, Message, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs a notice event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Notice(string Message, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Notice, Message, Object, Tags));
		}

		/// <summary>
		/// Logs a notice event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Notice(string Message, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Notice, Message, Tags));
		}

		#endregion

		#region Warning

		/// <summary>
		/// Logs a warning event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="StackTrace">Stack Trace of event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Warning(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, string StackTrace, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Warning, Message, Object, Actor, EventId, Level, Facility, Module, StackTrace, Tags));
		}

		/// <summary>
		/// Logs a warning event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Warning(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Warning, Message, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs a warning event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Warning(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Warning, Message, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs a warning event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Warning(string Message, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Warning, Message, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs a warning event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Warning(string Message, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Warning, Message, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs a warning event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Warning(string Message, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Warning, Message, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs a warning event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Warning(string Message, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Warning, Message, Object, Tags));
		}

		/// <summary>
		/// Logs a warning event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Warning(string Message, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Warning, Message, Tags));
		}

		#endregion

		#region Error

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="StackTrace">Stack Trace of event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, string StackTrace, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Message, Object, Actor, EventId, Level, Facility, Module, StackTrace, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Message, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Message, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(string Message, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Message, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(string Message, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Message, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(string Message, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Message, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(string Message, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Message, Object, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(string Message, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Message, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Exception, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Exception, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Exception, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(Exception Exception, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Exception, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(Exception Exception, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Exception, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(Exception Exception, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Exception, Object, Tags));
		}

		/// <summary>
		/// Logs an error event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Error(Exception Exception, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Error, Exception, Tags));
		}

		#endregion

		#region Critical

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="StackTrace">Stack Trace of event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, string StackTrace, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Message, Object, Actor, EventId, Level, Facility, Module, StackTrace, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Message, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Message, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(string Message, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Message, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(string Message, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Message, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(string Message, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Message, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(string Message, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Message, Object, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(string Message, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Message, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Exception, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Exception, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Exception, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(Exception Exception, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Exception, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(Exception Exception, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Exception, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(Exception Exception, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Exception, Object, Tags));
		}

		/// <summary>
		/// Logs a critical event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Critical(Exception Exception, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Critical, Exception, Tags));
		}

		#endregion

		#region Alert

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="StackTrace">Stack Trace of event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, string StackTrace, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Message, Object, Actor, EventId, Level, Facility, Module, StackTrace, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Message, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Message, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(string Message, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Message, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(string Message, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Message, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(string Message, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Message, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(string Message, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Message, Object, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(string Message, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Message, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Exception, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Exception, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Exception, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(Exception Exception, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Exception, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(Exception Exception, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Exception, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(Exception Exception, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Exception, Object, Tags));
		}

		/// <summary>
		/// Logs an alert event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Alert(Exception Exception, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Alert, Exception, Tags));
		}

		#endregion

		#region Emergency

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="StackTrace">Stack Trace of event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, string StackTrace, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Message, Object, Actor, EventId, Level, Facility, Module, StackTrace, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Message, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(string Message, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Message, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(string Message, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Message, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(string Message, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Message, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(string Message, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Message, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(string Message, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Message, Object, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Message">Free-text event message.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(string Message, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Message, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Module">Module where the event is reported.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, string Module, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Exception, Object, Actor, EventId, Level, Facility, Module, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Facility">Facility can be either a facility in the network sense or in the system sense.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			string Facility, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Exception, Object, Actor, EventId, Level, Facility, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Level">Event Level.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(Exception Exception, string Object, string Actor, string EventId, EventLevel Level,
			params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Exception, Object, Actor, EventId, Level, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="EventId">Computer-readable Event ID identifying type of even.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(Exception Exception, string Object, string Actor, string EventId, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Exception, Object, Actor, EventId, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Actor">Actor responsible for the action causing the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(Exception Exception, string Object, string Actor, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Exception, Object, Actor, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Object">Object related to the event.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(Exception Exception, string Object, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Exception, Object, Tags));
		}

		/// <summary>
		/// Logs an emergency event.
		/// </summary>
		/// <param name="Exception">Exception Object.</param>
		/// <param name="Tags">Variable set of tags providing event-specific information.</param>
		public static void Emergency(Exception Exception, params KeyValuePair<string, object>[] Tags)
		{
			Event(new Event(EventType.Emergency, Exception, Tags));
		}

		#endregion

		// TODO: Traverse stack trace until out of mscore
		// TODO: Traverse TargetInvocation exceptions
		// TODO: Include information (messages & stack traces) from inner exceptions
	}
}
