using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenTyping
{
    /// <summary>
    /// KeyLayoutBox.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class KeyLayoutBox : UserControl
    {
        private List<List<KeyBox>> keyLayout;
        private List<KeyPos> selectedKeys;

        public bool Clickable
        {
            get => (bool)GetValue(ClickableProperty);
            set => SetValue(ClickableProperty, value);
        }
        public static readonly DependencyProperty ClickableProperty =
            DependencyProperty.Register("Clickable", typeof(bool), typeof(KeyLayoutBox), new PropertyMetadata(true));

        public double WidthRatio
        {
            get => (double)GetValue(WidthRatioProperty);
            set => SetValue(WidthRatioProperty, value);
        }
        public static readonly DependencyProperty WidthRatioProperty =
            DependencyProperty.Register("WidthRatio", typeof(double), typeof(KeyLayoutBox), 
                new PropertyMetadata(1.0, OnWidthRatioChanged));

        private static void OnWidthRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var keyLayoutBox = d as KeyLayoutBox;
            keyLayoutBox?.UpdateHandsTransform();
        }

        private static readonly System.Collections.Generic.Dictionary<string, double> HandCalibrationX = new System.Collections.Generic.Dictionary<string, double>
        {
            { "a", -119.92 },
            { "b", -91.64 },
            { "backslash", -55.14 },
            { "c", -105.00 },
            { "close_bracket", -47.24 },
            { "comma", -93.28 },
            { "d", -121.00 },
            { "dot", -66.12 },
            { "e", -108.54 },
            { "enter", -27.36 },
            { "equal", -52.17 },
            { "f", -119.96 },
            { "g", -79.05 },
            { "h", -97.57 },
            { "i", -91.96 },
            { "j", -97.45 },
            { "k", -83.76 },
            { "key_0", -71.57 },
            { "key_1", -134.79 },
            { "key_2", -135.30 },
            { "key_3", -120.12 },
            { "key_4", -125.21 },
            { "key_5", -126.90 },
            { "key_6", -106.08 },
            { "key_7", -96.08 },
            { "key_8", -91.41 },
            { "key_9", -86.89 },
            { "l", -78.79 },
            { "m", -81.24 },
            { "minus", -64.97 },
            { "n", -99.37 },
            { "neutral_left", -117.9 },
            { "neutral_right", -75.0 },
            { "o", -77.37 },
            { "open_bracket", -60.19 },
            { "p", -72.41 },
            { "q", -126.02 },
            { "quote", -52.32 },
            { "r", -109.79 },
            { "s", -123.53 },
            { "semicolon", -60.96 },
            { "shift_left", -140.43 },
            { "shift_right", -28.75 },
            { "slash", -76.09 },
            { "space", -42.96 },
            { "t", -83.67 },
            { "tab", -145.91 },
            { "tilda", -151.15 },
            { "u", -92.34 },
            { "v", -95.00 },
            { "w", -127.41 },
            { "x", -81.26 },
            { "y", -106.18 },
            { "z", -104.42 }
        };

        private void UpdateHandsTransform()
        {
            if (HandsOverlay == null) return;
            
            var leftContainer = HandsOverlay.FindName("LeftHandContainer") as UIElement;
            var rightContainer = HandsOverlay.FindName("RightHandContainer") as UIElement;

            if (leftContainer != null)
            {
                double scaleX = 1.479 * WidthRatio;
                double scaleY = 1.86 * WidthRatio;

                double tx_base = -117.9;
                if (currentLeftActive != null && HandCalibrationX.ContainsKey(currentLeftActive))
                {
                    tx_base = HandCalibrationX[currentLeftActive];
                }
                double tx = tx_base * WidthRatio;
                double ty = -180.0 * WidthRatio;

                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform(scaleX, scaleY));
                group.Children.Add(new TranslateTransform(tx, ty));
                leftContainer.RenderTransform = group;
            }

            if (rightContainer != null)
            {
                double scaleX = 1.479 * WidthRatio;
                double scaleY = 1.86 * WidthRatio;

                double tx_base = -75.0;
                if (currentRightActive != null && HandCalibrationX.ContainsKey(currentRightActive))
                {
                    tx_base = HandCalibrationX[currentRightActive];
                }
                double tx = tx_base * WidthRatio;
                double ty = -180.0 * WidthRatio;

                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform(scaleX, scaleY));
                group.Children.Add(new TranslateTransform(tx, ty));
                rightContainer.RenderTransform = group;
            }
        }

        private string currentLeftActive = null;
        private string currentRightActive = null;

        private static readonly System.Collections.Generic.HashSet<string> LeftHandIds = new System.Collections.Generic.HashSet<string>
        {
            "q", "w", "e", "r", "t", "a", "s", "d", "f", "g", "z", "x", "c", "v", "b",
            "tab", "capslock", "shift_left", "tilda", "key_1", "key_2", "key_3", "key_4", "key_5"
        };

        private static readonly System.Collections.Generic.HashSet<string> RightHandIds = new System.Collections.Generic.HashSet<string>
        {
            "y", "u", "i", "o", "p", "h", "j", "k", "l", "semicolon", "quote", "n", "m",
            "comma", "dot", "slash", "shift_right", "enter", "space", "minus", "equal",
            "key_6", "key_7", "key_8", "key_9", "key_0", "backslash", "open_bracket", "close_bracket"
        };

        private static readonly System.Collections.Generic.Dictionary<string, string> KeyMap = new System.Collections.Generic.Dictionary<string, string>
        {
            { " ", "space" },
            { "space", "space" },
            { "enter", "enter" },
            { "\r", "enter" },
            { "\n", "enter" },
            { "tab", "tab" },
            { "caps lock", "capslock" },
            { "capslock", "capslock" },
            { "shift", "shift_left" },
            { "backspace", "neutral_right" },
            { ";", "semicolon" },
            { ":", "semicolon" },
            { "'", "quote" },
            { "\"", "quote" },
            { ",", "comma" },
            { "<", "comma" },
            { ".", "dot" },
            { ">", "dot" },
            { "/", "slash" },
            { "?", "slash" },
            { "[", "open_bracket" },
            { "{", "open_bracket" },
            { "]", "close_bracket" },
            { "}", "close_bracket" },
            { "\\", "backslash" },
            { "|", "backslash" },
            { "-", "minus" },
            { "_", "minus" },
            { "=", "equal" },
            { "+", "equal" },
            { "`", "tilda" },
            { "~", "tilda" },
            { "1", "key_1" }, { "!", "key_1" },
            { "2", "key_2" }, { "@", "key_2" },
            { "3", "key_3" }, { "#", "key_3" },
            { "4", "key_4" }, { "$", "key_4" },
            { "5", "key_5" }, { "%", "key_5" },
            { "6", "key_6" }, { "^", "key_6" },
            { "7", "key_7" }, { "&", "key_7" },
            { "8", "key_8" }, { "*", "key_8" },
            { "9", "key_9" }, { "(", "key_9" },
            { "0", "key_0" }, { ")", "key_0" }
        };

        public KeyLayoutBox()
        {
            InitializeComponent();
            selectedKeys = MainWindow.CurrentKeyLayout.DefaultKeys;
            Loaded += KeyLayoutBox_Loaded; 
            Loaded += (s, e) => ResetAllHands();
        }

        private void SetElementVisibility(string id, Visibility visibility)
        {
            if (HandsOverlay != null)
            {
                var element = HandsOverlay.FindName(id) as UIElement;
                if (element != null)
                {
                    element.Visibility = visibility;
                }
            }
        }

        private void ShowHand(string key, bool isShift = false)
        {
            key = key.ToLower();
            string id = key;
            if (KeyMap.ContainsKey(key))
            {
                id = KeyMap[key];
            }
            id = id.Replace("-", "_");

            bool isLeft = LeftHandIds.Contains(id);
            bool isRight = RightHandIds.Contains(id);

            if (isLeft)
            {
                if (currentLeftActive != null)
                {
                    SetElementVisibility(currentLeftActive, Visibility.Collapsed);
                }
                SetElementVisibility("neutral_left", Visibility.Collapsed);

                SetElementVisibility(id, Visibility.Visible);
                currentLeftActive = id;

                // Handle right hand
                string targetRightId = isShift ? "shift_right" : "neutral_right";
                if (currentRightActive != targetRightId)
                {
                    if (currentRightActive != null)
                    {
                        SetElementVisibility(currentRightActive, Visibility.Collapsed);
                    }
                    SetElementVisibility("neutral_right", Visibility.Collapsed);
                    SetElementVisibility(targetRightId, Visibility.Visible);
                    currentRightActive = targetRightId;
                }
            }

            if (isRight)
            {
                if (currentRightActive != null)
                {
                    SetElementVisibility(currentRightActive, Visibility.Collapsed);
                }
                SetElementVisibility("neutral_right", Visibility.Collapsed);

                SetElementVisibility(id, Visibility.Visible);
                currentRightActive = id;

                // Handle left hand
                string targetLeftId = isShift ? "shift_left" : "neutral_left";
                if (currentLeftActive != targetLeftId)
                {
                    if (currentLeftActive != null)
                    {
                        SetElementVisibility(currentLeftActive, Visibility.Collapsed);
                    }
                    SetElementVisibility("neutral_left", Visibility.Collapsed);
                    SetElementVisibility(targetLeftId, Visibility.Visible);
                    currentLeftActive = targetLeftId;
                }
            }
            UpdateHandsTransform();
        }

        private void HideHand(string key)
        {
            key = key.ToLower();
            string id = key;
            if (KeyMap.ContainsKey(key))
            {
                id = KeyMap[key];
            }
            id = id.Replace("-", "_");

            if (currentLeftActive == id)
            {
                SetElementVisibility(id, Visibility.Collapsed);
                SetElementVisibility("neutral_left", Visibility.Visible);
                currentLeftActive = null;
            }
            if (currentRightActive == id)
            {
                SetElementVisibility(id, Visibility.Collapsed);
                SetElementVisibility("neutral_right", Visibility.Visible);
                currentRightActive = null;
            }
            UpdateHandsTransform();
        }

        private void ResetAllHands()
        {
            if (currentLeftActive != null)
            {
                SetElementVisibility(currentLeftActive, Visibility.Collapsed);
                currentLeftActive = null;
            }
            if (currentRightActive != null)
            {
                SetElementVisibility(currentRightActive, Visibility.Collapsed);
                currentRightActive = null;
            }

            foreach (var leftId in LeftHandIds)
            {
                SetElementVisibility(leftId, Visibility.Collapsed);
            }
            foreach (var rightId in RightHandIds)
            {
                SetElementVisibility(rightId, Visibility.Collapsed);
            }

            SetElementVisibility("neutral_left", Visibility.Visible);
            SetElementVisibility("neutral_right", Visibility.Visible);

            UpdateHandsTransform();
        }

        private void KeyLayoutBox_Loaded(object sender, RoutedEventArgs e)
        {
            LoadKeyLayout();
            UpdateHandsTransform();
        }

        public void LoadKeyLayout()
        {
            NumberRow.Children.Clear();
            FirstRow.Children.Clear();
            SecondRow.Children.Clear();
            ThirdRow.Children.Clear();
            ForthRow.Children.Clear();

            keyLayout = new List<List<KeyBox>>();

            foreach (IList<Key> keyRow in MainWindow.CurrentKeyLayout.KeyLayoutData)
            {
                var keyBoxes = new List<KeyBox>();

                foreach (Key key in keyRow)
                {
                    var keyBox = new KeyBox
                    {
                        Key = key,
                        Width = 50 * WidthRatio,
                        Height = 50 * WidthRatio,
                        Margin = new Thickness(0, 0, 2, 0),
                    };
                    keyBox.KeyText.FontSize = 15 * WidthRatio;
                    keyBox.ShiftKeyText.FontSize = 15 * WidthRatio;

                    if (Clickable)
                    {
                        keyBox.MouseDown += KeyBox_MouseDown;
                    }

                    keyBoxes.Add(keyBox);
                }

                keyLayout.Add(keyBoxes);
            }

            var keyRows = new List<StackPanel> { NumberRow, FirstRow, SecondRow, ThirdRow, ForthRow };
            for (int i = 0; i < keyRows.Count; i++)
            {
                for (int j = 0; j < keyLayout[i].Count; j++)
                {
                    if (i == 1 && j == keyLayout[i].Count - 1)
                    {
                        keyLayout[i][j].Width = 70 * WidthRatio;
                    }
                    else if (i == keyRows.Count - 1 && j == 0) // Spacebar
                    {
                        keyLayout[i][j].Width = 400 * WidthRatio;
                    }

                    keyRows[i].Children.Add(keyLayout[i][j]);
                }
            }

            if (Clickable)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,
                    new Action(PressDefaultKeys));
            }

            // Mode Keys
            BackspaceKey.Width = 100 * WidthRatio;
            BackspaceKey.KeyText.FontSize = 15 * WidthRatio;

            TabKey.Width = 80 * WidthRatio;
            TabKey.KeyText.FontSize = 15 * WidthRatio;

            CapsLockKey.Width = 100 * WidthRatio;
            CapsLockKey.KeyText.FontSize = 15 * WidthRatio;

            EnterKey.Width = 103 * WidthRatio;
            EnterKey.KeyText.FontSize = 15 * WidthRatio;

            LShiftKey.Width = 130 * WidthRatio;
            LShiftKey.KeyText.FontSize = 15 * WidthRatio;

            RShiftKey.Width = 125 * WidthRatio;
            RShiftKey.KeyText.FontSize = 15 * WidthRatio;
        }

        private string GetQwertyKeyName(int row, int col)
        {
            string[][] qwertyGrid = new string[][]
            {
                new string[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" },
                new string[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "[", "]", "\\" },
                new string[] { "a", "s", "d", "f", "g", "h", "j", "k", "l", ";", "'" },
                new string[] { "z", "x", "c", "v", "b", "n", "m", ",", ".", "/" },
                new string[] { "space" }
            };

            if (row >= 0 && row < qwertyGrid.Length && col >= 0 && col < qwertyGrid[row].Length)
            {
                return qwertyGrid[row][col];
            }
            return "";
        }

        public void PressCorrectKey(KeyPos pos, bool isHandPopup = false, bool isShift = false)
        {
            keyLayout[pos.Row][pos.Column].PressCorrect(isHandPopup);

            if (isHandPopup)
            {
                HandsOverlay.Visibility = Visibility.Visible;
                string keyName = GetQwertyKeyName(pos.Row, pos.Column);
                if (!string.IsNullOrEmpty(keyName))
                {
                    ShowHand(keyName, isShift);
                }
            }
            else
            {
                HandsOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public void PressIncorrectKey(KeyPos pos)
        {
            keyLayout[pos.Row][pos.Column].PressIncorrect();
        }

        public void ReleaseKey(KeyPos pos, bool isColored = false)
        {
            keyLayout[pos.Row][pos.Column].Release(isColored);
            string keyName = GetQwertyKeyName(pos.Row, pos.Column);
            if (!string.IsNullOrEmpty(keyName))
            {
                HideHand(keyName);
            }
        }

        public void ReleaseKeys (List<KeyPos> keys)
        {
            List<KeyPos> defaultKeys = keys;

            for (int i = 0; i < keyLayout.Count; i++)
            {
                for (int j = 0; j < keyLayout[i].Count; j++)
                {
                    if (defaultKeys.Contains(new KeyPos(i, j)))
                    {
                        keyLayout[i][j].Release();
                        string keyName = GetQwertyKeyName(i, j);
                        if (!string.IsNullOrEmpty(keyName))
                        {
                            HideHand(keyName);
                        }
                    }
                }
            }
            selectedKeys = PressedKeys();
        }

        public void PressDefaultKeys()
        {
            for (int i = 0; i < keyLayout.Count; i++)
            {
                for (int j = 0; j < keyLayout[i].Count; j++)
                {
                    if (selectedKeys.Contains(new KeyPos(i, j)))
                    {
                        keyLayout[i][j].PressCorrect(false, true);
                    }
                }
            }
        }

        public void PressDefaultKeys(List<KeyPos> keys)
        {
            List<KeyPos> defaultKeys = keys;

            for (int i = 0; i < keyLayout.Count; i++)
            {
                for (int j = 0; j < keyLayout[i].Count; j++)
                {
                    if (defaultKeys.Contains(new KeyPos(i, j)))
                    {
                        keyLayout[i][j].PressCorrect(false, true);
                    }
                }
            }
            selectedKeys = PressedKeys();
        }

        public List<KeyPos> PressedKeys()
        {
            var result = new List<KeyPos>();

            for (int i=0; i<keyLayout.Count; i++)
            {
                for (int j=0; j<keyLayout[i].Count; j++)
                {
                    if (keyLayout[i][j].Pressed)
                    {
                        result.Add(new KeyPos(i, j));
                    }
                }
            }

            return result;
        }

        public void ToggleColoredKeys(bool colored, KeyPos currentKey)
        {
            for (int i = 0; i < keyLayout.Count; i++)
            {
                for (int j = 0; j < keyLayout[i].Count; j++)
                {
                    // Don't fill a color in case of current practicing key
                    if (!(currentKey.Row == i && currentKey.Column == j))
                    {
                        keyLayout[i][j].ToggleColor(colored);
                    }
                }
            }
        }

        public void PressShiftKey()
        {
            LShiftKey.KeyColor = RShiftKey.KeyColor = (Brush)Application.Current.FindResource("CorrectKeyColor");
            LShiftKey.ShadowColor = RShiftKey.ShadowColor = (Brush)Application.Current.FindResource("CorrectKeyColor");
        }

        public void ReleaseShiftKey()
        {
            LShiftKey.KeyColor = RShiftKey.KeyColor = Brushes.White;
            LShiftKey.ShadowColor = RShiftKey.ShadowColor = (Brush)Application.Current.FindResource("DefaultKeyShadowColor");
        }

        private void KeyBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ((KeyBox)sender).PressToggle();
            selectedKeys = PressedKeys();
        }
    }
}
