using System;
using System.Data;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using NHibernate.Type;
using NHibernate.Cache;
using NHibernate.Collection;
using NHibernate.Engine;
using NHibernate.Loader;
using NHibernate.Persister;
using NHibernate.Proxy;
using NHibernate.Hql;
using NHibernate.Util;
using NHibernate.Id;

namespace NHibernate.Impl {

	#warning Hack transaction and parameters
	// Put all hacks to get SimpleTest to work into this class so we can trace it
	// used by:
	public class AdoHack {
		
		//TODO: DEISGNISSUE: come up with a good way to create named parameters instead of ?
		// because IDbCommand has to use @Name and can't use ?

		// Force parametercollection to be created
		// Of course this is not the right place, this really means the entire concept
		// of named parameters should be revised!!!
		public static void CreateParameters(Dialect.Dialect dialect, IDbCommand cmd) 
		{
			string sql = cmd.CommandText;

			if (sql == null)
				return;
			//TODO: once this HACK class is removed get rid of the UseNamedParameters
			// and the NamedParametersPrefix Properties in Dialect and all subclasses.
			if (dialect.UseNamedParameters)
			{
				Regex parser = new Regex("(?<param>" + dialect.NamedParametersPrefix + "\\w*\\b)", RegexOptions.None); //.Compiled);
				string[] tokens = parser.Split(sql);
				if  (tokens.Length > 0)	
				{
					for (int idx=0; idx < tokens.Length; idx++)	
					{
						string token = tokens[idx];

						if (token != null && token.Length > 1 && token.Substring(0, 1).Equals(dialect.NamedParametersPrefix)) 
						{
							IDbDataParameter param;

							param = cmd.CreateParameter();
							param.ParameterName = token;
							cmd.Parameters.Add(param);
						}
					}
				}
			}
			else 
			{
				int idx      = 0;
				int paramIdx = 0;

				while((idx=sql.IndexOf("?", idx)) != -1) 
				{
					IDbDataParameter param;

					param = cmd.CreateParameter();
					param.ParameterName = paramIdx.ToString();
					cmd.Parameters.Add(param);
					paramIdx++;
					idx++;
				}
			}
		}

		public static void ReplaceHqlParameters(Dialect.Dialect dialect, IDbCommand cmd) 
		{
			Regex parser = new Regex("(?<param>\\[<\\w*>\\])", RegexOptions.Compiled);
			string[] tokens;
			string sql = cmd.CommandText;
			
			if (sql == null)
				return;
			tokens = parser.Split(sql);
			if  (tokens.Length > 0)	
			{
				StringBuilder sb = new StringBuilder();

				for (int idx=0; idx < tokens.Length; idx++)	
				{
					string token = tokens[idx];

					if (token != null && token.Length > 1 && token.Substring(0, 2).Equals("[<")) 
					{
						if (dialect.UseNamedParameters) 
						{
							IDbDataParameter param;

							param = cmd.CreateParameter();
							param.ParameterName = dialect.NamedParametersPrefix + token.Substring(2, token.Length - 4);
							cmd.Parameters.Add(param);
							sb.Append(param.ParameterName);
						}
						else 
						{
							throw new NotImplementedException("Hack not complete for this dialect");
						}
					}
					else 
					{
						sb.Append(token);
					}
				}
				cmd.CommandText = sb.ToString();
			}
		}

		// parametercollection starts at 0
		public static int ParameterPos(int pos)
		{
			return pos - 1;
		}

	}
	// -- end of Hack





	/// <summary>
	/// Concrete implementation of a Session, also the central, organizing component of
	/// Hibernate's internal implementaiton.
	/// </summary>
	/// <remarks>
	/// Exposes two interfaces: ISession itself, to the application and ISessionImplementor
	/// to other components of hibernate. This is where the hard stuff is...
	/// NOT THREADSAFE
	/// </remarks>
	[Serializable]
	internal class SessionImpl : ISessionImplementor, IDisposable  {
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(SessionImpl));

		private SessionFactoryImpl factory;

		private bool autoClose;
		private long timestamp;

		private bool closed = false;
		private FlushMode flushMode = FlushMode.Auto;

		private bool callAfterTransactionCompletionFromDisconnect = true;

		private IDictionary entitiesByKey; //key=Key, value=Object
		private IDictionary proxiesByKey; //key=Key, value=HibernateProxy
		
		[NonSerialized] private IdentityMap entries;//key=Object, value=Entry
		[NonSerialized] private IdentityMap arrayHolders; //key=array, value=ArrayHolder
		[NonSerialized] private IdentityMap collections; //key=PersistentCollection, value=CollectionEntry
		//[NonSerialized] private IDictionary entries; //key=Object, value=Entry
		//[NonSerialized] private IDictionary collections; //key=PersistentCollection, value=CollectionEntry

		private IList nullifiables = new ArrayList();

		private IInterceptor interceptor;

		// TODO: find out why this is holding a reference to a IDbConnection instead of a 
		// Connection.IConnectionProvider object
		[NonSerialized] private IDbConnection connection;
		[NonSerialized] private bool connect;

		// TODO: find out if we want the reference to IDbTransaction or ITransaction - leaning
		// towards an ITransaction because we can get the IDbTransaction from that.
		[NonSerialized] private ITransaction transaction;

		// We keep scheduled insertions, deletions and updates in collections
		// and actually execute them as part of the flush() process. Actually,
		// not every flush() ends in execution of the scheduled actions. Auto-
		// flushes initiated by a query execution might be "shortcircuited".
	
		// Object insertions and deletions have list semantics because they
		// must happen in the right order so as to respect referential integrity
		[NonSerialized] private ArrayList insertions;
		[NonSerialized] private ArrayList deletions;
		// updates are kept in a Map because successive flushes might need to add
		// extra, new changes for an object thats already scheduled for update.
		// Note: we *could* treat updates the same way we treat collection actions
		// (discarding them at the end of a "shortcircuited" auto-flush) and then
		// we would keep them in a list
		//[NonSerialized] private IDictionary updates;
		[NonSerialized] private ArrayList updates;
		// Actually the semantics of the next three are really "Bag"
		// Note that, unlike objects, collection insertions, updates,
		// deletions are not really remembered between flushes. We
		// just re-use the same Lists for convenience.
		[NonSerialized] private ArrayList collectionCreations;
		[NonSerialized] private ArrayList collectionUpdates;
		[NonSerialized] private ArrayList collectionRemovals;

		[NonSerialized] private ArrayList executions;

		[NonSerialized] private int dontFlushFromFind = 0;
		[NonSerialized] private bool reentrantCallback = false; //leaving in despite warning, WIP.
		[NonSerialized] private int cascading = 0;

		[NonSerialized] private IBatcher batcher;
		private IPreparer preparer;

		
		/// <summary>
		/// Represents the status of an entity with respect to 
		/// this session. These statuses are for internal 
		/// book-keeping only and are not intended to represent 
		/// any notion that is visible to the _application_. 
		/// </summary>
		[Serializable]
		internal enum Status 
		{
			Loaded,
			Deleted,
			Gone,
			Loading,
			Saving
		}

//		internal class Status {
//			private string name;
//			public Status(string name) {
//				this.name = name;
//			}
//			public override string ToString() {
//				return name;
//			}
//			private object ReadResolve() {
//				if ( name.Equals(LOADED.name) ) return LOADED;
//				if ( name.Equals(DELETED.name) ) return DELETED;
//				if ( name.Equals(GONE.name) ) return GONE;
//				if ( name.Equals(LOADING.name) ) return LOADING;
//				throw new InvalidExpressionException("invalid Status");
//			}
//		}
//		
//		private static Status LOADED = new Status("LOADED");
//		private static Status DELETED = new Status("DELETED");
//		private static Status GONE = new Status("GONE");
//		private static Status LOADING = new Status("LOADING");
//		private static Status SAVING = new Status("SAVING");

		internal interface IExecutable {
			void Execute();
			void AfterTransactionCompletion();
			object[] PropertySpaces { get; }
		}

		/// <summary>
		/// We need an entry to tell us all about the current state
		/// of an object with respect to its persistent state
		/// </summary>
		[Serializable]
		sealed internal class EntityEntry  {
			
			internal LockMode lockMode;
			[NonSerialized] internal LockMode nextLockMode;
			internal Status status;
			internal object id;
			internal object[] loadedState;
			internal object[] deletedState;
			internal bool existsInDatabase;
			internal object lastVersion;
			[NonSerialized] internal object nextVersion;
			[NonSerialized] internal IClassPersister persister;
			internal string className;

			public EntityEntry(Status status, object[] loadedState, object id, object version, LockMode lockMode, bool existsInDatabase, IClassPersister persister) {
				this.status = status;
				this.loadedState = loadedState;
				this.id = id;
				this.existsInDatabase = existsInDatabase;
				this.lastVersion = version;
				this.lockMode = lockMode;
				this.persister = persister;
				if (persister!=null) className = persister.ClassName;
			}
			
			// called after a *successful* flush
			internal void PostFlush(object obj) {
				if ( nextVersion!=null ) {
					this.lastVersion = nextVersion;
					Versioning.SetVersion(loadedState, nextVersion, persister);
					persister.SetPropertyValue( obj, persister.VersionProperty, nextVersion );
					nextVersion = null;
				}
				if ( nextLockMode!=null ) {
					lockMode = nextLockMode;
					nextLockMode = null;
				}
			}
			internal object CurrentVersion {
				get { return (nextVersion==null) ? lastVersion : nextVersion; }
			}
		}

		
		/// <summary>
		/// We need an entry to tell us all about the current state
		/// of a collection with respect to its persistent state
		/// </summary>
		[Serializable]
		public class CollectionEntry : ICollectionSnapshot 
		{
			internal bool dirty;
			[NonSerialized] internal bool reached;
			[NonSerialized] internal bool processed;
			[NonSerialized] internal bool doupdate;
			[NonSerialized] internal bool doremove;
			[NonSerialized] internal bool dorecreate;
			internal bool initialized;
			[NonSerialized] internal CollectionPersister currentPersister;
			[NonSerialized] internal CollectionPersister loadedPersister;
			[NonSerialized] internal object currentKey;
			internal object loadedKey;
			internal object snapshot; //session-start/post-flush persistent state
			internal  string role;
			
			public CollectionEntry() 
			{
				this.dirty = false;
				this.initialized = true;
			}

			public CollectionEntry(CollectionPersister loadedPersister, object loadedID, bool initialized) 
			{
				this.dirty = false;
				this.initialized = initialized;
				this.loadedKey = loadedID;
				SetLoadedPersister(loadedPersister);
			}

			public CollectionEntry(ICollectionSnapshot cs, ISessionFactoryImplementor factory) 
			{
				this.dirty = cs.Dirty;
				this.snapshot = cs.Snapshot;
				this.loadedKey = cs.Key;
				SetLoadedPersister( factory.GetCollectionPersister( cs.Role ) );
				this.initialized = true;
			}

			//default behavior; will be overridden in deep lazy collections
			public virtual bool IsDirty(PersistentCollection coll) 
			{
				if ( dirty || (
					!coll.IsDirectlyAccessible && !loadedPersister.ElementType.IsMutable
					) ) 
				{
					return dirty;
				} 
				else 
				{
					return !coll.EqualsSnapshot( loadedPersister.ElementType );
				}
			}

			public void PreFlush(PersistentCollection collection) 
			{
				// if the collection is initialized and it was previously persistent
				// initialize the dirty flag
				dirty = ( initialized && loadedPersister!=null && IsDirty(collection) ) ||
					(!initialized && dirty ); //only need this so collection with queued adds will be removed from JCS cache

				if ( log.IsDebugEnabled && dirty && loadedPersister!=null ) 
				{
					log.Debug("Collection dirty: " + MessageHelper.InfoString(loadedPersister, loadedKey) );
				}

				doupdate = false;
				doremove = false;
				dorecreate = false;
				reached = false;
				processed = false;
			}

			public void PostInitialize(PersistentCollection collection) 
			{
				snapshot = collection.GetSnapshot(loadedPersister);
			}

			// called after a *successful* flush
			public void PostFlush(PersistentCollection collection) 
			{
				if (!processed) 
					throw new AssertionFailure("Hibernate has a bug processing collections");
				loadedKey = currentKey;
				SetLoadedPersister( currentPersister );
				dirty = false;
				if ( initialized && ( doremove || dorecreate || doupdate ) ) 
				{
					snapshot = collection.GetSnapshot(loadedPersister); //re-snapshot
				}
			}

			public bool Dirty 
			{
				get { return dirty; }
			}
			public object Key 
			{
				get { return loadedKey; }
			}
			public string Role 
			{
				get { return role; }
			}
			public object Snapshot 
			{
				get { return snapshot; }
			}

