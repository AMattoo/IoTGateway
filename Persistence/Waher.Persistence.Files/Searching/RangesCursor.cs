﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Waher.Persistence.Filters;
using Waher.Persistence.Files.Serialization;

namespace Waher.Persistence.Files.Searching
{
	/// <summary>
	/// Provides a cursor that enumerates ranges of values using an index.
	/// </summary>
	/// <typeparam name="T">Class defining how to deserialize objects found.</typeparam>
	internal class RangesCursor<T> : ICursor<T>
	{
		private RangeInfo[] ranges;
		private RangeInfo[] currentLimits;
		private IndexBTreeFile index;
		private IApplicableFilter[] additionalfilters;
		private ICursor<T> currentRange;
		private KeyValuePair<string, IApplicableFilter>[] startRangeFilters;
		private KeyValuePair<string, IApplicableFilter>[] endRangeFilters;
		private int nrRanges;
		private int limitsUpdatedAt;
		private bool locked;
		private bool firstAscending;
		private bool[] ascending;
		private string[] fieldNames;

		/// <summary>
		/// Provides a cursor that joins results from multiple cursors. It only returns an object once, regardless of how many times
		/// it appears in the different child cursors.
		/// </summary>
		/// <param name="Index">Index.</param>
		/// <param name="Ranges">Ranges to enumerate.</param>
		/// <param name="AdditionalFilters">Additional filters.</param>
		/// <param name="Locked">If locked access is desired.</param>
		public RangesCursor(IndexBTreeFile Index, RangeInfo[] Ranges, IApplicableFilter[] AdditionalFilters, bool Locked)
		{
			this.index = Index;
			this.ranges = Ranges;
			this.additionalfilters = AdditionalFilters;
			this.locked = Locked;
			this.currentRange = null;
			this.ascending = Index.Ascending;
			this.fieldNames = Index.FieldNames;
			this.firstAscending = this.ascending[0];
			this.nrRanges = this.ranges.Length;

			int i;

			this.currentLimits = new RangeInfo[this.nrRanges];

			for (i = 0; i < this.nrRanges; i++)
				this.currentLimits[i] = this.ranges[i].Copy();
		}

		/// <summary>
		/// Gets the element in the collection at the current position of the enumerator.
		/// </summary>
		/// <exception cref="InvalidOperationException">If the enumeration has not started. 
		/// Call <see cref="MoveNextAsync()"/> to start the enumeration after creating or resetting it.</exception>
		public T Current
		{
			get
			{
				return this.CurrentCursor.Current;
			}
		}

		private ICursor<T> CurrentCursor
		{
			get
			{
				if (this.currentRange == null)
					throw new InvalidOperationException("Enumeration not started or has already ended.");
				else
					return this.currentRange;
			}
		}

		/// <summary>
		/// Serializer used to deserialize <see cref="Current"/>.
		/// </summary>
		public IObjectSerializer CurrentSerializer
		{
			get
			{
				return this.CurrentCursor.CurrentSerializer;
			}
		}

		/// <summary>
		/// If the curent object is type compatible with <typeparamref name="T"/> or not. If not compatible, <see cref="Current"/> 
		/// will be null, even if there exists an object at the current position.
		/// </summary>
		public bool CurrentTypeCompatible
		{
			get
			{
				return this.CurrentCursor.CurrentTypeCompatible;
			}
		}

		/// <summary>
		/// Gets the Object ID of the current object.
		/// </summary>
		/// <exception cref="InvalidOperationException">If the enumeration has not started. 
		/// Call <see cref="MoveNext()"/> to start the enumeration after creating or resetting it.</exception>
		public Guid CurrentObjectId
		{
			get
			{
				return this.CurrentCursor.CurrentObjectId;
			}
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			if (this.currentRange != null)
			{
				this.currentRange.Dispose();
				this.currentRange = null;
			}
		}

