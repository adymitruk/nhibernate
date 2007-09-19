//------------------------------------------------------------------------------
// <autogenerated>
//     This code was generated by a tool.
//     Runtime Version: v1.1.4322
//
//     Changes to this file may cause incorrect behavior and will be lost if 
//     the code is regenerated.
// </autogenerated>
//------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Text;
using System.Threading;

using NHibernate.Classic;
using NHibernate.DomainModel.NHSpecific;

namespace NHibernate.DomainModel
{
	/// <summary>
	/// POJO for Foo
	/// </summary>
	/// <remark>
	/// This class is autogenerated
	/// </remark>
	[Serializable]
	public class Foo : FooProxy, ILifecycle
	{
		#region Fields

		private string _key;
		private FooComponent _component;

		private long _long;
		private int _integer;
		private float _float;
		private int _x;
//		private double _double;
		private DateTime _date;
		private DateTime _timestamp;
		private bool _boolean;
		private bool _bool;
		private NullableInt32 _null;
		private short _short;
		private char _char;
		private float _zero;
		private int _int;
		private String _string;
		private byte _byte;
		private bool _yesno;
		private FooStatus _status;
		private byte[] _bytes;
		private CultureInfo _locale;
		// in h2.0.3 this was a float
		private int _formula;
		private string[] custom;
		private int _version;
		private FooProxy _foo;
		private Fee _dependent;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor for class Foo
		/// </summary>
		public Foo()
		{
		}

		public Foo(int x)
		{
			_x = x;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Get/set for key
		/// </summary>
		public string Key
		{
			get { return _key; }
			set { _key = value; }
		}

		/// <summary>
		/// Get/set for long
		/// </summary>
		public long Long
		{
			get { return _long; }
			set { _long = value; }
		}

		/// <summary>
		/// Get/set for integer
		/// </summary>
		public int Integer
		{
			get { return _integer; }
			set { _integer = value; }
		}

		/// <summary>
		/// Get/set for float
		/// </summary>
		public float Float
		{
			get { return _float; }
			set { _float = value; }
		}

		/// <summary>
		/// Get/set for x
		/// </summary>
		public virtual int X
		{
			get { return _x; }
			set { _x = value; }
		}

//		/// <summary>
//		/// Get/set for double
//		/// </summary>
//		public double Double
//		{
//			get { return _double; }
//			set { _double = value; }
//		}

		/// <summary>
		/// Get/set for date
		/// </summary>
		public DateTime Date
		{
			get { return _date; }
			set { _date = value; }
		}

		/// <summary>
		/// Get/set for timestamp
		/// </summary>
		public DateTime Timestamp
		{
			get { return _timestamp; }
			set { _timestamp = value; }
		}

		/// <summary>
		/// Get/set for boolean
		/// </summary>
		public bool Boolean
		{
			get { return _boolean; }
			set { _boolean = value; }
		}

		/// <summary>
		/// Get/set for bool
		/// </summary>
		public bool Bool
		{
			get { return _bool; }
			set { _bool = value; }
		}

		/// <summary>
		/// Get/set for null
		/// </summary>
		public NullableInt32 Null
		{
			get { return _null; }
			set { _null = value; }
		}

		/// <summary>
		/// Get/set for short
		/// </summary>
		public short Short
		{
			get { return _short; }
			set { _short = value; }
		}

		/// <summary>
		/// Get/set for char
		/// </summary>
		public char Char
		{
			get { return _char; }
			set { _char = value; }
		}

		/// <summary>
		/// Get/set for zero
		/// </summary>
		public float Zero
		{
			get { return _zero; }
			set { _zero = value; }
		}

		/// <summary>
		/// Get/set for int
		/// </summary>
		public int Int
		{
			get { return _int; }
			set { _int = value; }
		}

		/// <summary>
		/// Get/set for string
		/// </summary>
		public string String
		{
			get { return _string; }
			set { _string = value; }
		}

		/// <summary>
		/// Get/set for byte
		/// </summary>
		public byte Byte
		{
			get { return _byte; }
			set { _byte = value; }
		}

		/// <summary>
		/// Get/set for yesno
		/// </summary>
		public bool YesNo
		{
			get { return _yesno; }
			set { _yesno = value; }
		}

		/// <summary>
		/// Get/set for status
		/// </summary>
		public FooStatus Status
		{
			get { return _status; }
			set { _status = value; }
		}

		public byte[] Bytes
		{
			get { return _bytes; }
			set { _bytes = value; }
		}

		/// <summary>
		/// Get/set for locale
		/// </summary>
		public CultureInfo Locale
		{
			get { return _locale; }
			set { _locale = value; }
		}

		/// <summary>
		/// Get/set for formula
		/// </summary>
		public int Formula
		{
			get { return _formula; }
			set { _formula = value; }
		}

		/// <summary>
		/// Get/set for custom
		/// </summary>
		public string[] Custom
		{
			get { return custom; }
			set { custom = value; }
		}

		/// <summary>
		/// Get/set for version
		/// </summary>
		public int Version
		{
			get { return _version; }
			set { _version = value; }
		}

		/// <summary>
		/// Get/set for foo
		/// </summary>
		public FooProxy TheFoo
		{
			get { return _foo; }
			set { _foo = value; }
		}

		/// <summary>
		/// Get/set for dependent
		/// </summary>
		public Fee Dependent
		{
			get { return _dependent; }
			set { _dependent = value; }
		}

		/// <summary>
		/// Gets or sets the component
		/// </summary> 
		public FooComponent Component
		{
			get { return _component; }
			set { _component = value; }
		}

		public FooComponent NullComponent
		{
			get { return null; }
			set { if (value != null) throw new Exception("Null component"); }
		}

		#endregion

		#region ILifecycle Members

		public LifecycleVeto OnUpdate(ISession s)
		{
			return LifecycleVeto.NoVeto;
		}

		public void OnLoad(ISession s, object id)
		{
		}

		public LifecycleVeto OnSave(ISession s)
		{
			_string = "a string";
			_date = new DateTime(1970, 01, 01);
			_timestamp = DateTime.Now;
			_integer = -666;
			_long = 696969696969696969L - count++;
			_short = 42;
			_float = 6666.66f;
			//_double = new Double( 1.33e-69 );  // this double is too big for the sap db jdbc driver
//			_double = 1.12e-36;
			_boolean = true;
			_byte = 127;
			_int = 2;
			_char = '@';
			_bytes = Encoding.ASCII.GetBytes(_string);
			_status = FooStatus.ON;
			custom = new string[]
				{
					"foo", "bar"
				};
			//_component = new FooComponent("foo", 12, new DateTime[] { _date, _timestamp, DateTime.MinValue, new DateTime( DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day ) }, new FooComponent("bar", 666, new DateTime[] { new DateTime(1999,12,3), DateTime.MinValue }, null ) );
			_component =
				new FooComponent("foo", 12,
				                 new DateTime[]
				                 	{_date, _timestamp, new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day)},
				                 new FooComponent("bar", 666, new DateTime[] {new DateTime(1999, 12, 3)}, null));
			_component.Glarch = new Glarch();
			_dependent = new Fee();
			_dependent.Fi = "belongs to foo # " + Key;
			_locale = Thread.CurrentThread.CurrentCulture;
			return LifecycleVeto.NoVeto;
		}

