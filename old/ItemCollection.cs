using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ManagerADO
{
    class DymItemCollection : ObservableCollection<DynItem>
    {
        StoreManager _manager;

        private Dictionary<string, int> _fields;

        public DymItemCollection()
            : base()
        {
            _fields = new Dictionary<string, int>();
            _fields.Add("Id", 0);
            _fields.Add("Name", 1);
            _fields.Add("StateInfo", 2);

            _manager = new StoreManager();
            foreach (var column in _manager.GetColumns())
                _fields.Add(column.name, _fields.Count);
        }

        public DynItem New()
        {
            return new DynItem(_fields);
        }

        public void ReadFromDB()
        {
            var f = from fld in _fields
                    orderby fld.Value
                    select fld.Key;
            string fieldNames = string.Join(",", f);

            using (SqlConnection connection = new SqlConnection(_manager.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(string.Format("SELECT {0} FROM Items", fieldNames), connection);
                SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    object[] values = new object[reader.FieldCount];
                    reader.GetValues(values);

                    DynItem di = new DynItem(_fields, values);
                    Add(di);
                }

                reader.Close();
            }

        }

        public void WriteToDB()
        {
            /*string fieldNames = "Name,StateInfo";
            foreach (var column in _manager.GetColumns())
            {
                fieldNames = string.Join(",", );
            }

            using (SqlConnection connection = new SqlConnection(_manager.ConnectionString))
            {
                connection.Open();

                String Sql = string.Empty;

                Sql += string.Format("UPDATE Items SET {0}='{1}' WHERE Id='{}';\n", column.name, column.displayName);
                Sql += string.Format("INSERT INTO ExtColumns ({0}) VALUES ('{0}','{1}');\n", fieldNames);

                SqlCommand command = new SqlCommand();
                command.Connection = connection;

                foreach (DynItem di in this)
                {

                }

                command.ExecuteNonQuery();
            }
            */
        }

        public override event NotifyCollectionChangedEventHandler CollectionChanged;
        protected override event PropertyChangedEventHandler PropertyChanged;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            using (BlockReentrancy())
            {
                if (e.NewItems != null)
                    foreach (DynItem item in e.NewItems)
                        item.PropertyChanged += DynItem_PropertyChanged;

                if (e.OldItems != null)
                    foreach (DynItem item in e.OldItems)
                        item.PropertyChanged -= DynItem_PropertyChanged;

                if (CollectionChanged != null)
                {
                    foreach (NotifyCollectionChangedEventHandler handler in CollectionChanged.GetInvocationList())
                    {
                        var dispatcherObject = handler.Target as DispatcherObject;
                        if (dispatcherObject != null && !dispatcherObject.CheckAccess())
                            dispatcherObject.Dispatcher.BeginInvoke(handler, DispatcherPriority.DataBind, this, e);
                        else
                            handler(this, e);
                    }
                }
            }
        }

        private void DynItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                foreach (PropertyChangedEventHandler handler in PropertyChanged.GetInvocationList())
                {
                    var dispatcherObject = handler.Target as DispatcherObject;
                    if (dispatcherObject != null && !dispatcherObject.CheckAccess())
                        dispatcherObject.Dispatcher.BeginInvoke(handler, DispatcherPriority.DataBind, this, e);
                    else
                        handler(this, e);
                }
            }
        }
    }
}
