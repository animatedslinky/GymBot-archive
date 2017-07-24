using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Shapes;

namespace Slinkybot
{
    /// <summary>
    /// Interaction logic for SelectLeaderDialog.xaml
    /// </summary>
    public partial class SelectLeaderDialog : Window
    {
        public string SelectedLeader
        {
            get
            {
                return cboLeaders.SelectedValue.ToString();
            }
        }

        public SelectLeaderDialog(ObservableCollection<GymLeader> gymLeaders,string title)
        {
            InitializeComponent();

            this.Title = title;

            foreach (GymLeader leader in gymLeaders)
            {
                cboLeaders.Items.Add(leader.Name);
            }
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
