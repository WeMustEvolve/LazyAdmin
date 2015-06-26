using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
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

namespace Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private LazyAdminEntities context = new LazyAdminEntities(); 

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            context.Items.Load();

            var itemsViewSource = (System.Windows.Data.CollectionViewSource)this.FindResource("itemsViewSource");
            itemsViewSource.Source = context.Items.Local;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {

            foreach (var property in context.Properties.Local.ToList())
                if (property.Item == null)
                    context.Properties.Remove(property);

            context.SaveChanges();

            // Refresh the grids so the database generated values show up. 
            //this.globalListDataGrid.Items.Refresh();
            this.itemsDataGrid.Items.Refresh();
            this.propertiesDataGrid.Items.Refresh();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            this.context.Dispose();
        }

    }

}
