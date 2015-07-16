using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
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
        private int _schemaVersion = 0;
        private Dictionary<string, TableDef> _tableDefs;

        public SQLiteStore(ChangeSchemaHandler createSchemaHandler, int validSchemaVersion)
        {
            var cnStrings = ConfigurationManager.ConnectionStrings["StoreSQLiteProvider"];
            _connString = cnStrings.ConnectionString;
            ChangeSchema += createSchemaHandler;
            Init(cnStrings.ProviderName, validSchemaVersion);
        }

        public SQLiteStore(string providerName, string connectionString)
        {
            _connString = connectionString;
            Init(providerName, 1);
        }

        private void Init(string providerName, int validSchemaVersion)
        {
            _dataSet = new DataSet("Store");
            _adapters = new Dictionary<string, SQLiteDataAdapter>(StringComparer.CurrentCultureIgnoreCase);

            using (SQLiteConnection cnn = OpenConnection())
            {
                CheckSchema(cnn, validSchemaVersion);

                LoadTableDefs(cnn);
                
                foreach (TableDef tableDef in _tableDefs.Values)
                    ConfigureAdapter(tableDef);

                BuildTableRelationship();
            }
        }

        private void LoadTableDefs(SQLiteConnection cnn)
        {
            if (_tableDefs == null)
                _tableDefs = new Dictionary<string, TableDef>(StringComparer.CurrentCultureIgnoreCase);
            else
                _tableDefs.Clear();

            // read table defs

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

            // read column defs

            cmd.CommandText = "SELECT Id, Name, DisplayName, Type, ParentTable FROM ColumnDefs WHERE TableId = ?";
            SQLiteParameter idParam = new SQLiteParameter();
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
                        type = reader.GetString(3),
                        parentTable = reader.GetString(4),
                        tableName = tableDef.name
                    };

                    colDefs.Add(colDef);
                }

                reader.Close();

                tableDef.columns = colDefs.ToArray();
            }
        }

        private void BuildTableRelationship()
        {
            foreach (TableDef tableDef in _tableDefs.Values)
                foreach (ColumnDef colDef in tableDef.columns)
                    if (colDef.IsChildTable)
                    {
                        DataColumn parentCol = _dataSet.Tables[colDef.parentTable].Columns["Id"];
                        DataColumn childCol = _dataSet.Tables[tableDef.name].Columns[colDef.name];

                        _dataSet.Relations.Add(colDef.RelationName, parentCol, childCol);
                    }
        }

        public int SchemaVersion
        {
            get { return _schemaVersion; }
        }

        public delegate void ChangeSchemaHandler(object sender, SQLiteConnection cnn, int currentVersion);
        public event ChangeSchemaHandler ChangeSchema;

        protected virtual void RaiseChangeSchema(SQLiteConnection cnn, int currentVersion)
        {
            if (ChangeSchema != null)
                ChangeSchema(this, cnn, currentVersion);
        }

        private void CheckSchema(SQLiteConnection cnn, int validSchemaVersion)
        {
            SQLiteCommand cmd = GetCommand(cnn, "SELECT Version FROM Store");
            int version = 0;

            try
            {
                version = (int)cmd.ExecuteScalar();
            }
            catch { }

            SQLiteTransaction trn;

            if (version == 0)
            {
                // create db schema

                // common schema
                string sql = string.Format(@"
CREATE TABLE Store (Version INT);
INSERT INTO Store VALUES ('{0}');", validSchemaVersion);

                sql += @"
CREATE TABLE TableDefs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name NVARCHAR NOT NULL,
    DisplayName NVARCHAR DEFAULT ''
);
CREATE TABLE ColumnDefs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name NVARCHAR NOT NULL,
    Type NVARCHAR,
    DisplayName NVARCHAR DEFAULT '',
    TableId INT NOT NULL,
    ParentTable NVARCHAR NOT NULL,
    FOREIGN KEY(TableId) REFERENCES TableDefs(Id)
);
CREATE INDEX Name_Idx ON ColumnDefs (Name)";

                trn = cnn.BeginTransaction(); 
                
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();

                    // notify user to expand schema
                    RaiseChangeSchema(cnn, 0);
                    trn.Commit();

                    _schemaVersion = validSchemaVersion;
                }
                catch
                {
                    trn.Rollback();
                    _schemaVersion = 0;
                    throw;
                }
            }
            else if ((int)version < validSchemaVersion)
            {
                trn = cnn.BeginTransaction();
                
                try
                {
                    // notify to change schema
                    RaiseChangeSchema(cnn, version);

                    ExecuteNonQuery(cnn, string.Format("UPDATE Store SET Version = '{0}';", validSchemaVersion));
                    _schemaVersion = validSchemaVersion;

                    trn.Commit();
                }
                catch
                {
                    trn.Rollback();
                    _schemaVersion = 0;
                    throw;
                }
            }
            else
                _schemaVersion = version;
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

                // to update primary key
                //adapter.InsertCommand = builder.GetInsertCommand();
                //adapter.InsertCommand.CommandText = adapter.InsertCommand.CommandText + "; SELECT last_insert_rowid() AS Id";
                //adapter.InsertCommand.UpdatedRowSource = UpdateRowSource.Both;
                
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
            //TODO: update order (parent-child relations) 
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

        // Schema Manager

        public TableDef GetTableDef(string tableName)
        {
            return _tableDefs[tableName];
        }

        // Add new table
        // cnn specified by CheckSchema create/update schema while Store object creating
        // specify newSchemaVersion after Store object created
        public void AddTable(SQLiteConnection cnn, TableDef tableDef, int newSchemaVersion = 0)
        {
            if (newSchemaVersion != 0 && _schemaVersion >= newSchemaVersion)
                throw new ArgumentException("New schema version should be greater then current");

            string quotedTableName = QuoteIdentifier(tableDef.name);
            
            string sql = string.Format("INSERT INTO TableDefs (Name, DisplayName) VALUES ('{0}', '{1}');\n", tableDef.name, tableDef.displayName);
            string colList = "Id INTEGER PRIMARY KEY AUTOINCREMENT, ";
            string valList = string.Format("('Id','INTEGER','Id','',(SELECT Id FROM TableDefs WHERE Name = '{0}')), ", tableDef.name);

            foreach (ColumnDef column in tableDef.columns)
            {
                if (column.type.StartsWith("TABLE")) // TABLE parentTable
                    colList += string.Format("{0} INTEGER {1}, FOREIGN KEY({0}) REFERENCES {2}(Id), ",
                        QuoteIdentifier(column.name),
                        (column.allowDBNull) ? string.Empty : "NOT NULL",
                        QuoteIdentifier(column.parentTable)
                    );
                else
                    colList += string.Format("{0} {1} {2} {3}, ",
                        QuoteIdentifier(column.name),
                        column.type,
                        (column.allowDBNull) ? string.Empty : "NOT NULL",
                        (column.defValue != null) ? "DEFAULT " + column.defValue : string.Empty
                    );

                valList += string.Format("('{0}','{1}','{2}','{3}',(SELECT Id FROM TableDefs WHERE Name = '{4}')), ",
                    column.name, column.type, column.displayName, column.parentTable, tableDef.name);
            }

            sql += string.Format("INSERT INTO ColumnDefs (Name,Type,DisplayName,ParentTable,TableId) VALUES {0};\n", valList.Remove(valList.Length - 2));
            sql += string.Format("CREATE TABLE {0} ({1});\n", quotedTableName, colList.Remove(colList.Length - 2));

            if (newSchemaVersion != 0 && _schemaVersion != newSchemaVersion)
                sql += string.Format("UPDATE Store SET Version = '{0}';", newSchemaVersion);

            ExecuteNonQuery(cnn, sql);

            // update _tableDefs if schema loaded
            if (_schemaVersion > 0)
            {
                _tableDefs.Add(tableDef.name, tableDef);
                ConfigureAdapter(tableDef);
            }

            if (newSchemaVersion != 0)
                _schemaVersion = newSchemaVersion;
        }

        public void AddColumns(SQLiteConnection cnn, string tableName, ColumnDef[] newColumns, int newSchemaVersion = 0)
        {
            if (_schemaVersion != 0 && _schemaVersion >= newSchemaVersion)
                throw new ArgumentException("New schema version should be greater then current");

            string quotedTableName = QuoteIdentifier(tableName);

            string sql = string.Empty;
            string colList = string.Empty;
            string valList = string.Empty;

            foreach (ColumnDef column in newColumns)
            {
                if (column.type.StartsWith("TABLE")) // TABLE parentTable
                    colList += string.Format("ALTER TABLE {0} ADD {1} INTEGER {2}, FOREIGN KEY({1}) REFERENCES {3}(Id), ",
                        quotedTableName,
                        QuoteIdentifier(column.name),
                        (column.allowDBNull) ? string.Empty : "NOT NULL",
                        QuoteIdentifier(column.parentTable)
                    );
                else
                    sql += string.Format("ALTER TABLE {0} ADD {1} {2} {3} {4};\n",
                        quotedTableName,
                        QuoteIdentifier(column.name),
                        column.type,
                        (column.allowDBNull) ? string.Empty : "NOT NULL",
                        (column.defValue != null) ? "DEFAULT " + column.defValue : string.Empty
                    );

                valList += string.Format("('{0}','{1}','{2}','{3}',(SELECT Id FROM TableDefs WHERE Name = '{4}')), ",
                    column.name, column.type, column.displayName, column.parentTable, tableName);
            }

            sql += string.Format("INSERT INTO ColumnDefs (Name,Type,DisplayName,ParentTable,TableId) VALUES {0};\n", valList.Remove(valList.Length - 2));
            
            if (newSchemaVersion != 0 && _schemaVersion != newSchemaVersion)
                sql += string.Format("UPDATE Store SET Version = '{0}';", newSchemaVersion);

            ExecuteNonQuery(cnn, sql);

            // update _tableDefs if schema already loaded
            if (_schemaVersion > 0)
            {
                LoadTableDefs(cnn);

                _adapters.Clear();
                foreach (TableDef tableDef in _tableDefs.Values)
                    ConfigureAdapter(tableDef);
            }

            if (newSchemaVersion != 0)
                _schemaVersion = newSchemaVersion;
        }

        public void DropTable(SQLiteConnection cnn, string tableName)
        {
            string sql = string.Format(@"
DELETE FROM ColumnDefs WHERE TableId = (SELECT Id FROM TableDefs WHERE Name = '{0}');
DELETE FROM TableDefs WHERE Name = '{0}';
DROP TABLE {1};", 
                tableName, QuoteIdentifier(tableName));

            ExecuteNonQuery(cnn, sql);
        }

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

    //TODO: separate in two classes ('In' and 'Out' Defs)
    public class ColumnDef
    {
        // IN & OUT
        public int id;
        public string name;
        public string displayName;
        public string type;
        public string parentTable = "";
        public bool allowDBNull;
        public string defValue;

        // OUT
        public string tableName;
        public bool IsChildTable 
        {
            get { return parentTable != ""; }
        }

        private string _relName = "";
        public string RelationName
        {
            get 
            { 
                if (_relName == "" && IsChildTable)
                    _relName = string.Format("{0}-{1}", parentTable, tableName);

                return _relName;
            }
        }
    }

    public class TableDef
    {
        public int id;
        public string name;
        public string displayName;
        public ColumnDef[] columns;

        public bool HasChildTables
        {
            get
            {
                foreach (ColumnDef column in columns)
                    if (column.IsChildTable)
                        return true;

                return false;
            }
        }
    }
}
