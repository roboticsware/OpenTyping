using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using OpenTyping.Properties;
using OpenTyping.Resources.Lang;

namespace OpenTyping
{
    /// <summary>
    /// SyllablePracticeWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SyllablePracticeWindow : MetroWindow, INotifyPropertyChanged
    {
        private static readonly Random Randomizer = new Random();
        private readonly string syllablesList;

        private char previousSyllable = ' ';
        public char PreviousSyllable
        {
            get => previousSyllable;
            set => SetField(ref previousSyllable, value);
        }

        private char currentSyllable = ' ';
        public char CurrentSyllable
        {
            get => currentSyllable;
            set => SetField(ref currentSyllable, value);
        }

        private char nextSyllable = ' ';
        public char NextSyllable
        {
            get => nextSyllable;
            set => SetField(ref nextSyllable, value);
        }

        private int correctCount;
        public int CorrectCount
        {
            get => correctCount;
            set => SetField(ref correctCount, value);
        }

        private readonly Brush incorrectBackground = Brushes.Pink;

        // Sound
        private readonly Volume volume;

        // Magnify window
        private bool isMagnified;
        private double baseFontSize;
        public double BaseFontSize
        {
            get => baseFontSize;
            private set => SetField(ref baseFontSize, value);
        }

        private bool isAdvancing;

        public SyllablePracticeWindow(string syllablesList)
        {
            BaseFontSize = App.BaseFontSize;

            InitializeComponent();
            this.SetTextBylanguage();

            void FocusCurrentTextBox(object sender, System.Windows.RoutedEventArgs e) { CurrentTextBox.Focus(); }
            this.Loaded += FocusCurrentTextBox;
            CurrentTextBox.LostFocus += FocusCurrentTextBox;
            // 음절 입력 텍스트 박스 포커스 항상 유지

            TextCompositionManager.AddPreviewTextInputUpdateHandler(CurrentTextBox, CurrentTextBox_PreviewTextInputUpdate);
            CurrentTextBox.PreviewKeyDown += CurrentTextBox_PreviewKeyDown;
            UpdateTextBoxPadding();

            this.syllablesList = syllablesList;

            NextSyllable = RandomSyllable();
            MoveSyllable();

            this.volume = (Volume)Settings.Default["Volume"];
        }

        private void SetTextBylanguage()
        {
            SelfWindow.Title = LangStr.AppName;
        }

        private char RandomSyllable()
        {
            return syllablesList[Randomizer.Next(syllablesList.Length)];
        }

        public void MoveSyllable()
        {
            isAdvancing = true;
            PreviousSyllable = CurrentSyllable;
            CurrentSyllable = NextSyllable;

            char newSyllable;
            do
            {
                newSyllable = RandomSyllable();
            } while (newSyllable == CurrentSyllable); // 새 음절과 전 음절 중복 확인

            NextSyllable = newSyllable;

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                CurrentTextBox.IsEnabled = false;
                CurrentTextBox.Clear();
                CurrentTextBox.IsEnabled = true;
                CurrentTextBox.Focus();
                isAdvancing = false;
            }));
        }

        private void SyllablePracticeWindow_Closed(object sender, EventArgs e)
        {
            // Restore magnification
            if (isMagnified) BaseFontSize /= 1.5;
        }

        private void CurrentTextBox_PreviewTextInputUpdate(object sender, TextCompositionEventArgs e)
        {
            string composingText = e.TextComposition?.CompositionText;
            if (!string.IsNullOrEmpty(composingText))
            {
                CheckInput(composingText);
            }
        }

        private void CurrentTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space || e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                if (!isAdvancing && !string.IsNullOrEmpty(CurrentTextBox.Text))
                {
                    CheckInput(CurrentTextBox.Text);
                }
            }
        }

        private void CurrentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SfxManager.PlayPressed(this.volume); // Key pressing sound
            CheckInput(CurrentTextBox.Text);
        }

        private void CheckInput(string input)
        {
            if (isAdvancing) return;

            switch (input.Length)
            {
                case 0:
                    CurrentTextBox.Background = Brushes.White;
                    return;
                case 1:
                    List<char> decomposedCurrentSyllable = new List<char>(Differ.DecomposeHangul(CurrentSyllable)),
                               decomposedInput = new List<char>(Differ.DecomposeHangul(input[0]));

                    if (decomposedInput.SequenceEqual(decomposedCurrentSyllable))
                    {
                        CorrectCount++;
                        MoveSyllable();
                        return;
                    }
                    if (decomposedInput.Any() &&
                        decomposedInput.SequenceEqual(decomposedCurrentSyllable.Take(decomposedInput.Count))) // 부분 일치
                    {
                        CurrentTextBox.Background = Brushes.White;
                        return;
                    }

                    break;
            }

            CurrentTextBox.Background = incorrectBackground;
        }

        private void MagnifyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isMagnified)
            {
                SizeToContent = SizeToContent.Manual;
                Width = ActualWidth * 1.5;

                BaseFontSize *= 1.5;
                MagnifyIcon.Kind = MahApps.Metro.IconPacks.PackIconModernKind.MagnifyMinus;
                SizeToContent = SizeToContent.Height; // Have to call to fit to content's height again

                isMagnified = true;
            }
            else
            {
                SizeToContent = SizeToContent.WidthAndHeight;

                BaseFontSize /= 1.5;
                MagnifyIcon.Kind = MahApps.Metro.IconPacks.PackIconModernKind.MagnifyAdd;

                isMagnified = false;
            }

            UpdateTextBoxPadding();
        }

        private void UpdateTextBoxPadding()
        {
            // 한글 폰트의 내장 상단 여백을 보정하여 텍스트박스 수직 정중앙에 배치
            double topOffset = -BaseFontSize * 1.25;
            CurrentTextBox.Padding = new Thickness(0, topOffset, 0, 0);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
