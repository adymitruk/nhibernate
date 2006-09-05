using System;
using NUnit.Framework;
using System.Collections;

namespace NHibernate.Test.Unconstrained
{
	[TestFixture]
	public class UnconstrainedTest: TestCase
	{
		protected override string MappingsAssembly
		{
			get { return "NHibernate.Test"; }
		}

		protected override IList Mappings
		{
			get
			{	return new string[] { "Unconstrained.Person.hbm.xml" };	}
		}

		private static void AssertNotFound(object obj)
		{
			try
			{
				NHibernateUtil.Initialize(obj);
				Assert.Fail();
			}
			catch (ObjectNotFoundException onfe)
			{
				// OK
			}
		}

		[Test]
		public void UnconstrainedNoCache() 
		{
			ISession session = OpenSession();
			ITransaction tx = session.BeginTransaction();
			Person p = new Person("gavin");
			p.EmployeeId = "123456";
			session.Save(p);
			tx.Commit();
			session.Close();
				 	                
			sessions.Evict(typeof(Person));

			session = OpenSession();
			tx = session.BeginTransaction();
			p = (Person)session.Get(typeof(Person), "gavin");
			AssertNotFound(p.Employee);

			p.Employee = new Employee("123456");
			tx.Commit();
			session.Close();

			sessions.Evict(typeof(Person));

			session = OpenSession();
			tx = session.BeginTransaction();
			p = (Person)session.Get(typeof(Person), "gavin");
			Assert.IsNotNull(p.Employee.Id);
			Assert.IsTrue( NHibernateUtil.IsInitialized( p.Employee ) );
			session.Delete(p);
			tx.Commit();
			session.Close();
		}

		[Test]
		public void UnconstrainedOuterJoinFetch()
		{
			ISession session = OpenSession();
			ITransaction tx = session.BeginTransaction();
			Person p = new Person("gavin");
			p.EmployeeId = "123456";
			session.Save(p);
			tx.Commit();
			session.Close();

			sessions.Evict(typeof(Person));

			session = OpenSession();
			tx = session.BeginTransaction();
			p = (Person)session.CreateCriteria(typeof(Person))
				.SetFetchMode("Employee", FetchMode.Join)
				.Add(Expression.Expression.Eq("Name", "gavin"))
				.UniqueResult();
			AssertNotFound(p.Employee);
			p.Employee = new Employee("123456");
			tx.Commit();
			session.Close();

			sessions.Evict(typeof(Person));

			session = OpenSession();
			tx = session.BeginTransaction();
			p = (Person)session.CreateCriteria(typeof(Person))
				.SetFetchMode("Employee", FetchMode.Join)
				.Add(Expression.Expression.Eq("Name", "gavin"))
				.UniqueResult();
			Assert.IsTrue(NHibernateUtil.IsInitialized(p.Employee));
			Assert.IsNotNull(p.Employee);
			session.Delete(p);
			tx.Commit();
			session.Close();
		}

		[Test]
		public void Unconstrained() 
		{
			ISession session = OpenSession();
			ITransaction tx = session.BeginTransaction();
			Person p = new Person("gavin");
			p.EmployeeId = "123456";
			session.Save(p);
			tx.Commit();
			session.Close();

			session = OpenSession();
			tx = session.BeginTransaction();
			p = (Person)session.Get(typeof(Person), "gavin");
			AssertNotFound(p.Employee);
			p.Employee = new Employee("123456");
			tx.Commit();
			session.Close();

			session = OpenSession();
			tx = session.BeginTransaction();
			p = (Person)session.Get(typeof(Person), "gavin");
			Assert.IsNotNull(p.Employee.Id);
			Assert.IsTrue(NHibernateUtil.IsInitialized(p.Employee));
			session.Delete(p);
			tx.Commit();
			session.Close();
		}
	}
}
