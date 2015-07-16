using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ManagerADO
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SQLiteStore _store;
        WPFProxy _wpfProxy;

        public MainWindow()
        {
            InitializeComponent();

            _store = new SQLiteStore(store_CreateSchema, 4);
            _wpfProxy = new WPFProxy();
            itemsDataGrid.ItemsSource = _store.GetTable("Files").DefaultView;
        }

        void store_CreateSchema(object sender, SQLiteConnection cnn, int actualVersion)
        {
            // cnn connection running in transaction

            SQLiteStore store = (SQLiteStore)sender;

            if (actualVersion <= 2)
            {
                // expand new schema

                if (actualVersion == 2)
                {
                    store.DropTable(cnn, "Attributes");
                    store.DropTable(cnn, "Files");
                }

                TableDef tableDef = new TableDef
                {
                    name = "Attributes",
                    displayName = "File Attributes",
                    columns = new ColumnDef[] {
                        new ColumnDef {name = "Name", displayName = "Name", type = "NVARCHAR"},
                        new ColumnDef {name = "Value", displayName = "Value", type = "NVARCHAR"},
                        new ColumnDef {name = "FileId", displayName = "FileId", type = "TABLE", parentTable = "Files"}
                    }                    
                };

                store.AddTable(cnn, tableDef);

                tableDef = new TableDef
                {
                    name = "Files",
                    displayName = "My Files",
                    columns = new ColumnDef[] {
                        new ColumnDef {name = "FileName", displayName = "File name", type = "NVARCHAR"},
                        new ColumnDef {name = "FilePath", displayName = "Path", type = "NVARCHAR"},
                        new ColumnDef {name = "FileSize", displayName = "Size", type = "INT"}
                    }
                };

                store.AddTable(cnn, tableDef);
            }
            else if (actualVersion == 3)
            {
                ColumnDef[] newCols = new ColumnDef[] { 
                    new ColumnDef{ name = "state", displayName = "State", type = "TEXT", defValue = "'n/a'"},
                    new ColumnDef{ name = "image_size", displayName = "Image Size", type = "TEXT", defValue = "''"},
                    new ColumnDef{ name = "pixel_format", displayName = "Pixel Format", type = "TEXT", defValue = "''"}
                };

                store.AddColumns(cnn, "Files", newCols);
            }
        }

        private void UpdateViewGrid()
        {
            foreach(ColumnDef columnDef in _store.GetTableDef("Files").columns)
            {
                var binding = new Binding(string.Format("[{0}]", columnDef.name));

                var dgColumn = new DataGridTextColumn();
                dgColumn.Header = columnDef.displayName;
                dgColumn.Binding = binding;
                dgColumn.Width = DataGridLength.SizeToHeader;

                itemsDataGrid.Columns.Add(dgColumn);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateViewGrid();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _store.Update("Files");
                _store.Update("Attributes");
                MessageBox.Show("Done");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnLoadList_Click(object sender, RoutedEventArgs e)
        {
            DataTable items = _store.GetTable("Files");
            foreach(DataRow row in items.Rows)
                row.Delete();
            int fileCnt = items.Rows.Count;

            DataTable attribs = _store.GetTable("Attributes");
            foreach (DataRow row in attribs.Rows)
                row.Delete();

            _store.Update("Attributes");
            _store.Update("Files");

            List<Task> tasks = new List<Task>();

            Random rnd = new Random();
            Task mainTask = Task.Factory.StartNew(() =>
            {
                foreach (string fileName in Directory.EnumerateFiles(@"D:\Andy\Art", "*.jpg", SearchOption.AllDirectories))
                {
                    Thread.Sleep(rnd.Next(2, 5) * 100);
                    FileInfo info = new FileInfo(fileName);

                    DataRow row = items.NewRow();
                    row["FileName"] = info.Name;
                    row["filepath"] = info.DirectoryName;
                    row["FileSize"] = info.Length;
                    row["image_size"] = "";
                    row["pixel_format"] = "";
                    row["State"] = "";

                    _wpfProxy.Invoke((Action<DataRow, FileAttributes>)((r1, attributes) => 
                        {
                            items.Rows.Add(r1);
                            //filesAdapter.Update(new DataRow[] {r1}); // store row and get Id

                            foreach (int attrVal in Enum.GetValues(typeof(FileAttributes)))
                                if (((FileAttributes)attrVal & attributes) != 0)
                                {
                                    DataRow attrRow = attribs.NewRow();
                                    attrRow["Name"] = ((FileAttributes)attrVal).ToString();
                                    attrRow["Value"] = 1;
                                    attrRow["FileId"] = r1["Id"];

                                    attribs.Rows.Add(attrRow);
                                }

                        }), row, info.Attributes);

                    Action<object> action = (obj) =>
                        {
                            Thread.Sleep(rnd.Next(3, 10) * 100);

                            DataRow r = (DataRow)obj;
                            string fileName1 = System.IO.Path.Combine((string)r["filepath"], (string)r["FileName"]);
                            using (var image = new System.Drawing.Bitmap(fileName1))
                            {
                                r.BeginEdit();
                                r["image_size"] = image.Size.ToString();
                                r["pixel_format"] = image.PixelFormat.ToString();
                                r["State"] = "Done";

                                _wpfProxy.Invoke((Action<DataRow>)((r1) => r1.EndEdit()), r);
                            }
                        };

                    tasks.Add(Task.Factory.StartNew(action, row));

                    if (items.Rows.Count - fileCnt > 20)
                        break;
                }
            });
        }

        private void btnAddItem_Click(object sender, RoutedEventArgs e)
        {
            DataTable items = _store.GetTable("Files");
            items.Clear();

            DataRow row = items.NewRow();
            row["FileName"] = "Name1";
            row["filepath"] = "DirectoryName1";
            row["FileSize"] = 1;
            row["image_size"] = "";
            row["pixel_format"] = "";
            row["State"] = "";

            items.Rows.Add(row);

            _store.Update("Files");
            long id = (long)row["Id"];
        }

        
    }
}
