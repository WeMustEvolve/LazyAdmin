using System;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ManagerADO
{
    class WPFProxy
    {
        private Dispatcher _appDispathcer;

        public WPFProxy()
        {
            _appDispathcer = Application.Current.Dispatcher;
        }

        public void Invoke(Delegate method, params object[] args)
        {
            _appDispathcer.BeginInvoke(method, DispatcherPriority.DataBind, args);
        }
    }

    class TableStore : DataSet
    {
        private DataSet storeDS = new DataSet("Store");

        private string cnString = string.Empty;

        private SqlDataAdapter itemsAdapter = null;
        private SqlDataAdapter propertiesAdapter = null;

        public TableStore()
        {
            var cnStrings = ConfigurationManager.ConnectionStrings["StoreSqlProvider"];
            cnString = cnStrings.ConnectionString;
            Init();
        }

        public TableStore(string connectionString)
        {
            cnString = connectionString;
            Init();
        }

        private void Init()
        {
            ConfigureAdapter(ref itemsAdapter, "Items");
            //ConfigureAdapter(ref propertiesAdapter, "Properties");
            //BuildTableRelationship();
        }

        private void ConfigureAdapter(ref SqlDataAdapter dAdapt, string tableName)
        {
            //if (!storeDS.Tables.Contains(tableName))
            //    storeDS.Tables.Add(new DataTableEx(tableName));

            if (dAdapt == null)
            {
                dAdapt = new SqlDataAdapter(string.Format("Select * From [{0}]", tableName), cnString);
                dAdapt.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                var b = new SqlCommandBuilder(dAdapt);
            }

            dAdapt.Fill(storeDS, tableName);
        }

        private void BuildTableRelationship()
        {
            DataRelation dr = new DataRelation("ItemProperty",
                storeDS.Tables["Items"].Columns["Id"],
                storeDS.Tables["Properties"].Columns["ItemId"]);

            storeDS.Relations.Add(dr);
        }

        public DataTable Items
        {
            get { return storeDS.Tables["Items"]; }
        }

        public DataTable Properties
        {
            get { return storeDS.Tables["Properties"]; }
        }

        public void Update()
        {
            itemsAdapter.Update(storeDS, "Items");
            propertiesAdapter.Update(storeDS, "Properties");
        }

        public void Update(string tableName)
        {
            if (tableName == "Items")
                itemsAdapter.Update(storeDS, tableName);
            else if (tableName == "Properties")
                propertiesAdapter.Update(storeDS, tableName);
        }

        public void ClearTable(string tableName)
        {
            using (SqlConnection connection = new SqlConnection(cnString))
            {
                connection.Open();

                string sql = string.Format("DELETE FROM [{0}]", tableName);
                SqlCommand command = new SqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }

            storeDS.Tables[tableName].Clear();
        }

        public void Reload()
        {
            /*storeDS.Relations.Clear();
            foreach (DataTable table in storeDS.Tables)
                table.Constraints.Clear();// !!! dependant constraints
            storeDS.Tables.Clear();*/

            storeDS = new DataSet("Store");

            Init();
        }
    }

    public class DataTableExt : DataTable
    {
        public DataTableExt(): base() { }
        public DataTableExt(string tableName) : base(tableName) { }
    }
}

