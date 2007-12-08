using System;
using System.Collections;
using System.Reflection;

using NHibernate.Cache;
using NHibernate.Engine;
using NHibernate.Event;
using NHibernate.Id;
using NHibernate.Mapping;
using NHibernate.Metadata;
using NHibernate.Persister.Entity;
using NHibernate.Type;

namespace NHibernate.DomainModel
{
	/// <summary>
	/// Summary description for CustomPersister.
	/// </summary>
	public class CustomPersister : IEntityPersister
	{
		private static readonly Hashtable Instances = new Hashtable();
		private static readonly IIdentifierGenerator Generator = new CounterGenerator();

		private static readonly IType[] Types = new IType[] {NHibernateUtil.String};
		private static readonly string[] Names = new string[] {"name"};
		private static readonly bool[] Mutability = new bool[] {true};
		private static readonly bool[] Nullability = new bool[] {true};

		private ISessionFactoryImplementor factory;

		public CustomPersister(PersistentClass model, ICacheConcurrencyStrategy cache, ISessionFactory factory,
		                       IMapping mapping)
		{
			this.factory = (ISessionFactoryImplementor) factory;
		}

		#region IEntityPersister Members

		public object IdentifierSpace
		{
			get { return "CUSTOMS"; }
		}

		public IClassMetadata ClassMetadata
		{
			get { return null; }
		}

		public bool HasCache
		{
			get { return false; }
		}

		public int[] FindDirty(object[] x, object[] y, object owner, ISessionImplementor session)
		{
			if (x[0].Equals(y[0]) == false)
			{
				return new int[] {0};
			}
			else
			{
				return null;
			}
		}

		public int[] FindModified(object[] x, object[] y, object owner, ISessionImplementor session)
		{
			return FindDirty(x, y, owner, session);
		}

		public bool[] PropertyUpdateability
		{
			get { return Mutability; }
		}

		public bool[] PropertyCheckability
		{
			get { return Mutability; }
		}

		public bool[] PropertyNullability
		{
			get { return Nullability; }
		}

		public bool IsBatchLoadable
		{
			get { return false; }
		}

		public bool IsCacheInvalidationRequired
		{
			get { return false; }
		}

		public bool HasCascades
		{
			get { return false; }
		}

		public object Instantiate(object id)
		{
			Custom c = new Custom();
			c.Id = (long) id;
			return c;
		}

		public IIdentifierGenerator IdentifierGenerator
		{
			get { return Generator; }
		}

		public bool[] PropertyInsertability
		{
			get { return Mutability; }
		}

		public bool[] PropertyVersionability
		{
			get { return Mutability; }
		}

		public System.Type MappedClass
		{
			get { return typeof(Custom); }
		}

		public object Insert(object[] fields, object obj, ISessionImplementor session)
		{
			throw new NotSupportedException("CustomPersister.Insert withou Id is not supported.");
		}

		public void Insert(object id, object[] fields, object obj, ISessionImplementor session)
		{
			Instances[id] = ((Custom) obj).Clone();
		}

		public bool IsUnsaved(object id)
		{
			return (long) id == 0;
		}

		public bool HasIdentifierPropertyOrEmbeddedCompositeIdentifier
		{
			get { return true; }
		}

		public object GetVersion(object obj)
		{
			return null;
		}

		public Cascades.CascadeStyle[] PropertyCascadeStyles
		{
			get { return null; }
		}

		public object[] PropertySpaces
		{
			get { return new string[] {"CUSTOMS"}; }
		}

		public void SetPropertyValues(object obj, object[] values)
		{
			Custom c = (Custom) obj;
			c.Name = (string) values[0];
		}

		public IType[] PropertyTypes
		{
			get { return Types; }
		}

		public bool IsIdentifierAssignedByInsert
		{
			get { return false; }
		}

		public System.Type ConcreteProxyClass
		{
			get { return typeof(Custom); }
		}

		public object GetIdentifier(object obj)
		{
			return (long) ((Custom) obj).Id;
		}

		public object GetPropertyValue(object obj, int i)
		{
			return ((Custom) obj).Name;
		}

		public object GetPropertyValue(object obj, string name)
		{
			return ((Custom) obj).Name;
		}

		public bool IsVersioned
		{
			get { return false; }
		}

		public IComparer VersionComparator
		{
			get { return null; }
		}

		public bool IsUnsavedVersion(object[] values)
		{
			return false;
		}

		public bool HasProxy
		{
			get { return false; }
		}

		public void SetIdentifier(object obj, object id)
		{
			((Custom) obj).Id = (long) id;
		}

		public bool ImplementsLifecycle
		{
			get { return false; }
		}

