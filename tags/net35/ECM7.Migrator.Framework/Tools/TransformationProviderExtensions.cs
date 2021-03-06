﻿using System.Data;

namespace ECM7.Migrator.Framework.Tools
{
	public static class TransformationProviderHiLoExtensions
	{
		/// <summary>
		/// Название служебной таблицы HiLo
		/// </summary>
		public const string HI_LO_TABLE_NAME = "hibernate_unique_key";
		public const string KEY_FIELD_NAME = "entity_key";
		public const string VALUE_FIELD_NAME = "next_hi_value";

		/// <summary>
		/// Добавление служебной таблицы HiLo
		/// </summary>
		/// <param name="database">Провайдер БД</param>
		public static void CreateHiLoTable(this ITransformationProvider database)
		{
			database.AddTable(HI_LO_TABLE_NAME,
				new Column(KEY_FIELD_NAME, DbType.AnsiString.WithSize(200), ColumnProperty.NotNull | ColumnProperty.PrimaryKey),
				new Column(VALUE_FIELD_NAME, DbType.Int64, ColumnProperty.NotNull, 1)
			);
		}

		/// <summary>
		/// Удаление служебной таблицы HiLo
		/// </summary>
		/// <param name="database">Провайдер БД</param>
		public static void RemoveHiLoTable(this ITransformationProvider database)
		{
			database.RemoveTable(HI_LO_TABLE_NAME);
		}

		/// <summary>
		/// Добавление записи в служебную таблицу HiLo
		/// </summary>
		/// <param name="database">Провайдер БД</param>
		/// <param name="table">Название добавляемой таблицы</param>
		public static void AddHiLoTableRecord(this ITransformationProvider database, string table)
		{
			AddHiLoTableRecord(database, table, 0);
		}

		/// <summary>
		/// Добавление записи в служебную таблицу HiLo
		/// </summary>
		/// <param name="database">Провайдер БД</param>
		/// <param name="table">Название добавляемой таблицы</param>
		/// <param name="initialHiValue">Текущее старшее значение идентификатора</param>
		public static void AddHiLoTableRecord(this ITransformationProvider database, string table, int initialHiValue)
		{
			Require.IsNotNullOrEmpty(table, "Не задано название таблицы для генерациии ID алгоритмом HiLo");
			Require.That(database.TableExists(HI_LO_TABLE_NAME), "Отсутствует служебная таблица HiLo");
			database.Insert(HI_LO_TABLE_NAME, new[] { KEY_FIELD_NAME, VALUE_FIELD_NAME }, new[] { table, initialHiValue.ToString() });
		}
	}
}
