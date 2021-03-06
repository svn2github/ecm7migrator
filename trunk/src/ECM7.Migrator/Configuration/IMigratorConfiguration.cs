﻿namespace ECM7.Migrator.Configuration
{
	/// <summary>
	/// Настройки мигратора
	/// </summary>
	public interface IMigratorConfiguration
	{
		/// <summary>
		/// Диалект
		/// </summary>
		string Provider { get; }

		/// <summary>
		/// Строка подключения
		/// </summary>
		string ConnectionString { get; }

		/// <summary>
		/// Название строки подключения
		/// </summary>
		string ConnectionStringName { get; }

		/// <summary>
		/// Сборка с миграциями
		/// </summary>
		string Assembly { get; }

		/// <summary>
		/// Путь к файлу с миграциями
		/// </summary>
		string AssemblyFile { get; }

		/// <summary>
		/// Максимальное время выполнения команды
		/// </summary>
		int? CommandTimeout { get; }

		/// <summary>
		/// Необходимо ли оборачивать имена в кавычки
		/// </summary>
		bool? NeedQuotesForNames { get; }
	}
}
