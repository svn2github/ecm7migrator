﻿using System.Configuration;
using ECM7.Migrator.Providers.MySql;
using NUnit.Framework;

namespace ECM7.Migrator.Tests.TestClasses.Providers.DataTypes
{
	[TestFixture]
	public class MySqlDataTypesTest : DataTypesTestBase<MySqlDialect>
	{
		public override string ConnectionString
		{
			get { return ConfigurationManager.AppSettings["MySqlConnectionString"]; }
		}

		public override int MaxStringFixedLength { get { return 2000; } }

		public override string ParameterName
		{
			get { return "?"; }
		}
	}
}