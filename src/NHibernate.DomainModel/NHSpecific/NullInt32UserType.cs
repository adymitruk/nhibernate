using System;
using System.Data;

using NHibernate.SqlTypes;
using NHibernate.Type;
using NHibernate.UserTypes;

namespace NHibernate.DomainModel.NHSpecific
{
	/// <summary>
	/// Converts a value of 0 to a DbNull
	/// </summary>
	public class NullInt32UserType : IUserType
	{
		private static NullableType _int32Type = NHibernateUtil.Int32;

		public NullInt32UserType()
		{
		}

		#region IUserType Members

		public new bool Equals(object x, object y)
		{
			if (x == y) return true;

			int lhs = (x == null) ? 0 : (int) x;
			int rhs = (y == null) ? 0 : (int) y;

			return _int32Type.Equals(lhs, rhs);
		}

		public int GetHashCode(object x)
		{
			return (x == null) ? 0 : x.GetHashCode();
		}

		public SqlType[] SqlTypes
		{
			get { return new SqlType[] {_int32Type.SqlType}; }
		}

		public object DeepCopy(object value)
		{
			return value;
		}

		public void NullSafeSet(IDbCommand cmd, object value, int index)
		{
			if (value.Equals(0))
			{
				((IDbDataParameter) cmd.Parameters[index]).Value = DBNull.Value;
			}
			else
			{
				_int32Type.Set(cmd, value, index);
			}
		}

		public System.Type ReturnedType
		{
			get { return typeof(Int32); }
		}

		public object NullSafeGet(IDataReader rs, string[] names, object owner)
		{
			return _int32Type.NullSafeGet(rs, names);
		}

		public bool IsMutable
		{
			get { return _int32Type.IsMutable; }
		}

		public object Replace(object original, object target, object owner)
		{
			return original;
		}

		public object Assemble(object cached, object owner)
		{
			return cached;
		}

		public object Disassemble(object value)
		{
			return value;
		}

		#endregion
	}
}