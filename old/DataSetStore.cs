using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerADO
{
    class DataSetStore
    {
        private DataSet _dataSet = new DataSet("Store");
        private DbProviderFactory _factory;
        private string _connString = string.Empty;
        private DbCommandBuilder _builder;
        private Dictionary<string, DbDataAdapter> _adapters = new Dictionary<string, DbDataAdapter>();

        public DataSetStore()
        {
            // first is default
            var cnStrings = ConfigurationManager.ConnectionStrings[0];
            _connString = cnStrings.ConnectionString;
            Init(cnStrings.ProviderName);
        }

        public DataSetStore(string providerName, string connectionString)
        {
            _connString = connectionString;
            Init(providerName);
        }

        private void Init(string providerName)
        {
            _factory = DbProviderFactories.GetFactory(providerName);
            
            using (DbConnection cnn = GetConnection())
            {
                cnn.Open();
                DbTransaction trn = cnn.BeginTransaction();

                ConfigureAdapter(cnn, "Items");
                // ...
                //BuildTableRelationship();

                trn.Commit(); // auto rollback on exception? 
            }
        }

        private void ConfigureAdapter(DbConnection cnn, string tableName)
        {
            DbDataAdapter adapter;

            if (!_adapters.TryGetValue(tableName, out adapter))
            {
                DbCommandBuilder builder = _factory.CreateCommandBuilder();

                adapter = _factory.CreateDataAdapter();
                adapter.SelectCommand = GetCommand(cnn, string.Format("SELECT * FROM {0}", builder.QuoteIdentifier(tableName)));
                adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;

                builder.DataAdapter = adapter;

                // save default builder to quote identifiers
                if (_builder == null)
                    _builder = builder;

                _adapters.Add(tableName, adapter);
            }

            adapter.Fill(_dataSet, tableName);
        }

        public DataTable Items
        {
            get { return _dataSet.Tables["Items"]; }
        }

        public DbConnection GetConnection()
        {
            DbConnection cnn = _factory.CreateConnection();
            cnn.ConnectionString = _connString;
            return cnn;
        }

        public DbCommand GetCommand(DbConnection connection, string commandText = "")
        {
            DbCommand cmd = _factory.CreateCommand();
            cmd.Connection = connection;
            cmd.CommandText = commandText;
            return cmd;
        }

        public string QuoteIdentifier(string identifier)
        {
            return _builder.QuoteIdentifier(identifier);
        }

        public void Update()
        {
            foreach(var kv in _adapters)
                kv.Value.Update(_dataSet, kv.Key);
        }

        public void Update(string tableName)
        {
            DbDataAdapter adapter;

            if (!_adapters.TryGetValue(tableName, out adapter))
                throw new ArgumentException("Unknown table: " + tableName);

            adapter.Update(_dataSet, tableName);
        }

        public void ClearTable(string tableName)
        {
            using (DbConnection cnn = GetConnection())
            {
                cnn.Open();

                string sql = string.Format("DELETE FROM {0}", QuoteIdentifier(tableName));
                DbCommand cmd = GetCommand(cnn, sql);
                cmd.ExecuteNonQuery();
            }

            // clear in-memory table
            _dataSet.Tables[tableName].Clear();
        }

        // Manager

        public void AddColumns(string tableName, ColumnDef[] columns, bool dropExisting)
        {
            string quotedTableName = QuoteIdentifier(tableName);

            using (DbConnection cnn = GetConnection())
            {
                cnn.Open();

                DbCommand cmd = GetCommand(cnn);

                if (dropExisting)
                    DropAllColumns(cmd, quotedTableName);

                string Sql = string.Empty;
                string colList = string.Empty;
                string valList = string.Empty;
                foreach (ColumnDef column in columns)
                {
                    colList += string.Format("{0} {1} {3}, ", QuoteIdentifier(column.name), column.type,
                        (column.allowDBNull) ? "NULL" : string.Empty);
                    valList += string.Format("('{0}','{1}'), ", column.name, column.displayName);
                }

                Sql += string.Format("ALTER TABLE {0} ADD {1};\n", quotedTableName, colList.Substring(0, colList.Length - 2));
                Sql += string.Format("INSERT INTO ExtColumns (ColumnName, DisplayName) VALUES {0}", valList.Substring(0, valList.Length - 2));

                cmd.CommandText = Sql;
                cmd.ExecuteNonQuery();
            }
        }

        private void DropAllColumns(DbCommand command, string quotedTableName)
        {
            string Sql = string.Empty;
            string colList = string.Empty;

            command.CommandText = "SELECT ColumnName FROM ExtColumns"; // WHERE TableName = {0}, quotedTableName

            var reader = command.ExecuteReader();
            while (reader.Read())
                colList += QuoteIdentifier((string)reader[0]) + ", ";

            Sql += string.Format("ALTER TABLE {0} DROP COLUMN {1};\n", quotedTableName, colList.Substring(0, colList.Length - 2));

            reader.Close();

            Sql += "DELETE FROM ExtColumns";

            command.CommandText = Sql;
            command.ExecuteNonQuery();
        }

        public void DropColumns(string[] columnNames, string tableName)
        {
            using (DbConnection cnn = GetConnection())
            {
                cnn.Open();

                string colList = string.Empty;
                string colVals = string.Empty;
                foreach (string columnName in columnNames)
                {
                    colList += string.Format("{0}, ", QuoteIdentifier(columnName));
                    colVals += string.Format("'{0}', ", columnName);
                }

                string Sql = string.Format("ALTER TABLE {0} DROP COLUMN {1};\n", QuoteIdentifier(tableName), colList.Substring(0, colList.Length - 2));
                Sql += string.Format("DELETE FROM ExtColumns WHERE ColumnName IN ({0})", colVals.Substring(0, colVals.Length - 2));

                DbCommand cmd = GetCommand(cnn, Sql);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Column> GetColumns()
        {
            List<Column> columns = new List<Column>();

            using (DbConnection cnn = GetConnection())
            {
                cnn.Open();
                
                DbCommand cmd = GetCommand(cnn, "SELECT ColumnName, DisplayName FROM ExtColumns");

                var reader = cmd.ExecuteReader();
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

    // ----------------

    public struct Column
    {
        public string name;
        public string displayName;
    }

    public struct ColumnDef
    {
        public string name;
        public string type;
        public string displayName;
        public bool allowDBNull;
        //public SqlDbType dbType;
    }


}