			public bool SnapshotIsEmpty 
			{
				get {
					//TODO: implementation here is non-extensible ... 
					//should use polymorphism 
//					return initialized && snapshot!=null && ( 
//						( snapshot is IList && ( (IList) snapshot ).Count==0 ) || // if snapshot is a collection 
//						( snapshot is Map && ( (Map) snapshot ).Count==0 ) || // if snapshot is a map 
//						(snapshot.GetType().IsArray && ( (Array) snapshot).Length==0 )// if snapshot is an array 
//						); 
					
					// TODO: in .NET an IList, IDictionary, and Array are all collections so we might be able
					// to just cast it to a ICollection instead of all the diff collections.
					return initialized && snapshot!=null && ( 
						( snapshot is IList && ( (IList) snapshot ).Count==0 ) || // if snapshot is a collection 
						( snapshot is IDictionary && ( (IDictionary) snapshot ).Count==0 ) || // if snapshot is a map 
						(snapshot.GetType().IsArray && ( (Array) snapshot).Length==0 )// if snapshot is an array 
						); 
				}
			} 

			public void SetDirty() 
			{
				dirty = true;
			}
			
			private void SetLoadedPersister(CollectionPersister persister) 
			{
				loadedPersister = persister;
				if (persister!=null) role=persister.Role;
			}

			public bool IsInitialized 
			{
				get { return initialized;}
			}

			//TODO: new in H2.0.3 - where is it used??
			public bool IsNew 
			{
				// TODO: is this correct implementation
				get { return initialized && (snapshot==null); }
			}
		}

		//TODO: add serialization / deserialization stuff here

		internal SessionImpl(IDbConnection connection, SessionFactoryImpl factory, bool autoClose, long timestamp, IInterceptor interceptor) {
			this.connection = connection;
			connect = connection==null;
			this.interceptor = interceptor;

			this.autoClose = autoClose;
			this.timestamp = timestamp;
			
			this.factory = factory;

			entitiesByKey = new Hashtable(50);
			proxiesByKey = new Hashtable(10);
			//TODO: hack with this cast
			entries = (IdentityMap)IdentityMap.InstantiateSequenced();
			collections = (IdentityMap)IdentityMap.InstantiateSequenced();
			arrayHolders = (IdentityMap)IdentityMap.Instantiate();

			InitTransientCollections();

			log.Debug("opened session");
		}

		public IBatcher Batcher {
			get {
				if (batcher==null) batcher = new NonBatchingBatcher(this); //TODO: should check something, no?
				return batcher;
			}
		}

		public IPreparer Preparer {
			get {
				if (preparer == null) preparer = new PreparerImpl(factory, this);
				return preparer;
			}
		}

		public ISessionFactoryImplementor Factory {
			get { return factory; }
		}

		public long Timestamp {
			get { return timestamp; }
		}

		public IDbConnection Close() {
			log.Debug("closing session");

			try {
				return (connection==null) ? null : Disconnect();
			} finally {
				Cleanup();
			}
		}

		public void AfterTransactionCompletion() {
			log.Debug("transaction completion");

			// downgrade locks
			foreach(EntityEntry entry in entries.Values) {
				entry.lockMode = LockMode.None;
			}
			// release cache softlocks
			foreach(IExecutable executable in executions) {
				try {
					executable.AfterTransactionCompletion();
				} catch (CacheException ce) {
					log.Error("could not release a cache lock", ce);
					// continue loop
				} catch (Exception e) {
					throw new AssertionFailure("Exception releasing cache locks", e);
				}
			}
			executions.Clear();

			callAfterTransactionCompletionFromDisconnect = true; //not really necessary
		}

		private void InitTransientCollections() {
			insertions = new ArrayList(20);
			deletions = new ArrayList(20);
			updates = new ArrayList(20);
			collectionCreations = new ArrayList(20);
			collectionRemovals = new ArrayList(20);
			collectionUpdates = new ArrayList(20);
			executions = new ArrayList(50);
		}

		private void Cleanup() {
			closed = true;
			entitiesByKey.Clear();
			proxiesByKey.Clear();
			entries.Clear();
			arrayHolders.Clear();
			collections.Clear();
			nullifiables.Clear();
		}

