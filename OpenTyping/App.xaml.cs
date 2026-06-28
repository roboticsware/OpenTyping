using NLog;
using OpenTyping.Resources.Lang;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OpenTyping
{
    /// <summary>
    ///     App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public readonly static Logger logger = LogManager.GetCurrentClassLogger();

        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize sound engine and JIT compile critical typing paths in the background during startup
            _ = Task.Run(() =>
            {
                try
                {
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(SfxManager).TypeHandle);

                    // Pre-JIT compile critical sound methods
                    var playSfxMethod = typeof(SfxManager).GetMethod("PlaySfx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (playSfxMethod != null)
                    {
                        System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(playSfxMethod.MethodHandle);
                    }

                    var playPressedMethod = typeof(SfxManager).GetMethod("PlayPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (playPressedMethod != null)
                    {
                        System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(playPressedMethod.MethodHandle);
                    }

                    var playBellMethod = typeof(SfxManager).GetMethod("PlayBell", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (playBellMethod != null)
                    {
                        System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(playBellMethod.MethodHandle);
                    }

                    // Pre-JIT compile KeyPos & Differ methods
                    var fromKeyCodeMethod = typeof(KeyPos).GetMethod("FromKeyCode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (fromKeyCodeMethod != null)
                    {
                        System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(fromKeyCodeMethod.MethodHandle);
                    }

                    var diffMethod = typeof(Differ).GetMethod("Diff", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (diffMethod != null)
                    {
                        System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(diffMethod.MethodHandle);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Background pre-warm failed: " + ex.Message);
                }
            });
        }

        private void App_DispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Error(e.Exception.ToString()); // leave the log to file
            e.Handled = true; // prevent the application from crashing

            MessageBox.Show(LangStr.UnhandledError, "TezTer", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(); // quit the application in a controlled way
        }

        public static double BaseFontSize
        {
            get => (double)Current.Resources["BaseFontSize"];
            set => Current.Resources["BaseFontSize"] = value;
        }
    }
}