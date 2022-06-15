using NLog;
using OpenTyping.Resources.Lang;
using System.IO;
using System.Management;
using System.Reflection;
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

        private async void App_DispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Error(e.Exception.ToString()); ; // leave the log to file
            e.Handled = true; // prevent the application from crashing

            string edition = "";
            ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
            foreach (ManagementObject managementObject in mos.Get())
            {
                if (managementObject["Caption"] != null)
                {
                    edition = managementObject["Caption"].ToString();
                }
            }

            string platformInfo =
                        $"\nMachineId: {SqliteProvider.machineId}\n" +
                        $"AppVersion: {Assembly.GetEntryAssembly().GetName().Version}\n" +
                        $"Edition: {edition}\n" +
                        $"osBuild: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}\n";

            string filePath = "./errors.log";
            File.AppendAllText(filePath, platformInfo);

            try
            {
                var provider = new RestfulProvider();
                if (await provider.SendErrorData(filePath))
                {
                    File.Delete(filePath); // log file delete
                }
            } 
            finally
            {
                MessageBox.Show(LangStr.UnhandledError, "TezTer", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(); // quit the application in a controlled way
            }
        }
    }
}