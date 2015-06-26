using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;

namespace ManagerADO
{
    class StoreManager
    {
        DataSetStore _store;

        public StoreManager(DataSetStore store)
        {
            _store = store;
        }

        public void AddColumns(ColumnDef[] columns, bool dropExisting)
        {
            DbCommand cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            using (connection)
            {
                connection.ConnectionString = connString;
                connection.Open();

                if (dropExisting)
                    DropAllColumns(cmd);

                string Sql = string.Empty;
                string colList = string.Empty;
                string valList = string.Empty;
                foreach (ColumnDef column in columns)
                {
                    colList += string.Format("[{0}] {1} {3}, ", column.name, column.type, 
                        (column.allowDBNull) ? "NULL" : string.Empty);
                    valList += string.Format("('{0}','{1}'), ", column.name, column.displayName);
                }

                Sql += string.Format("ALTER TABLE Items ADD {0};\n", colList.Substring(0, colList.Length - 2));
                Sql += string.Format("INSERT INTO ExtColumns (ColumnName, DisplayName) VALUES {0}", valList.Substring(0, valList.Length - 2));

                cmd.CommandText = Sql;
                cmd.ExecuteNonQuery();
            }
        }

        private void DropAllColumns(DbCommand command)
        {
            string Sql = string.Empty;
            string colList = string.Empty;

            command.CommandText = "SELECT ColumnName FROM ExtColumns";

            var reader = command.ExecuteReader();
            while (reader.Read())
                colList += string.Format("[{0}], ", reader[0]);

            Sql += string.Format("ALTER TABLE [Items] DROP COLUMN [{0}];\n", colList.Substring(0, colList.Length - 2));

            reader.Close();

            Sql += "DELETE FROM ExtColumns";

            command.CommandText = Sql;
            command.ExecuteNonQuery();
        }

        public void DropColumns(string[] columnNames)
        {
            DbCommand command = connection.CreateCommand();
            command.CommandType = CommandType.Text;

            using (connection)
            {
                connection.ConnectionString = connString;
                connection.Open();

                string colList = string.Empty;
                string colVals = string.Empty;
                foreach (string columnName in columnNames)
                {
                    colList += string.Format("[{0}], ", columnName);
                    colVals += string.Format("'{0}', ", columnName);
                }

                string Sql = string.Format("ALTER TABLE [Items] DROP COLUMN {0};\n", colList.Substring(0, colList.Length - 2));
                Sql += string.Format("DELETE FROM ExtColumns WHERE ColumnName IN ({0})", colVals.Substring(0, colVals.Length - 2));

                command.CommandText = Sql;
                command.ExecuteNonQuery();
            }
        }

        public List<Column> GetColumns()
        {
            List<Column> columns = new List<Column>();

            DbCommand command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = "SELECT ColumnName, DisplayName FROM ExtColumns";

            using (connection)
            {
                connection.ConnectionString = connString;
                connection.Open();

                var reader = command.ExecuteReader();
                while (reader.Read())
                    columns.Add(new Column()
                    {
                        name = (string)reader[0],
                        displayName = (string)reader[1]
                    });
                
                reader.Close();
            }

            return columns;
        }

    }
}
