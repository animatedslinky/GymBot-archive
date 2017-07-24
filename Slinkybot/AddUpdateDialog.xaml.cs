using System.Windows;

namespace Slinkybot
{
    /// <summary>
    /// Interaction logic for AddUpdateDialog.xaml
    /// </summary>
    public partial class AddUpdateDialog : Window
    {
        public GymLeader leader;

        
        public AddUpdateDialog()
        {
            InitializeComponent();
        }

        public AddUpdateDialog(GymLeader lead)
        {
            InitializeComponent();
            txtName.Text = lead.Name;
            cbogymType.SelectedIndex = (lead.gymType == GymType.Elite4) ? 0 : 1;
            txtGymUp.Text = lead.gymUpMessage;
            txtGymDown.Text = lead.gymDownMessage;
            txtName.IsEnabled = false;
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            leader = new GymLeader
            {
                Name = txtName.Text,
                Online = "Offline",
                gymType = cbogymType.SelectedValue.ToString().Equals("Elite 4") ? GymType.Elite4 : GymType.Gym,
                offlineCountdown = 0,
                gymUpMessage = txtGymUp.Text,
                gymDownMessage = txtGymDown.Text
            };
            this.DialogResult = true;
        }
    }
}