		public LifecycleVeto OnDelete(ISession s)
		{
			return LifecycleVeto.NoVeto;
		}

		#endregion

		public void Disconnect()
		{
			if (_foo != null) _foo.Disconnect();
			_foo = null;
		}

		public bool EqualsFoo(Foo other)
		{
			if (_bytes != other._bytes)
			{
				if (_bytes == null || other.Bytes == null) return false;
				if (_bytes.Length != other.Bytes.Length) return false;
				for (int i = 0; i < _bytes.Length; i++)
				{
					if (_bytes[i] != other.Bytes[i]) return false;
				}
			}


			return (_bool == other.Bool)
			       && ((_boolean == other.Boolean) || (_boolean.Equals(other.Boolean)))
			       && ((_byte == other.Byte) || (_byte.Equals(other.Byte)))
			       //&& ( ( this._date == other._date ) || ( this._date.getDate() == other._date.getDate() && this._date.getMonth() == other._date.getMonth() && this._date.getYear() == other._date.getYear() ) )
//				&& ( ( _double == other.Double ) || ( _double.Equals(other.Double) ) )
			       && ((_float == other.Float) || (_float.Equals(other.Float)))
			       && (_int == other.Int)
			       && ((_integer == other.Integer) || (_integer.Equals(other.Integer)))
			       && ((_long == other.Long) || (_long.Equals(other.Long)))
			       && (_null == other.Null)
			       && ((_short == other.Short) || (_short.Equals(other.Short)))
			       && ((_string == other.String) || (_string.Equals(other.String)))
			       //&& ( ( this._timestamp==other._timestamp) || ( this._timestamp.getDate() == other._timestamp.getDate() && this._timestamp.getYear() == other._timestamp.getYear() && this._timestamp.getMonth() == other._timestamp.getMonth() ) )
			       && (_zero == other.Zero)
			       && ((_foo == other.TheFoo) || (_foo.Key.Equals(other.TheFoo.Key)))
//				&& ( ( _blob == other.Blob ) || ( _blob.Equals(other.Blob) ) )
			       && (_yesno == other.YesNo)
			       && (_status == other.Status)
			       // moved binary to its own loop - .net's Collections don't implement Equals() like java's collections.
			       //&& ( ( _binary == other.Binary ) || _binary.Equals(other.Binary))
			       && (_key.Equals(other.Key))
			       && (_locale.Equals(other.Locale))
			       && ((custom == other.Custom) || (custom[0].Equals(other.Custom[0]) && custom[1].Equals(other.Custom[1])))
				;
		}

//		public override int GetHashCode()
//		{
//			return key.GetHashCode() - _string.GetHashCode();
//		}


		private static int count = 0;
	}
}