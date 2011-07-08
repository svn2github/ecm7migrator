﻿namespace ECM7.Migrator.Tests2
{
	using System;
	using System.Reflection;

	using ECM7.Migrator.Framework;
	using ECM7.Migrator.Framework.Logging;

	using log4net;

	using Moq;

	using NUnit.Framework;

	/// <summary>
	/// Проверка выполнения миграций
	/// </summary>
	[TestFixture]
	public class ExecuteMigrationTest
	{
		/// <summary>
		/// Проверка выполнения миграции "вверх"
		/// </summary>
		[Test]
		public void CanMoveUp()
		{
			var provider = new Mock<ITransformationProvider>();
			Assembly asm = Assembly.Load("ECM7.Migrator.TestAssembly");

			var migrator = new Migrator(provider.Object, asm);

			migrator.ExecuteMigration(2, 1);

			provider.Verify(db => db.BeginTransaction());
			provider.Verify(db => db.ExecuteNonQuery("up"));
			provider.Verify(db => db.MigrationApplied(2, "test-key111"));
			provider.Verify(db => db.Commit());

			logger.Verify(log => log.MigrateUp(2, It.IsAny<string>()));
		}

		/// <summary>
		/// Проверка выполнения миграции "вниз"
		/// </summary>
		[Test]
		public void CanMoveDown()
		{
			var provider = new Mock<ITransformationProvider>();
			Assembly asm = Assembly.Load("ECM7.Migrator.TestAssembly");

			var migrator = new Migrator(provider.Object, asm);

			migrator.ExecuteMigration(2, 2);

			provider.Verify(db => db.BeginTransaction());
			provider.Verify(db => db.ExecuteNonQuery("down"));
			provider.Verify(db => db.MigrationUnApplied(2, "test-key111"));
			provider.Verify(db => db.Commit());

			logger.Verify(log => log.MigrateDown(2, It.IsAny<string>()));
		}

		/// <summary>
		/// Проверка, что при возникновении ошибки выполняется откат транзакции
		/// </summary>
		[Test]
		public void ShouldPerformRollbackWhenException()
		{
			Assembly asm = Assembly.Load("ECM7.Migrator.TestAssembly");
			var provider = new Mock<ITransformationProvider>();

			var logger = new Mock<ILog>();
			logger
				.Setup(log => log.MigrateDown(It.IsAny<long>(), It.IsAny<string>()))
				.Throws<Exception>();

			var migrator = new Migrator(provider.Object, asm, logger.Object);

			Assert.Throws<Exception>(() => migrator.ExecuteMigration(2, 2));

			provider.Verify(db => db.BeginTransaction());
			logger.Verify(log => log.MigrateDown(2, It.IsAny<string>()));
			provider.Verify(db => db.Rollback());
		}
	}
}