		public object[] GetPropertyValues(object obj)
		{
			Custom c = (Custom) obj;
			return new object[] {c.Name};
		}

		public string ClassName
		{
			get { return typeof(Custom).FullName; }
		}

		public string RootEntityName
		{
			get { return "CUSTOMS"; }
		}

		public string EntityName
		{
			get { return typeof(Custom).FullName; }
		}

		public bool HasIdentifierProperty
		{
			get { return false; }
		}

		public int VersionProperty
		{
			get { return 0; }
		}

		public IType IdentifierType
		{
			get { return NHibernateUtil.Int64; }
		}

		public PropertyInfo ProxyIdentifierProperty
		{
			get { return null; }
		}

		public bool IsMutable
		{
			get { return true; }
		}

		public bool HasCollections
		{
			get { return false; }
		}

		public void Update(object id, object[] fields, int[] dirtyFields, bool hasDirtyCollection, object[] oldFields,
		                   object oldVersion, object obj, ISessionImplementor session)
		{
			Instances[id] = ((Custom) obj).Clone();
		}

		public void Delete(object id, object version, object obj, ISessionImplementor session)
		{
			Instances.Remove(id);
		}

		public string[] PropertyNames
		{
			get { return Names; }
		}

		public void SetPropertyValue(object obj, int i, object value)
		{
			((Custom) obj).Name = (string) value;
		}

		public void SetPropertyValue(object obj, string name, object value)
		{
			((Custom) obj).Name = (string) value;
		}

		public IType GetPropertyType(string propertyName)
		{
			// TODO: Implement this
			return null;
		}

		public object Load(object id, object optionalObject, LockMode lockMode, ISessionImplementor session)
		{
			// fails when optional object is supplied
			Custom clone = null;
			Custom obj = (Custom)Instances[id];
			if (obj != null)
			{
				clone = (Custom)obj.Clone();
				TwoPhaseLoad.AddUninitializedEntity(
						new EntityKey(id, this),
						clone,
						this,
						LockMode.None,
						false,
						session
					);
				TwoPhaseLoad.PostHydrate(
						this, id,
						new String[] { obj.Name },
						clone,
						LockMode.None,
						false,
						session
					);
				TwoPhaseLoad.InitializeEntity(
						clone,
						false,
						session,
						new PreLoadEvent((IEventSource)session),
						new PostLoadEvent((IEventSource)session)
					);
			}
			return clone;
		}

		public ICacheConcurrencyStrategy Cache
		{
			get { return null; }
		}

		public bool ImplementsValidatable
		{
			get { return false; }
		}

		public void PostInstantiate()
		{
		}

		public IVersionType VersionType
		{
			get { return null; }
		}

		public string IdentifierPropertyName
		{
			get { return "Id"; }
		}

		public System.Type[] ProxyInterfaces
		{
			get { return null; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <param name="version"></param>
		/// <param name="obj"></param>
		/// <param name="lockMode"></param>
		/// <param name="session"></param>
		public void Lock(object id, object version, object obj, LockMode lockMode, ISessionImplementor session)
		{
			throw new NotSupportedException("CustomPersister.Lock is not implemented");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <param name="session"></param>
		/// <returns></returns>
		public object[] GetDatabaseSnapshot(object id, ISessionImplementor session)
		{
			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <param name="session"></param>
		/// <returns></returns>
		public object GetCurrentVersion(object id, ISessionImplementor session)
		{
			return this;
		}

		public object ForceVersionIncrement(object id, object currentVersion, ISessionImplementor session)
		{
			return null;
		}

		public object CreateProxy(object id, ISessionImplementor session)
		{
			throw new NotSupportedException("CustomPersister.CreateProxy is not implemented");
		}

		public object[] QuerySpaces
		{
			get { return null; }
		}

		public ISessionFactoryImplementor Factory
		{
			get { return factory; }
		}

		public bool IsInstance(object entity)
		{
			return entity is Custom;
		}

		public bool IsVersionPropertyGenerated
		{
			get { return false; }
		}

		public bool HasInsertGeneratedProperties
		{
			get { return false; }
		}

		public bool HasUpdateGeneratedProperties
		{
			get { return false; }
		}

		public bool IsSelectBeforeUpdateRequired
		{
			get { return false; }
		}

		public void ProcessInsertGeneratedProperties(object id, object entity, object[] state, ISessionImplementor session)
		{
			throw new NotImplementedException();
		}

		public void ProcessUpdateGeneratedProperties(object id, object entity, object[] state, ISessionImplementor session)
		{
			throw new NotImplementedException();
		}

		public bool IsLazyPropertiesCacheable
		{
			get { return true; }
		}

		#endregion

	}
}