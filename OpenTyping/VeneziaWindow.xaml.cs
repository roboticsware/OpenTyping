using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using OpenTyping.Properties;
using OpenTyping.Resources.Lang;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using NAudio.Wave;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OpenTyping
{
    public partial class VeneziaWindow : MetroWindow
    {
        public delegate void UserRecordHandler(Dictionary<string, string> rtnUserRecord);
        public event UserRecordHandler RtnUserRecord;

        private enum WordType
        {
            Normal,
            Bomb,         // Destroys all words
            Freeze,       // Freezes all words for 5s
            SlowMotion,   // Slows words by 30% for 8s
            Shield,       // Protects tower from 1 hit
            Heal,         // Heals 1 brick
            DoubleScore,  // 2x score for 10s
            Meteor,       // (Red) Falls fast, does 2 damage
            Comet         // (Gold) Extremely fast, awards 500 points & triggers Fever
        }

        private class FallingWord
        {
            public string WordText { get; set; }
            public TextBlock textBlock { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Speed { get; set; }
            public WordType Type { get; set; }
        }

        // Gameplay timers
        private DispatcherTimer gameTimer;
        private DispatcherTimer introTimer;
        private DateTime lastSpawnTime;
        private double spawnIntervalMs = 3000;

        // Game states
        private int currentStage = 1;
        private int currentScore = 0;
        private int comboCount = 0;
        private bool isPaused = false;
        private bool isIntroPlaying = true;
        private bool isGameOver = false;

        // Bricks state (12 bricks: 4 rows x 3 columns)
        private bool[] bricks = new bool[12];
        private Border[] brickBorders = new Border[12];

        // Active items state
        private bool hasShield = false;
        private double freezeTimeRemaining = 0;
        private double slowTimeRemaining = 0;
        private double doubleScoreTimeRemaining = 0;

        // Word list
        private List<string> wordList = new List<string>();
        private List<FallingWord> activeWords = new List<FallingWord>();
        private Random random = new Random();

        // Intro Screen Stars & Meteors state
        private class IntroMeteor
        {
            public Line Line { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double SpeedX { get; set; }
            public double SpeedY { get; set; }
            public double Opacity { get; set; }
            public double MaxOpacity { get; set; }
        }

        private readonly List<Ellipse> introStars = new List<Ellipse>();
        private readonly List<IntroMeteor> introMeteors = new List<IntroMeteor>();

        // Local rankings save helper
        private readonly SqliteProvider Sqlite = new SqliteProvider();

        // Sound — ALL via NAudio so every stream is independent and can mix
        // BGM
        private WaveOutEvent bgmPlayer;
        private AudioFileReader bgmReader;
        private bool bgmShouldLoop = false;
        // SFX (each WaveOutEvent is a separate audio session = true simultaneous mixing)
        private WaveOutEvent sfxPressedPlayer;  private AudioFileReader sfxPressedReader;
        private WaveOutEvent sfxBellPlayer;     private AudioFileReader sfxBellReader;
        private WaveOutEvent sfxComboPlayer;    private AudioFileReader sfxComboReader;
        private WaveOutEvent sfxBreakPlayer;    private AudioFileReader sfxBreakReader;
        private WaveOutEvent sfxGameOverPlayer; private AudioFileReader sfxGameOverReader;
        private WaveOutEvent sfxWrongPlayer;    private AudioFileReader sfxWrongReader;

        private Volume volume;

        public VeneziaWindow()
        {
            InitializeComponent();
            InitializeLanguage();
            InitializeBricks();
            LoadWords();
            StartIntro();

            this.volume = (Volume)Settings.Default["Volume"];
            InitializeSounds();
            StartBgm();  // BGM starts on intro screen, continues throughout game
        }

        private void InitializeSounds()
        {
            try
            {
                string soundsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sounds");

                // BGM (loops via PlaybackStopped event)
                string bgmPath = System.IO.Path.Combine(soundsDir, "bgm.wav");
                if (System.IO.File.Exists(bgmPath))
                {
                    bgmReader = new AudioFileReader(bgmPath) { Volume = GetBgmVolume() };
                    bgmPlayer = new WaveOutEvent();
                    bgmPlayer.Init(bgmReader);
                    bgmPlayer.PlaybackStopped += (s, ev) =>
                    {
                        if (bgmShouldLoop && bgmReader != null && bgmPlayer != null)
                            try { bgmReader.Position = 0; bgmPlayer.Play(); } catch { }
                    };
                }

                // SFX — each gets its own independent WaveOutEvent for true mixing
                float sfxVol = GetSfxVolume();
                sfxPressedPlayer  = InitSfx(System.IO.Path.Combine(soundsDir, "Pressed.wav"),     sfxVol, out sfxPressedReader);
                sfxBellPlayer     = InitSfx(System.IO.Path.Combine(soundsDir, "Bell.wav"),        sfxVol, out sfxBellReader);
                sfxComboPlayer    = InitSfx(System.IO.Path.Combine(soundsDir, "Combo.wav"),       sfxVol, out sfxComboReader);
                sfxBreakPlayer    = InitSfx(System.IO.Path.Combine(soundsDir, "Break.wav"),       sfxVol, out sfxBreakReader);
                sfxGameOverPlayer = InitSfx(System.IO.Path.Combine(soundsDir, "GameOverSound.wav"), sfxVol, out sfxGameOverReader);
                sfxWrongPlayer    = InitSfx(System.IO.Path.Combine(soundsDir, "WrongPressed.wav"), sfxVol, out sfxWrongReader);
            }
            catch (Exception ex)
            {
                App.logger.Error("Failed to initialize game sounds: " + ex.Message);
            }
        }

        private WaveOutEvent InitSfx(string path, float volume, out AudioFileReader reader)
        {
            if (!System.IO.File.Exists(path)) { reader = null; return null; }
            reader = new AudioFileReader(path) { Volume = volume };
            var player = new WaveOutEvent();
            player.Init(reader);
            return player;
        }

        private float GetSfxVolume()
        {
            if (this.volume == Volume.Off)  return 0f;
            if (this.volume == Volume.Down) return 0.5f;
            return 1.0f;
        }

        // Play an SFX: stop any current playback, rewind, play
        private void PlaySfx(WaveOutEvent player, AudioFileReader reader)
        {
            if (player == null || reader == null || this.volume == Volume.Off) return;
            try
            {
                if (player.PlaybackState == PlaybackState.Playing)
                {
                    reader.Position = 0;
                }
                else
                {
                    reader.Position = 0;
                    player.Play();
                }
            }
            catch (Exception ex)
            {
                App.logger.Error("PlaySfx failed: " + ex.Message);
                try { player.Stop(); reader.Position = 0; player.Play(); } catch { }
            }
        }

        private float GetBgmVolume()
        {
            if (this.volume == Volume.Off)  return 0f;
            if (this.volume == Volume.Down) return 0.35f;
            return 0.85f;
        }

        private void StartBgm()
        {
            if (bgmPlayer == null || this.volume == Volume.Off) return;
            try
            {
                bgmShouldLoop = true;
                if (bgmReader != null) bgmReader.Position = 0;
                bgmReader.Volume = GetBgmVolume();
                bgmPlayer.Play();
            }
            catch (Exception ex) { App.logger.Error("StartBgm failed: " + ex.Message); }
        }

        private void StopBgm()
        {
            bgmShouldLoop = false;
            try { bgmPlayer?.Stop(); } catch { }
        }

        private void InitializeLanguage()
        {
            Title = LangStr.SamarkandTitle;
            TxtStageLabel.Text = LangStr.SamarkandStage + ": ";
            TxtScoreLabel.Text = LangStr.SamarkandScore + ": ";
            TxtComboLabel.Text = LangStr.SamarkandCombo + ": ";
            TxtPauseMessage.Text = LangStr.SamarkandHelp;
            BtnSkip.Content = LangStr.SamarkandSkip;
            BtnCloseGame.Content = LangStr.SamarkandClose;
            TxtGameOverTitle.Text = LangStr.SamarkandGameOver;

            // Game Rules popup localization
            BtnViewRules.Content = LangStr.SamarkandRulesButton;
            TxtRulesTitle.Text = LangStr.SamarkandRulesTitle;
            TxtRulesObjTitle.Text = LangStr.SamarkandRulesObjTitle;
            TxtRulesObjDesc.Text = LangStr.SamarkandRulesObjDesc;
            TxtRulesTypesTitle.Text = LangStr.SamarkandRulesTypesTitle;
            TxtRulesNormal.Text = LangStr.SamarkandRulesNormal;
            TxtRulesNormalDesc.Text = LangStr.SamarkandRulesNormalDesc;
            TxtRulesMeteor.Text = LangStr.SamarkandRulesMeteor;
            TxtRulesMeteorDesc.Text = LangStr.SamarkandRulesMeteorDesc;
            TxtRulesShield.Text = LangStr.SamarkandRulesShield;
            TxtRulesShieldDesc.Text = LangStr.SamarkandRulesShieldDesc;
            TxtRulesFreeze.Text = LangStr.SamarkandRulesFreeze;
            TxtRulesFreezeDesc.Text = LangStr.SamarkandRulesFreezeDesc;
            TxtRulesSlow.Text = LangStr.SamarkandRulesSlow;
            TxtRulesSlowDesc.Text = LangStr.SamarkandRulesSlowDesc;
            TxtRulesBomb.Text = LangStr.SamarkandRulesBomb;
            TxtRulesBombDesc.Text = LangStr.SamarkandRulesBombDesc;
            TxtRulesHealDouble.Text = LangStr.SamarkandRulesHealDouble;
            TxtRulesHealDoubleDesc.Text = LangStr.SamarkandRulesHealDoubleDesc;
            TxtRulesComet.Text = LangStr.SamarkandRulesComet;
            TxtRulesCometDesc.Text = LangStr.SamarkandRulesCometDesc;
            TxtRulesComboTitle.Text = LangStr.SamarkandRulesComboTitle;
            TxtRulesComboDesc.Text = LangStr.SamarkandRulesComboDesc;
            BtnCloseRules.Content = LangStr.SamarkandClose;

            TxtFinalScoreLabel.Text = LangStr.SamarkandFinalScore;
            TxtFinalStageLabel.Text = LangStr.SamarkandMaxStage;
            InputPlaceholder.Text = LangStr.SamarkandTypeHere;

            TxtBuffShield.Text = LangStr.SamarkandBuffShield;
            TxtBuffSlow.Text = LangStr.SamarkandBuffSlow;
            TxtBuffDouble.Text = LangStr.SamarkandBuffDouble;
            TxtBuffFreeze.Text = LangStr.SamarkandBuffFreeze;
        }

        private void InitializeBricks()
        {
            TowerGrid.Children.Clear();
            for (int i = 0; i < 12; i++)
            {
                bricks[i] = true; // All bricks healthy initially

                Border border = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(17, 78, 68)),
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(1)
                };

                // Draw brick content
                UpdateBrickVisual(border, true);

                brickBorders[i] = border;
                TowerGrid.Children.Add(border);
            }
        }

        private void UpdateBrickVisual(Border border, bool healthy)
        {
            if (healthy)
            {
                // Beautiful Uzbek blue ceramic mosaic style
                border.Background = new SolidColorBrush(Color.FromRgb(58, 134, 200));
                
                // Add a small inner border to simulate tile pattern
                Grid tileGrid = new Grid();
                tileGrid.Children.Add(new Rectangle
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(116, 192, 252)),
                    StrokeThickness = 1,
                    Margin = new Thickness(2),
                    Fill = Brushes.Transparent
                });
                border.Child = tileGrid;
            }
            else
            {
                // Broken tile - dark stone charcoal with green moss/cracks
                border.Background = new SolidColorBrush(Color.FromRgb(33, 37, 41));
                
                Grid crackGrid = new Grid();
                // Add crack lines using Paths
                System.Windows.Shapes.Path crackPath = new System.Windows.Shapes.Path
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(55, 178, 77)), // Green crack lines
                    StrokeThickness = 1.5,
                    Data = Geometry.Parse("M 5 5 L 15 20 L 30 10 M 40 5 L 35 18 L 60 22"),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                crackGrid.Children.Add(crackPath);
                border.Child = crackGrid;
            }
        }

        private void LoadWords()
        {
            try
            {
                PracticeData data = PracticeData.LoadWordData();
                if (data != null && data.TextData != null)
                {
                    wordList = data.TextData.ToList();
                }
            }
            catch (Exception ex)
            {
                App.logger.Error("Failed to load word data for Samarkand game: " + ex.ToString());
            }

            // Fallback word list just in case
            if (wordList.Count == 0)
            {
                wordList = new List<string> { "사마르칸트", "울루그베크", "관측소", "돔사원", "유성우", "바다", "모자이크", "타일", "비단", "오아시스", "천문학" };
            }
        }

        // --- INTRO STAGE ---
        private void StartIntro()
        {
            isIntroPlaying = true;
            IntroGrid.Visibility = Visibility.Visible;
            GameGrid.Visibility = Visibility.Collapsed;

            TxtIntro.Text = LangStr.SamarkandIntro;

            // Initialize and clean up intro animations
            introStars.Clear();
            introMeteors.Clear();

            // Spawn 40 random twinkling stars in the background (inserted at index 0 so they are behind everything)
            for (int i = 0; i < 40; i++)
            {
                Ellipse star = new Ellipse
                {
                    Width = random.Next(2, 5),
                    Height = random.Next(2, 5),
                    Fill = Brushes.White,
                    Opacity = random.NextDouble() * 0.7 + 0.1
                };
                Canvas.SetLeft(star, random.Next(10, 790));
                Canvas.SetTop(star, random.Next(10, 420));
                
                IntroCanvas.Children.Insert(0, star);
                introStars.Add(star);
            }

            // Scroll text upwards
            introTimer = new DispatcherTimer();
            introTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
            double textTop = 550;
            TxtIntro.SetValue(Canvas.TopProperty, textTop);

            introTimer.Tick += (s, e) =>
            {
                textTop -= 1.2; // scroll speed
                TxtIntro.SetValue(Canvas.TopProperty, textTop);

                // 1. Twinkle stars: change their opacity randomly by a small amount
                foreach (var star in introStars)
                {
                    if (random.NextDouble() < 0.08) // 8% chance to change opacity slightly
                    {
                        star.Opacity = random.NextDouble() * 0.8 + 0.1;
                    }
                }

                // 2. Spawn new meteors randomly (e.g. 2.5% chance per tick)
                if (random.NextDouble() < 0.025)
                {
                    double spawnX = random.Next(100, 850); // spawn from top, falling down-left
                    double spawnY = 0;
                    double speedY = random.NextDouble() * 3.5 + 2.5; // speed downward
                    double speedX = -speedY * (random.NextDouble() * 0.4 + 0.6); // speed leftward
                    double maxOpacity = random.NextDouble() * 0.6 + 0.4;
                    double strokeThick = random.NextDouble() * 1.5 + 1.0;

                    Line meteorLine = new Line
                    {
                        Stroke = new SolidColorBrush(Color.FromRgb(255, (byte)random.Next(180, 230), 80)),
                        StrokeThickness = strokeThick,
                        Opacity = maxOpacity,
                        X1 = spawnX,
                        Y1 = spawnY,
                        X2 = spawnX - speedX * 3, // trail size
                        Y2 = spawnY - speedY * 3
                    };

                    IntroCanvas.Children.Insert(0, meteorLine); // Render behind scrolling text

                    introMeteors.Add(new IntroMeteor
                    {
                        Line = meteorLine,
                        X = spawnX,
                        Y = spawnY,
                        SpeedX = speedX,
                        SpeedY = speedY,
                        Opacity = maxOpacity,
                        MaxOpacity = maxOpacity
                    });
                }

                // 3. Update existing meteors
                List<IntroMeteor> meteorsToRemove = new List<IntroMeteor>();
                foreach (var meteor in introMeteors)
                {
                    meteor.X += meteor.SpeedX;
                    meteor.Y += meteor.SpeedY;

                    // Update trail line end and head points
                    meteor.Line.X1 = meteor.X;
                    meteor.Line.Y1 = meteor.Y;
                    meteor.Line.X2 = meteor.X - meteor.SpeedX * 3;
                    meteor.Line.Y2 = meteor.Y - meteor.SpeedY * 3;

                    // Fade out as it approaches bottom or edge
                    if (meteor.Y > 350)
                    {
                        double ratio = Math.Max(0, 1.0 - (meteor.Y - 350) / 150.0);
                        meteor.Line.Opacity = meteor.MaxOpacity * ratio;
                    }

                    // Remove conditions
                    if (meteor.Y > 500 || meteor.X < -50 || meteor.Line.Opacity <= 0.01)
                    {
                        meteorsToRemove.Add(meteor);
                    }
                }

                // Cleanup finished meteors
                foreach (var meteor in meteorsToRemove)
                {
                    IntroCanvas.Children.Remove(meteor.Line);
                    introMeteors.Remove(meteor);
                }

                // If text is scrolled fully off-screen, auto start game
                if (textTop < -TxtIntro.ActualHeight - 50)
                {
                    SkipIntro();
                }
            };
            introTimer.Start();
        }

        private void SkipIntro()
        {
            if (introTimer != null)
            {
                introTimer.Stop();
                introTimer = null;
            }

            // Cleanup intro elements
            foreach (var star in introStars)
            {
                IntroCanvas.Children.Remove(star);
            }
            introStars.Clear();

            foreach (var meteor in introMeteors)
            {
                IntroCanvas.Children.Remove(meteor.Line);
            }
            introMeteors.Clear();

            isIntroPlaying = false;
            IntroGrid.Visibility = Visibility.Collapsed;
            GameGrid.Visibility = Visibility.Visible;

            InputTextBox.Focus();
            StartGame();
        }

        private void StartGame()
        {
            isPaused = false;
            isGameOver = false;
            currentScore = 0;
            currentStage = 1;
            comboCount = 0;
            spawnIntervalMs = 3000;

            TxtStage.Text = currentStage.ToString();
            TxtScore.Text = currentScore.ToString();
            TxtCombo.Text = comboCount.ToString();

            InitializeBricks();
            activeWords.Clear();
            GameCanvas.Children.Clear();

            // Re-add static elements to canvas
            GameCanvas.Children.Add(LeftWaterCanvas);
            GameCanvas.Children.Add(RightWaterCanvas);
            GameCanvas.Children.Add(TowerGrid);
            GameCanvas.Children.Add(DomePlatform);
            GameCanvas.Children.Add(TowerShieldVisual);

            // Reset Shield visual state
            TowerShieldVisual.Visibility = Visibility.Collapsed;
            TowerShieldVisual.Opacity = 0;
            hasShield = false;
            BuffShield.Visibility = Visibility.Collapsed;

            // Spawn background stars
            SpawnStars();

            lastSpawnTime = DateTime.Now;

            gameTimer = new DispatcherTimer();
            gameTimer.Interval = TimeSpan.FromMilliseconds(25); // ~40fps loop
            gameTimer.Tick += GameTimer_Tick;
            gameTimer.Start();
        }

        private void SpawnStars()
        {
            // Spawn 30 static stars in background
            for (int i = 0; i < 35; i++)
            {
                Ellipse star = new Ellipse
                {
                    Width = random.Next(2, 5),
                    Height = random.Next(2, 5),
                    Fill = Brushes.White,
                    Opacity = random.NextDouble() * 0.7 + 0.3
                };
                Canvas.SetLeft(star, random.Next(20, 780));
                Canvas.SetTop(star, random.Next(10, 320));
                GameCanvas.Children.Add(star);
            }
        }

        // --- GAME LOOP ---
        private void GameTimer_Tick(object sender, EventArgs e)
        {
            if (isPaused || isGameOver) return;

            UpdateTimers();
            MoveWords();
            CheckSpawning();
        }

        private void UpdateTimers()
        {
            // Update item durations
            if (freezeTimeRemaining > 0)
            {
                freezeTimeRemaining -= 0.025; // tick is 25ms
                BuffFreeze.Visibility = Visibility.Visible;
                if (freezeTimeRemaining <= 0) BuffFreeze.Visibility = Visibility.Collapsed;
            }

            if (slowTimeRemaining > 0)
            {
                slowTimeRemaining -= 0.025;
                BuffSlow.Visibility = Visibility.Visible;
                GameCanvas.Background = new SolidColorBrush(Color.FromRgb(10, 70, 80)); // tinted background
                if (slowTimeRemaining <= 0)
                {
                    BuffSlow.Visibility = Visibility.Collapsed;
                    GameCanvas.Background = new SolidColorBrush(Color.FromRgb(14, 138, 124));
                }
            }

            if (doubleScoreTimeRemaining > 0)
            {
                doubleScoreTimeRemaining -= 0.025;
                BuffDouble.Visibility = Visibility.Visible;
                if (doubleScoreTimeRemaining <= 0) BuffDouble.Visibility = Visibility.Collapsed;
            }
        }

        private void MoveWords()
        {
            double speedMultiplier = 1.0 + (currentStage - 1) * 0.05;
            if (slowTimeRemaining > 0) speedMultiplier *= 0.3; // slow motion active

            List<FallingWord> wordsToDestroy = new List<FallingWord>();

            foreach (var word in activeWords.ToList())
            {
                // If freeze is active, words don't move
                if (freezeTimeRemaining <= 0)
                {
                    word.Y += word.Speed * speedMultiplier;
                    Canvas.SetTop(word.textBlock, word.Y);
                }

                double wordHeight = word.textBlock.ActualHeight > 0 ? word.textBlock.ActualHeight : word.textBlock.DesiredSize.Height;
                double wordBottom = word.Y + wordHeight;

                // Check collision based on Shield (circular dome) or Tower
                if (hasShield)
                {
                    // The shield is a semi-circle centered at X=400, Y=ActualHeight+10, with radius=215.
                    // The word spans horizontally from word.X to word.X + word.Width.
                    // We check if the word's horizontal span overlaps with the shield's horizontal span [185, 615].
                    double xOverlapLeft = Math.Max(word.X, 185);
                    double xOverlapRight = Math.Min(word.X + word.Width, 615);

                    if (xOverlapLeft <= xOverlapRight)
                    {
                        // Find the point in the overlap region closest to the center (X = 400)
                        double xClosest;
                        if (400 < xOverlapLeft) xClosest = xOverlapLeft;
                        else if (400 > xOverlapRight) xClosest = xOverlapRight;
                        else xClosest = 400;

                        double dX = xClosest - 400;
                        double shieldY = (GameCanvas.ActualHeight + 10) - Math.Sqrt(215 * 215 - dX * dX);

                        if (wordBottom >= shieldY)
                        {
                            wordsToDestroy.Add(word);
                            if (word.Type != WordType.Comet)
                            {
                                DamageTower(word.Type == WordType.Meteor ? 2 : 1);
                            }
                            continue;
                        }
                    }
                }

                // Check collision bounds
                double collisionY = GameCanvas.ActualHeight - 145; // Height of tower starting point
                double waterY = GameCanvas.ActualHeight - 40;     // Height of water level

                if (wordBottom >= collisionY)
                {
                    // Check if word hits the tower center (X between 240 and 540)
                    bool hitsTower = (word.X >= 240 && word.X <= 540);

                    if (hitsTower)
                    {
                        wordsToDestroy.Add(word);
                        if (word.Type != WordType.Comet) // Comet words do not damage on falling
                        {
                            DamageTower(word.Type == WordType.Meteor ? 2 : 1);
                        }
                    }
                    else if (wordBottom >= waterY)
                    {
                        // Falls in water
                        wordsToDestroy.Add(word);
                        // Normal/Meteor words falling in water also damage the observatory
                        if (word.Type != WordType.Comet)
                        {
                            DamageTower(1);
                        }
                    }
                }
            }

            // Cleanup destroyed words
            foreach (var word in wordsToDestroy)
            {
                RemoveWord(word);
            }
        }

        private void DamageTower(int damage)
        {
            if (hasShield)
            {
                hasShield = false;
                BuffShield.Visibility = Visibility.Collapsed;
                AnimateShieldHit();
                return;
            }

            // Reset combo
            comboCount = 0;
            TxtCombo.Text = comboCount.ToString();

            for (int d = 0; d < damage; d++)
            {
                // Find first healthy brick to destroy (from top to bottom, row by row)
                int healthyIndex = -1;
                for (int i = 0; i < 12; i++)
                {
                    if (bricks[i])
                    {
                        healthyIndex = i;
                        break;
                    }
                }

                if (healthyIndex != -1)
                {
                    bricks[healthyIndex] = false;
                    AnimateBrickBreak(brickBorders[healthyIndex]);
                    SpawnDebris(Canvas.GetLeft(TowerGrid) + 120, GameCanvas.ActualHeight - 50);

                    // Play brick breaking sound effect!
                    PlaySfx(sfxBreakPlayer, sfxBreakReader);
                }
                else
                {
                    // No bricks left — collapse animation then game over
                    TriggerTowerCollapse();
                    break;
                }
            }

            // Double check if all are broken
            if (bricks.All(b => !b))
            {
                TriggerTowerCollapse();
            }
        }

        // Shake + flash a brick when it breaks
        private void AnimateBrickBreak(Border brick)
        {
            int ticks = 0;
            var origLeft  = brick.Margin.Left;
            var shakeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            shakeTimer.Tick += (s, e) =>
            {
                ticks++;
                // 6 frames of horizontal shake
                if (ticks <= 6)
                {
                    double offset = (ticks % 2 == 0) ? 4 : -4;
                    brick.Margin = new Thickness(origLeft + offset,
                                                brick.Margin.Top,
                                                brick.Margin.Right,
                                                brick.Margin.Bottom);
                }
                else
                {
                    shakeTimer.Stop();
                    brick.Margin = new Thickness(origLeft,
                                                brick.Margin.Top,
                                                brick.Margin.Right,
                                                brick.Margin.Bottom);
                    UpdateBrickVisual(brick, false);   // show cracked texture
                    // Brief red flash
                    brick.Background = new SolidColorBrush(Color.FromRgb(220, 50, 50));
                    var flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                    flashTimer.Tick += (fs, fe) =>
                    {
                        flashTimer.Stop();
                        UpdateBrickVisual(brick, false);
                    };
                    flashTimer.Start();
                }
            };
            shakeTimer.Start();
        }

        private void SpawnDebris(double centerX, double centerY)
        {
            // Spawn 10 small brick particle debris falling down
            for (int i = 0; i < 10; i++)
            {
                Rectangle particle = new Rectangle
                {
                    Width = random.Next(4, 10),
                    Height = random.Next(4, 10),
                    Fill = new SolidColorBrush(Color.FromRgb(58, 134, 200))
                };
                Canvas.SetLeft(particle, centerX + random.Next(-50, 50));
                Canvas.SetTop(particle, centerY + random.Next(-10, 10));

                GameCanvas.Children.Add(particle);

                // Quick timer to make particles fall
                DispatcherTimer particleTimer = new DispatcherTimer();
                particleTimer.Interval = TimeSpan.FromMilliseconds(30);
                double pY = centerY;
                double pX = Canvas.GetLeft(particle);
                double pSpeedY = random.NextDouble() * 3 + 2;
                double pSpeedX = random.NextDouble() * 4 - 2;
                int elapsed = 0;

                particleTimer.Tick += (s, e) =>
                {
                    pY += pSpeedY;
                    pX += pSpeedX;
                    Canvas.SetTop(particle, pY);
                    Canvas.SetLeft(particle, pX);
                    elapsed++;
                    if (elapsed > 25 || pY > GameCanvas.ActualHeight)
                    {
                        particleTimer.Stop();
                        GameCanvas.Children.Remove(particle);
                    }
                };
                particleTimer.Start();
            }
        }

        private void CheckSpawning()
        {
            double interval = spawnIntervalMs / (1.0 + (currentStage - 1) * 0.06);
            if ((DateTime.Now - lastSpawnTime).TotalMilliseconds >= interval)
            {
                SpawnWord();
                lastSpawnTime = DateTime.Now;
            }
        }

        private void SpawnWord()
        {
            if (wordList.Count == 0) return;

            // Filter words by length based on current stage
            // Variety Guard: ensure the candidate pool has at least 50 words to avoid excessive repetition
            int minAllowedLength = currentStage + 2;
            int maxAllowedLength = currentStage + 3;
            List<string> candidates;
            do
            {
                int minLen = minAllowedLength;
                int maxLen = maxAllowedLength;
                candidates = wordList.Where(w => w.Length >= minLen && w.Length <= maxLen).ToList();
                if (candidates.Count >= 50 || (maxAllowedLength >= 15 && minAllowedLength <= 3))
                {
                    break;
                }
                
                if (maxAllowedLength < 15)
                {
                    maxAllowedLength++;
                }
                else if (minAllowedLength > 3)
                {
                    minAllowedLength--;
                }
            } while (true);

            // Fallback: if no words are short enough, allow the shortest words in the list (+ 1 char offset)
            if (candidates.Count == 0)
            {
                int minLength = wordList.Min(w => w.Length);
                candidates = wordList.Where(w => w.Length <= minLength + 1).ToList();
            }

            // 중복 단어 생성 방지 (최대 10번 시도)
            string text = "";
            int attempts = 0;
            do
            {
                text = candidates[random.Next(candidates.Count)];
                attempts++;
            } while (activeWords.Any(w => w.WordText == text) && attempts < 10);
            WordType type = WordType.Normal;

            // Roll for special type
            double roll = random.NextDouble();
            if (roll < 0.05) // 5% special yellow item
            {
                WordType[] items = { WordType.Bomb, WordType.Freeze, WordType.SlowMotion, WordType.Shield, WordType.Heal, WordType.DoubleScore };
                type = items[random.Next(items.Length)];
            }
            else if (roll < 0.10) // 5% Golden Comet
            {
                type = WordType.Comet;
            }
            else if (roll < 0.20) // 10% red meteor
            {
                type = WordType.Meteor;
            }

            Brush textColor = Brushes.White;
            FontWeight weight = FontWeights.Bold;

            if (type == WordType.Meteor)
            {
                textColor = Brushes.Red;
            }
            else if (type == WordType.Comet)
            {
                textColor = Brushes.Gold;
            }
            else if (type != WordType.Normal) // Yellow items
            {
                textColor = Brushes.Yellow;
            }

            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = textColor,
                FontSize = (type == WordType.Meteor || type == WordType.Comet) ? 18 : 14,
                FontWeight = weight,
                FontStyle = type == WordType.Comet ? FontStyles.Italic : FontStyles.Normal,
                FontFamily = new FontFamily("NanumBarunGothic, PTMono")
            };

            // Calculate width and set random X position (keeping within bounds of the canvas)
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double wordWidth = tb.DesiredSize.Width;
            double posX = random.Next(20, (int)(GameCanvas.ActualWidth - wordWidth - 20));

            FallingWord fw = new FallingWord
            {
                WordText = text,
                textBlock = tb,
                X = posX,
                Y = 0,
                Width = wordWidth,
                Speed = type == WordType.Comet ? random.NextDouble() * 1.0 + 2.6 : (type == WordType.Meteor ? random.NextDouble() * 0.6 + 1.2 : random.NextDouble() * 0.4 + 0.45),
                Type = type
            };

            Canvas.SetLeft(tb, posX);
            Canvas.SetTop(tb, 0);

            GameCanvas.Children.Add(tb);
            activeWords.Add(fw);
        }

        private void RemoveWord(FallingWord word)
        {
            GameCanvas.Children.Remove(word.textBlock);
            activeWords.Remove(word);
        }

        // --- TYPING INPUT & LOGIC ---
        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputTextBox.Text))
            {
                InputPlaceholder.Visibility = Visibility.Visible;
            }
            else
            {
                InputPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (isIntroPlaying)
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    if (RulesGrid.Visibility == Visibility.Visible)
                    {
                        CloseRules();
                    }
                    else
                    {
                        SkipIntro();
                    }
                }
                return;
            }

            if (isPaused)
            {
                if (e.Key == System.Windows.Input.Key.Space)
                {
                    e.Handled = true; // prevent inputting space into TextBox
                    ResumeGame();
                }
                return;
            }

            if (isGameOver) return;

            if (e.Key == System.Windows.Input.Key.Enter)
            {
                string input = InputTextBox.Text.Trim();
                InputTextBox.Text = "";

                if (!string.IsNullOrEmpty(input))
                {
                    CheckInput(input);
                }
            }
        }

        private void CheckInput(string input)
        {
            // Check if input matches any active word
            FallingWord matchedWord = activeWords.FirstOrDefault(w => w.WordText == input);

            if (matchedWord != null)
            {
                // Play sound: Combo sound on multiples of 5, otherwise standard Bell sound
                bool isComboMilestone = false;
                int nextCombo = comboCount + 1;
                if (nextCombo > 0 && nextCombo % 5 == 0)
                {
                    isComboMilestone = true;
                }

                if (isComboMilestone)
                {
                    PlaySfx(sfxComboPlayer, sfxComboReader);
                    AnimateComboPop();
                }
                else
                {
                    PlaySfx(sfxBellPlayer, sfxBellReader);
                }

                // Score multiplier based on Combo
                double comboMultiplier = 1.0 + (comboCount / 5) * 0.25;
                if (comboMultiplier > 2.5) comboMultiplier = 2.5; // Cap multiplier at 2.5x

                int baseScore = 100;
                if (matchedWord.Type == WordType.Meteor)
                {
                    baseScore = 200;
                }
                else if (matchedWord.Type == WordType.Comet)
                {
                    baseScore = 500;
                }
                int scoreGained = (int)(baseScore * comboMultiplier);

                if (doubleScoreTimeRemaining > 0)
                {
                    scoreGained *= 2;
                }

                currentScore += scoreGained;
                TxtScore.Text = currentScore.ToString();

                // Increment combo
                comboCount++;
                TxtCombo.Text = comboCount.ToString();

                // Fever Mode at 15 combos
                if (comboCount == 15)
                {
                    TriggerFever();
                }

                // Process item effect
                ApplyItemEffect(matchedWord.Type);

                // Remove word
                RemoveWord(matchedWord);

                // Level up check
                if (currentScore >= currentStage * 2000)
                {
                    TriggerLevelUp();
                }
            }
            else
            {
                // Typos break combos!
                comboCount = 0;
                TxtCombo.Text = comboCount.ToString();
                PlaySfx(sfxWrongPlayer, sfxWrongReader);
            }
        }

        private void TriggerDramaticEffect(string text, Color flashColor)
        {
            TxtEffectPopup.Text = text;
            TxtEffectPopup.Foreground = new SolidColorBrush(flashColor == Colors.Yellow ? Color.FromRgb(252, 196, 25) : flashColor);

            // Set initial states
            ScreenFlashBorder.Background = new SolidColorBrush(flashColor);
            ScreenFlashBorder.Opacity = 0.5;
            TxtEffectPopup.Opacity = 1.0;
            TxtEffectScale.ScaleX = 0.5;
            TxtEffectScale.ScaleY = 0.5;

            // Simple dispatcher timer to animate fade/scale
            int frame = 0;
            var animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
            animTimer.Tick += (s, e) =>
            {
                frame++;
                if (frame <= 10)
                {
                    // Scale up text
                    double scale = 0.5 + 0.7 * (frame / 10.0); // scales from 0.5 to 1.2
                    TxtEffectScale.ScaleX = scale;
                    TxtEffectScale.ScaleY = scale;
                }
                else if (frame <= 30)
                {
                    // Fade out screen flash and text
                    double ratio = (frame - 10) / 20.0;
                    ScreenFlashBorder.Opacity = 0.5 * (1.0 - ratio);
                    TxtEffectPopup.Opacity = 1.0 - ratio;
                }
                else
                {
                    animTimer.Stop();
                    ScreenFlashBorder.Opacity = 0;
                    TxtEffectPopup.Opacity = 0;
                }
            };
            animTimer.Start();
        }

        private void AnimateComboPop()
        {
            if (TxtComboScale == null || TxtCombo == null) return;

            // Set initial scale and color highlight
            TxtComboScale.ScaleX = 1.6;
            TxtComboScale.ScaleY = 1.6;
            TxtCombo.Foreground = Brushes.White;

            int step = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (s, e) =>
            {
                step++;
                if (step <= 12)
                {
                    // Smoothly scale back to 1.0
                    double progress = step / 12.0;
                    double scale = 1.6 - 0.6 * progress;
                    TxtComboScale.ScaleX = scale;
                    TxtComboScale.ScaleY = scale;
                    
                    // Interpolate color back to original #ffd43b (Gold)
                    byte r = 255;
                    byte g = (byte)(255 - 43 * progress);
                    byte b = (byte)(255 - 196 * progress);
                    TxtCombo.Foreground = new SolidColorBrush(Color.FromRgb(r, g, b));
                }
                else
                {
                    timer.Stop();
                    TxtComboScale.ScaleX = 1.0;
                    TxtComboScale.ScaleY = 1.0;
                    TxtCombo.Foreground = new SolidColorBrush(Color.FromRgb(255, 212, 59)); // Restore gold #ffd43b
                }
            };
            timer.Start();
        }

        private void ApplyItemEffect(WordType type)
        {
            switch (type)
            {
                case WordType.Bomb:
                    TriggerDramaticEffect(LangStr.SamarkandBomb, Color.FromRgb(230, 73, 73));
                    // Cascade-clear: remove words one-by-one with sound, 100ms apart
                    var wordsToDestroy = activeWords.Where(w => w.Type != WordType.Comet).ToList();
                    int bombBaseScore  = wordsToDestroy.Count * 50;
                    currentScore += bombBaseScore;
                    TxtScore.Text = currentScore.ToString();

                    // Then cascade-clear the rest with sound + flash
                    int cascadeIndex = 0;
                    var cascadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                    cascadeTimer.Tick += (s, e) =>
                    {
                        if (cascadeIndex >= wordsToDestroy.Count)
                        {
                            cascadeTimer.Stop();
                            return;
                        }
                        var w = wordsToDestroy[cascadeIndex];
                        GameCanvas.Children.Remove(w.textBlock);
                        activeWords.Remove(w);

                        // Brief flash at word position before removal
                        PlaySfx(sfxBreakPlayer, sfxBreakReader);
                        SpawnDebris(Canvas.GetLeft(w.textBlock) + 40, Canvas.GetTop(w.textBlock));
                        cascadeIndex++;
                    };
                    cascadeTimer.Start();
                    break;

                case WordType.Freeze:
                    TriggerDramaticEffect(LangStr.SamarkandFreeze, Color.FromRgb(116, 192, 252));
                    freezeTimeRemaining = 5.0; // 5 seconds freeze
                    break;

                case WordType.SlowMotion:
                    TriggerDramaticEffect(LangStr.SamarkandSlowMotion, Color.FromRgb(28, 126, 214));
                    slowTimeRemaining = 8.0; // 8 seconds slow down
                    break;

                case WordType.Shield:
                    TriggerDramaticEffect(LangStr.SamarkandShield, Color.FromRgb(55, 178, 77));
                    hasShield = true;
                    BuffShield.Visibility = Visibility.Visible;
                    ActivateShieldVisual();
                    break;

                case WordType.Heal:
                    // Heal 1 brick (find last broken brick)
                    int brokenIndex = -1;
                    for (int i = 11; i >= 0; i--)
                    {
                        if (!bricks[i])
                        {
                            brokenIndex = i;
                            break;
                        }
                    }
                    if (brokenIndex != -1)
                    {
                        TriggerDramaticEffect(LangStr.SamarkandHeal, Color.FromRgb(169, 227, 37));
                        bricks[brokenIndex] = true;
                        UpdateBrickVisual(brickBorders[brokenIndex], true);
                    }
                    break;

                case WordType.DoubleScore:
                    TriggerDramaticEffect(LangStr.SamarkandDoubleScore, Color.FromRgb(252, 196, 25));
                    doubleScoreTimeRemaining = 10.0; // 10 seconds 2x score
                    break;

                case WordType.Comet:
                    TriggerDramaticEffect(LangStr.SamarkandComet, Color.FromRgb(255, 215, 0)); // Gold flash
                    TriggerFever();
                    break;
            }
        }

        private void ActivateShieldVisual()
        {
            TowerShieldVisual.Visibility = Visibility.Visible;
            TowerShieldVisual.Opacity = 0;
            
            var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            int step = 0;
            fadeTimer.Tick += (s, e) =>
            {
                step++;
                TowerShieldVisual.Opacity = step / 10.0; // fade in over 10 frames (~160ms)
                if (step >= 10)
                {
                    fadeTimer.Stop();
                }
            };
            fadeTimer.Start();
        }

        private void AnimateShieldHit()
        {
            // Rapid flash (white/green) then fade out
            var flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            int step = 0;
            Brush origBrush = TowerShieldVisual.BorderBrush;
            
            flashTimer.Tick += (s, e) =>
            {
                step++;
                if (step <= 6)
                {
                    // Alternate white and bright green border
                    TowerShieldVisual.BorderBrush = (step % 2 == 0) ? Brushes.White : new SolidColorBrush(Color.FromRgb(116, 228, 140));
                    TowerShieldVisual.Opacity = 0.8;
                }
                else if (step <= 16)
                {
                    // Fade out
                    double ratio = (step - 6) / 10.0;
                    TowerShieldVisual.Opacity = 0.8 * (1.0 - ratio);
                }
                else
                {
                    flashTimer.Stop();
                    TowerShieldVisual.Visibility = Visibility.Collapsed;
                    TowerShieldVisual.BorderBrush = origBrush;
                }
            };
            flashTimer.Start();
        }

        private void TriggerFever()
        {
            TriggerDramaticEffect(LangStr.SamarkandFever, Color.FromRgb(240, 62, 62));
            // Give a Shield or Heal 1 brick
            if (!hasShield)
            {
                hasShield = true;
                BuffShield.Visibility = Visibility.Visible;
                ActivateShieldVisual();
            }
            else
            {
                // Heal 1 brick
                int brokenIndex = -1;
                for (int i = 11; i >= 0; i--)
                {
                    if (!bricks[i])
                    {
                        brokenIndex = i;
                        break;
                    }
                }
                if (brokenIndex != -1)
                {
                    bricks[brokenIndex] = true;
                    UpdateBrickVisual(brickBorders[brokenIndex], true);
                }
            }
        }

        // --- LEVEL UP & PAUSE ---
        private void TriggerLevelUp()
        {
            isPaused = true;
            currentStage++;

            // Update UI
            TxtPauseLevel.Text = string.Format(LangStr.SamarkandLevelUp, currentStage);
            PauseGrid.Visibility = Visibility.Visible;

            // Hide shield visual temporarily during pause
            TowerShieldVisual.Visibility = Visibility.Collapsed;

            // Play level up sound (bell)
            PlaySfx(sfxBellPlayer, sfxBellReader);

            try { StopBgm(); }
            catch { }

            // Clear words on screen
            foreach (var word in activeWords)
            {
                GameCanvas.Children.Remove(word.textBlock);
            }
            activeWords.Clear();
        }

        private void ResumeGame()
        {
            PauseGrid.Visibility = Visibility.Collapsed;
            TxtStage.Text = currentStage.ToString();
            lastSpawnTime = DateTime.Now;
            isPaused = false;
            InputTextBox.Focus();

            if (hasShield)
            {
                TowerShieldVisual.Visibility = Visibility.Visible;
                TowerShieldVisual.Opacity = 1.0;
            }

            if (this.volume != Volume.Off)
            {
                try { StartBgm(); }
                catch { }
            }
        }

        // Tower collapse animation → then actual game over screen
        private void TriggerTowerCollapse()
        {
            if (isGameOver) return;   // guard against double-trigger
            isGameOver = true;

            if (gameTimer != null) gameTimer.Stop();

            // Stop BGM, play collapse sound immediately
            try { StopBgm(); } catch { }
            PlaySfx(sfxGameOverPlayer, sfxGameOverReader);

            // Hide shield visual
            TowerShieldVisual.Visibility = Visibility.Collapsed;

            // Spawn extra debris from the whole tower
            for (int i = 0; i < 5; i++)
                SpawnDebris(Canvas.GetLeft(TowerGrid) + 120, GameCanvas.ActualHeight - 50);

            // Animate tower + dome sinking downward
            double towerTop    = Canvas.GetBottom(TowerGrid);
            double domeTop     = Canvas.GetBottom(DomePlatform);
            int    collapseStep = 0;
            double sinkSpeed    = 0;

            var collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            collapseTimer.Tick += (s, e) =>
            {
                collapseStep++;
                sinkSpeed += 0.6;                         // accelerate downwards
                towerTop -= sinkSpeed;
                domeTop  -= sinkSpeed;

                Canvas.SetBottom(TowerGrid,    towerTop);
                Canvas.SetBottom(DomePlatform, domeTop);

                // Wobble: alternate slight left/right tilt via RenderTransform
                double wobble = (collapseStep % 6 < 3) ? 3 : -3;
                TowerGrid.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                TowerGrid.RenderTransform = new RotateTransform(wobble);

                // Programmatic collapse sound sequencing:
                if (collapseStep == 10 || collapseStep == 20 || collapseStep == 30)
                {
                    PlaySfx(sfxBreakPlayer, sfxBreakReader);
                }
                else if (collapseStep == 40)
                {
                    PlaySfx(sfxWrongPlayer, sfxWrongReader);
                }

                // Spawn extra debris mid-collapse
                if (collapseStep % 5 == 0)
                    SpawnDebris(Canvas.GetLeft(TowerGrid) + 120 + (collapseStep % 2 == 0 ? 30 : -30),
                                GameCanvas.ActualHeight - Math.Max(0, towerTop));

                // After 40 frames (≈0.8 s) or sunk below screen, show game over
                if (collapseStep >= 40 || towerTop < -200)
                {
                    collapseTimer.Stop();
                    TowerGrid.RenderTransform = null;
                    ShowGameOverScreen();
                }
            };
            collapseTimer.Start();
        }

        private void TriggerGameOver()
        {
            isGameOver = true;
            if (gameTimer != null) gameTimer.Stop();

            try { StopBgm(); } catch { }
            PlaySfx(sfxGameOverPlayer, sfxGameOverReader);
            TowerShieldVisual.Visibility = Visibility.Collapsed;

            ShowGameOverScreen();
        }

        private void ShowGameOverScreen()
        {
            TxtFinalScore.Text = currentScore.ToString();
            TxtFinalStage.Text = currentStage.ToString();
            TxtRegisterStatus.Text = LangStr.ResourceManager.GetString("FetchServerData") ?? "Registering score...";

            GameOverGrid.Visibility = Visibility.Visible;

            // Auto-submit score in the background
            _ = AutoSaveScoreAsync();
        }

        private async Task AutoSaveScoreAsync()
        {
            if (!isGameOver) return; // Guard: No saving if the game was closed/exited midway

            string name = ((string)Settings.Default["Name"])?.Trim();
            string org = ((string)Settings.Default["Org"])?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                name = "Anonymous";
            }
            if (string.IsNullOrEmpty(org))
            {
                org = "Anonymous";
            }

            string country = (string)Settings.Default["Country"];
            string lang = (string)Settings.Default["ProgramLang"];
            string charType = lang == "ko" ? "한글" : "QWERTY";

            string dateStr = (lang == "uz")
                ? DateTime.Now.ToString("dd/MM/yy")
                : DateTime.Now.ToString("yy/MM/dd");

            VeneziaUser scoreRecord = new VeneziaUser(
                name,
                country,
                org,
                charType,
                currentStage,
                currentScore,
                dateStr
            );

            bool localRegistered = false;

            // Local cutoff: Score >= 1000 and Stage >= 2
            if (currentScore >= 1000 && currentStage >= 2)
            {
                try
                {
                    if (await Sqlite.OpenDatabase())
                    {
                        // Save to local SQLite
                        List<VeneziaUser> localRecords = await Sqlite.GetVeneziaUsersAsync();
                        localRecords.Add(scoreRecord);
                        localRecords.Sort();

                        // Keep top 10 only
                        if (localRecords.Count > 10)
                        {
                            localRecords.RemoveRange(10, localRecords.Count - 10);
                        }

                        await Sqlite.ReWriteAllVeneziaAsync(localRecords);
                        localRegistered = true;
                    }
                }
                catch (Exception ex)
                {
                    App.logger.Error("Failed to save high score: " + ex.ToString());
                }
            }
            // Display registration status message in the background Game Over screen
            string msg = "";

            if (localRegistered)
            {
                msg = LangStr.ResourceManager.GetString("SamarkandRegisterLocalMsg") ?? "Registered locally!";
            }
            else
            {
                // Not registered because score/stage were too low
                msg = LangStr.ResourceManager.GetString("SamarkandLocalLimitMsg") ?? "To register locally, you need at least 1000 points and Stage 2.";
            }

            TxtRegisterStatus.Text = msg;

            // Trigger event for menu integration if needed
            RtnUserRecord?.Invoke(new Dictionary<string, string>
            {
                { "score", currentScore.ToString() },
                { "stage", currentStage.ToString() }
            });
        }

        private void BtnCloseGame_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            SkipIntro();
        }

        private void BtnViewRules_Click(object sender, RoutedEventArgs e)
        {
            if (introTimer != null)
            {
                introTimer.Stop();
            }
            RulesGrid.Visibility = Visibility.Visible;
        }

        private void BtnCloseRules_Click(object sender, RoutedEventArgs e)
        {
            CloseRules();
        }

        private void CloseRules()
        {
            RulesGrid.Visibility = Visibility.Collapsed;
            if (introTimer != null)
            {
                introTimer.Start();
            }
        }

        // Keep focus on TextBox
        private void InputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            InputPlaceholder.Visibility = string.IsNullOrEmpty(InputTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!isPaused && !isGameOver && !isIntroPlaying)
            {
                // Force focus back to text box so user can keep typing
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => InputTextBox.Focus()));
            }
        }

        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat) return;

            if (e.Key == System.Windows.Input.Key.LeftShift || e.Key == System.Windows.Input.Key.RightShift ||
                e.Key == System.Windows.Input.Key.LeftCtrl || e.Key == System.Windows.Input.Key.RightCtrl ||
                e.Key == System.Windows.Input.Key.LeftAlt || e.Key == System.Windows.Input.Key.RightAlt ||
                e.Key == System.Windows.Input.Key.System || e.Key == System.Windows.Input.Key.Enter)
            {
                return;
            }

            if (this.volume == Volume.Up)
            {
                PlaySfx(sfxPressedPlayer, sfxPressedReader);
            }
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (isGameOver)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;

            // Trigger game over sequence directly (without tower collapse animation)
            TriggerGameOver();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            StopBgm();
            void DisposeNAudio(WaveOutEvent p, AudioFileReader r) { try { p?.Dispose(); r?.Dispose(); } catch { } }
            DisposeNAudio(bgmPlayer,       bgmReader);
            DisposeNAudio(sfxPressedPlayer, sfxPressedReader);
            DisposeNAudio(sfxBellPlayer,    sfxBellReader);
            DisposeNAudio(sfxComboPlayer,   sfxComboReader);
            DisposeNAudio(sfxBreakPlayer,   sfxBreakReader);
            DisposeNAudio(sfxGameOverPlayer, sfxGameOverReader);
            DisposeNAudio(sfxWrongPlayer,   sfxWrongReader);
        }
    }
}
