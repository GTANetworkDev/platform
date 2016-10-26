using FirstFloor.ModernUI.Presentation;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using FirstFloor.ModernUI.App;
using System.IO;
using System.Xml.Serialization;
using GTANetworkShared;

namespace FirstFloor.ModernUI.App.Content
{
    /// <summary>
    /// Interaction logic for SettingsAppearance.xaml
    /// </summary>
    public partial class SettingsAppearance : UserControl
    {
        public SettingsAppearance()
        {
            InitializeComponent();
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }


        private void TextFirstName_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void Steam_Click(object sender, RoutedEventArgs e)
        {


        }

        private void TextRepository_TextChanged(object sender, TextChangedEventArgs e)
        {

            LauncherSettings.GameParams = TextRepository.Text.Split(' ');
        }
    }
}
