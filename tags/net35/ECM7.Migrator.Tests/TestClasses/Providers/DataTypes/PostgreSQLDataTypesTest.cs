﻿using System.Configuration;
using ECM7.Migrator.Providers.PostgreSQL;
using NUnit.Framework;

namespace ECM7.Migrator.Tests.TestClasses.Providers.DataTypes
{
	[TestFixture]
	public class PostgreSQLDataTypesTest : DataTypesTestBase<PostgreSQLDialect>
	{
		public override string ConnectionString
		{
			get { return ConfigurationManager.AppSettings["NpgsqlConnectionString"]; }
		}

		public override string ParameterName
		{
			get { return "@value"; }
		}
	}
}