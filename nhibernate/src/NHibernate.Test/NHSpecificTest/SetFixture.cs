using System;
using System.Collections;
using System.Data;
using Iesi.Collections;
using NHibernate.Cache;
using NHibernate.Collection;
using NHibernate.Engine;
using NHibernate.Id;
using NHibernate.Metadata;
using NHibernate.Persister.Collection;
using NHibernate.Persister.Entity;
using NHibernate.Type;
using NUnit.Framework;
using System.Collections.Generic;

namespace NHibernate.Test.NHSpecificTest
{
	internal class CollectionSnapshotStub : ICollectionSnapshot
	{
		#region ICollectionSnapshot Members

		public bool Dirty
		{
			get
			{
				// TODO:  Add CollectionSnapshotStub.Dirty getter implementation
				return false;
			}
		}

		public object Key
		{
			get
			{
				// TODO:  Add CollectionSnapshotStub.Key getter implementation
				return null;
			}
		}

		public string Role
		{
			get
			{
				// TODO:  Add CollectionSnapshotStub.Role getter implementation
				return null;
			}
		}

		public void SetDirty()
		{
			// TODO:  Add CollectionSnapshotStub.SetDirty implementation
		}

		public bool WasDereferenced
		{
			get
			{
				// TODO:  Add CollectionSnapshotStub.WasDereferenced getter implementation
				return false;
			}
		}

		public ICollection Snapshot
		{
			get
			{
				// TODO:  Add CollectionSnapshotStub.Snapshot getter implementation
				return null;
			}
		}

		#endregion
	}


	internal class CollectionPersisterStub : ICollectionPersister
	{
		#region ICollectionPersister Members

		public System.Type OwnerClass
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.OwnerClass getter implementation
				return null;
			}
		}

		public IEntityPersister OwnerEntityPersister
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.OwnerEntityPersister getter implementation
				return null;
			}
		}

		public bool HasCache
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.HasCache getter implementation
				return false;
			}
		}

		public IIdentifierGenerator IdentifierGenerator
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.IdentifierGenerator getter implementation
				return null;
			}
		}

		public bool IsInverse
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.IsInverse getter implementation
				return false;
			}
		}

		public IType IndexType
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.IndexType getter implementation
				return null;
			}
		}

		public bool HasIndex
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.HasIndex getter implementation
				return false;
			}
		}

		public bool IsOneToMany
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.IsOneToMany getter implementation
				return false;
			}
		}

		public string GetManyToManyFilterFragment(string alias, IDictionary<string, IFilter> enabledFilters)
		{
			throw new NotImplementedException();
		}

		public System.Type ElementClass
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.ElementClass getter implementation
				return null;
			}
		}

		public IType KeyType
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.KeyType getter implementation
				return null;
			}
		}

		public void InsertRows(IPersistentCollection collection, object key, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.InsertRows implementation
		}

		public bool IsLazy
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.IsLazy getter implementation
				return false;
			}
		}

		private CollectionType collectionType = new SetType(null, null);

		public CollectionType CollectionType
		{
			get { return collectionType; }
		}

		public void UpdateRows(IPersistentCollection collection, object key, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.UpdateRows implementation
		}

		public void DeleteRows(IPersistentCollection collection, object key, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.DeleteRows implementation
		}

		public void WriteElement(IDbCommand st, object elt, bool writeOrder, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.WriteElement implementation
		}

		public void Recreate(IPersistentCollection collection, object key, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.Recreate implementation
		}

		public bool HasOrdering
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.HasOrdering getter implementation
				return false;
			}
		}

		private IType elementType;

		public IType ElementType
		{
			get { return elementType; }
			set { elementType = value; }
		}

		public void Remove(object id, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.Remove implementation
		}

		public object ReadElement(IDataReader rs, object owner, string[] aliases, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.ReadElement implementation
			return null;
		}

		public string Role
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.Role getter implementation
				return null;
			}
		}

		public ICollectionMetadata CollectionMetadata
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.CollectionMetadata getter implementation
				return null;
			}
		}

		public object ReadIndex(IDataReader rs, string[] aliases, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.ReadIndex implementation
			return null;
		}

		public void Initialize(object key, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.Initialize implementation
		}

		public object ReadKey(IDataReader rs, string[] aliases, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.ReadKey implementation
			return null;
		}

		public IType IdentifierType
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.IdentifierType getter implementation
				return null;
			}
		}

		public bool IsArray
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.IsArray getter implementation
				return false;
			}
		}

		public ICacheConcurrencyStrategy Cache
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.Cache getter implementation
				return null;
			}
		}

		public bool IsPrimitiveArray
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.IsPrimitiveArray getter implementation
				return false;
			}
		}

		public object ReadIdentifier(IDataReader rs, string alias, ISessionImplementor session)
		{
			// TODO:  Add CollectionPersisterStub.ReadIdentifier implementation
			return null;
		}

		public object CollectionSpace
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.CollectionSpace getter implementation
				return null;
			}
		}

		public bool HasOrphanDelete
		{
			get
			{
				// TODO:  Add CollectionPersisterStub.HasOrphanDelete getter implementation
				return false;
			}
		}

		public void PostInstantiate()
		{
		}

		public string[] GetKeyColumnAliases(string suffix)
		{
			return null;
		}

		public string[] GetIndexColumnAliases(string suffix)
		{
			return null;
		}

		public string[] GetElementColumnAliases(string suffix)
		{
			return null;
		}

		public string GetIdentifierColumnAlias(string suffix)
		{
			return null;
		}

		public ISessionFactoryImplementor Factory
		{
			get { return null; }
		}


		public bool IsAffectedByEnabledFilters(ISessionImplementor session)
		{
			return false;
		}

		public bool HasManyToManyOrdering
		{
			get { return false; }
		}

		public bool IsVersioned
		{
			get { return false; }
		}

		#endregion
	}

	[TestFixture]
	public class SetFixture
	{
		[Test]
		public void DisassembleAndAssemble()
		{
			PersistentSet set = new PersistentSet(null, new ListSet());

			set.CollectionSnapshot = new CollectionSnapshotStub();

			set.Add(10);
			set.Add(20);

			CollectionPersisterStub collectionPersister = new CollectionPersisterStub();
			collectionPersister.ElementType = NHibernateUtil.Int32;

			object disassembled = set.Disassemble(collectionPersister);

			PersistentSet assembledSet = new PersistentSet(null);
			assembledSet.InitializeFromCache(collectionPersister, disassembled, null);

			Assert.AreEqual(2, assembledSet.Count);
			Assert.IsTrue(assembledSet.Contains(10));
			Assert.IsTrue(assembledSet.Contains(20));
		}
	}
}