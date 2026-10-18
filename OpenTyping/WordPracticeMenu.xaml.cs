using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using OpenTyping.Properties;
using OpenTyping.Resources.Lang;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using FamFamFam.Flags.Wpf;

namespace OpenTyping
{
    /// <summary>
    /// WordPracticeMenu.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 
    public class Timer
    {
        public string Name { get; set; }

        public Timer(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public partial class WordPracticeMenu : UserControl
    {
        private readonly Rank Rank = new Rank();
        private Dictionary<string, string> rtnUserRecord;

        private int countdownTime;
        static public List<Timer> Timers = new List<Timer>
        {
            new Timer("1M"),
            new Timer("3M"),
            new Timer("5M"),
            new Timer("10M"),
            new Timer("15M"),
            new Timer("30M"),
            new Timer("∞")
        };

        public List<CountryData> CustomCountries { get; }

        public WordPracticeMenu()
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

            this.Loaded += WordPracticeMenu_Loaded;

            LoadDatabase();
        }

        private async void LoadDatabase()
        {
            await Rank.GetUsersAsync();
            LVusers.ItemsSource = Rank.users;
        }

        private void WordPracticeMenu_Loaded(object sender, RoutedEventArgs e)
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

        private void SetTextBylanguage()
        {
            TBname.Text = (string)Settings.Default["Name"];
            TBorg.Text = (string)Settings.Default["Org"];
        }


        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default["Name"] = TBname.Text;
            Settings.Default["Org"] = TBorg.Text;
            if (CountryPicker.SelectedItem is CountryData country)
            {
                Settings.Default["Country"] = country.Iso2.ToLower();
            }

            WordPracticeWindow wordPracticeWindow = new WordPracticeWindow(countdownTime);
            wordPracticeWindow.Closed += new EventHandler(WordPracticeWindow_Closed);
            wordPracticeWindow.RtnUserRecord += value => this.rtnUserRecord = value;
            wordPracticeWindow.ShowDialog();
        }

        private void WordPracticeWindow_Closed(object sender, EventArgs e)
        {
             if ((string)Settings.Default["Name"] == "")
            {
                Settings.Default["Name"] = LangStr.Anonymous;
            }
            if ((string)Settings.Default["Org"] == "")
            {
                Settings.Default["Org"] = LangStr.Anonymous;
            }

            User user = new User(
                (string)Settings.Default["Name"],
                (string)Settings.Default["Org"],
                int.Parse(rtnUserRecord["averageAccuracy"]),
                int.Parse(rtnUserRecord["averageTypingSpeed"]),
                int.Parse(rtnUserRecord["currentWordIndex"]),
                double.Parse(rtnUserRecord["elapsedTime"])
            );

            this.FinishPracticeAsync(user, int.Parse(rtnUserRecord["practiceTotal"]));
        }

        private async void FinishPracticeAsync(User newUser, int practiceTotal)
        {
            string congMsg = "";

            int curPos = await Rank.AddSync(newUser);
            LVusers.Items.Refresh();

            if (curPos >= 0 && curPos <= 9)
            {
                congMsg = LangStr.CongratMsg;
                LVusers.SelectedIndex = curPos;
            }

            await this.TryFindParent<MetroWindow>().ShowMessageAsync(LangStr.FinishedPrac + " " + congMsg,
                                        LangStr.LastSpeed + ": " + newUser.Speed + ", " 
                                        + LangStr.Accuracy + ": " + newUser.Accuracy + "%" + ", "
                                        + LangStr.WordCount + ": " + newUser.Count + ", " 
                                        + LangStr.ElapsedTime + ": " 
                                        + newUser.Time.ToString(CultureInfo.GetCultureInfo("en-US")),
                                         MessageDialogStyle.Affirmative,
                                         new MetroDialogSettings { AnimateHide = false });
        }

        private void SplitButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string value = Regex.Replace(e.AddedItems[0].ToString(), @"\D", ""); // Extract numbers from string
            if (int.TryParse(value, out int temp)) 
            {
                countdownTime = int.Parse(value);
            }
            else
            {
                countdownTime = 0; // Init
            }
        }
    }

    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type TargetType, object parameter, CultureInfo culture)
        {
            ListViewItem item = (ListViewItem)value;
            ListView listView = ItemsControl.ItemsControlFromItemContainer(item) as ListView;
            int index = listView.ItemContainerGenerator.IndexFromContainer(item);
            return (index + 1).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