		/// <summary>
		/// Advances the enumerator to the next element of the collection.
		/// </summary>
		/// <returns>true if the enumerator was successfully advanced to the next element; false if
		/// the enumerator has passed the end of the collection.</returns>
		/// <exception cref="InvalidOperationException">The collection was modified after the enumerator was created.</exception>
		public async Task<bool> MoveNextAsync()
		{
			int i;

			while (true)
			{
				if (this.currentRange == null)
				{
					List<KeyValuePair<string, object>> SearchParameters = new List<KeyValuePair<string, object>>();
					List<KeyValuePair<string, IApplicableFilter>> StartFilters = null;
					List<KeyValuePair<string, IApplicableFilter>> EndFilters = null;
					RangeInfo Range;
					object Value;

					for (i = 0; i < this.nrRanges; i++)
					{
						Range = this.currentLimits[i];

						if (Range.IsPoint)
						{
							if (EndFilters == null)
								EndFilters = new List<KeyValuePair<string, IApplicableFilter>>();

							SearchParameters.Add(new KeyValuePair<string, object>(Range.FieldName, Range.Point));
							EndFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldEqualTo(Range.FieldName, Range.Point)));
						}
						else
						{
							if (Range.HasMin)
							{
								Value = Range.Min;

								if (this.ascending[i])
								{
									if (StartFilters == null)
										StartFilters = new List<KeyValuePair<string, IApplicableFilter>>();

									if (Range.MinInclusive)
										StartFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldGreaterOrEqualTo(Range.FieldName, Value)));
									else
									{
										StartFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldGreaterThan(Range.FieldName, Value)));

										if (!Comparison.Increment(ref Value))
											return false;
									}

									SearchParameters.Add(new KeyValuePair<string, object>(Range.FieldName, Value));
								}
								else
								{
									if (EndFilters == null)
										EndFilters = new List<KeyValuePair<string, IApplicableFilter>>();

									if (Range.MinInclusive)
										EndFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldGreaterOrEqualTo(Range.FieldName, Value)));
									else
										EndFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldGreaterThan(Range.FieldName, Value)));
								}
							}

							if (Range.HasMax)
							{
								Value = Range.Max;
								if (!this.ascending[i])
								{
									if (StartFilters == null)
										StartFilters = new List<KeyValuePair<string, IApplicableFilter>>();

									if (Range.MaxInclusive)
										StartFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldLesserOrEqualTo(Range.FieldName, Value)));
									else
									{
										StartFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldLesserThan(Range.FieldName, Value)));

										if (!Comparison.Decrement(ref Value))
											return false;
									}

									SearchParameters.Add(new KeyValuePair<string, object>(Range.FieldName, Value));
								}
								else
								{
									if (EndFilters == null)
										EndFilters = new List<KeyValuePair<string, IApplicableFilter>>();

									if (Range.MaxInclusive)
										EndFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldLesserOrEqualTo(Range.FieldName, Value)));
									else
										EndFilters.Add(new KeyValuePair<string, IApplicableFilter>(Range.FieldName, new FilterFieldLesserThan(Range.FieldName, Value)));
								}
							}
						}
					}

					if (this.firstAscending)
						this.currentRange = await this.index.FindFirstGreaterOrEqualTo<T>(this.locked, SearchParameters.ToArray());
					else
						this.currentRange = await this.index.FindLastLesserOrEqualTo<T>(this.locked, SearchParameters.ToArray());

					this.startRangeFilters = StartFilters == null ? null : StartFilters.ToArray();
					this.endRangeFilters = EndFilters == null ? null : EndFilters.ToArray();
					this.limitsUpdatedAt = this.nrRanges;
				}

				if (!await this.currentRange.MoveNextAsync())
				{
					this.currentRange.Dispose();
					this.currentRange = null;

					if (this.limitsUpdatedAt >= this.nrRanges)
						return false;

					continue;
				}

				if (!this.currentRange.CurrentTypeCompatible)
					continue;

				object CurrentValue = this.currentRange.Current;
				IObjectSerializer CurrentSerializer = this.currentRange.CurrentSerializer;
				string OutOfStartRangeField = null;
				string OutOfEndRangeField = null;
				bool Ok = true;
				object FieldValue;
				bool Smaller;

				if (this.additionalfilters != null)
				{
					foreach (IApplicableFilter Filter in this.additionalfilters)
					{
						if (!Filter.AppliesTo(CurrentValue, CurrentSerializer))
						{
							Ok = false;
							break;
						}
					}
				}

				if (this.startRangeFilters != null)
				{
					foreach (KeyValuePair<string, IApplicableFilter> Filter in this.startRangeFilters)
					{
						if (!Filter.Value.AppliesTo(CurrentValue, CurrentSerializer))
						{
							OutOfStartRangeField = Filter.Key;
							Ok = false;
							break;
						}
					}
				}

				if (this.endRangeFilters != null && OutOfStartRangeField == null)
				{
					foreach (KeyValuePair<string, IApplicableFilter> Filter in this.endRangeFilters)
					{
						if (!Filter.Value.AppliesTo(CurrentValue, CurrentSerializer))
						{
							OutOfEndRangeField = Filter.Key;
							Ok = false;
							break;
						}
					}
				}

				for (i = 0; i < this.limitsUpdatedAt; i++)
				{
					if (CurrentSerializer.TryGetFieldValue(this.ranges[i].FieldName, CurrentValue, out FieldValue))
					{
						if (this.ascending[i])
						{
							if (this.currentLimits[i].SetMin(FieldValue, OutOfStartRangeField != null, out Smaller) && Smaller)
							{
								i++;
								this.limitsUpdatedAt = i;

								while (i < this.nrRanges)
								{
									this.ranges[i].CopyTo(this.currentLimits[i]);
									i++;
								}
							}
						}
						else
						{
							if (this.currentLimits[i].SetMax(FieldValue, OutOfStartRangeField != null, out Smaller) && Smaller)
							{
								i++;
								this.limitsUpdatedAt = i;

								while (i < this.nrRanges)
								{
									this.ranges[i].CopyTo(this.currentLimits[i]);
									i++;
								}
							}
						}
					}
				}

				if (Ok)
					return true;
				else if (OutOfStartRangeField != null || OutOfEndRangeField != null)
				{
					this.currentRange.Dispose();
					this.currentRange = null;

					if (this.limitsUpdatedAt >= this.nrRanges)
						return false;
				}
			}
		}

		/// <summary>
		/// Advances the enumerator to the previous element of the collection.
		/// </summary>
		/// <returns>true if the enumerator was successfully advanced to the previous element; false if
		/// the enumerator has passed the beginning of the collection.</returns>
		/// <exception cref="InvalidOperationException">The collection was modified after the enumerator was created.</exception>
		public Task<bool> MovePreviousAsync()
		{
			return this.MoveNextAsync();    // Range operator not ordered.
		}

	}
}
