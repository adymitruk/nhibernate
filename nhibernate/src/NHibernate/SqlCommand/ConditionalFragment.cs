using System.Text;
using NHibernate.Util;

namespace NHibernate.SqlCommand
{
	/// <summary></summary>
	public class ConditionalFragment
	{
		private string tableAlias;
		private string[ ] lhs;
		private string[ ] rhs;
		private string op = "=";

		/// <summary>
		/// Sets the op
		/// </summary>
		/// <param name="op">The op to set</param>
		public ConditionalFragment SetOp( string op )
		{
			this.op = op;
			return this;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="tableAlias"></param>
		/// <returns></returns>
		public ConditionalFragment SetTableAlias( string tableAlias )
		{
			this.tableAlias = tableAlias;
			return this;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="lhs"></param>
		/// <param name="rhs"></param>
		/// <returns></returns>
		public ConditionalFragment SetCondition( string[ ] lhs, string[ ] rhs )
		{
			this.lhs = lhs;
			this.rhs = rhs;
			return this;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="lhs"></param>
		/// <param name="rhs"></param>
		/// <returns></returns>
		public ConditionalFragment SetCondition( string[ ] lhs, string rhs )
		{
			this.lhs = lhs;
			this.rhs = ArrayHelper.FillArray( rhs, lhs.Length );
			return this;
		}

		/// <summary></summary>
		public SqlString ToSqlStringFragment()
		{
			StringBuilder buf = new StringBuilder( lhs.Length*10 );
			for( int i = 0; i < lhs.Length; i++ )
			{
				buf.Append( tableAlias )
					.Append( StringHelper.Dot )
					.Append( lhs[ i ] )
					.Append( op )
					.Append( rhs[ i ] );
				if( i < lhs.Length - 1 )
				{
					buf.Append( " and " );
				}
			}
			return new SqlString( buf.ToString() );
		}
	}
}