using System;
using NHibernate.Cache;
using NHibernate.Engine;
using NHibernate.Persister;

namespace NHibernate.Impl 
{
	internal class ScheduledUpdate : ScheduledEntityAction 
	{
		private object[] _fields;
		private object _lastVersion;
		private object _nextVersion;
		private int[] _dirtyFields;
		private object[] _updatedState;

		/// <summary>
		/// Initializes a new instance of <see cref="ScheduledUpdate"/>.
		/// </summary>
		/// <param name="id">The identifier of the object.</param>
		/// <param name="fields">An object array that contains the value of each Property.</param>
		/// <param name="dirtyProperties">An array that contains the indexes of the dirty Properties.</param>
		/// <param name="lastVersion">The current version of the object.</param>
		/// <param name="nextVersion">The version the object should be after update.</param>
		/// <param name="instance">The actual object instance.</param>
		/// <param name="updatedState"></param>
		/// <param name="persister">The <see cref="IClassPersister"/> that is responsible for the persisting the object.</param>
		/// <param name="session">The <see cref="ISessionImplementor"/> that the Action is occuring in.</param>
		public ScheduledUpdate(object id, object[] fields, int[] dirtyProperties, object lastVersion, object nextVersion, object instance, object[] updatedState, IClassPersister persister, ISessionImplementor session) : base(session, id, instance, persister) 
		{
			_fields = fields;
			_lastVersion = lastVersion;
			_nextVersion = nextVersion;
			_dirtyFields = dirtyProperties;
			_updatedState = updatedState;
		}

		public override void Execute() 
		{
			if ( Persister.HasCache ) Persister.Cache.Lock(Id);
			Persister.Update( Id, _fields, _dirtyFields, _lastVersion, Instance, Session );
			Session.PostUpdate( Instance, _updatedState, _nextVersion );
		}

		public override void AfterTransactionCompletion() 
		{
			if( Persister.HasCache ) Persister.Cache.Release( Id) ;
		}
	}
}
