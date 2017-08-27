﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Waher.Persistence.Attributes;

namespace Waher.Networking.CoAP.LWM2M
{
	/// <summary>
	/// Base class for all LWM2M objects.
	/// </summary>
	[CollectionName("Lwm2mObjects")]
	[TypeName(TypeNameSerialization.FullName)]
	[Index("Id")]
	public abstract class Lwm2mObject : CoapResource
	{
		private SortedDictionary<int, Lwm2mObjectInstance> instances = new SortedDictionary<int, Lwm2mObjectInstance>();
		private Lwm2mClient client = null;
		private string objectId = null;
		private int id;

		/// <summary>
		/// Base class for all LWM2M objects.
		/// </summary>
		/// <param name="Id">ID of object.</param>
		/// <param name="Instances">Object instances.</param>
		public Lwm2mObject(int Id, params Lwm2mObjectInstance[] Instances)
			: base("/" + Id.ToString())
		{
			this.id = Id;

			foreach (Lwm2mObjectInstance Instance in Instances)
				this.Add(Instance);
		}

		/// <summary>
		/// Object ID in database.
		/// </summary>
		[ObjectId]
		public string ObjectId
		{
			get { return this.objectId; }
			set { this.objectId = value; }
		}

		/// <summary>
		/// Adds an object instance.
		/// </summary>
		/// <param name="Instance">Object instance.</param>
		public void Add(Lwm2mObjectInstance Instance)
		{
			lock (this.instances)
			{
				if (Instance.SubId < 0)
					throw new ArgumentException("Invalid object instance ID.", nameof(Instance));

				if (this.instances.ContainsKey(Instance.SubId))
				{
					throw new ArgumentException("An object instance with ID " + Instance.SubId +
						" already is registered.", nameof(Instance));
				}

				this.instances[Instance.SubId] = Instance;
				Instance.Object = this;
			}
		}

		/// <summary>
		/// Removes all instances.
		/// </summary>
		protected void ClearInstances()
		{
			Lwm2mObjectInstance[] Instances;

			lock (this.instances)
			{
				Instances = new Lwm2mObjectInstance[this.instances.Count];
				this.instances.Values.CopyTo(Instances, 0);
				this.instances.Clear();
			}

			foreach (Lwm2mObjectInstance Instance in Instances)
				this.client?.CoapEndpoint.Unregister(Instance);
		}

		/// <summary>
		/// LWM2M Client.
		/// </summary>
		public Lwm2mClient Client
		{
			get { return this.client; }
			internal set { this.client = value; }
		}

		/// <summary>
		/// ID of object.
		/// </summary>
		public int Id
		{
			get { return this.id; }
			set
			{
				if (this.id != value)
				{
					this.id = value;
					this.Path = "/" + this.id.ToString();
				}
			}
		}

		/// <summary>
		/// Registered instances.
		/// </summary>
		public Lwm2mObjectInstance[] Instances
		{
			get
			{
				Lwm2mObjectInstance[] Result;

				lock (this.instances)
				{
					Result = new Lwm2mObjectInstance[this.instances.Count];
					this.instances.Values.CopyTo(Result, 0);
				}

				return Result;
			}
		}

		/// <summary>
		/// If the object has instances registered on it.
		/// </summary>
		public bool HasInstances
		{
			get
			{
				lock (this.instances)
				{
					return this.instances.Count > 0;
				}
			}
		}

		/// <summary>
		/// Deletes any Bootstrap information.
		/// </summary>
		public virtual async Task LoadBootstrapInfo()
		{
			foreach (Lwm2mObjectInstance Instance in this.Instances)
				await Instance.LoadBootstrapInfo();
		}

		/// <summary>
		/// Deletes any Bootstrap information.
		/// </summary>
		public virtual async Task DeleteBootstrapInfo()
		{
			foreach (Lwm2mObjectInstance Instance in this.Instances)
				await Instance.DeleteBootstrapInfo();
		}

		/// <summary>
		/// Applies any Bootstrap information.
		/// </summary>
		public virtual async Task ApplyBootstrapInfo()
		{
			foreach (Lwm2mObjectInstance Instance in this.Instances)
				await Instance.ApplyBootstrapInfo();
		}
	}
}
