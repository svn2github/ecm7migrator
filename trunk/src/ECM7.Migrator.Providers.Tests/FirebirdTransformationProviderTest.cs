﻿using System.Data;
using System.Text;

namespace ECM7.Migrator.Providers.Tests
{
	using ECM7.Migrator.Providers.Firebird;

    using NUnit.Framework;

    [TestFixture, Category("Firebird")]
    public class FirebirdTransformationProviderTest 
		: TransformationProviderConstraintBase<FirebirdTransformationProvider>
    {
    	public override string ConnectionStrinSettingsName
    	{
    		get { return "FirebirdConnectionString"; }
    	}

    	public override bool UseTransaction
    	{
			get { return false; }
    	}

		protected override string BatchSql
		{
			get
			{
				StringBuilder sb = new StringBuilder();

				sb.AppendLine("insert into \"TestTwo\" (\"Id\", \"TestId\") values (11, 111);");
				sb.AppendLine("insert into \"TestTwo\" (\"Id\", \"TestId\") values (22, 222);");
				sb.AppendLine("insert into \"TestTwo\" (\"Id\", \"TestId\") values (33, 333);");
				sb.AppendLine("insert into \"TestTwo\" (\"Id\", \"TestId\") values (44, 444);");
				sb.AppendLine("insert into \"TestTwo\" (\"Id\", \"TestId\") values (55, 555);");

				return sb.ToString();
			}
		}

		[Test]
		public override void AddDecimalColumn()
		{
			provider.AddColumn("TestTwo", "TestDecimal", DbType.Decimal, 18);
			Assert.IsTrue(provider.ColumnExists("TestTwo", "TestDecimal"));
		}
    }
}