using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FamFamFam.Flags.Wpf;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using OpenTyping.Properties;
using OpenTyping.Resources.Lang;

namespace OpenTyping
{
    public partial class VeneziaMenu : UserControl
    {
        private List<VeneziaUser> localUsers;
        private readonly SqliteProvider Sqlite = new SqliteProvider();

        public List<CountryData> CustomCountries { get; }

        public VeneziaMenu()
        {
            CustomCountries = new List<CountryData>(CountryData.AllCountries);
            try
            {
                CustomCountries.Add(CreateCountryData("kaa", "Qaraqalpaqstan"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add custom country: {ex}");
            }

            InitializeComponent();
            SetTextBylanguage();

            this.Loaded += VeneziaMenu_Loaded;

            LoadDatabase();
        }

        private void VeneziaMenu_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDefaultCountry();
        }

        public void UpdateDefaultCountry()
        {
            string currentLayoutChar = MainWindow.CurrentKeyLayout.Character;
            string recommendedCountry = "";

            if (currentLayoutChar == "KAA")
            {
                recommendedCountry = "kaa";
            }
            else
            {
                recommendedCountry = "uz";
            }

            string currentSelectedCode = "";
            if (CountryPicker.SelectedItem is CountryData currentCountry)
            {
                currentSelectedCode = currentCountry.Iso2.ToLower();
            }
            else
            {
                currentSelectedCode = ((string)Settings.Default["Country"]).ToLower();
            }

            var defaultRecommendedCountries = new List<string> { "uz", "kaa", "kr", "gb", "" };

            if (defaultRecommendedCountries.Contains(currentSelectedCode))
            {
                CountryPicker.SelectedItem = GetCountryData(recommendedCountry);
            }
        }

        private CountryData CreateCountryData(string iso2, string name)
        {
            var ctor = typeof(CountryData).GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            if (ctor != null)
            {
                var parameters = ctor.GetParameters();
                if (parameters[0].Name.ToLower().Contains("name"))
                {
                    return (CountryData)ctor.Invoke(new object[] { name, iso2 });
                }
                else
                {
                    return (CountryData)ctor.Invoke(new object[] { iso2, name });
                }
            }

            var country = (CountryData)Activator.CreateInstance(typeof(CountryData));

            var iso2Prop = typeof(CountryData).GetProperty("Iso2");
            if (iso2Prop != null && iso2Prop.CanWrite)
            {
                iso2Prop.SetValue(country, iso2, null);
            }
            else
            {
                var field = typeof(CountryData).GetField("<Iso2>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null) field.SetValue(country, iso2);
            }

            var nameProp = typeof(CountryData).GetProperty("Name");
            if (nameProp != null && nameProp.CanWrite)
            {
                nameProp.SetValue(country, name, null);
            }
            else
            {
                var field = typeof(CountryData).GetField("<Name>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null) field.SetValue(country, name);
            }

            return country;
        }

        private CountryData? GetCountryData(string countryName)
        {
            foreach (var country in CustomCountries)
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
