using System;
using System.Data;
using NHibernate.Engine;
using NHibernate.SqlTypes;

namespace NHibernate.Type
{
	/// <summary>
	/// A one-to-one association to an entity
	/// </summary>
	public class OneToOneType : EntityType, IAssociationType
	{
		private static readonly SqlType[ ] NoSqlTypes = new SqlType[0];

		private readonly ForeignKeyDirection foreignKeyDirection;

		public override int GetColumnSpan( IMapping session )
		{
			return 0;
		}

		public override SqlType[ ] SqlTypes( IMapping session )
		{
			return NoSqlTypes;
		}

		public OneToOneType( System.Type persistentClass, ForeignKeyDirection foreignKeyDirection, string uniqueKeyPropertyName ) : base( persistentClass, uniqueKeyPropertyName )
		{
			this.foreignKeyDirection = foreignKeyDirection;
		}

		public override void NullSafeSet( IDbCommand cmd, object value, int index, ISessionImplementor session )
		{
			//nothing to do
		}

		public override bool IsOneToOne
		{
			get { return true; }
		}

		public override bool IsDirty( object old, object current, ISessionImplementor session )
		{
			return false;
		}

		public override bool IsModified( object old, object current, ISessionImplementor session )
		{
			return false;
		}

		public override ForeignKeyDirection ForeignKeyDirection
		{
			get { return foreignKeyDirection; }
		}

		public override object Hydrate( IDataReader rs, string[ ] names, ISessionImplementor session, object owner )
		{
			return session.GetEntityIdentifier( owner );
		}

		protected override object ResolveIdentifier( object value, ISessionImplementor session )
		{
			System.Type clazz = AssociatedClass;

			return IsNullable ?
				session.InternalLoadOneToOne( clazz, value ) :
				session.InternalLoad( clazz, value );
		}

		public virtual bool IsNullable
		{
			get { return foreignKeyDirection == ForeignKeyDirection.ForeignKeyToParent; }
		}

		public override bool UseLHSPrimaryKey
		{
			get { return true; }
		}

		public override object Disassemble( object value, ISessionImplementor session )
		{
			return null;
		}

		public override object Assemble( object cached, ISessionImplementor session, object owner )
		{
			return ResolveIdentifier( session.GetEntityIdentifier( owner ), session, owner );
		}
	}
}