		public LockMode GetCurrentLockMode(object obj) {
			if ( obj is HibernateProxy ) {
				obj = (HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) obj)).GetImplementation(this);
				if (obj==null) return LockMode.None;
			}
			EntityEntry e = GetEntry(obj);
			if (e==null) throw new TransientObjectException("Given object not associated with the session");
			//if (e.status!=LOADED) throw new ObjectDeletedException("The given object was deleted", e.id);
			if (e.status!=Status.Loaded) throw new ObjectDeletedException("The given object was deleted", e.id);
			return e.lockMode;
		}

		public LockMode GetLockMode(object entity) 	{
			return GetEntry(entity).lockMode;
		}

		private void AddEntity(Key key, object obj) {
			entitiesByKey.Add(key, obj);
		}
		public object GetEntity(Key key) {
			return entitiesByKey[key];
		}
		private object RemoveEntity(Key key) {
			object retVal = entitiesByKey[key];
			entitiesByKey.Remove(key);
			return retVal;
		}

		public void SetLockMode(object entity, LockMode lockMode) {
			GetEntry(entity).lockMode = lockMode;
		}

		private EntityEntry AddEntry(
			object obj,
			Status status,
			object[] loadedState,
			object id,
			object version,
			LockMode lockMode,
			bool existsInDatabase,
			IClassPersister persister) {

			EntityEntry e = new EntityEntry(status, loadedState, id, version, lockMode, existsInDatabase, persister);
			entries[obj] = e;
			return e;
		}

		private EntityEntry GetEntry(object obj) {
			return (EntityEntry) entries[obj];
		}
		private EntityEntry RemoveEntry(object obj) {
			object retVal = entries[obj];
			entries.Remove(obj);
			return (EntityEntry) retVal;
		}
		private bool IsEntryFor(object obj) {
			return entries.Contains(obj);
		}

		/// <summary>
		/// Add a new collection (ie an initialized one, instantiated by the application)
		/// </summary>
		/// <param name="collection"></param>
		private void AddNewCollection(PersistentCollection collection) 
		{
			CollectionEntry ce = new CollectionEntry();
			collections[collection] = ce;
			collection.CollectionSnapshot = ce;
		}

		private CollectionEntry GetCollectionEntry(PersistentCollection coll) {
			return (CollectionEntry) collections[coll];
		}

		public bool IsOpen {
			get { return !closed; }
		}

		/// <summary>
		/// Save a transient object. An id is generated, assigned to the object and returned
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public object Save(object obj) {
			
			if (obj==null) throw new NullReferenceException("attempted to save null");

			if ( !NHibernate.IsInitialized(obj) ) throw new PersistentObjectException("uninitialized proxy passed to save()"); 
			object theObj = UnproxyAndReassociate(obj); 


			EntityEntry e = GetEntry(theObj);
			if ( e!=null ) {
				//if ( e.status==DELETED ) {
				if ( e.status==Status.Deleted) {
					Flush();
				} 
				else {
					log.Debug( "object already associated with session" );
					return e.id;
				}
			}

			object id;
			try {
				id = GetPersister(theObj).IdentifierGenerator.Generate(this, theObj);
				if( id == (object) IdentifierGeneratorFactory.ShortCircuitIndicator) return GetIdentifier(theObj); //TODO: yick!
			} catch (Exception ex) {
				throw new ADOException("Could not save object", ex);
			}
			return DoSave(theObj, id);
		}

		/// <summary>
		/// Save a transient object with a manually assigned ID
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="id"></param>
		public void Save(object obj, object id) {

			if (obj==null) throw new NullReferenceException("attemted to insert null");
			if (id==null) throw new NullReferenceException("null identifier passed to insert()");

			if ( !NHibernate.IsInitialized(obj) ) throw new PersistentObjectException("uninitialized proxy passed to save()"); 
			object theObj = UnproxyAndReassociate(obj); 

			EntityEntry e = GetEntry(theObj);
			if ( e!=null ) {
				//if ( e.status==DELETED ) {
				if ( e.status==Status.Deleted ) {
					Flush();
				} 
				else {
					if ( !id.Equals(e.id) ) throw new PersistentObjectException(
												"object passed to save() was already persistent: " + MessageHelper.InfoString(e.persister, id)
												);
					log.Debug( "object already associated with session" );
				}
			}
			DoSave(theObj, id);
		}

		private object DoSave(object obj, object id) {
			IClassPersister persister = GetPersister(obj);

			Key key = null;
			bool identityCol;
			if (id==null) {
				if ( persister.IsIdentifierAssignedByInsert ) {
					identityCol = true;
				} else {
					throw new AssertionFailure("null id");
				}
			} else {
				identityCol = false;
			}

			if ( log.IsDebugEnabled ) log.Debug( "saving " + MessageHelper.InfoString(persister, id) );

			if (!identityCol) { // if the id is generated by the db, we assign the key later
				key = new Key(id, persister);

				object old = GetEntity(key);
				if (old!=null) {
					//if ( GetEntry(old).status==DELETED ) {
					if ( GetEntry(old).status==Status.Deleted) {
						Flush();
					} 
					else {
						throw new HibernateException(
							"The generated identifier is already in use: " + MessageHelper.InfoString(persister, id)
							);
					}
				}

				persister.SetIdentifier(obj, id);
			}

			// sub-insertions should occur befoer containing insertions so
			// try to do the callback now
			if ( persister.ImplementsLifecycle ) {
				if ( ( (ILifecycle) obj ).OnSave(this) == LifecycleVeto.Veto ) return id;
			}

			if ( persister.ImplementsValidatable ) ( (IValidatable) obj ).Validate();

			// Put a placeholder in entries, so we don't recurse back and try to save() th
			// same object again.
			//AddEntry(obj, SAVING, null, id, null, LockMode.Write, identityCol, persister);
			AddEntry(obj, Status.Saving, null, id, null, LockMode.Write, identityCol, persister);

			// cascade-save to many-to-one BEFORE the parent is saved
			cascading++;
			try {
				Cascades.Cascade(this, persister, obj, Cascades.CascadingAction.ActionSaveUpdate, CascadePoint.CascadeBeforeInsertAfterDelete);
			} 
			finally {
				cascading--;
			}

			object[] values = persister.GetPropertyValues(obj);
			IType[] types = persister.PropertyTypes;

			bool substitute = interceptor.OnSave( obj, id, values, persister.PropertyNames, types );

			substitute = ( persister.IsVersioned && Versioning.SeedVersion(
				values, persister.VersionProperty, persister.VersionType ) ) || substitute;

			if ( Wrap( values, persister.PropertyTypes ) || substitute) { //substitutes into values by side-effect
				persister.SetPropertyValues(obj, values);
			}

			TypeFactory.DeepCopy(values, types, persister.PropertyUpdateability, values);
			NullifyTransientReferences(values, types, identityCol, obj);

			if (identityCol) {
				try {
					id = persister.Insert(values, obj, this);
				} catch (Exception e) {
					throw new ADOException("Could not insert", e);
				}

				key = new Key(id, persister);

				if ( GetEntity(key) != null ) throw new HibernateException("The natively generated ID is already in use " + MessageHelper.InfoString(persister, id));

				persister.SetIdentifier(obj, id);
			}

			AddEntity(key, obj);
			//AddEntry(obj, LOADED, values, id, Versioning.GetVersion(values, persister), LockMode.Write, identityCol, persister);
			AddEntry(obj, Status.Loaded, values, id, Versioning.GetVersion(values, persister), LockMode.Write, identityCol, persister);
			
			if (!identityCol) insertions.Add( new ScheduledInsertion( id, values, obj, persister, this ) );

			// cascade-save to collections AFTER the collection owner was saved
			cascading++;
			try {
				Cascades.Cascade(this, persister, obj, Cascades.CascadingAction.ActionSaveUpdate, CascadePoint.CascadeAfterInsertBeforeDelete);
			} finally {
				cascading--;
			}
			
			return id;
		}

		private void ReassociateProxy(Object value) { 
			HibernateProxy proxy = (HibernateProxy) value; 
			LazyInitializer li = HibernateProxyHelper.GetLazyInitializer(proxy); 
			ReassociateProxy(li, proxy); 
		} 
    
		private object UnproxyAndReassociate(object maybeProxy) { 
			if ( maybeProxy is HibernateProxy ) { 
				HibernateProxy proxy = (HibernateProxy) maybeProxy; 
				LazyInitializer li = HibernateProxyHelper.GetLazyInitializer(proxy); 
				ReassociateProxy(li, proxy); 
				return li.GetImplementation(); //initialize + unwrap the object 
			} 
			else { 
				return maybeProxy; 
			} 
		} 
    
		/// <summary>
		/// associate a proxy that was instantiated by another session with this session
		/// </summary>
		/// <param name="li"></param>
		/// <param name="proxy"></param>
		private void ReassociateProxy(LazyInitializer li, HibernateProxy proxy) { 
			if ( li.Session!=this ) { 
				IClassPersister persister = GetPersister( li.PersistentClass ); 
				Key key = new Key( li.Identifier, persister ); 
				if ( !proxiesByKey.Contains(key) ) proxiesByKey.Add(key, proxy); // any earlier proxy takes precedence 
				HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) proxy ).SetSession(this); 
			} 
		} 

		private void NullifyTransientReferences(object[] values, IType[] types, bool earlyInsert, object self) {
			for (int i=0; i<types.Length; i++ ) {
				values[i] = NullifyTransientReferences( values[i], types[i], earlyInsert, self );
			}
		}

		/// <summary>
		/// Return null if the argument is an "unsaved" entity (ie. one with no existing database row), or the input argument otherwise. This is how Hibernate avoids foreign key constraint violations.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <param name="earlyInsert"></param>
		/// <param name="self"></param>
		/// <returns></returns>
		private object NullifyTransientReferences(object value, IType type, bool earlyInsert, object self) {
			if ( value==null ) {
				return null;
			} else if ( type.IsEntityType || type.IsObjectType ) {
				return ( IsUnsaved(value, earlyInsert, self) ) ? null : value;
			} else if ( type.IsComponentType ) {
				IAbstractComponentType actype = (IAbstractComponentType) type;
				object[] subvalues = actype.GetPropertyValues(value, this);
				IType[] subtypes = actype.Subtypes;
				bool substitute = false;
				for (int i=0; i<subvalues.Length; i++ ) {
					object replacement = NullifyTransientReferences( subvalues[i], subtypes[i], earlyInsert, self );
					if ( replacement != subvalues[i] ) {
						substitute = true;
						subvalues[i] = replacement;
					}
				}
				if (substitute) actype.SetPropertyValues(value, subvalues);
				return value;
			} else {
				return value;
			}
		}

		/// <summary>
		/// determine if the object already exists in the database, using a "best guess"
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="earlyInsert"></param>
		/// <param name="self"></param>
		/// <returns></returns>
		private bool IsUnsaved(object obj, bool earlyInsert, object self) {
			if ( obj is HibernateProxy ) {
				// if its an uninitialized proxy, it can't be transietn
				LazyInitializer li = HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) obj );
				if ( li.GetImplementation(this)==null ) {
					return false;
					// ie we never have to null out a reference to an uninitialized proxy
				} else {
					try {
						//unwrap it
						obj = li.GetImplementation(this);
					} catch (HibernateException he) {
						//does not occur
						throw new AssertionFailure("Unexpected HibernateException occurred in IsTransient()", he);
					}
				}
			}

			// if it was a reference to self, don't need to nullify
			// unless we are using native id generation, in which
			// case we definitely need to nullify
			if (obj==self) return earlyInsert;

			// See if the entity is already bound to this session, if not look at the
			// entity identifier and assume that the entity is persistent if the
			// id is not "unsaved" (that is, we rely on foreign keys to keep
			// database integrity)

			EntityEntry e = GetEntry(obj);
			if (e==null) {
				IClassPersister persister = GetPersister(obj);
				if ( persister.HasIdentifierProperty ) {
					object id = persister.GetIdentifier( obj );
					if (id!=null) {
						// see if theres another object that *is* associated with the sesison for that id
						e = GetEntry( GetEntity( new Key(id, persister) ) );

						if (e==null) { // look at the id value
							return persister.IsUnsaved(id);
						}
						// else use the other object's entry...
					} else { // null id, so have to assume transient (because that's safer)
						return true;
					}
				} else { //can't determine the id, so assume transient (because that's safer)
					return true;
				}
			}

			//return e.status==SAVING || (
			return e.status==Status.Saving || (
				earlyInsert ? !e.existsInDatabase : nullifiables.Contains( new Key(e.id, e.persister) )
				);
		}

		/// <summary>
		/// Delete a persistent object
		/// </summary>
		/// <param name="obj"></param>
		public void Delete(object obj) {

			if (obj==null) throw new NullReferenceException("attempted to delete null");

			object theObj = UnproxyAndReassociate(obj);

			EntityEntry entry = GetEntry(theObj);
			IClassPersister persister=null;
			if (entry==null) {
				log.Debug("deleting a transient instance");

				persister = GetPersister(theObj);
				object id = persister.GetIdentifier(theObj);

				if (id==null) throw new HibernateException("the transient instance passed to Delete() has a null identifier");

				object old = GetEntry( new Key(id, persister) );

				if (old!=null) {
					throw new HibernateException(
						"another object with the same id was already associated with the session: " +
						MessageHelper.InfoString(persister, id)
						);
				}

				RemoveCollectionsFor(persister, id, theObj);

				AddEntity( new Key(id, persister), theObj);
				entry = AddEntry(
					theObj, 
					Status.Loaded, //LOADED,
					persister.GetPropertyValues(theObj),
					id,
					persister.GetVersion(theObj),
					LockMode.None,
					true,
					persister
					);
				// not worth worrying about the proxy
			}
			else {
				log.Debug("deleting a persistent instance");

				//if ( entry.status==DELETED || entry.status==GONE ) {
				if ( entry.status==Status.Deleted || entry.status==Status.Gone) {
					log.Debug("object was already deleted");
					return;
				}
				persister = entry.persister;
			}

			if ( !persister.IsMutable ) throw new HibernateException(
											"attempted to delete an object of immutable class: " + MessageHelper.InfoString(persister)
											);

			if ( log.IsDebugEnabled ) log.Debug( "deleting " + MessageHelper.InfoString(persister, entry.id) );

			IType[] propTypes = persister.PropertyTypes;

			object version = entry.CurrentVersion;

			if (entry.loadedState==null ) { //ie the object came in from Update()
				entry.deletedState = persister.GetPropertyValues(theObj);
			} else {
				entry.deletedState = new object[entry.loadedState.Length];
				TypeFactory.DeepCopy(entry.loadedState, propTypes, persister.PropertyUpdateability, entry.deletedState);
			}

			interceptor.OnDelete(theObj, entry.id, entry.deletedState, persister.PropertyNames, propTypes);

			NullifyTransientReferences(entry.deletedState, propTypes, false, theObj);

			ArrayList oldNullifiables = null;
			ArrayList oldDeletions = null;
			if ( persister.HasCascades ) {
				oldNullifiables = new ArrayList();
				oldNullifiables.AddRange(nullifiables);
				oldDeletions = (ArrayList) deletions.Clone();
			}

			nullifiables.Add( new Key(entry.id, persister) );
			//entry.status = DELETED; // before any callbacks, etc, so subdeletions see that this deletion happend first
			entry.status = Status.Deleted; // before any callbacks, etc, so subdeletions see that this deletion happend first
			ScheduledDeletion delete = new ScheduledDeletion(entry.id, version, theObj, persister, this);
			deletions.Add(delete); // ensures that containing deletions happen before sub-deletions

			try {

				// after nullify, because we don't want to nullify references to subdeletions
				// try to do callback + cascade
				if ( persister.ImplementsLifecycle ) {
					if ( ( (ILifecycle)theObj).OnDelete(this) == LifecycleVeto.Veto ) {
						//rollback deletion
						RollbackDeletion(entry, delete);
						return; //don't let it cascade
					}
				}

				//BEGIN YUCKINESS:
				if ( persister.HasCascades ) {
					int start = deletions.Count;

					IList newNullifiables = nullifiables;
					nullifiables = oldNullifiables;

					cascading++;
					try {
						Cascades.Cascade(this, persister, theObj, Cascades.CascadingAction.ActionDelete, CascadePoint.CascadeAfterInsertBeforeDelete);
					} finally {
						cascading--;
						foreach(object oldNullify in oldNullifiables) {
							newNullifiables.Add(oldNullify);
						}
						nullifiables = newNullifiables;
					}

					int end = deletions.Count;

					if ( end!=start ) { //ie if any deletions occurred as a result of cascade

						//move them earlier. this is yucky code:

						IList middle = deletions.GetRange( oldDeletions.Count, start );
						IList tail = deletions.GetRange( start, end);

						oldDeletions.AddRange(tail);
						oldDeletions.AddRange(middle);

						if ( oldDeletions.Count != end ) throw new AssertionFailure("Bug cascading collection deletions");

						deletions = oldDeletions;
					}
				}
				//END YUCKINESS

				// cascade-save to many-to-one AFTER the parent was saved
				Cascades.Cascade(this, persister, theObj, Cascades.CascadingAction.ActionDelete, CascadePoint.CascadeBeforeInsertAfterDelete);
			} catch (Exception e) { //mainly a CallbackException
				RollbackDeletion(entry, delete);
				SessionImpl.Handle(e); //rethrow exception
			}
		}

		private void RollbackDeletion(EntityEntry entry, ScheduledDeletion delete) {
			//entry.status = LOADED;
			entry.status = Status.Loaded;
			entry.deletedState = null;
			deletions.Remove(delete);
		}

		private void RemoveCollectionsFor(IClassPersister persister, object id, object obj) {
			if ( persister.HasCollections ) {
				IType[] types = persister.PropertyTypes;
				for (int i=0; i<types.Length; i++) {
					RemoveCollectionsFor( types[i], id, persister.GetPropertyValue(obj, i) );
				}
			}
		}

		private void RemoveCollection(CollectionPersister role, object id) {
			if ( log.IsDebugEnabled ) log.Debug( "collection dereferenced while transient " + MessageHelper.InfoString(role, id) ); 
			collectionRemovals.Add( new ScheduledCollectionRemove(role, id, false, this) );
		}

		// TODO: rename this method
		/// <summary>
		/// When an entity is passed to update(), we must inspect all its collections and 
		/// 1. associate any uninitialized PersistentCollections with this session 
		/// 2. associate any initialized PersistentCollections with this session, using the		///    existing snapshot 
		/// 3. execute a collection removal (SQL DELETE) for each null collection property 
		///    or "new" collection 
		/// </summary>
		/// <param name="type"></param>
		/// <param name="id"></param>
		/// <param name="value"></param>
		private void RemoveCollectionsFor(IType type, object id, object value) {
			if ( type.IsPersistentCollectionType ) {
				CollectionPersister persister = GetCollectionPersister( ( (PersistentCollectionType) type).Role );
				if ( value!=null && (value is PersistentCollection) ) {
					PersistentCollection coll = (PersistentCollection) value;
					if ( coll.WasInitialized ) {
						ICollectionSnapshot snapshot = coll.CollectionSnapshot;
						if( snapshot != null &&
							snapshot.Role.Equals( persister.Role ) &&
							snapshot.Key.Equals( id )
							) {
							if( coll.SetSession( this ) ) AddInitializedCollection(coll, snapshot );
						}
						else {
							RemoveCollection(persister, id);
						}
					} else {
						if ( coll.SetSession(this) ) {
							AddUninitializedCollection(coll, persister, id);
						}
					}
				} else {
					RemoveCollection(persister, id);
				}
			} else if ( type.IsEntityType ) {
				if ( value!=null ) {
					IClassPersister persister = GetPersister( ( (EntityType) type).PersistentClass );
					if ( persister.HasProxy && !NHibernate.IsInitialized(value) )
						ReassociateProxy(value);
				}
			} else if ( type.IsComponentType ) {
				if ( value!=null ) {
					IAbstractComponentType actype = (IAbstractComponentType) type;
					IType[] types = actype.Subtypes;
					for (int i=0; i<types.Length; i++ ) {
						RemoveCollectionsFor( types[i], id, actype.GetPropertyValue(value, i, this) );
					}
				}
			}
		}
		
		public void Update(object obj) {

			if (obj==null) throw new NullReferenceException("attempted to update null");
			
			if(!NHibernate.IsInitialized(obj)) {
				ReassociateProxy(obj);
				return;
			}

			object theObj = UnproxyAndReassociate(obj);

			IClassPersister persister = GetPersister(theObj);

			if ( IsEntryFor(theObj) ) {
				log.Debug("object already associated with session");
				// do nothing
			} else {
				// the object is transient
				object id = persister.GetIdentifier(theObj);

				if (id==null) {
					// assume this is a newly instantiated transient object 
					throw new HibernateException("The given object has a null identifier property " + MessageHelper.InfoString(persister));
				} else {
					DoUpdate(theObj, id);
				}
			}
		}

		public void SaveOrUpdate(object obj) {
			if (obj==null) throw new NullReferenceException("attempted to update null");
			
			if ( !NHibernate.IsInitialized(obj) ) {
				ReassociateProxy(obj);
				return;
			}
			object theObj = UnproxyAndReassociate(obj);

			EntityEntry e = GetEntry(theObj);
			//if (e!=null && e.status!=DELETED) {
			if (e!=null && e.status!=Status.Deleted) {
				// do nothing for persistent instances
				log.Debug("SaveOrUpdate() persistent instance");
			} 
			else if (e!=null) { //ie status==DELETED
				log.Debug("SaveOrUpdate() deleted instance");
				Save(obj);
			} 
			else {

				// the object is transient
				object isUnsaved = interceptor.IsUnsaved(theObj);
				if (isUnsaved==null)
				{
					// use unsaved-value
					IClassPersister persister = GetPersister(theObj);
					if ( persister.HasIdentifierProperty ) 
					{
						
						object id = persister.GetIdentifier(theObj);

						if ( persister.IsUnsaved(id) ) 
						{
							if ( log.IsDebugEnabled ) log.Debug("SaveOrUpdate() unsaved instance with id: " + id);
							Save(obj);
						} 
						else 
						{
							if ( log.IsDebugEnabled ) log.Debug("SaveOrUpdate() previously saved instance with id: " + id);
							DoUpdate(theObj, id);
						}
					} 
					else 
					{
						// no identifier property ... default to save()
						log.Debug("SaveOrUpdate() unsaved instance with no identifier property");
						Save(obj);
					}
				} 
				else 
				{
					if ( true.Equals(isUnsaved) ) {
						log.Debug("SaveOrUpdate() unsaved instance");
						Save(obj);
					} else {
						log.Debug("SaveOrUpdate() previously saved instance");
						Update(obj);
					}
				}
			}
		}

		public void Update(object obj, object id) {
			if (id==null) throw new NullReferenceException("null is not a valid identifier");
			if (obj==null) throw new NullReferenceException("attempted to update null");
			
			if ( obj is HibernateProxy ) {
				object pid = HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) obj ).Identifier;
				if( !id.Equals(pid) )
					throw new HibernateException("The given proxy had a different identifier value to the given identifier: " + pid + "!=" + id);
			}

			if( !NHibernate.IsInitialized(obj) ) {
				ReassociateProxy(obj);
				return;
			}

			object theObj = UnproxyAndReassociate(obj);

			EntityEntry e = GetEntry(theObj);
			if (e==null) {
				DoUpdate(theObj, id);
			} else {
				if ( !e.id.Equals(id) ) throw new PersistentObjectException(
											"The instance passed to Update() was already persistent: " +
											MessageHelper.InfoString(e.persister, id)
											);
			}
		}

		private void DoUpdate(object obj, object id) {
			
			IClassPersister persister = GetPersister(obj);

			if ( !persister.IsMutable ) throw new HibernateException(
											"attempted to update an object of immutable class: " + MessageHelper.InfoString(persister)
											);
			
			if ( log.IsDebugEnabled ) log.Debug( "updating " + MessageHelper.InfoString(persister, id) );

			Key key = new Key(id, persister);
			object old = GetEntity(key);
			if (old==obj) {
				throw new AssertionFailure(
					"Hibernate has a bug in Update() ... or you are using an illegal id type: " +
					MessageHelper.InfoString(persister, id)
					);
			} else if ( old!=null ) {
				throw new HibernateException(
					"Another object was associated with this id ( the object with the given id was already loaded): " +
					MessageHelper.InfoString(persister, id)
					);
			}

			// this is a transient object with existing persistent state not loaded by the session

			if ( persister.ImplementsLifecycle && ((ILifecycle) obj ).OnUpdate(this) == LifecycleVeto.Veto ) return; // do callback

			RemoveCollectionsFor(persister, id, obj);

			AddEntity(key, obj);
			//AddEntry(obj, LOADED, null, id, persister.GetVersion(obj), LockMode.None, true, persister);
			AddEntry(obj, Status.Loaded, null, id, persister.GetVersion(obj), LockMode.None, true, persister);

			cascading++;
			try {
				Cascades.Cascade(this, persister, obj, Cascades.CascadingAction.ActionSaveUpdate, CascadePoint.CascadeOnUpdate); // do cascade
			} finally {
				cascading--;
			}
		}

		private static object[] NoArgs = new object[0];
		private static IType[] NoTypes = new IType[0];

		public IList Find(string query) {
			return Find(query, NoArgs, NoTypes);
		}

		public IList Find(string query, object value, IType type) {
			return Find( query, new object[] { value }, new IType[] { type } );
		}

		public IList Find(string query, object[] values, IType[] types) {
			return Find(query, values, types, null, null, null);
		}

		public IList Find(string query, object[] values, IType[] types, RowSelection selection, IDictionary namedParams,
			IDictionary lockModes) {

			if ( log.IsDebugEnabled ) {
				log.Debug( "find: " + query);
				if (values.Length!=0) log.Debug( "parameters: " + StringHelper.ToString(values) );
			}

			QueryTranslator[] q = GetQueries(query, false);

			IList results = new ArrayList();

			dontFlushFromFind++; //stops flush being called multiple times if this method is recursively called

			//execute the queries and return all result lists as a single list
			try {
				for (int i=0; i<q.Length; i++ ) {
					IList currentResults;
					try {
						currentResults = q[i].FindList(this, values, types, true, selection, namedParams, lockModes);
					} catch (Exception e) {
						throw new ADOException("Could not execute query", e);
					}
					for (int j=0;j<results.Count;j++) {
						currentResults.Add( results[j] );
					}
					results = currentResults;
				}
			} finally {
				dontFlushFromFind--;
			}
			return results;
		}

		private QueryTranslator[] GetQueries(string query, bool scalar) {

			// a query that naemes an interface or unmapped class in the from clause
			// is actually executed as multiple queries
			string[] concreteQueries = QueryTranslator.ConcreteQueries(query, factory);

			// take the union of the query spaces (ie the queried tables)
			QueryTranslator[] q = new QueryTranslator[concreteQueries.Length];
			ArrayList qs = new ArrayList();
			for (int i=0; i<concreteQueries.Length; i++ ) {
				q[i] = scalar ? factory.GetShallowQuery( concreteQueries[i] ) : factory.GetQuery( concreteQueries[i] );
				qs.AddRange( q[i].QuerySpaces );
			}

			AutoFlushIfRequired(qs);

			return q;
		}

		public IEnumerable Enumerable(string query) {
			return Enumerable(query, NoArgs, NoTypes);
		}

		public IEnumerable Enumerable(string query, object value, IType type) {
			return Enumerable( query, new object[] { value }, new IType[] { type } );
		}

		public IEnumerable Enumerable(string query, object[] values, IType[] types) {
			return Enumerable(query, values, types, null, null, null);
		}

		public IEnumerable Enumerable(string query, object[] values, IType[] types, RowSelection selection, 
			IDictionary namedParams, IDictionary lockModes) {

			if ( log.IsDebugEnabled ) {
				log.Debug( "GetEnumerable: " + query );
				if (values.Length!=0) log.Debug( "parameters: " + StringHelper.ToString(values) );
			}

			QueryTranslator[] q = GetQueries(query, true);

			if (q.Length==0) return new ArrayList();

			IEnumerable result = null;
			IEnumerable[] results = null;
			bool many = q.Length>1;
			if (many) results = new IEnumerable[q.Length];

			//execute the queries and return all results as a single enumerable
			for (int i=0; i<q.Length; i++) {
				try {
					result = q[i].GetEnumerable(values, types, selection, namedParams, lockModes, this);
				} catch (Exception e) {
					throw new ADOException("Could not execute query", e);
				}
			}

			return many ? new JoinedEnumerable(results) : result;
		}

		public int Delete(string query) {
			return Delete(query, NoArgs, NoTypes);
		}

		public int Delete(string query, object value, IType type) {
			return Delete( query, new object[] { value }, new IType[] { type } );
		}

		public int Delete(string query, object[] values, IType[] types) {
			if ( log.IsDebugEnabled ) {
				log.Debug ( "delete: " + query );
				if ( values.Length!=0 ) log.Debug( "parameters: " + StringHelper.ToString(values) );
			}

			IList list = Find(query, values, types);
			int count = list.Count;
			for (int i=0; i<count; i++ ) Delete( list[i] );
			return count;
		}

		public void Lock(object obj, LockMode lockMode) {
			
			if (obj==null) throw new NullReferenceException("attempted to lock null");

			if (lockMode==LockMode.Write) throw new HibernateException("Invalid lock mode for Lock()");

			object theObj = UnproxyAndReassociate(obj);
			//TODO: if object was an uninitialized proxy, this is inefficient, 
			//resulting in two SQL selects 

			EntityEntry e = GetEntry(theObj);
			if (e==null) throw new TransientObjectException("attempted to lock a transient instance");
			IClassPersister persister = e.persister;

			if ( lockMode.GreaterThan(e.lockMode) ) {

				//if (e.status!=LOADED) throw new TransientObjectException("attempted to lock a deleted instance");
				if (e.status!=Status.Loaded) throw new TransientObjectException("attempted to lock a deleted instance");

				if ( log.IsDebugEnabled ) log.Debug( "locking " + MessageHelper.InfoString(persister, e.id) + " in mode: " + lockMode);

				if ( persister.HasCache ) persister.Cache.Lock(e.id);
				try {
					persister.Lock(e.id, e.lastVersion, theObj, lockMode, this);
					e.lockMode = lockMode;
				} catch (Exception exp) {
					throw new ADOException("could not lock object", exp);
				} finally {
					// the database now holds a lock + the object is flushed from the cache,
					// so release the soft lock
					if ( persister.HasCache ) persister.Cache.Release(e.id);
				}
			}
		}

		public IQuery CreateFilter(object collection, string queryString) {
			return new FilterImpl(queryString, collection, this);
		}
		public IQuery CreateQuery(string queryString) {
			return new QueryImpl(queryString, this);
		}
		public IQuery GetNamedQuery(string queryName) {
			return CreateQuery( factory.GetNamedQuery(queryName) );
		}

		/// <summary>
		/// Give the interceptor an opportunity to override the default instantiation
		/// </summary>
		/// <param name="clazz"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public object Instantiate(System.Type clazz, object id) {
			return Instantiate( factory.GetPersister(clazz), id );
		}

		public object Instantiate(IClassPersister persister, object id) {
			object result = interceptor.Instantiate( persister.MappedClass, id );
			if (result==null) result = persister.Instantiate(id);
			return result;
		}

		public FlushMode FlushMode {
			get { return flushMode; }
			set { flushMode = value; }
		}

		/// <summary>
		/// detect in-memory changes, determine if the changes are to tables
		/// named in the query and, if so, complete execution the flush
		/// </summary>
		/// <param name="querySpaces"></param>
		/// <returns></returns>
		private bool AutoFlushIfRequired(IList querySpaces) {

			if ( flushMode==FlushMode.Auto && dontFlushFromFind==0 ) {

				int oldSize = collectionRemovals.Count;

				FlushEverything();

				if ( AreTablesToBeUpdated(querySpaces) ) {
					
					log.Debug("Need to execute flush");

					Execute();
					PostFlush();
					return true;
				} else {
					log.Debug("dont need to execute flush");

					// sort of don't like this: we re-use the same collections each flush
					// even though their state is not kept between flushes. However, its
					// nice for performance since the collection sizes will be "nearly"
					// what we need them to be next time.
					collectionCreations.Clear();
					collectionUpdates.Clear();
					updates.Clear();
					// collection deletes are a special case since Update() can add
					// deletions of collections not loaded by the session.
					for (int i=collectionRemovals.Count-1; i>=oldSize; i-- ) {
						collectionRemovals.RemoveAt(i);
					}
				}
			}

			return false;
		}

		/// <summary>
		/// If the existing proxy is insufficiently "narrow" (derived), instantiate a new proxy and overwrite the registration of the old one. This breaks == and occurs only for "class" proxies rather than "interface" proxies.
		/// </summary>
		/// <param name="proxy"></param>
		/// <param name="p"></param>
		/// <param name="key"></param>
		/// <param name="obj"></param>
		/// <returns></returns>
		public object NarrowProxy(object proxy, IClassPersister p, Key key, object obj) {

			if ( !p.ConcreteProxyClass.IsAssignableFrom( proxy.GetType() ) ) {

				if ( log.IsWarnEnabled ) log.Warn(
											 "Narrowing proxy to " + p.ConcreteProxyClass + " - this operation breaks =="
											 );

				if (obj!=null) {
					proxiesByKey.Remove(key);
					return obj;
				} else {
					proxy = null; //TODO: Get the proxy

					proxiesByKey.Add(key, proxy);
					return proxy;
				}
			} else {
				return proxy;
			}
		}

		// Grab the existing proxy for an instance, if one exists
		public object ProxyFor(IClassPersister persister, Key key, object impl) {
			if ( !persister.HasProxy ) return impl;
			object proxy = proxiesByKey[key];
			if (proxy!=null) {
				return NarrowProxy(proxy, persister, key, impl);
			} else {
				return impl;
			}
		}

		public object ProxyFor(object impl) {
			EntityEntry e = GetEntry(impl);

			IClassPersister p = GetPersister(impl);
			return ProxyFor( p, new Key(e.id, p), impl);
		}

		/// <summary>
		/// Create a "temporary" entry for a newly instantiated entity. The entity is uninitialized, but we need the mapping from id to instance in order to guarantee uniqueness.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="obj"></param>
		/// <param name="lockMode"></param>
		public void AddUninitializedEntity(Key key, object obj, LockMode lockMode) {
			IClassPersister p = GetPersister(obj);
			AddEntity(key, obj);
			//AddEntry(obj, LOADING, null, key.Identifier, null, lockMode, true, p );
			AddEntry(obj, Status.Loading, null, key.Identifier, null, lockMode, true, p );
		}

		/// <summary>
		/// Add the "hydrated state" (an array) of an uninitialized entity to the session. We don't try to resolve any associations yet, because there might be other entities waiting to be read from the ADO datareader we are currently processing
		/// </summary>
		/// <param name="persister"></param>
		/// <param name="id"></param>
		/// <param name="values"></param>
		/// <param name="obj"></param>
		/// <param name="lockMode"></param>
		public void PostHydrate(IClassPersister persister, object id, object[] values, object obj, LockMode lockMode) {
			persister.SetIdentifier(obj, id);
			object version = Versioning.GetVersion(values, persister);
			//AddEntry(obj, LOADED, values, id, version, lockMode, true, persister);
			AddEntry(obj, Status.Loaded, values, id, version, lockMode, true, persister);

			if ( log.IsDebugEnabled && version!=null) log.Debug("Version: " + version);
		}

		private void ThrowObjectNotFound(object o, object id, System.Type clazz) {
			if (o==null) throw new ObjectNotFoundException( "No row with the given identifier exists", id, clazz );
		}

		public void Load(object obj, object id) {
			if (id==null) throw new NullReferenceException("null is not a valid identifier");
			DoLoadByObject(obj, id, true);
		}

		public object Load(System.Type clazz, object id) {
			if (id==null) throw new NullReferenceException("null is not a valid identifier");
			object result = DoLoadByClass(clazz, id, true, true);
			ThrowObjectNotFound(result, id, clazz);
			return result;
		}

		/**
		* Load the data for the object with the specified id into a newly created object.
		* Do NOT return a proxy.
		*/
		public object ImmediateLoad(System.Type clazz, object id) {
			object result = DoLoad(clazz, id, null, LockMode.None, false);
			ThrowObjectNotFound(result, id, clazz);
			return result;
		}

		/**
		* Return the object with the specified id or null if no row with that id exists. Do not defer the load
		* or return a new proxy (but do return an existing proxy). Do not check if the object was deleted.
		*/
		public object InternalLoadOneToOne(System.Type clazz, object id) {
			return DoLoadByClass(clazz, id, false, false);
		}

		/**
		* Return the object with the specified id or throw exception if no row with that id exists. Defer the load,
		* return a new proxy or return an existing proxy if possible. Do not check if the object was deleted.
		*/
		public object InternalLoad(System.Type clazz, object id) {
			object result = DoLoadByClass(clazz, id, false, true);
			ThrowObjectNotFound(result, id, clazz);
			return result;
		}
		
		/**
		* Load the data for the object with the specified id into the supplied
		* instance. A new key will be assigned to the object. If there is an
		* existing uninitialized proxy, this will break identity equals as far
		* as the application is concerned.
		*/
		private void DoLoadByObject(object obj, object id, bool checkDeleted) {
			
			System.Type clazz = obj.GetType();
			if ( GetEntry(obj)!=null ) throw new PersistentObjectException(
										   "attempted to load into an instance that was already associated with the Session: "+
										   MessageHelper.InfoString(clazz, id)
										   );
			object result = DoLoad(clazz, id, obj, LockMode.None, checkDeleted);
			ThrowObjectNotFound(result, id, clazz);
			if (result!=obj) throw new HibernateException(
								 "The object with that id was already loaded by the Session: " +
								 MessageHelper.InfoString(clazz, id)
								 );
		}

		/**
		* Load the data for the object with the specified id into a newly created
		* object. A new key will be assigned to the object. If the class supports
		* lazy initialization, return a proxy instead, leaving the real work for
		* later. This should return an existing proxy where appropriate.
		*/
		private object DoLoadByClass(System.Type clazz, object id, bool checkDeleted, bool allowProxyCreation) {
			
			if ( log.IsDebugEnabled ) log.Debug( "loading " + MessageHelper.InfoString(clazz, id) );

			IClassPersister persister = GetPersister(clazz);
			if ( !persister.HasProxy ) {
				// this class has no proxies (so do a shortcut)
				return DoLoad(clazz, id, null, LockMode.None, checkDeleted);
			} else {
				Key key = new Key(id, persister);
				object proxy = null;
				if ( GetEntity(key)!=null ) {
					// return existing object or initialized proxy (unless deleted)
					return ProxyFor(
						persister,
						key,
						DoLoad(clazz, id, null, LockMode.None, checkDeleted)
						);
				} else if ( ( proxy = proxiesByKey[key] ) != null ) {
					// return existing uninitizlied proxy
					return NarrowProxy(proxy, persister, key, null);
				} else if ( allowProxyCreation ) {
					// retunr new uninitailzed proxy
					if ( persister.HasProxy ) {
						proxy = null; //TODO: Create the proxy
					}
					proxiesByKey.Add(key, proxy);
					return proxy;
				} else {
					// return a newly loaded object
					return DoLoad(clazz, id, null, LockMode.None, checkDeleted);
				}
			}
		}

		/// <summary>
		/// Load the data for the object with the specified id into a newly created object
		/// using "for update", if supported. A new key will be assigned to the object.
		/// This should return an existing proxy where appropriate.
		/// </summary>
		/// <param name="clazz"></param>
		/// <param name="id"></param>
		/// <param name="lockMode"></param>
		/// <returns></returns>
		public object Load(System.Type clazz, object id, LockMode lockMode) {

			if ( lockMode==LockMode.Write ) throw new HibernateException("invalid lock mode for Load()");

			if ( log.IsDebugEnabled ) log.Debug( "loading " + MessageHelper.InfoString(clazz, id) + " in lock mode: " + lockMode );
			if (id==null) throw new NullReferenceException("null is not a valid identifier");

			IClassPersister persister = GetPersister(clazz);
			if ( persister.HasCache ) persister.Cache.Lock(id); //increments the lock
			object result;
			try {
				result = DoLoad(clazz, id, null, lockMode, true);
			} finally {
				// the datbase now hold a lock + the object is flushed from the cache,
				// so release the soft lock
				if ( persister.HasCache ) persister.Cache.Release(id);
			}

			ThrowObjectNotFound( result, id, persister.MappedClass );

			// retunr existing proxy (if one exists)
			return ProxyFor(persister, new Key(id, persister), result );
		}

		private object DoLoad(System.Type theClass, object id, object optionalObject, LockMode lockMode, bool checkDeleted) {
			//DONT need to flush before a load by id

			if ( log.IsDebugEnabled ) log.Debug( "attempting to resolve " + MessageHelper.InfoString(theClass, id) );

			bool isOptionalObject = optionalObject!=null;

			IClassPersister persister = GetPersister(theClass);
			Key key = new Key(id, persister);

			// LOOK FOR LOADED OBJECT 
			// Look for Status.Loaded object
			object old = GetEntity(key);
			if (old!=null) { //if this object was already loaded
				Status status = GetEntry(old).status;
				//if ( checkDeleted && ( status==DELETED || status==GONE ) ) {
				if ( checkDeleted && ( status==Status.Deleted || status==Status.Gone) ) {
					throw new ObjectDeletedException("The object with that id was deleted", id);
				}
				Lock(old, lockMode);
				if ( log.IsDebugEnabled ) log.Debug( "resolved object in session cache " + MessageHelper.InfoString(persister, id) );
				return old;
			
			} else {

				// LOOK IN CACHE
				CacheEntry entry = persister.HasCache ? (CacheEntry) persister.Cache.Get(id, timestamp) : null;
				if (entry!=null) {
					if ( log.IsDebugEnabled ) log.Debug( "resolved object in JCS cache " + MessageHelper.InfoString(persister, id) );
					IClassPersister subclassPersister = GetPersister( entry.Subclass );
					object result = (isOptionalObject) ? optionalObject : Instantiate(subclassPersister, id);
					//AddEntry(result, LOADING, null, id, null, LockMode.None, true, subclassPersister);
					AddEntry(result, Status.Loading, null, id, null, LockMode.None, true, subclassPersister);
					AddEntity( new Key(id, persister), result );
					object[] values = entry.Assemble(result, id, subclassPersister, this); // intializes result by side-effect

					IType[] types = subclassPersister.PropertyTypes;
					TypeFactory.DeepCopy(values, types, subclassPersister.PropertyUpdateability, values);
					object version = Versioning.GetVersion(values, subclassPersister);
					
					if ( log.IsDebugEnabled ) log.Debug("Cached Version: " + version);
					//AddEntry(result, LOADED, values, id, version, LockMode.None, true, subclassPersister);
					AddEntry(result, Status.Loaded, values, id, version, LockMode.None, true, subclassPersister);
					
					// upgrate lock if necessary;
					Lock(result, lockMode);

					return result;
				
				} else {
					//GO TO DATABASE
					if ( log.IsDebugEnabled ) log.Debug( "object not resolved in any cache " + MessageHelper.InfoString(persister, id) );
					try {
						return persister.Load(id, optionalObject, lockMode, this);
					} catch (Exception e) {
						throw new ADOException("could not load object", e);
					}
				}
			}
		}

		public void Refresh(object obj) {
			Refresh(obj, LockMode.Read);
		}

		public void Refresh(object obj, LockMode lockMode) {
			if (obj==null) throw new NullReferenceException("attempted to refresh null");

			if ( !NHibernate.IsInitialized(obj) ) { 
				ReassociateProxy(obj); 
				return; 
			} 

			object theObj = UnproxyAndReassociate(obj);
			EntityEntry e = RemoveEntry(theObj);

			if ( log.IsDebugEnabled ) log.Debug( "refreshing " + MessageHelper.InfoString(e.persister, e.id) );

			if ( !e.existsInDatabase ) throw new HibernateException("this instance does not yet exist as a row in the database");

			Key key = new Key(e.id, e.persister);
			RemoveEntity( key );
			try {
				e.persister.Load( e.id, theObj, lockMode, this);
			} catch (Exception exp) {
				throw new ADOException("could not refresh object", exp);
			}
			GetEntry(theObj).lockMode = e.lockMode;
		}

		/// <summary>
		/// After processing a JDBC result set, we "resolve" all the associations
		/// between the entities which were instantiated and had their state
		/// "hydrated" into an array
		/// </summary>
		/// <param name="obj"></param>
		public void InitializeEntity(object obj) {

			EntityEntry e = GetEntry(obj);
			IClassPersister persister = e.persister;
			object id = e.id;
			object[] hydratedState = e.loadedState;

#warning SimpleTest runs until this point: persister is null somehow!!!!

			IType[] types = persister.PropertyTypes;

			if(log.IsDebugEnabled)
				log.Debug("resolving associations for: " + MessageHelper.InfoString(persister, id) );

			interceptor.OnLoad( obj, id, hydratedState, persister.PropertyNames, types );

			for ( int i=0; i<hydratedState.Length; i++ ) {
				hydratedState[i] = types[i].ResolveIdentifier( hydratedState[i], this, obj );
			}
			persister.SetPropertyValues(obj, hydratedState);
			TypeFactory.DeepCopy(hydratedState, persister.PropertyTypes, persister.PropertyUpdateability, hydratedState); 

			if ( persister.HasCache ) {
				if ( log.IsDebugEnabled ) log.Debug( "adding entity to JCS cache " + MessageHelper.InfoString(persister, id) );
				persister.Cache.Put( id, new CacheEntry( obj, persister, this), timestamp );
			}

			reentrantCallback=true;

			if ( persister.ImplementsLifecycle ) ((ILifecycle) obj).OnLoad(this, id);

			reentrantCallback=false;

			if ( log.IsDebugEnabled ) log.Debug( "done materializing entity " + MessageHelper.InfoString(persister, id) );
		}

		public ITransaction BeginTransaction() {
			callAfterTransactionCompletionFromDisconnect = false;

			transaction = factory.TransactionFactory.BeginTransaction(this);

			return transaction;

		}

		public ITransaction Transaction 
		{
			get {return transaction;}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <remarks>
		/// This can be called from commit() or at the start of a Find() method.
		/// <para>
		/// Perform all the necessary SQL statements in a sensible order, to allow
		/// users to repect foreign key constraints:
		/// <list type="">
		///		<item>Inserts, in the order they were performed</item>
		///		<item>Updates</item>
		///		<item>Deletion of collection elements</item>
		///		<item>Insertion of collection elements</item>
		///		<item>Deletes, in the order they were performed</item>
		/// </list>
		/// </para>
		/// <para>
		/// Go through all the persistent objects and look for collections they might be
		/// holding. If they had a nonpersistable collection, substitute a persistable one
		/// </para>
		/// </remarks>
		public void Flush() 
		{
			if (cascading>0) throw new HibernateException(
								 "Flush during cascade is dangerous - this might occur if an object as deleted and then re-saved by cascade"
								 );
			FlushEverything();
			Execute();
			PostFlush();
		}

		private void FlushEverything() {

			log.Debug("flushing session");

			interceptor.PreFlush( entitiesByKey.Values );

			PreFlushEntities();
			PreFlushCollections();
			FlushEntities();
			FlushCollections();

			//some stats

			if ( log.IsDebugEnabled ) {
				log.Debug( "Flushed: " +
					insertions.Count + " insertions, " +
					updates.Count + " updates, " +
					deletions.Count + " deletions to " +
					entries.Count + " objects");
				log.Debug( "Flushed: " +
					collectionCreations.Count + " (re)creations, " +
					collectionUpdates.Count + " updates, " +
					collectionRemovals.Count + " removals to " +
					collections.Count + " collections");
			}
		}

		private bool AreTablesToBeUpdated(IList tables) {
			return AreTablesToUpdated( updates, tables) ||
				AreTablesToUpdated( insertions, tables) ||
				AreTablesToUpdated( deletions, tables) ||
				AreTablesToUpdated( collectionUpdates, tables) ||
				AreTablesToUpdated( collectionCreations, tables) ||
				AreTablesToUpdated( collectionRemovals, tables);
		}

		private bool AreTablesToUpdated(ICollection coll, IList theSet) {
			foreach( IExecutable executable in coll ) {
				object[] spaces = executable.PropertySpaces;
				for (int i=0; i<spaces.Length; i++) {
					if ( theSet.Contains( spaces[i] ) ) return true;
				}
			}
			return false;
		}

		private void Execute() {

			log.Debug("executing flush");

			try {
				ExecuteAll( insertions );
				ExecuteAll( updates );
				ExecuteAll( collectionRemovals );
				ExecuteAll( collectionUpdates );
				ExecuteAll( collectionCreations );
				ExecuteAll( deletions );

				// have to do this here because ICollection does not have a remove method
				insertions.Clear();
				updates.Clear();
				collectionRemovals.Clear();
				collectionUpdates.Clear();
				collectionCreations.Clear();
				deletions.Clear();
			} catch (Exception e) {
				throw new ADOException("could not synchronize database state with session", e);
			}
		}

		public void PostInsert(object obj) {
			GetEntry(obj).existsInDatabase = true;
		}

		public void PostDelete(object obj) {
			EntityEntry e = RemoveEntry(obj);
			e.status = Status.Gone;
			Key key = new Key(e.id, e.persister);
			RemoveEntity(key);
			proxiesByKey.Remove(key);
		}

		public void PostUpdate(object obj, object[] updatedState, object nextVersion) 
		{
			EntityEntry e = GetEntry(obj);
			e.loadedState = updatedState;
			e.lockMode = LockMode.Write;
			if(e.persister.IsVersioned) 
			{
				//TODO: h2.0.3 - the e.version should exist but it does not in NH
				//e.version = nextVersion;
				e.persister.SetPropertyValue(obj, e.persister.VersionProperty, nextVersion);
			}
		}

		private void ExecuteAll(ICollection coll) {
			foreach(IExecutable e in coll) {
				executions.Add(e);
				e.Execute();
				//TODO: h2.0.3 has this but not NH
				// iter.remove -> coll.Remove()??
			}
			if ( batcher!=null ) batcher.ExecuteBatch();
		}

		

		/// <summary>
		/// 1. detect any dirty entities
		/// 2. schedule any entity updates
		/// 3. search out any reachable collections
		/// </summary>
		private void FlushEntities() {

			log.Debug("Flushing entities and processing referenced collections");

			// Among other things, updateReachables() will recursively load all
			// collections that are moving roles. This might cause entities to
			// be loaded.
		
			// So this needs to be safe from concurrent modification problems.
			// It is safe because of how IdentityMap implements entrySet()
			
			ICollection iterSafeCollection = IdentityMap.ConcurrentEntries(entries);

			foreach(DictionaryEntry me in iterSafeCollection) {	
				EntityEntry entry = (EntityEntry) me.Value;
				Status status = entry.status;

				if (status != Status.Loading && status != Status.Gone) {
					object obj = me.Key;
					IClassPersister persister = entry.persister;

					// make sure user didn't mangle the id
					if ( persister.HasIdentifierProperty ) {
						object oid = persister.GetIdentifier(obj);

						if ( !entry.id.Equals(oid) ) throw new HibernateException(
														 "identiifier of an instance of " +
														 persister.ClassName +
														 " altered from " +
														 entry.id + 
														 " to " + oid
														 );
					}

					object[] values;
					if ( status==Status.Deleted) {
						//grab its state saved at deletion
						values = entry.deletedState;
					} 
					else {
						//grab its current state
						values = persister.GetPropertyValues(obj);
					}
					IType[] types = persister.PropertyTypes;

					// wrap up any new collections directly referenced by the object
					// or its compoents

					// NOTE: we need to do the wrap here even if its not "dirty",
					// because nested collections need wrapping but changes to
					// _them_ don't dirty the container. Also, for versioned
					// data, we need to wrap before calling searchForDirtyCollections

					bool substitute = Wrap(values, types); // substitutes into values by side-effect

					bool cannotDirtyCheck;
					bool interceptorHandledDirtyCheck;

					int[] dirtyProperties = interceptor.FindDirty(obj, entry.id, values, entry.loadedState, persister.PropertyNames, types);


					if ( dirtyProperties==null ) {
						// interceptor returned null, so do the dirtycheck ourself, if possible
						interceptorHandledDirtyCheck = false;
						cannotDirtyCheck = entry.loadedState==null; // object loaded by update()
						if ( !cannotDirtyCheck ) {
							dirtyProperties = persister.FindDirty(values, entry.loadedState, obj, this);
						}
					} else {
						// the interceptor handled the dirty checking
						cannotDirtyCheck = false;
						interceptorHandledDirtyCheck = true;
					}

					// compare to cached state (ignoring nested collections)
					if ( persister.IsMutable &&
						(cannotDirtyCheck ||
						(dirtyProperties!=null && dirtyProperties.Length!=0 ) ||
						(status==Status.Loaded && persister.IsVersioned && persister.HasCollections && SearchForDirtyCollections(values, types) )
						)
						) 
					{
						// its dirty!

						if ( log.IsDebugEnabled ) 
						{
							if(status == Status.Deleted) 
							{ 
								log.Debug("Updating deleted entity: " + MessageHelper.InfoString(persister, entry.id) );
							}
							else 
							{
								log.Debug("Updating entity: " + MessageHelper.InfoString(persister, entry.id) );
							}
						}

						// give the Interceptor a chance to modify property values
						bool intercepted = interceptor.OnFlushDirty(
							obj, entry.id, values, entry.loadedState, persister.PropertyNames, types);

						//no we might need to recalculate the dirtyProperties array
						if(intercepted && !cannotDirtyCheck && !interceptorHandledDirtyCheck) 
						{
							dirtyProperties = persister.FindDirty(values, entry.loadedState, obj, this);
						}
						// if the properties were modified by the Interceptor, we need to set them back to the object
						substitute = substitute || intercepted;

						// validate() instances of Validatable
						if(status == Status.Loaded && persister.ImplementsValidatable) 
						{
							((IValidatable)obj).Validate();
						}

						//increment the version number (if necessary)
						// TODO: H2.0.3 need to add entry.version field
						object nextVersion = entry.lastVersion;
						if(persister.IsVersioned) 
						{
							if(status!=Status.Deleted) nextVersion = Versioning.Increment(entry.lastVersion, persister.VersionType);
							Versioning.SetVersion(values, nextVersion, persister);
						}
						
						object[] updatedState = null;
						if(status==Status.Loaded)
						{
							updatedState = new object[values.Length];
							TypeFactory.DeepCopy(values, types, persister.PropertyUpdateability, updatedState);
						}
						
						updates.Add(
							new ScheduledUpdate(entry.id, values, dirtyProperties, entry.lastVersion, nextVersion, obj, updatedState, persister, this)
							);
					}
					
					if(status==Status.Deleted) 
					{
						//entry.status = Status.Gone;
					}
					else 
					{
						// now update the object... has to be outside the main if block above (because of collections)
						if(substitute) persister.SetPropertyValues(obj, values);
						
						// search for collections by reachability, updating their role.
						// we don't want to touch collections reachable from a deleted object.
						UpdateReachables(values, types, obj);
					}
					
				}
			}
		}

		/// <summary>
		/// process cascade save/update at the start of a flush to discover
		/// any newly referenced entity that must be passed to saveOrUpdate()
		/// </summary>
		private void PreFlushEntities() {

			ICollection iterSafeCollection = IdentityMap.ConcurrentEntries(entries);

			// so that we can be safe from the enumerator & concurrent modifications
			foreach(DictionaryEntry me in iterSafeCollection) {
			
				EntityEntry entry = (EntityEntry) me.Value;
				Status status = entry.status;

				if ( status!=Status.Loading && status!=Status.Gone && status!=Status.Deleted) {
					object obj = me.Key;
					cascading++;
					try {
						Cascades.Cascade(this, entry.persister, obj, Cascades.CascadingAction.ActionSaveUpdate, CascadePoint.CascadeOnUpdate);
					} 
					finally {
						cascading--;
					}
				}
			}
		}

		// this just does a table lookup, but cacheds the last result

		[NonSerialized] private System.Type lastClass;
		[NonSerialized] private IClassPersister lastResultForClass;

		private IClassPersister GetPersister(System.Type theClass) {
			if ( lastClass!=theClass ) {
				lastResultForClass = factory.GetPersister(theClass);
				lastClass = theClass;
			}
			return lastResultForClass;
		}

		public IClassPersister GetPersister(object obj) {
			return GetPersister( obj.GetType() );
		}

		/// <summary>
		/// Not for internal use
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public object GetIdentifier(object obj) {
			if (obj is HibernateProxy) {
				LazyInitializer li = HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) obj );
				if ( li.Session!=this ) throw new TransientObjectException("The proxy was not associated with this session");
				return li.Identifier;
			} else {
				EntityEntry entry = GetEntry(obj);
				if (entry==null) throw new TransientObjectException("the instance was not associated with this session");
				return entry.id;
			}
		}

		public bool IsSaved(object obj) 
		{
			if(obj is HibernateProxy) return true;

			EntityEntry entry = GetEntry(obj);
			if(entry!=null) return true;

			object isUnsaved = interceptor.IsUnsaved(obj);
			if(isUnsaved!=null) return !(bool)isUnsaved;

			IClassPersister persister = GetPersister(obj);
			if(!persister.HasIdentifierPropertyOrEmbeddedCompositeIdentifier) return false; // I _think_ that this is reasonable!

			object id = persister.GetIdentifier(obj);
			return !persister.IsUnsaved(id);
		}

		/// <summary>
		/// Get the id value for an object that is actually associated with the session.
		/// This is a bit stricter than getEntityIdentifierIfNotUnsaved().
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public object GetEntityIdentifier(object obj) {
			if (obj is HibernateProxy) {
				return HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) obj ).Identifier;
			} else {
				EntityEntry entry = GetEntry(obj);
				return (entry!=null) ? entry.id : null;
			}
		}

		/// <summary>
		/// Used by OneToOneType and ManyToOneType to determine what id value
		/// should be used for an object that may or may not be associated with
		/// the session. This does a "best guess" using any/all info available
		/// to use (not just the EntityEntry).
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public object GetEntityIdentifierIfNotUnsaved(object obj) {
			if (obj==null) return null;

			if (obj is HibernateProxy) {
				return HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) obj ).Identifier;
			} 
			else 
			{
				EntityEntry entry = GetEntry(obj);
				if(entry!=null) 
				{
					return entry.id;
				}
				else 
				{

					object isUnsaved = interceptor.IsUnsaved(obj);
					
					if(isUnsaved!=null && ((bool)isUnsaved)) 
						ThrowTransientObjectException(obj);
				
					IClassPersister persister = GetPersister(obj);
					if(!persister.HasIdentifierPropertyOrEmbeddedCompositeIdentifier)
						ThrowTransientObjectException(obj);

					object id = persister.GetIdentifier(obj);
					if(persister.IsUnsaved(id)) 
						ThrowTransientObjectException(obj);

					return id;
				}

			}
		}

		private static void ThrowTransientObjectException(object obj) 
		{
			throw new TransientObjectException(
				"object references an unsaved transient instance - save the transient instance before flushing: " +
				obj.GetType().Name
				);
		}
		/// <summary>
		/// process any unreferenced collections and then inspect all known collections,
		/// scheduling creates/removes/updates
		/// </summary>
		private void FlushCollections() {

			int unreferencedCount = 0;
			int scheduledCount = 0;
			int recreateCount = 0;
			int removeCount = 0;
			int updateCount = 0;

			log.Debug("Processing unreferenced collections");

			foreach(DictionaryEntry e in IdentityMap.ConcurrentEntries(collections))// collections.EntryList) 
			{
				if ( ! ( (CollectionEntry) e.Value ).reached ) 
				{
					UpdateUnreachableCollection( (PersistentCollection) e.Key );
					unreferencedCount++;
				}
					
			}
			log.Debug("Processed " + unreferencedCount + " unreachable collections.");

			// schedule updates to collections:

			log.Debug("scheduling collection removes/(re)creates/updates");

			foreach(DictionaryEntry me in IdentityMap.ConcurrentEntries(collections)) // collections.EntryList) 
			{
				PersistentCollection coll = (PersistentCollection) me.Key;
				CollectionEntry ce = (CollectionEntry) me.Value;

				// TODO: move this to the entry

				if ( ce.dorecreate ) 
				{
					collectionCreations.Add( new ScheduledCollectionRecreate(coll, ce.currentPersister, ce.currentKey, this) );
					recreateCount++;
				}
				if ( ce.doremove ) 
				{
					collectionRemovals.Add( new ScheduledCollectionRemove(ce.loadedPersister, ce.loadedKey, ce.SnapshotIsEmpty, this) );
					removeCount++;
				}

				if ( ce.doupdate )
				{
					collectionUpdates.Add( new ScheduledCollectionUpdate(coll, ce.loadedPersister, ce.loadedKey, ce.SnapshotIsEmpty, this) );
					updateCount++;
				}

				scheduledCount++;
			}

			log.Debug("Processed " + scheduledCount + " for recreate (" + recreateCount + "), remove (" + removeCount + "), and update (" + updateCount + ")");
		}

		private void PostFlush() {

			log.Debug("post flush");

			foreach(DictionaryEntry de in IdentityMap.ConcurrentEntries(collections)) // collections.EntryList) 
			{
				((CollectionEntry) de.Value).PostFlush( (PersistentCollection) de.Key );
			}

			foreach(DictionaryEntry de in IdentityMap.ConcurrentEntries(entries)) //entries.EntryList) 
			{
				EntityEntry entry = (EntityEntry) de.Value;
				object obj = de.Key;

				entry.PostFlush(obj);
			}

			interceptor.PostFlush( entitiesByKey.Values );
		}

		private void PreFlushCollections() {

			// initialize dirty flags for arrays + collections with composte elements
			// and reset reached, doupdate, etc

			foreach(DictionaryEntry de in IdentityMap.ConcurrentEntries(collections)) // collections.EntryList) 
			{
				CollectionEntry ce = (CollectionEntry)de.Value;
				PersistentCollection pc = (PersistentCollection)de.Key;

				ce.PreFlush(pc);
			}
		}

		// Wrap all collections in an array of fields with PersistentCollections
		private bool Wrap(object[] fields, IType[] types) {

			bool substitute=false;
			for (int i=0; i<fields.Length; i++) {
				object result = Wrap(fields[i], types[i]);
				if ( result!=fields[i] ) {
					fields[i] = result;
					substitute = true;
				}
			}
			return substitute;
		}

		/// <summary>
		/// If the given object is a collection that can be wrapped by
		/// some subclass of PersistentCollection, wrap it up and
		/// return the wrapper
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		private object Wrap(object obj, IType type) {

			if (obj==null) return null;

			if ( type.IsComponentType ) 
			{
				IAbstractComponentType componentType = (IAbstractComponentType) type;
				object[] values = componentType.GetPropertyValues(obj, this);
				if ( Wrap( values, componentType.Subtypes ) ) componentType.SetPropertyValues(obj, values);
			}

			if ( type.IsPersistentCollectionType ) 
			{

				if ( obj is PersistentCollection ) 
				{
					//TODO: this looks out of synch with H2.0.3
					PersistentCollection pc = (PersistentCollection) obj;
					if ( pc.SetSession(this) ) 
					{
						ICollectionSnapshot snapshot = pc.CollectionSnapshot;
						if(snapshot.IsInitialized) 
						{
							AddNewCollection(pc);  //AddCollectionEntry(pc);
						}
						else 
						{
							object id = snapshot.Key;
							if(id==null) throw new HibernateException("reference created to previously dereferenced uninitialized collection");
							AddUninitializedCollection(pc, GetCollectionPersister(snapshot.Role), id);
							pc.ForceLoad();
							// ugly & inefficient little hack to force collection to be recreated
							// after "disconnected" collection replaces the "connected" one
							// H2.0.3 comments
							//GetCollectionEntry(pc).loadedKey = null;
							//GetCollectionEntry(pc).loadedPersister = null;
							AddNewCollection(pc);
							//AddCollectionEntry(pc);
						}
						//AddCollectionEntry(pc); // (if it wasn't initialized, the user stuffed up, not hibernate
					}
				
				} 
				else if ( obj.GetType().IsArray ) {

					// TODO: we could really re-use the existing arrayholder
					// for this new array (if it exists)
					ArrayHolder ah = GetArrayHolder(obj);
					if (ah==null) 
					{
						ah = new ArrayHolder(this, obj);
						AddNewCollection(ah); //AddCollectionEntry(ah);
						AddArrayHolder(ah);
					}
				} 
				else 
				{
					PersistentCollection pc = ((PersistentCollectionType) type).Wrap(this, obj);
					if ( log.IsDebugEnabled ) log.Debug( "Wrapped collection in role: " + ((PersistentCollectionType) type).Role);
					AddNewCollection(pc); // AddCollectionEntry(pc);
					obj = pc;
				}
			}

			return obj;
		}

		/// <summary>
		/// Initialize the role of the collection.
		/// The CollectionEntry.reached stuff is just to detect any silly users who set up
		/// circular or shared references between/to collections.
		/// </summary>
		/// <param name="coll"></param>
		/// <param name="type"></param>
		/// <param name="owner"></param>
		private void UpdateReachableCollection(PersistentCollection coll, IType type, object owner) {

			CollectionEntry ce = GetCollectionEntry(coll);

			if (ce.reached) {
				// we've been here before
				throw new HibernateException("found shared references to a collection");
			}
			ce.reached = true;

			CollectionPersister persister = GetCollectionPersister( ((PersistentCollectionType)type).Role );
			ce.currentPersister = persister;
			ce.currentKey = GetEntityIdentifier(owner);

			if ( log.IsDebugEnabled ) {
				log.Debug (
					"Collection found: " + MessageHelper.InfoString(persister, ce.currentKey) +
					", was: " +MessageHelper.InfoString(ce.loadedPersister, ce.loadedKey)
					);
			}

			PrepareCollectionForUpdate(coll, ce);
		}

		/// <summary>
		/// Given a reachable object, decide if it is a collection or a component holding collections.
		/// If so, recursively update contained collections
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="type"></param>
		/// <param name="owner"></param>
		private void UpdateReachable(object obj, IType type, object owner) {

			// this method assmues wrap was already called on obj!

			if ( obj!=null ) {

				if ( type.IsPersistentCollectionType ) {

					if ( obj.GetType().IsArray ) {
						UpdateReachableCollection( GetArrayHolder(obj), type, owner );
					} else {
						UpdateReachableCollection( (PersistentCollection) obj, type, owner );
					}
				}

				else if ( type.IsComponentType ) {

					IAbstractComponentType componentType = (IAbstractComponentType) type;
					object[] values = componentType.GetPropertyValues(obj, this);
					IType[] types = componentType.Subtypes;
					if ( Wrap(values, types) ) componentType.SetPropertyValues(obj, values);
					UpdateReachables(values, types, owner);
				}
			}
		}

		/// <summary>
		/// record the fact that this collection was dereferenced
		/// </summary>
		/// <param name="coll"></param>
		private void UpdateUnreachableCollection(PersistentCollection coll) {
			CollectionEntry entry = GetCollectionEntry(coll);
			
			if ( log.IsDebugEnabled && entry.loadedPersister!=null ) 
				log.Debug("collection dereferenced: " + MessageHelper.InfoString(entry.loadedPersister, entry.loadedKey));

			// TODO: H2.0.3 has code here for OrphanDeletes - looks like we are missing a method in CollectionPersister.hasOrphanDelete()
			//if(entry.loadedPersister!=null && entry.loadedPersister.h
			entry.currentPersister=null;
			entry.currentKey=null;

			PrepareCollectionForUpdate(coll, entry);
		}

		/// <summary>
		/// 1. record the collection role that this collection is referenced by
		/// 2. decide if the collection needs deleting/creating/updating (but
		///    don't actually schedule the action yet)
		/// </summary>
		/// <param name="coll"></param>
		/// <param name="entry"></param>
		private void PrepareCollectionForUpdate(PersistentCollection coll, CollectionEntry entry) {

			// TODO: figure out if this message is accurate.  why does it have a bug???
			if ( entry.processed ) throw new AssertionFailure("hibernate has a bug processing collections");

			entry.processed = true;

			// it is or was referenced _somewhere_
			if ( entry.loadedPersister!=null || entry.currentPersister!=null ) { 

				if (
					entry.loadedPersister!=entry.currentPersister || //if either its role changed,
					!entry.currentPersister.KeyType.Equals(entry.loadedKey, entry.currentKey) // or its key changed
					) {

					//TODO: h2.0.3 has code in here for OrphanDeletes


					if (entry.currentPersister!=null) entry.dorecreate = true; //we will need to create new entry

					if (entry.loadedPersister!=null) {
						entry.doremove = true; // we will need to remove the old entres
						if (entry.dorecreate ) {
							log.Debug("forcing collection initialization");
							coll.ForceLoad();
						}
					}
				} 
				else if (entry.dirty ) { // else if it's elements changed
					entry.doupdate = true;
				}
			}
		}

		/// <summary>
		/// ONLY near the end of the flush process, determine if the collection is dirty
		/// by checking its entry
		/// </summary>
		/// <param name="coll"></param>
		/// <returns></returns>
		private bool CollectionIsDirty(PersistentCollection coll) { 
			CollectionEntry entry = GetCollectionEntry(coll); 
			return entry.initialized && entry.dirty; //( entry.dirty || coll.hasQueuedAdds() ); 
		} 

		/// <summary>
		/// Given an array of fields, search recursively for collections and update them
		/// </summary>
		/// <param name="fields"></param>
		/// <param name="types"></param>
		/// <param name="owner"></param>
		private void UpdateReachables(object[] fields, IType[] types, object owner) {

			// this method assumes wrap was already called on fields

			for (int i=0; i<types.Length; i++ ) {
				UpdateReachable(fields[i], types[i], owner);
			}
		}

		//Given an array of fields, search recrusfively for dirty collections. return true if we find one
		private bool SearchForDirtyCollections(object[] fields, IType[] types) {
			for (int i=0; i<types.Length; i++ ) {
				if ( SearchForDirtyCollections( fields[i], types[i] ) ) return true;
			}
			return false;
		}

		/// <summary>
		/// Do we have a dirty collection here?
		/// 1. if it is a new application-instantiated collection, return true (does not occur anymore!)
		/// 2. if it is a component, recurse
		/// 3. if it is a wrappered collection, ask the collection entry
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		private bool SearchForDirtyCollections(object obj, IType type) {

			if ( obj!=null ) {
				
				if ( type.IsPersistentCollectionType ) {

					if ( obj.GetType().IsArray ) {
						PersistentCollection ah = GetArrayHolder(obj);
						// if no array holder we found an unwrappered array (this can't occur, 
						//if no array holder we found an unwrappered array                                         // because we now always call wrap() before getting to here) 
						//return (ah==null) ? true : SearchForDirtyCollections(ah, type);
						return CollectionIsDirty(ah);
					} else {
						// if not wrappered yet, its dirty (this can't occur, because 
						// we now always call wrap() before getting to here) 
						// return ( ! (obj is PersistentCollection) ) ?
						//	true : SearchForDirtyCollections( (PersistentCollection) obj, type );
						return CollectionIsDirty( (PersistentCollection) obj );
					}
				}

				else if ( type.IsComponentType ) {

					IAbstractComponentType componentType = (IAbstractComponentType) type;
					object[] values = componentType.GetPropertyValues(obj, this);
					IType[] types = componentType.Subtypes;
					for (int i=0; i<values.Length; i++) {
						if ( SearchForDirtyCollections( values[i], types[i] ) ) return true;
					}
				}
			}
			return false;
		}

		private IDictionary loadingCollections = new Hashtable();
		private string loadingRole;

		private sealed class LoadingCollectionEntry {
			public PersistentCollection collection;
			public bool initialize;
			public object id;
			public object owner;

			internal LoadingCollectionEntry(PersistentCollection collection, object id) {
				this.collection = collection;
				this.id = id;
			}

			internal LoadingCollectionEntry(PersistentCollection collectin, object id, object owner) {
				this.collection = collection;
				this.id = id;
				this.owner = owner;
			}
		}


		// TODO: replace with owner version of this method...
		public PersistentCollection GetLoadingCollection(CollectionPersister persister, object id) {
			LoadingCollectionEntry lce = (LoadingCollectionEntry)loadingCollections[id];
			if(lce==null) {
				PersistentCollection pc = persister.CollectionType.Instantiate(this, persister);
				pc.BeforeInitialize(persister);
				pc.BeginRead();
				if(loadingRole!=null && !loadingRole.Equals(persister.Role)) throw new AssertionFailure("recursive collection load");

				loadingCollections.Add(id, new  LoadingCollectionEntry(pc, id));
				loadingRole = persister.Role;
				return pc;
			}
			else {
				return lce.collection;
			}
		}

		//NEW overloaded version that should replace the 2 object without owner
		public PersistentCollection GetLoadingCollection(CollectionPersister persister, object id, object owner) {
			LoadingCollectionEntry lce = (LoadingCollectionEntry)loadingCollections[id];
			if(lce==null) {
				PersistentCollection pc = persister.CollectionType.Instantiate(this, persister);
				pc.BeforeInitialize(persister);
				pc.BeginRead();
				if(loadingRole!=null && !loadingRole.Equals(persister.Role)) throw new AssertionFailure("recursive collection load");

				loadingCollections.Add(id, new  LoadingCollectionEntry(pc, id, owner));
				loadingRole = persister.Role;
				return pc;
			}
			else {
				return lce.collection;
			}
		}

		public void EndLoadingCollections() {
			if(loadingRole!=null) {
				CollectionPersister persister = GetCollectionPersister(loadingRole);
				foreach (LoadingCollectionEntry lce in loadingCollections.Values) {
					//lce.collection.EndRead();
					lce.collection.EndRead(persister, lce.owner);
					AddInitializedCollection(lce.collection, persister, lce.id);
					persister.Cache(lce.id, lce.collection, this);
				}

				loadingCollections.Clear();
				loadingRole = null;
			}

		}

		public PersistentCollection GetLoadingCollection(string role, object id) {
			if(role.Equals(loadingRole)) {
				LoadingCollectionEntry lce = (LoadingCollectionEntry) loadingCollections[id];
				if(lce==null) {
					return null;
				}
				else {
					lce.initialize = true;
					return lce.collection;
				}
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// add a collection we just loaded up (still needs initializing)
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="persister"></param>
		/// <param name="id"></param>
		public void AddUninitializedCollection(PersistentCollection collection, CollectionPersister persister, object id) {
			CollectionEntry ce = new CollectionEntry(persister, id, false);
			collections[collection] = ce;
			collection.CollectionSnapshot = ce;
		}

		/// <summary>
		/// add a collection we just pulled out of the cache (does not need initializing)
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="persister"></param>
		/// <param name="id"></param>
		public void AddInitializedCollection(PersistentCollection collection, CollectionPersister persister, object id) {
			CollectionEntry ce = new CollectionEntry(persister, id, true);
			ce.PostInitialize(collection);
			collections[collection] = ce;
			collection.CollectionSnapshot = ce;
		}

		/// <summary>
		/// add an (initialized) collection that was created by another session and passed into update()
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="cs"></param>
		private void AddInitializedCollection(PersistentCollection collection, ICollectionSnapshot cs) {
			CollectionEntry ce = new CollectionEntry(cs, factory);
			collections[collection] = ce;
			collection.CollectionSnapshot = ce;
		}

		public ArrayHolder GetArrayHolder(object array) {
			return (ArrayHolder) arrayHolders[array];
		}

		//must call after loading array (so array exists for key of map);
		public void AddArrayHolder(ArrayHolder holder) {
			arrayHolders[holder.Array] = holder;
		}

		private CollectionPersister GetCollectionPersister(string role) {
			return factory.GetCollectionPersister(role);
		}

		public void Dirty(PersistentCollection coll) {
			GetCollectionEntry(coll).dirty = true;
		}

		public object GetSnapshot(PersistentCollection coll) {
			return GetCollectionEntry(coll).snapshot;
		}

		public object GetLoadedCollectionKey(PersistentCollection coll) {
			return GetCollectionEntry(coll).loadedKey;
		}

		public bool IsInverseCollection(PersistentCollection collection) {
			CollectionEntry ce = GetCollectionEntry(collection);
			return ce!=null && ce.loadedPersister.IsInverse;
		}

		//TODO: h2.0.3 add a method GetOrphans()

		/// <summary>
		/// called by a collection that wants to initialize itself
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="writing"></param>
		public void Initialize(PersistentCollection collection, bool writing) {

			CollectionEntry ce = GetCollectionEntry(collection);

			if ( !ce.initialized ) {

				if (log.IsDebugEnabled ) log.Debug( "initializing collection " + MessageHelper.InfoString(ce.loadedPersister, ce.loadedKey) );

				CollectionPersister persister = ce.loadedPersister;
				object id = ce.loadedKey;

				object owner = GetEntity( new Key( id, GetPersister( persister.OwnerClass ) ) );

				collection.BeforeInitialize(persister);
				try {
					persister.Initializer.Initialize(id, collection, owner, this);
				}
				catch(ADOException sqle) {
					throw new ADOException("SQLException initializing collection", sqle);
				}

				ce.initialized = true;
				ce.PostInitialize(collection);

				if (!writing) persister.Cache(id, collection, this);
			}
		}

		public IDbConnection Connection {
			get {
				if (connection==null) {
					if (connect) {
						connection = factory.OpenConnection();
						connect = false;
					} else {
						throw new HibernateException("session is currently disconnected");
					}
				}
				return connection;
			}
		}

		public bool IsConnected {
			get { return connection!=null || connect; }
		}

		public IDbConnection Disconnect() {

			log.Debug("disconnecting session");

			try { 

				if (connect) {
					connect = false;
					return null;
				} else {

					if (connection==null) throw new HibernateException("session already disconnected");

					if (batcher!=null) batcher.CloseStatements();
					IDbConnection c = connection;
					connection=null;
					if (autoClose) {
						factory.CloseConnection(c);
						return null;
					} else {
						return c;
					}
				}
			} finally {
				if ( callAfterTransactionCompletionFromDisconnect )
					AfterTransactionCompletion();
			}
		}

		public void Reconnect() {
			if ( IsConnected ) throw new HibernateException("session already connected");

			log.Debug("reconnecting session");

			connect = true;
		}

		public void Reconnect(IDbConnection conn) {
			if ( IsConnected ) throw new HibernateException("session already connected");
			this.connection = conn;
		}

		void IDisposable.Dispose() {
		//~SessionImpl() {

			log.Debug("running Session.Finalize()");

			if (connection!=null) {

				AfterTransactionCompletion();

				if ( connection.State == ConnectionState.Closed ) {
					log.Warn("finalizing unclosed session with closed connection");
				} else {
					log.Warn("unclosed connection");
					if (autoClose) connection.Close();
				}
			}
		}

		public static void Handle(Exception e) {
			if (e is HibernateException) {
				throw (HibernateException) e;
			} else {
				log.Error("unexpected exception", e);
				throw new HibernateException("unexpected exception", e);
			}
		}

		public ICollection Filter(object collection, string filter) {
			return Filter( collection, filter, new object[1], new IType[1], null, null, null);
		}

		public ICollection Filter(object collection, string filter, object value, IType type) {
			return Filter( collection, filter, new object[] { null, value }, new IType[] { null, type }, null, null, null );
		}

		public ICollection Filter(object collection, string filter, object[] values, IType[] types) {
			object[] vals = new object[ values.Length + 1 ];
			IType[] typs = new IType[ values.Length + 1 ];
			Array.Copy(values, 0, vals, 1, values.Length);
			Array.Copy(types, 0, typs, 1, types.Length);
			return Filter(collection, filter, vals, typs, null, null, null);
		}

		/// <summary>
		/// 1. determine the collection role of the given collection (this may require a flush, if the collection is recorded as unreferenced)
		/// 2. obtain a compiled filter query
		/// 3. autoflush if necessary
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="filter"></param>
		/// <param name="values"></param>
		/// <param name="types"></param>
		/// <param name="selection"></param>
		/// <param name="namedParams"></param>
		/// <param name="scalar"></param>
		/// <returns></returns>
		private FilterTranslator GetFilterTranslator(object collection, string filter, object[] values, IType[] types, RowSelection selection, IDictionary namedParams, bool scalar) {

			if ( log.IsDebugEnabled ) {
				log.Debug( "filter: " + filter );
				if ( values.Length!=0 ) log.Debug( "parameters: " + StringHelper.ToString(values) );
			}

			if ( ! (collection is PersistentCollection) ) {
				collection = GetArrayHolder(collection);
				if (collection==null) throw new TransientObjectException("collection was not yet persistent");
			}
			PersistentCollection coll = (PersistentCollection) collection;
			CollectionEntry e = GetCollectionEntry(coll);
			if (e==null) throw new TransientObjectException("collection was not persistent in this session");

			FilterTranslator q;
			CollectionPersister roleBeforeFlush = e.loadedPersister;
			if ( roleBeforeFlush==null ) { //ie. it was previously unreferenced
				Flush();
				if ( e.loadedPersister==null ) throw new QueryException("the collection was unreferenced");
				q = factory.GetFilter( filter, e.loadedPersister.Role, scalar);
			} else {
				q = factory.GetFilter( filter, roleBeforeFlush.Role, scalar );
				if ( AutoFlushIfRequired( q.QuerySpaces ) && roleBeforeFlush!=e.loadedPersister ) {
					if ( e.loadedPersister==null ) throw new QueryException("the collection was dereferenced");

					q = factory.GetFilter( filter, e.loadedPersister.Role, scalar);
				}
			}
			
			values[0] = e.loadedKey;
			types[0] = e.loadedPersister.KeyType;

			return q;

		}

		public IList Filter(object collection, string filter, object[] values, IType[] types, RowSelection selection, 
			IDictionary namedParams, IDictionary lockModes) {

			string[] concreteFilters = QueryTranslator.ConcreteQueries(filter, factory);
			FilterTranslator[] filters = new FilterTranslator[ concreteFilters.Length ];

			for ( int i=0; i<concreteFilters.Length; i++ ) {
				filters[i] = GetFilterTranslator(collection, concreteFilters[i], values, types, selection, namedParams, false);
			}

			dontFlushFromFind++; // stops flush being called multiple times if this method is recursively called

			IList results = new ArrayList();
			try {
				for (int i=0; i<concreteFilters.Length; i++ ) {
					IList currentResults;
					try {
						currentResults = filters[i].FindList(this, values, types, true, selection, namedParams, lockModes);
					} catch (Exception e) {
						throw new ADOException("could not execute query", e);
					}
					foreach(object res in results) {
						currentResults.Add(res);
					}
					results = currentResults;
				}
			} finally {
				dontFlushFromFind--;
			}
			return results;
		}

		public IEnumerable EnumerableFilter(object collection, string filter, object[] values, IType[] types, 
			RowSelection selection, IDictionary namedParams, IDictionary lockModes) {

			string[] concreteFilters = QueryTranslator.ConcreteQueries(filter, factory);
			FilterTranslator[] filters = new FilterTranslator[ concreteFilters.Length ];

			for (int i=0; i<concreteFilters.Length; i++ ) {
				filters[i] = GetFilterTranslator(collection, concreteFilters[i], values, types, selection, namedParams, true);
			}

			if (filters.Length==0) return new ArrayList(0);

			IEnumerable result = null;
			IEnumerable[] results = null;
			bool many = filters.Length>1;
			if (many) results = new IEnumerable[filters.Length];

			// execute the queries and return all results as a single enumerable
			for (int i=0; i<filters.Length; i++ ) {

				try { 
					result = filters[i].GetEnumerable(values, types, selection, namedParams, lockModes, this);
				} catch (Exception e) {
					throw new ADOException("could not execute query", e);
				}
				if (many) {
					results[i] = result;
				}
			}

			return many ? new JoinedEnumerable(results) : result;
		}

		public ICriteria CreateCriteria(System.Type persistentClass) {
			return new CriteriaImpl(persistentClass, this);
		}

		public IList Find(CriteriaImpl criteria) {

			System.Type persistentClass = criteria.PersistentClass;

			if ( log.IsDebugEnabled ) {
				log.Debug( "search: " + persistentClass.Name );
				log.Debug( "criteria: " + criteria );
			}

			ILoadable persister = (ILoadable) GetPersister(persistentClass);
			CriteriaLoader loader = new CriteriaLoader(persister, factory, criteria);
			object[] spaces = persister.PropertySpaces;
			ArrayList sett = new ArrayList();
			for (int i=0; i<spaces.Length; i++) sett.Add( spaces[i] );
			AutoFlushIfRequired(sett);

			dontFlushFromFind++;
			try {
				return loader.List(this);
			} catch (Exception e) {
				throw new ADOException("problem in find", e);
			} finally {
				dontFlushFromFind--;
			}
		}

		public bool Contains(object obj) { 
			if (obj is HibernateProxy) { 
				return HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) obj ).Session==this; 
			} 
			else { 
				return entries.Contains(obj); 
			}
		}
    
       /// <summary>
		/// remove any hard references to the entity that are held by the infrastructure
		/// (references held by application or other persistant instances are okay)
		/// </summary>
		/// <param name="obj"></param>
        public void Evict(object obj) { 
                if (obj is HibernateProxy) { 
                        LazyInitializer li = HibernateProxyHelper.GetLazyInitializer( (HibernateProxy) obj ); 
                        object id = li.Identifier; 
                        IClassPersister persister = GetPersister( li.PersistentClass ); 
                        Key key = new Key(id, persister); 
                        proxiesByKey.Remove(key); 
                        if ( !li.IsUninitialized ) { 
                                object entity = RemoveEntity(key); 
                                if (entity!=null) { 
                                        RemoveEntry(entity); 
                                        DoEvict(persister, entity); 
                                } 
                        } 
                } 
                else { 
                        EntityEntry e = (EntityEntry) RemoveEntry(obj); 
                        if (e!=null) { 
                                RemoveEntity( new Key(e.id, e.persister) ); 
                                DoEvict(e.persister, obj); 
                        } 
                } 
        } 

        private void DoEvict(IClassPersister persister, object obj) { 

                if ( log.IsDebugEnabled ) log.Debug( "evicting " + MessageHelper.InfoString(persister) ); 

                //remove all collections for the entity 
                EvictCollections( persister.GetPropertyValues(obj), persister.PropertyTypes ); 
                Cascades.Cascade(this, persister, obj, Cascades.CascadingAction.ActionEvict, CascadePoint.CascadeOnEvict); 
        } 
    
          
		/// <summary>
		/// Evict any collections referenced by the object from the session cache. This will NOT 
		/// pick up any collections that were dereferenced, so they will be deleted (suboptimal 
		/// but not exactly incorrect). 
		/// </summary>
		/// <param name="values"></param>
		/// <param name="types"></param>
		private void EvictCollections(Object[] values, IType[] types) { 
			for ( int i=0; i<types.Length; i++ ) { 
				if ( types[i].IsPersistentCollectionType ) { 
					object pc=null; 
					if ( ( (PersistentCollectionType) types[i] ).IsArrayType ) { 
						pc = arrayHolders[ values[i] ];
						arrayHolders.Remove( values[i] ); 
					} 
					else if ( values[i] is PersistentCollection ) { 
						pc = values[i]; 
					} 

					if (pc!=null) { 
						if ( ( (PersistentCollection) pc ).UnsetSession(this) ) collections.Remove(pc); 
					} 
				} 
				else if ( types[i].IsComponentType ) { 
					IAbstractComponentType actype = (IAbstractComponentType) types[i]; 
					EvictCollections( 
						actype.GetPropertyValues( values[i], this ), 
						actype.Subtypes 
					); 
				} 
			} 
        } 
	
		public object GetVersion(object entity) {
			return GetEntry(entity).lastVersion;
		}
		
	}
}
