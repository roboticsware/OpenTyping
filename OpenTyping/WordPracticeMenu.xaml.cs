using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using OpenTyping.Properties;
using OpenTyping.Resources.Lang;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace OpenTyping
{
    /// <summary>
    /// WordPracticeMenu.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WordPracticeMenu : UserControl
    {
        private IRank Rank;
        private RankLocal RankLocal;
        private RankServer RankServer;

        private User newUser;
        private ListView LVusers;

        private ProgressDialogController pdController;

        public WordPracticeMenu()
        {
            InitializeComponent();
            SetTextBylanguage();

            RankLocal = new RankLocal();
            RankServer = new RankServer();

            LVusers = LVlocal;
            Rank = RankLocal;
            LoadDatabase();
        }

        private void SetTextBylanguage()
        {
            TBname.Text = (string)Settings.Default["Name"];
            TBorg.Text = (string)Settings.Default["Org"];
        }

        private async void LoadDatabase()
        {
            await OpenProgressDialog();

            var users = await Rank.GetUsersAsync();
            LVusers.ItemsSource = users;
            if (users == null)
            {
                NoNetwork.Opacity = 0.5;
                await pdController.CloseAsync();
                pdController = null;

                await this.TryFindParent<MetroWindow>().ShowMessageAsync(LangStr.NoInternetWarn, "");
            }

            if (pdController != null)
            {
                NoNetwork.Opacity = 0;
                await pdController.CloseAsync();
                pdController = null;
            }
        }

        private async Task OpenProgressDialog()
        {
            if (this.TryFindParent<MetroWindow>() != null)
            {
                pdController = await this.TryFindParent<MetroWindow>().ShowProgressAsync(LangStr.FetchServerData, "");
                pdController.SetIndeterminate();
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default["Name"] = TBname.Text;
            Settings.Default["Org"] = TBorg.Text;

            WordPracticeWindow wordPracticeWindow = new WordPracticeWindow();
            wordPracticeWindow.Closed += new EventHandler(WordPracticeWindow_Closed);
            wordPracticeWindow.RtnNewUser += value => this.newUser = value;
            wordPracticeWindow.ShowDialog();
        }

        private void WordPracticeWindow_Closed(object sender, EventArgs e)
        {
            this.FinishPracticeAsync();
        }

        private async void FinishPracticeAsync()
        {
            string congMsg = "";
            int curPosServer = -1;

            int curPosLocal = await RankLocal.AddSync(newUser);
            var users = await RankServer.GetUsersAsync();  // Always try to add from recent rank
            if ( users != null)
            {
                NoNetwork.Opacity = 0;
                curPosServer = await RankServer.AddSync(newUser);
            }
            else
            {
                NoNetwork.Opacity = 0.5;
            }
            LVserver.ItemsSource = users;
            LVusers.Items.Refresh();

            if (curPosLocal >= 0 && curPosLocal <= 9)
            {
                congMsg = LangStr.CongratLocalMsg;
                LVlocal.SelectedIndex = curPosLocal;
            }
            if (curPosServer >= 0 && curPosServer <= 9)
            {
                congMsg = LangStr.CongratServerMsg;
                LVserver.SelectedIndex = curPosServer;

                // Open a server tab if your rank records server rank and you're in local tab
                if (RankTabControl.SelectedItem == TabLocal)
                {
                    RankTabControl.SelectedItem = TabServer;
                    ((TabItem)RankTabControl.SelectedItem).Focus();
                }
            }

            // Open a local tab if no rank update in server tab where you're and local update instead
            if (RankTabControl.SelectedItem == TabServer && congMsg == LangStr.CongratLocalMsg)
            {
                RankTabControl.SelectedItem = TabLocal;
                ((TabItem)RankTabControl.SelectedItem).Focus();
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

        private void Tab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl) // To block event calling infinitely
            {
                switch (((TabControl)sender).SelectedIndex)
                {
                    case 0:
                        {
                            LVusers = LVlocal;
                            Rank = RankLocal;

                            // Manually color change for not working automatically
                            RectLocal.Fill = new SolidColorBrush(Colors.RoyalBlue);
                            RectServer.Fill = new SolidColorBrush(Colors.LightGray);
                        }
                        break;
                    case 1:
                        {
                            Rank = RankServer;
                            LVusers = LVserver;
                            LoadDatabase();

                            // Manually color change for not working automatically
                            RectServer.Fill = new SolidColorBrush(Colors.RoyalBlue);
                            RectLocal.Fill = new SolidColorBrush(Colors.LightGray);
                        }
                        break;
                    default:
                        break;
                }
                LVusers.Items.Refresh();
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
