namespace OpenTyping
{
    /// <summary>
    /// TutorialMenu.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TutorialMenu : PracticeMenuBase
    {
        public TutorialMenu()
        {
            InitializeComponent();
        }

        public void SetTextBylanguage(string langCode)
        {
            if (langCode == "kaa")
            {
                Page1Uz.Visibility = System.Windows.Visibility.Collapsed;
                Page1Kaa.Visibility = System.Windows.Visibility.Visible;
                Page3Uz.Visibility = System.Windows.Visibility.Collapsed;
                Page3Kaa.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                Page1Uz.Visibility = System.Windows.Visibility.Visible;
                Page1Kaa.Visibility = System.Windows.Visibility.Collapsed;
                Page3Uz.Visibility = System.Windows.Visibility.Visible;
                Page3Kaa.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
    }
}
