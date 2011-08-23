using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using ECM7.Migrator.Compatibility;
using ECM7.Migrator.Exceptions;
using ECM7.Migrator.Framework;

using ForeignKeyConstraint = ECM7.Migrator.Framework.ForeignKeyConstraint;

namespace ECM7.Migrator.Providers
{
	using ECM7.Migrator.Framework.Logging;

	/// <summary>
	/// Base class for every transformation providers.
	/// A 'tranformation' is an operation that modifies the database.
	/// </summary>
	public abstract class TransformationProvider<TConnection> : SqlGenerator<TConnection>, ITransformationProvider
		where TConnection : IDbConnection
	{
		private const string SCHEMA_INFO_TABLE = "SchemaInfo";

		protected TransformationProvider(TConnection connection)
			: base(connection)
		{
			RegisterProperty(ColumnProperty.Null, "NULL");
			RegisterProperty(ColumnProperty.NotNull, "NOT NULL");
			RegisterProperty(ColumnProperty.Unique, "UNIQUE");
			RegisterProperty(ColumnProperty.PrimaryKey, "PRIMARY KEY");
		}

		public virtual void RemoveForeignKey(string table, string name)
		{
			RemoveConstraint(table, name);
		}

		public virtual string[] GetTables()
		{
			List<string> tables = new List<string>();
			string sql = FormatSql("SELECT {0:NAME} FROM {1:NAME}.{2:NAME}",
				"table_name", "information_schema", "tables");

			using (IDataReader reader = ExecuteReader(sql))
			{
				while (reader.Read())
				{
					tables.Add((string)reader[0]);
				}
			}
			return tables.ToArray();
		}

		public virtual void AddTable(string name, params Column[] columns)
		{
			// Most databases don't have the concept of a storage engine, so default is to not use it.
			AddTable(name, null, columns);
		}

		public virtual void AddTable(string name, string engine, params Column[] columns)
		{
			List<string> pks = GetPrimaryKeys(columns);
			bool compoundPrimaryKey = pks.Count > 1;

			List<string> listQuerySections = new List<string>(columns.Length);
			foreach (Column column in columns)
			{
				// Remove the primary key notation if compound primary key because we'll add it back later
				if (compoundPrimaryKey && column.IsPrimaryKey)
					column.ColumnProperty |= ColumnProperty.NotNull;

				string columnSql = this.GetSqlColumnDef(column, compoundPrimaryKey);
				listQuerySections.Add(columnSql);
			}

			if (compoundPrimaryKey)
			{
				string primaryKeyQuerySection = BuildPrimaryKeyQuerySection(name, pks);
				listQuerySections.Add(primaryKeyQuerySection);
			}

			string sectionsSql = listQuerySections.ToCommaSeparatedString();
			string createTableSql = this.GetSqlAddTable(name, engine, sectionsSql);

			ExecuteNonQuery(createTableSql);
		}

		protected virtual string BuildPrimaryKeyQuerySection(string tableName, List<string> primaryKeyColumns)
		{
			string pkName = "PK_" + tableName;

			return FormatSql("CONSTRAINT {0:NAME} PRIMARY KEY ({1:COLS})", pkName, primaryKeyColumns);

		}

		public List<string> GetPrimaryKeys(IEnumerable<Column> columns)
		{
			return columns
				.Where(column => column.IsPrimaryKey)
				.Select(column => column.Name)
				.ToList();
		}

		public virtual void RenameTable(string oldName, string newName)
		{
			if (TableExists(newName))
			{
				throw new MigrationException(String.Format("Table with name '{0}' already exists", newName));
			}

			if (TableExists(oldName))
			{
				string sql = FormatSql("ALTER TABLE {0:NAME} RENAME TO {1:NAME}", oldName, newName);
				ExecuteNonQuery(sql);
			}
		}

		public virtual void RenameColumn(string tableName, string oldColumnName, string newColumnName)
		{
			try
			{
				string sql = FormatSql("ALTER TABLE {0:NAME} RENAME COLUMN {1:NAME} TO {2:NAME}",
					tableName, oldColumnName, newColumnName);
				ExecuteNonQuery(sql);
			}
			catch (Exception ex)
			{
				string message = "Error when rename column '{0}' to '{1}' from table '{2}'".FormatWith(oldColumnName, newColumnName, tableName);
				throw new MigrationException(message, ex);
			}
		}

		public virtual void RemoveColumn(string table, string column)
		{
			try
			{
				string sql = FormatSql("ALTER TABLE {0:NAME} DROP COLUMN {1:NAME} ", table, column);
				ExecuteNonQuery(sql);
			}
			catch (Exception ex)
			{
				string message = "Error when remove column '{0}' from table '{1}'".FormatWith(column, table);
				throw new MigrationException(message, ex);
			}
		}

		public virtual bool ColumnExists(string table, string column)
		{
			try
			{
				string sql = FormatSql("SELECT {0:NAME} FROM {1:NAME}", column, table);
				ExecuteNonQuery(sql);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		#region ForeignKeys

		public void GenerateForeignKey(string primaryTable, string refTable)
		{
			GenerateForeignKey(primaryTable, refTable, ForeignKeyConstraint.NoAction);
		}

		public void GenerateForeignKey(string primaryTable, string refTable, ForeignKeyConstraint constraint)
		{
			GenerateForeignKey(primaryTable, refTable + "Id", refTable, "Id", constraint);
		}

		/// <summary>
		/// Guesses the name of the foreign key and add it
		/// </summary>
		public virtual void GenerateForeignKey(string primaryTable, string primaryColumn, string refTable, string refColumn)
		{
			string fkName = "FK_" + primaryTable + "_" + refTable;
			AddForeignKey(fkName, primaryTable, primaryColumn, refTable, refColumn);
		}

		/// <summary>
		/// Guesses the name of the foreign key and add it
		/// </summary>
		public virtual void GenerateForeignKey(string primaryTable, string[] primaryColumns, string refTable,
											   string[] refColumns)
		{
			string fkName = "FK_" + primaryTable + "_" + refTable;
			AddForeignKey(fkName, primaryTable, primaryColumns, refTable, refColumns);
		}

		/// <summary>
		/// Guesses the name of the foreign key and add it
		/// </summary>
		public virtual void GenerateForeignKey(string primaryTable, string primaryColumn, string refTable,
											   string refColumn, ForeignKeyConstraint constraint)
		{
			string fkName = "FK_" + primaryTable + "_" + refTable;
			AddForeignKey(fkName, primaryTable, primaryColumn, refTable, refColumn, constraint);
		}

		/// <summary>
		/// Guesses the name of the foreign key and add it
		/// </summary>
		public virtual void GenerateForeignKey(string primaryTable, string[] primaryColumns, string refTable,
											   string[] refColumns, ForeignKeyConstraint constraint)
		{
			string fkName = "FK_" + primaryTable + "_" + refTable;
			AddForeignKey(fkName, primaryTable, primaryColumns, refTable, refColumns, constraint);
		}

		/// <summary>
		/// Append a foreign key (relation) between two tables.
		/// tables.
		/// </summary>
		/// <param name="name">Constraint name</param>
		/// <param name="primaryTable">Table name containing the primary key</param>
		/// <param name="primaryColumn">Primary key column name</param>
		/// <param name="refTable">Foreign table name</param>
		/// <param name="refColumn">Foreign column name</param>
		public virtual void AddForeignKey(string name, string primaryTable, string primaryColumn, string refTable,
										  string refColumn)
		{
			AddForeignKey(name, primaryTable, new[] { primaryColumn }, refTable, new[] { refColumn });
		}

		/// <summary>
		/// <see cref="ITransformationProvider.AddForeignKey(string, string, string, string, string)">
		/// AddForeignKey(string, string, string, string, string)
		/// </see>
		/// </summary>
		public virtual void AddForeignKey(string name, string primaryTable, string[] primaryColumns, string refTable, string[] refColumns)
		{
			AddForeignKey(name, primaryTable, primaryColumns, refTable, refColumns, ForeignKeyConstraint.NoAction);
		}

		public virtual void AddForeignKey(string name, string primaryTable, string primaryColumn, string refTable, string refColumn, ForeignKeyConstraint constraint)
		{
			AddForeignKey(name, primaryTable, new[] { primaryColumn }, refTable, new[] { refColumn }, constraint);
		}

		public virtual void AddForeignKey(string name, string primaryTable, string[] primaryColumns, string refTable,
			string[] refColumns, ForeignKeyConstraint constraint)
		{
			AddForeignKey(name, primaryTable, primaryColumns, refTable, refColumns, constraint, ForeignKeyConstraint.NoAction);
		}

		public virtual void AddForeignKey(string name, string primaryTable, string[] primaryColumns, string refTable,
			string[] refColumns, ForeignKeyConstraint onDeleteConstraint, ForeignKeyConstraint onUpdateConstraint)
		{
			if (ConstraintExists(primaryTable, name))
			{
				MigratorLogManager.Log.WarnFormat("Constraint {0} already exists", name);
				return;
			}

			string onDeleteConstraintResolved = SqlForConstraint(onDeleteConstraint);
			string onUpdateConstraintResolved = SqlForConstraint(onUpdateConstraint);

			string sql = FormatSql(
				"ALTER TABLE {0:NAME} ADD CONSTRAINT {1:NAME} FOREIGN KEY ({2:COLS}) REFERENCES {3:NAME} ({4:COLS}) ON UPDATE {5} ON DELETE {6}",
				primaryTable, name, primaryColumns, refTable, refColumns, onUpdateConstraintResolved, onDeleteConstraintResolved);

			ExecuteNonQuery(sql);
		}

		#endregion

		#region generate sql

		protected virtual string GetSqlAddTable(string table, string engine, string columnsSql)
		{
			return FormatSql("CREATE TABLE {0:NAME} ({1})", table, columnsSql);
		}

		protected virtual string GetSqlAddColumn(string table, string columnSql)
		{
			return FormatSql("ALTER TABLE {0:NAME} ADD COLUMN {1}", table, columnSql);
		}

		protected virtual string GetSqlChangeColumn(string table, string columnSql)
		{
			return FormatSql("ALTER TABLE {0:NAME} ALTER COLUMN {1}", table, columnSql);
		}

		#endregion

		#region DDL

		#region tables

		public abstract bool TableExists(string table);

		public virtual void RemoveTable(string name)
		{
			string sql = FormatSql("DROP TABLE {0:NAME}", name);
			ExecuteNonQuery(sql);
		}

		#endregion

		#region columns

		public void AddColumn(string table, Column column)
		{
			string sqlColumnDef = GetSqlColumnDef(column, false);
			string sqlAddColumn = GetSqlAddColumn(table, sqlColumnDef);

			ExecuteNonQuery(sqlAddColumn);
		}

		public virtual void ChangeColumn(string table, Column column)
		{
			string sqlColumnDef = GetSqlColumnDef(column, false);
			string sqlChangeColumn = GetSqlChangeColumn(table, sqlColumnDef);

			ExecuteNonQuery(sqlChangeColumn);
		}


		#endregion

		#region constraints

		public virtual void AddPrimaryKey(string name, string table, params string[] columns)
		{
			string sql = FormatSql(
				"ALTER TABLE {0:NAME} ADD CONSTRAINT {1:NAME} PRIMARY KEY ({2:COLS})", table, name, columns);

			ExecuteNonQuery(sql);
		}

		public virtual void AddUniqueConstraint(string name, string table, params string[] columns)
		{
			string sql = FormatSql(
				"ALTER TABLE {0:NAME} ADD CONSTRAINT {1:NAME} UNIQUE({2:COLS})", table, name, columns);

			ExecuteNonQuery(sql);
		}

		public virtual void AddCheckConstraint(string name, string table, string checkSql)
		{
			string sql = FormatSql(
				"ALTER TABLE {0:NAME} ADD CONSTRAINT {1:NAME} CHECK ({2}) ", table, name, checkSql);

			ExecuteNonQuery(sql);
		}

		/// <summary>
		/// Determines if a constraint exists.
		/// </summary>
		/// <param name="name">Constraint name</param>
		/// <param name="table">Table owning the constraint</param>
		public abstract bool ConstraintExists(string table, string name);

		public virtual void RemoveConstraint(string table, string name)
		{
			string format = FormatSql(
				"ALTER TABLE {0:NAME} DROP CONSTRAINT {1:NAME}", table, name);

			ExecuteNonQuery(format);
		}

		#endregion

		#region indexes

		public virtual void AddIndex(string name, bool unique, string table, params string[] columns)
		{
			Require.That(columns.Length > 0, "Not specified columns of the table to create an index");

			string uniqueString = unique ? "UNIQUE" : string.Empty;
			string sql = FormatSql("CREATE {0} INDEX {1:NAME} ON {2:NAME} ({3:COLS})",
				uniqueString, name, table, columns);

			ExecuteNonQuery(sql);
		}

		public abstract bool IndexExists(string indexName, string tableName);

		public virtual void RemoveIndex(string indexName, string tableName)
		{
			string sql = FormatSql("DROP INDEX {0:NAME} ON {1:NAME}", indexName, tableName);

			ExecuteNonQuery(sql);
		}

		#endregion

		#endregion

		#region DML

		public virtual int Insert(string table, string[] columns, string[] values)
		{
			string sql = FormatSql("INSERT INTO {0:NAME} ({1:COLS}) VALUES ({2})",
				table, columns, QuoteValues(values).ToCommaSeparatedString());

			return ExecuteNonQuery(sql);
		}

		public virtual int Update(string table, string[] columns, string[] values, string where = null)
		{
			string namesAndValues = JoinColumnsAndValues(columns, values);

			string query = where.IsNullOrEmpty(true)
								? "UPDATE {0:NAME} SET {1}"
								: "UPDATE {0:NAME} SET {1} WHERE {2}";

			string sql = FormatSql(query, table, namesAndValues, where);
			return ExecuteNonQuery(sql);
		}

		public virtual int Delete(string table, string whereSql = null)
		{
			string format = whereSql.IsNullOrEmpty(true)
								? "DELETE FROM {0:NAME}"
								: "DELETE FROM {0:NAME} WHERE {1}";

			string sql = FormatSql(format, table, whereSql);

			return ExecuteNonQuery(sql);
		}

		#endregion

		#region For

		/// <summary>
		/// Get this provider or a NoOp provider if you are not running in the context of 'TTargetProvider'.
		/// </summary>
		public void For<TProvider>(Action<ITransformationProvider> actions)
		{
			For(typeof(TProvider), actions);
		}

		/// <summary>
		/// Get this provider or a NoOp provider if you are not running in the context of 'TTargetProvider'.
		/// </summary>
		public void For(Type providerType, Action<ITransformationProvider> actions)
		{
			if (GetType() == providerType)
				actions(this);
		}

		/// <summary>
		/// Get this provider or a NoOp provider if you are not running in the context of provider with name 'providerTypeName'.
		/// </summary>
		public void For(string providerTypeName, Action<ITransformationProvider> actions)
		{
			Type providerType = Type.GetType(providerTypeName);
			Require.IsNotNull(providerType, "�� ������� ��������� ��� ����������: {0}".FormatWith(providerTypeName.Nvl("null")));
			For(providerType, actions);
		}

		#endregion

		#region methods for migrator core
		/// <summary>
		/// The list of Migrations currently applied to the database.
		/// </summary>
		public List<long> GetAppliedMigrations(string key = "")
		{
			Require.IsNotNull(key, "�� ������ ���� ���������");
			var appliedMigrations = new List<long>();

			CreateSchemaInfoTable();

			string sql = FormatSql("SELECT {0:NAME} FROM {1:NAME} WHERE {2:NAME} = '{3}'",
				"Version", SCHEMA_INFO_TABLE, "Key", key.Replace("'", "''"));

			using (IDataReader reader = ExecuteReader(sql))
			{
				while (reader.Read())
				{
					appliedMigrations.Add(reader.GetInt64(0));
				}
			}

			appliedMigrations.Sort();

			return appliedMigrations;
		}

		/// <summary>
		/// Marks a Migration version number as having been applied
		/// </summary>
		/// <param name="version">The version number of the migration that was applied</param>
		/// <param name="key">Key of migration series</param>
		public void MigrationApplied(long version, string key)
		{
			CreateSchemaInfoTable();
			Insert(SCHEMA_INFO_TABLE, new[] { "Version", "Key" }, new[] { version.ToString(), key });
		}

		/// <summary>
		/// Marks a Migration version number as having been rolled back from the database
		/// </summary>
		/// <param name="version">The version number of the migration that was removed</param>
		/// <param name="key">Key of migration series</param>
		public void MigrationUnApplied(long version, string key)
		{
			CreateSchemaInfoTable();

			string whereSql = FormatSql("{0:NAME} = {1} AND {2:NAME} = '{3}'",
				"Version", version, "Key", key);

			Delete(SCHEMA_INFO_TABLE, whereSql);
		}

		protected void CreateSchemaInfoTable()
		{
			EnsureHasConnection();

			if (!TableExists(SCHEMA_INFO_TABLE))
			{
				AddTable(
					SCHEMA_INFO_TABLE,
					new Column("Version", DbType.Int64, ColumnProperty.PrimaryKey),
					new Column("Key", DbType.String.WithSize(200), ColumnProperty.PrimaryKey, "''"));
			}
			else
			{
				if (!ColumnExists(SCHEMA_INFO_TABLE, "Key"))
				{
					// TODO: ������� ��� ������������� ��� ������ ������� SchemaInfo � ��������� �������
					UpdateSchemaInfo.Update(this);
				}
			}
		}

		#endregion
	}
}
