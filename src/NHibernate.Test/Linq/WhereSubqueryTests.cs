using System;
using System.Linq;
using System.Linq.Expressions;
using NHibernate.DomainModel.Northwind.Entities;
using NUnit.Framework;

namespace NHibernate.Test.Linq
{
	[TestFixture]
	public class WhereSubqueryTests : LinqTestCase
	{
		protected override void Configure(Cfg.Configuration configuration)
		{
			configuration.SetProperty(Cfg.Environment.ShowSql, "true");
			base.Configure(configuration);
		}

		[Test]
		public void TimesheetsWithNoEntries()
		{
			var query = (from timesheet in db.Timesheets
						 where !timesheet.Entries.Any()
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithCountSubquery()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Count() >= 1
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithCountSubqueryReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where 1 <= timesheet.Entries.Count()
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithCountSubqueryComparedToProperty()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Count() > timesheet.Id
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithCountSubqueryComparedToPropertyReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Id < timesheet.Entries.Count()
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithAverageSubquery()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Average(e => e.NumberOfHours) > 12
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithAverageSubqueryReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where 12 < timesheet.Entries.Average(e => e.NumberOfHours)
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		[Ignore("Need to coalesce the subquery - timesheet with no entries should return average of 0, not null")]
		public void TimeSheetsWithAverageSubqueryComparedToProperty()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Average(e => e.NumberOfHours) < timesheet.Id
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		[Ignore("Need to coalesce the subquery - timesheet with no entries should return average of 0, not null")]
		public void TimeSheetsWithAverageSubqueryComparedToPropertyReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Id > timesheet.Entries.Average(e => e.NumberOfHours)
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithMaxSubquery()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Max(e => e.NumberOfHours) == 14
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithMaxSubqueryReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where 14 == timesheet.Entries.Max(e => e.NumberOfHours)
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithMaxSubqueryComparedToProperty()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Max(e => e.NumberOfHours) > timesheet.Id
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithMaxSubqueryComparedToPropertyReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Id < timesheet.Entries.Max(e => e.NumberOfHours)
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithMinSubquery()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Min(e => e.NumberOfHours) < 7
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithMinSubqueryReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where 7 > timesheet.Entries.Min(e => e.NumberOfHours)
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithMinSubqueryComparedToProperty()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Min(e => e.NumberOfHours) > timesheet.Id
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithMinSubqueryComparedToPropertyReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Id < timesheet.Entries.Min(e => e.NumberOfHours)
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithSumSubquery()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Sum(e => e.NumberOfHours) <= 20
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithSumSubqueryReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where 20 >= timesheet.Entries.Sum(e => e.NumberOfHours)
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		[Ignore("Need to coalesce the subquery - timesheet with no entries should return sum of 0, not null")]
		public void TimeSheetsWithSumSubqueryComparedToProperty()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Sum(e => e.NumberOfHours) <= timesheet.Id
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		[Ignore("Need to coalesce the subquery - timesheet with no entries should return sum of 0, not null")]
		public void TimeSheetsWithSumSubqueryComparedToPropertyReversed()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Id >= timesheet.Entries.Sum(e => e.NumberOfHours)
						 select timesheet).ToList();

			Assert.AreEqual(1, query.Count);
		}

		[Test]
		public void TimeSheetsWithStringContainsSubQuery()
		{
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.Any(e => e.Comments.Contains("testing"))
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}
		
		[Test]
		public void TimeSheetsWithStringContainsSubQueryWithAsQueryable()
		{
			//NH-2998
			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.AsQueryable().Any(e => e.Comments.Contains("testing"))
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void TimeSheetsWithStringContainsSubQueryWithAsQueryableAndExternalPredicate()
		{
			//NH-2998
			Expression<Func<TimesheetEntry, bool>> predicate = e => e.Comments.Contains("testing");

			var query = (from timesheet in db.Timesheets
						 where timesheet.Entries.AsQueryable().Any(predicate)
						 select timesheet).ToList();

			Assert.AreEqual(2, query.Count);
		}

		[Test]
		public void HqlOrderLinesWithInnerJoinAndSubQuery()
		{
			//NH-3002
			var lines = session.CreateQuery(@"select c from OrderLine c
join c.Order o
where o.Customer.CustomerId = 'VINET'
	and not exists (from c.Order.Employee.Subordinates x where x.EmployeeId = 100)
").List<OrderLine>();

			Assert.AreEqual(10, lines.Count);
		}

		[Test]
		public void HqlOrderLinesWithImpliedJoinAndSubQuery()
		{
			//NH-3002
			var lines = session.CreateQuery(@"from OrderLine c
where c.Order.Customer.CustomerId = 'VINET'
	and not exists (from c.Order.Employee.Subordinates x where x.EmployeeId = 100)
").List<OrderLine>();

			Assert.AreEqual(10, lines.Count);
		}

		[Test]
		public void OrderLinesWithImpliedJoinAndSubQuery()
		{
			//NH-2999 and NH-2988
			var lines = (from l in db.OrderLines
						 where l.Order.Customer.CustomerId == "VINET"
						 where !l.Order.Employee.Subordinates.Any(x => x.EmployeeId == 100)
						 select l).ToList();

			Assert.AreEqual(10, lines.Count);
		}
	}
}
