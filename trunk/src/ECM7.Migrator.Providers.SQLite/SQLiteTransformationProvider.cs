namespace ECM7.Migrator.Providers.SQLite
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Linq;
	using ECM7.Migrator.Framework;
	using ECM7.Migrator.Framework.Logging;

	using SqliteConnection = System.Data.SQLite.SQLiteConnection;

	/// <summary>
	/// Summary description for SQLiteTransformationProvider.
	/// </summary>
	public class SQLiteTransformationProvider : TransformationProvider
	{
		/// <summary>
		/// �������������
		/// </summary>
		/// <param name="dialect"></param>
		/// <param name="connectionString"></param>
		public SQLiteTransformationProvider(Dialect dialect, string connectionString)
			: base(dialect, new SqliteConnection(connectionString))
		{
		}

		/// <summary>
		/// Check that the index with the specified name already exists
		/// </summary>
		public override bool IndexExists(string indexName, string tableName)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// ���������� �������� �����
		/// </summary>
		public override void AddForeignKey(
			string name,
			string primaryTable,
			string[] primaryColumns,
			string refTable,
			string[] refColumns,
			ECM7.Migrator.Framework.ForeignKeyConstraint onDeleteConstraint,
			ECM7.Migrator.Framework.ForeignKeyConstraint onUpdateConstraint)
		{
			// todo: �������� ����� �� ���������� ��������� ������� ������ � SQLite
			throw new NotSupportedException("SQLite �� ������������ ������� �����");
		}


		/// <summary>
		/// Remove an existing foreign key constraint
		/// </summary>
		/// <param name="name">The name of the foreign key to remove</param>
		/// <param name="table">The table that contains the foreign key.</param>
		public override void RemoveForeignKey(string name, string table)
		{
			throw new NotSupportedException("SQLite �� ������������ ������� �����");
		}

		/// <summary>
		/// Remove an existing column from a table
		/// </summary>
		/// <param name="table">The name of the table to remove the column from</param>
		/// <param name="column">The column to remove</param>
		public override void RemoveColumn(string table, string column)
		{
			if (!(TableExists(table) && ColumnExists(table, column)))
			{
				return;
			}

			string[] origColDefs = GetColumnDefs(table);

			string[] newColDefs = origColDefs.Where(origdef => !ColumnMatch(column, origdef)).ToArray();
			string colDefsSql = String.Join(",", newColDefs);

			string[] colNames = ParseSqlForColumnNames(newColDefs);
			string colNamesSql = String.Join(",", colNames);

			string tmpTable = table + "_temp";

			AddTable(tmpTable, null, colDefsSql);
			ExecuteQuery(FormatSql("INSERT INTO {0:NAME} SELECT {1} FROM {2:NAME}", tmpTable, colNamesSql, table));
			RemoveTable(table);
			ExecuteQuery(FormatSql("ALTER TABLE {0:NAME} RENAME TO {1:NAME}", tmpTable, table));
		}

		/// <summary>
		/// Rename an existing table
		/// </summary>
		/// <param name="tableName">The name of the table</param>
		/// <param name="oldColumnName">The old name of the column</param>
		/// <param name="newColumnName">The new name of the column</param>
		public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
		{
			if (ColumnExists(tableName, newColumnName))
			{
				throw new MigrationException(String.Format("Table '{0}' has column named '{1}' already", tableName, newColumnName));
			}

			if (ColumnExists(tableName, oldColumnName))
			{
				string[] columnDefs = GetColumnDefs(tableName);
				string columnDef = Array.Find(columnDefs, col => ColumnMatch(oldColumnName, col));

				string newColumnDef = columnDef.Replace(oldColumnName, newColumnName);

				AddColumn(tableName, newColumnDef);
				ExecuteQuery(FormatSql("UPDATE {0:NAME} SET {1:NAME}={2:NAME}", tableName, newColumnName, oldColumnName));
				RemoveColumn(tableName, oldColumnName);
			}
		}

		/// <summary>
		/// Change the definition of an existing column.
		/// </summary>
		/// <param name="table">The name of the table that will get the new column</param>
		/// <param name="column">
		/// An instance of a <see cref="Column">Column</see> with the specified properties and the name of an existing column</param>
		public override void ChangeColumn(string table, Column column)
		{
			if (!ColumnExists(table, column.Name))
			{
				MigratorLogManager.Log.WarnFormat("Column {0}.{1} does not exist", table, column.Name);
				return;
			}

			string tempColumn = "temp_" + column.Name;
			RenameColumn(table, column.Name, tempColumn);
			AddColumn(table, column);
			ExecuteQuery(FormatSql("UPDATE {0:NAME} SET {1:NAME}={2:NAME}", table, column.Name, tempColumn));
			RemoveColumn(table, tempColumn);
		}

		/// <summary>
		/// Check if a table already exists
		/// </summary>
		/// <param name="table">The name of the table that you want to check on.</param>
		public override bool TableExists(string table)
		{
			using (IDataReader reader =
				ExecuteQuery(String.Format("SELECT [name] FROM [sqlite_master] WHERE [type]='table' and [name]='{0}'", table)))
			{
				return reader.Read();
			}
		}

		/// <summary>
		/// Determines if a constraint exists.
		/// </summary>
		/// <param name="table">Table owning the constraint</param>
		/// <param name="name">Constraint name</param>
		/// <returns><c>true</c> if the constraint exists.</returns>
		public override bool ConstraintExists(string table, string name)
		{
			return false;
		}

		/// <summary>
		/// Get the names of all of the tables
		/// </summary>
		/// <returns>The names of all the tables.</returns>
		public override string[] GetTables()
		{
			List<string> tables = new List<string>();

			string sql = "SELECT [name] FROM [sqlite_master] WHERE [type]='table' AND [name] <> 'sqlite_sequence' ORDER BY [name]";
			using (IDataReader reader = ExecuteQuery(sql))
			{
				while (reader.Read())
				{
					tables.Add((string)reader[0]);
				}
			}

			return tables.ToArray();
		}

		public override Column[] GetColumns(string table)
		{
			List<Column> columns = new List<Column>();
			foreach (string columnDef in GetColumnDefs(table))
			{
				string name = ExtractNameFromColumnDef(columnDef);
				// FIXME: Need to get the real type information
				Column column = new Column(name, DbType.String);
				bool isNullable = IsNullable(columnDef);
				column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;
				columns.Add(column);
			}
			return columns.ToArray();
		}

		public string GetSqlDefString(string table)
		{
			string sqldef = null;
			using (IDataReader reader = ExecuteQuery(String.Format("SELECT [sql] FROM [sqlite_master] WHERE [type]='table' AND [name]='{0}'", table)))
			{
				if (reader.Read())
				{
					sqldef = (string)reader[0];
				}
			}
			return sqldef;
		}

		public string[] GetColumnNames(string table)
		{
			return ParseSqlForColumnNames(GetSqlDefString(table));
		}

		public string[] GetColumnDefs(string table)
		{
			return ParseSqlColumnDefs(GetSqlDefString(table));
		}

		/// <summary>
		/// Turn something like 'columnName INTEGER NOT NULL' into just 'columnName'
		/// </summary>
		public string[] ParseSqlForColumnNames(string sqldef)
		{
			string[] parts = ParseSqlColumnDefs(sqldef);
			return ParseSqlForColumnNames(parts);
		}

		public string[] ParseSqlForColumnNames(string[] parts)
		{
			if (null == parts)
				return null;

			for (int i = 0; i < parts.Length; i++)
			{
				parts[i] = ExtractNameFromColumnDef(parts[i]);
			}
			return parts;
		}

		/// <summary>
		/// Name is the first value before the space.
		/// </summary>
		/// <param name="columnDef"></param>
		/// <returns></returns>
		public string ExtractNameFromColumnDef(string columnDef)
		{
			int idx = columnDef.IndexOf(" ");
			if (idx > 0)
			{
				return columnDef.Substring(0, idx);
			}
			return null;
		}

		public bool IsNullable(string columnDef)
		{
			return !columnDef.Contains("NOT NULL");
		}

		public string[] ParseSqlColumnDefs(string sqldef)
		{
			if (String.IsNullOrEmpty(sqldef))
			{
				return null;
			}

			sqldef = sqldef.Replace(Environment.NewLine, " ");
			int start = sqldef.IndexOf("(");
			int end = sqldef.IndexOf(")");

			sqldef = sqldef.Substring(0, end);
			sqldef = sqldef.Substring(start + 1);

			string[] cols = sqldef.Split(new[] { ',' });
			for (int i = 0; i < cols.Length; i++)
			{
				cols[i] = cols[i].Trim();
			}
			return cols;
		}

		public bool ColumnMatch(string column, string columnDef)
		{
			return columnDef.StartsWith(column + " ") || columnDef.StartsWith(dialect.QuoteName(column));
		}
	}
}
