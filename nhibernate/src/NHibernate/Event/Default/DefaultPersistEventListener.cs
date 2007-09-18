using System;
using System.Collections;
using log4net;
using NHibernate.Engine;
using NHibernate.Persister.Entity;
using NHibernate.Proxy;
using NHibernate.Util;

namespace NHibernate.Event.Default
{
	/// <summary> 
	/// Defines the default create event listener used by hibernate for creating
	/// transient entities in response to generated create events. 
	/// </summary>
	[Serializable]
	public class DefaultPersistEventListener : AbstractSaveEventListener, IPersistEventListener
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(DefaultPersistEventListener));

		protected internal override Cascades.CascadingAction CascadeAction
		{
			get { return Cascades.CascadingAction.ActionPersist; }
		}

		protected internal override bool? AssumedUnsaved
		{
			get { return true; }
		}

		public void OnPersist(PersistEvent theEvent)
		{
			OnPersist(theEvent, IdentityMap.Instantiate(10));
		}

		public void OnPersist(PersistEvent theEvent, IDictionary createdAlready)
		{
			ISessionImplementor source = theEvent.Session;
			object obj = theEvent.Entity;

			object entity;
			if (obj is INHibernateProxy)
			{
				LazyInitializer li = NHibernateProxyHelper.GetLazyInitializer((INHibernateProxy)obj);
				if (li.IsUninitialized)
				{
					if (li.Session == source)
					{
						return; //NOTE EARLY EXIT!
					}
					else
					{
						throw new PersistentObjectException("uninitialized proxy passed to persist()");
					}
				}
				entity = li.GetImplementation();
			}
			else
			{
				entity = obj;
			}

			EntityState entityState = GetEntityState(entity, theEvent.EntityName, source.GetEntry(entity), source);

			switch (entityState)
			{
				case EntityState.Persistent:
					EntityIsPersistent(theEvent, createdAlready);
					break;
				case EntityState.Transient:
					EntityIsTransient(theEvent, createdAlready);
					break;
				case EntityState.Detached:
					throw new PersistentObjectException("detached entity passed to persist: " + GetLoggableName(theEvent.EntityName, entity));
				default:
					throw new ObjectDeletedException("deleted instance passed to merge", null, entity.GetType());
			}
		}

		protected internal void EntityIsPersistent(PersistEvent @event, IDictionary createCache)
		{
			log.Debug("ignoring persistent instance");
			IEventSource source = @event.Session;

			//TODO: check that entry.getIdentifier().equals(requestedId)
			object entity = source.Unproxy(@event.Entity);
			IEntityPersister persister = source.GetEntityPersister(entity);

			object tempObject;
			tempObject = createCache[entity];
			createCache[entity] = entity;
			if (tempObject == null)
			{
				//TODO: merge into one method!
				CascadeBeforeSave(source, persister, entity, createCache);
				CascadeAfterSave(source, persister, entity, createCache);
			}
		}

		/// <summary> Handle the given create event. </summary>
		/// <param name="event">The save event to be handled. </param>
		/// <param name="createCache"></param>
		protected internal virtual void EntityIsTransient(PersistEvent @event, IDictionary createCache)
		{

			log.Debug("saving transient instance");

			IEventSource source = @event.Session;
			object entity = source.Unproxy(@event.Entity);

			object tempObject;
			tempObject = createCache[entity];
			createCache[entity] = entity;
			if (tempObject == null)
			{
				SaveWithGeneratedId(entity, @event.EntityName, createCache, source, false);
			}
		}
	}
}
