using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using OpenTyping.Properties;
using OpenTyping.Resources.Lang;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FamFamFam.Flags.Wpf;

namespace OpenTyping
{
    public partial class VeneziaMenu : UserControl
    {
        private List<VeneziaUser> localUsers;
        private readonly SqliteProvider Sqlite = new SqliteProvider();

        public VeneziaMenu()
        {
            InitializeComponent();
            SetTextBylanguage();

            if ((string)Settings.Default["Country"] == "")
            {
                string country = (string)Settings.Default["ProgramLang"];
                if ((string)Settings.Default["ProgramLang"] == "en")
                {
                    country = "gb";
                }
                else if ((string)Settings.Default["ProgramLang"] == "ko")
                {
                    country = "kr";
                }
                CountryPicker.SelectedItem = GetCountryData(country);
            }
            else
            {
                CountryPicker.SelectedItem = GetCountryData((string)Settings.Default["Country"]);
            }

            LoadDatabase();
        }

        private CountryData? GetCountryData(string countryName)
        {
            foreach (var country in CountryData.AllCountries)
            {
                if (country.Iso2.ToLower() == countryName)
                {
                    return country;
                }
            }
            return null;
        }

        public void SetTextBylanguage()
        {
            TBname.Text = (string)Settings.Default["Name"];
            TBorg.Text = (string)Settings.Default["Org"];

            MenuName.Text = LangStr.SamarkandTabHeader;
            MenuDesc.Text = LangStr.SamarkandDesc;
            StartBtn.Text = LangStr.SamarkandStart;

            Header1.Header = LangStr.Rank;
            Header2.Header = LangStr.Name;
            Header3.Header = LangStr.Org;
            Header15.Header = LangStr.Country;
            Header16.Header = LangStr.Keyboard;
            Header4.Header = LangStr.SamarkandStage;
            Header5.Header = LangStr.SamarkandScore;
            Header7.Header = LangStr.SamarkandDate;

            NotSupport.Text = LangStr.NotSupport;
        }

        private async void LoadDatabase()
        {
            try
            {
                if (await Sqlite.OpenDatabase())
                {
                    localUsers = await Sqlite.GetVeneziaUsersAsync();
                    localUsers.Sort();
                    LVlocal.ItemsSource = localUsers;
                }
            }
            catch (Exception ex)
            {
                App.logger.Error("Failed to load Venezia database: " + ex.ToString());
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default["Name"] = TBname.Text;
            Settings.Default["Org"] = TBorg.Text;
            if (CountryPicker.SelectedValue is CountryData country)
            {
                Settings.Default["Country"] = country.Iso2.ToLower();
            }

            VeneziaWindow veneziaWindow = new VeneziaWindow();
            veneziaWindow.Closed += new EventHandler(VeneziaWindow_Closed);
            veneziaWindow.ShowDialog();
        }

        private void VeneziaWindow_Closed(object sender, EventArgs e)
        {
            LoadDatabase(); // Refresh local high scores when game closes
        }

        private void Tab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                switch (((TabControl)sender).SelectedIndex)
                {
                    case 0:
                        RectLocal.Fill = new SolidColorBrush(Colors.RoyalBlue);
                        RectServer.Fill = new SolidColorBrush(Colors.LightGray);
                        break;
                    case 1:
                        RectServer.Fill = new SolidColorBrush(Colors.RoyalBlue);
                        RectLocal.Fill = new SolidColorBrush(Colors.LightGray);
                        break;
                }
            }
        }
    }
}
