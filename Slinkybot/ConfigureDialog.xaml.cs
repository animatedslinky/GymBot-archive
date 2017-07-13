using System;
using System.Collections.Generic;
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
    /// Interaction logic for ConfigureDialog.xaml
    /// </summary>
    public partial class ConfigureDialog : Window
    {
        public ConfigureDialog()
        {
            InitializeComponent();
        }

        public ConfigureDialog(ConnectionConfig config)
        {
            InitializeComponent();
            txtUser.Text = config.username;
            txtoauth.Password = config.oauth;
            txtchannel.Text = config.channel;
        }
        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        public string UserName
        {
            get { return txtUser.Text; }
        }

        public string OAuth
        {
            get { return txtoauth.Password; }
        }

        public string Channel
        {
            get { return txtchannel.Text; }
        }
    }
}
