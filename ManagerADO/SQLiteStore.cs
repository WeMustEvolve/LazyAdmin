using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerADO
{
    class SQLiteStore
    {
        private DataSet _dataSet;
        private string _connString = string.Empty;
        private SQLiteCommandBuilder _builder;
        private Dictionary<string, SQLiteDataAdapter> _adapters;
        private int _schemaVersion = 1;
        private Dictionary<string, TableDef> _tableDefs;

        public SQLiteStore(CreateSchemaHandler createSchemaHandler)
        {
            var cnStrings = ConfigurationManager.ConnectionStrings["StoreSQLiteProvider"];
            _connString = cnStrings.ConnectionString;
            CreateSchema += createSchemaHandler;
            Init(cnStrings.ProviderName);
        }

        public SQLiteStore(string providerName, string connectionString)
        {
            _connString = connectionString;
            Init(providerName);
        }

        private void Init(string providerName)
        {
            _dataSet = new DataSet("Store");
            _adapters = new Dictionary<string, SQLiteDataAdapter>(StringComparer.CurrentCultureIgnoreCase);

            using (SQLiteConnection cnn = OpenConnection())
            {
                CheckSchema(cnn);

                LoadTableDefs(cnn);
                
                foreach (TableDef tableDef in _tableDefs.Values)
                    ConfigureAdapter(tableDef);

                //BuildTableRelationship();
            }
        }

        private void LoadTableDefs(SQLiteConnection cnn)
        {
            _tableDefs = new Dictionary<string, TableDef>(StringComparer.CurrentCultureIgnoreCase);

            SQLiteCommand cmd = GetCommand(cnn, "SELECT Id, Name, DisplayName FROM TableDefs");
            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                TableDef tableDef = new TableDef
                {
                    id = reader.GetInt32(0),
                    name = reader.GetString(1),
                    displayName = reader.GetString(2)
                };

                _tableDefs.Add(tableDef.name, tableDef);
            }
            reader.Close();

            SQLiteParameter idParam = new SQLiteParameter();

            cmd.CommandText = "SELECT Id, Name, DisplayName, Type FROM ColumnDefs WHERE TableId = ?";
            cmd.Parameters.Add(idParam);
            cmd.Prepare();
            foreach (TableDef tableDef in _tableDefs.Values)
            {
                List<ColumnDef> colDefs = new List<ColumnDef>();

                idParam.Value = tableDef.id;
                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    ColumnDef colDef = new ColumnDef()
                    {
                        id = reader.GetInt32(0),
                        name = reader.GetString(1),
                        displayName = reader.GetString(2),
                        type = reader.GetString(3)
                    };

                    colDefs.Add(colDef);
                }

                reader.Close();

                tableDef.columns = colDefs.ToArray();
            }
        }

        public int SchemaVersion
        {
            get { return _schemaVersion; }
        }

        public delegate void CreateSchemaHandler(object sender);
        public event CreateSchemaHandler CreateSchema;

        protected virtual void RaiseCreateSchema()
        {
            if (CreateSchema != null)
                CreateSchema(this);
        }

        private void CheckSchema(SQLiteConnection cnn)
        {
            SQLiteCommand cmd = GetCommand(cnn, "SELECT Version FROM Store");
            object version = null;
            
            try
            {
                version = cmd.ExecuteScalar();
            }
            catch { }

            if (version == null)
            {
                // create db schema

                // common schema
                string sql = string.Format(@"
CREATE TABLE Store (Version INT);
INSERT INTO Store VALUES ('{0}');", _schemaVersion);

                sql += @"
CREATE TABLE TableDefs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name NVARCHAR NOT NULL,
    DisplayName NVARCHAR
);
CREATE TABLE ColumnDefs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name NVARCHAR NOT NULL,
    Type NVARCHAR,
    DisplayName NVARCHAR,
    TableId INT NOT NULL,
    FOREIGN KEY(TableId) REFERENCES TableDefs(Id)
);
CREATE INDEX Name_Idx ON ColumnDefs (Name)";

                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();

                //SQLiteTransaction trn = cnn.BeginTransaction();

                // notify user to expand schema
                RaiseCreateSchema();
                
                //trn.Commit(); // auto rollback on exception? 
            }
        }

        private void ConfigureAdapter(TableDef tableDef)
        {
            SQLiteDataAdapter adapter;

            if (!_adapters.TryGetValue(tableDef.name, out adapter))
            {
                SQLiteCommandBuilder builder = new SQLiteCommandBuilder();

                // save default builder to quote identifiers
                if (_builder == null)
                    _builder = builder;

                adapter = new SQLiteDataAdapter(string.Format("SELECT * FROM {0}", QuoteIdentifier(tableDef.name)), _connString);
                adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;

                builder.DataAdapter = adapter;

                _adapters.Add(tableDef.name, adapter);
            }

            adapter.Fill(_dataSet, tableDef.name);
        }

        public DataTable GetTable(string tableName)
        {
            return _dataSet.Tables[tableName];
        }

        public SQLiteConnection OpenConnection()
        {
            SQLiteConnection cnn = new SQLiteConnection(_connString);
            cnn.Open();

            return cnn;
        }

        public SQLiteCommand GetCommand(SQLiteConnection connection, string commandText = "")
        {
            return new SQLiteCommand(commandText, connection);
        }

        public void ExecuteNonQuery(SQLiteConnection connection, string commandText)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(commandText, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public string QuoteIdentifier(string identifier)
        {
            if (_builder == null)
                _builder = new SQLiteCommandBuilder();
            return _builder.QuoteIdentifier(identifier);
        }

        public void Update()
        {
            foreach(var kv in _adapters)
                kv.Value.Update(_dataSet, kv.Key);
        }

        public void Update(string tableName)
        {
            SQLiteDataAdapter adapter;

            if (!_adapters.TryGetValue(tableName, out adapter))
                throw new ArgumentException("Unknown table: " + tableName);

            adapter.Update(_dataSet, tableName);
        }

        public void ClearTable(string tableName)
        {
            using (SQLiteConnection cnn = OpenConnection())
            {
                string sql = string.Format("DELETE FROM {0}", QuoteIdentifier(tableName));
                ExecuteNonQuery(cnn, sql);

                // clear in-memory table
                _dataSet.Tables[tableName].Clear();
            }
        }

        // Manager

        /*public TableDef[] TableDefs
        {
            get { return _tableDefs.ToArray(); }
        }*/

        public TableDef GetTableDef(string tableName)
        {
            return _tableDefs[tableName];
        }

        public void AddTable(TableDef tableDef, int newSchemaVersion = 0)
        {
            if (newSchemaVersion != 0 && _schemaVersion >= newSchemaVersion)
                throw new ArgumentException("New schema version should be greater then current");

            string quotedTableName = QuoteIdentifier(tableDef.name);

            using (SQLiteConnection cnn = OpenConnection())
            {
                string sql = string.Empty;

                sql = string.Format("INSERT INTO TableDefs (Name, DisplayName) VALUES ('{0}', '{1}');\n", tableDef.name, tableDef.displayName);
                string colList = "Id INTEGER PRIMARY KEY AUTOINCREMENT, ";
                string valList = string.Format("('Id','INTEGER','Id',(SELECT Id FROM TableDefs WHERE Name = '{0}')), ", tableDef.name);

                foreach (ColumnDef column in tableDef.columns)
                {
                    if (column.type.StartsWith("TABLE")) // TABLE parentTable
                    {
                        colList += string.Format("{0} INT {1}, FOREIGN KEY({0}) REFERENCES 2}(Id), ",
                            QuoteIdentifier(column.name),
                            (column.allowDBNull) ? "NULL" : string.Empty,
                            QuoteIdentifier(column.parentTable)
                            );
                    }
                    colList += string.Format("{0} {1} {2} {3}, ",
                        QuoteIdentifier(column.name),
                        column.type,
                        (column.allowDBNull) ? "NULL" : string.Empty,
                        (column.defValue != null) ? "DEFAULT " + column.defValue : string.Empty
                        );

                    valList += string.Format("('{0}','{1}','{2}',(SELECT Id FROM TableDefs WHERE Name = '{3}')), ",
                        column.name, column.type, column.displayName, tableDef.name);
                }

                sql += string.Format("INSERT INTO ColumnDefs (Name, Type, DisplayName, TableId) VALUES {0};\n", valList.Remove(valList.Length - 2));
                sql += string.Format("CREATE TABLE {0} ({1});\n", quotedTableName, colList.Remove(colList.Length - 2));

                if (newSchemaVersion != 0)
                    sql += string.Format("UPDATE Store SET Version = '{0}';", newSchemaVersion);

                ExecuteNonQuery(cnn, sql);
            }

            if (newSchemaVersion != 0)
                _schemaVersion = newSchemaVersion;
        }


        public void AddColumns(string tableName, ColumnDef[] newColumns, int newSchemaVersion)
        {
            if (_schemaVersion >= newSchemaVersion)
                throw new ArgumentException("New schema version should be greater then current");

            string quotedTableName = QuoteIdentifier(tableName);

            using (SQLiteConnection cnn = OpenConnection())
            {
                string sql = string.Empty;
                string colList = string.Empty;
                string valList = string.Empty;

                foreach (ColumnDef column in newColumns)
                {
                    if (column.type.StartsWith("TABLE")) // TABLE parentTable
                    {
                        colList += string.Format("ALTER TABLE {0} ADD {1} INT {2}, FOREIGN KEY({1}) REFERENCES {3}(Id), ",
                            quotedTableName,
                            QuoteIdentifier(column.name),
                            (column.allowDBNull) ? "NULL" : string.Empty,
                            QuoteIdentifier(column.parentTable)
                            );
                    }
                    else
                        sql += string.Format("ALTER TABLE {0} ADD {1} {2} {3} {4};\n",
                            quotedTableName,
                            QuoteIdentifier(column.name),
                            column.type,
                            (column.allowDBNull) ? "NULL" : string.Empty,
                            (column.defValue != null) ? "DEFAULT " + column.defValue : string.Empty
                            );

                    valList += string.Format("('{0}','{1}','{2}',(SELECT Id FROM TableDefs WHERE Name = '{3}')), ",
                        column.name, column.type, column.displayName, tableName);
                }

                sql += string.Format("INSERT INTO ColumnDefs (Name, Type, DisplayName, TableId) VALUES {0};\n", valList.Remove(valList.Length - 2));
                sql += string.Format("UPDATE Store SET Version = '{0}';", newSchemaVersion);

                ExecuteNonQuery(cnn, sql);

                _schemaVersion = newSchemaVersion;
            }
        }

        /*private void DropAllColumns(SQLiteCommand command, string quotedTableName)
        {
            string colList = string.Empty;

            command.CommandText = string.Format("SELECT Name FROM ColumnDefs WHERE TableId = (SELECT Id FROM TableDefs WHERE Name = '{0}')", 
                quotedTableName);

            var reader = command.ExecuteReader();
            while (reader.Read())
                colList += QuoteIdentifier((string)reader[0]) + ", ";

            string Sql = string.Format("ALTER TABLE {0} DROP COLUMN {1};\n", quotedTableName, colList.Remove(colList.Length - 2));
            Sql += string.Format("DELETE FROM ColumnDefs WHERE TableId = (SELECT Id FROM TableDefs WHERE Name = '{0}')", quotedTableName);

            reader.Close();

            command.CommandText = Sql;
            command.ExecuteNonQuery();
        }*/

        /*public void DropColumns(string[] columnNames, string tableName, int newSchemaVersion)
        {
            using (SQLiteConnection cnn = OpenConnection())
            {
                string colList = string.Empty;
                string colVals = string.Empty;
                foreach (string columnName in columnNames)
                {
                    colList += string.Format("{0}, ", QuoteIdentifier(columnName));
                    colVals += string.Format("'{0}', ", columnName);
                }

                string quotedTableName = QuoteIdentifier(tableName);
                string sql = string.Format("ALTER TABLE {0} DROP COLUMN {1};\n", quotedTableName, colList.Remove(colList.Length - 2));
                sql += string.Format("DELETE FROM ColumnDefs WHERE Name IN ({0}) AND TableId = (SELECT Id FROM TableDefs WHERE Name = '{1}')",
                    colVals.Remove(colVals.Length - 2), quotedTableName);
                sql += string.Format("UPDATE Store SET Version = '{0}';", newSchemaVersion);

                ExecuteNonQuery(cnn, sql);
                _schemaVersion = newSchemaVersion;
            }
        }*/
    }

    // ----------------

    public class ColumnDef
    {
        public int id;
        public string name;
        public string displayName;
        public string type;
        public string parentTable;
        public bool allowDBNull;
        public string defValue;
    }

    public class TableDef
    {
        public int id;
        public string name;
        public string displayName;
        public ColumnDef[] columns;
    }
}
