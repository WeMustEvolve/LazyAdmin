using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
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

            _store = new SQLiteStore(store_CreateSchema);
            _wpfProxy = new WPFProxy();
            itemsDataGrid.ItemsSource = _store.GetTable("Files").DefaultView;
        }

        void store_CreateSchema(object sender)
        {
            TableDef tableDef = new TableDef
            {
                name = "Attributes",
                displayName = "File Attributes",
                columns = new ColumnDef[] {
                    new ColumnDef {name = "Name", displayName = "Name", type = "NVARCHAR"},
                    new ColumnDef {name = "Value", displayName = "Value", type = "NVARCHAR"}
                }
            };

            SQLiteStore store = (SQLiteStore)sender;
            store.AddTable(tableDef);

            tableDef = new TableDef
            {
                name = "Files",
                displayName = "My Files",
                columns = new ColumnDef[] {
                    new ColumnDef {name = "FileName", displayName = "File name", type = "NVARCHAR"},
                    new ColumnDef {name = "FilePath", displayName = "Path", type = "NVARCHAR"},
                    new ColumnDef {name = "FileSize", displayName = "Size", type = "INT"},
                    new ColumnDef {name = "Attributes", displayName = "Attributes", type = "TABLE", parentTable = "Attributes"}
                }
            };

            store.AddTable(tableDef);
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
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnLoadList_Click(object sender, RoutedEventArgs e)
        {
            DataTable items = _store.GetTable("files");
            items.Rows.Clear();

            if (_store.SchemaVersion == 1)
            {
                _store.AddColumns(
                    "files",
                    new ColumnDef[] { 
                        new ColumnDef{ name = "state", displayName = "State", type = "TEXT", defValue = "'n/a'" },
                        new ColumnDef{ name = "image_size", displayName = "Image Size", type = "TEXT"},
                        new ColumnDef{ name = "pixel_format", displayName = "Pixel Format", type = "TEXT"}
                    },
                    2
                    );
            }

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

                    // !!! +Attributes
                    
                    _wpfProxy.Invoke((Action<DataRow>)((r1) => items.Rows.Add(r1)), row);

                    if (items.Rows.Count > 50)
                        break;

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
                                
                                _wpfProxy.Invoke((Action<DataRow>)((r1) => r1.AcceptChanges()), r);
                            }
                        };

                    Task.Factory.StartNew(action, row);
                }

            });
        }

        private void btnAddItem_Click(object sender, RoutedEventArgs e)
        {
            
        }

        
    }
}
