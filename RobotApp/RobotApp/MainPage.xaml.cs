using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Storage;
using Windows.UI.Core;
using Microsoft.Band;
using Microsoft.Band.Sensors;

namespace RobotApp
{
    public sealed partial class MainPage
    {
        private static String defaultHostName = "169.254.250.82";
        public static String serverHostName = defaultHostName; // read from config file
        public static bool isRobot = true; // determined by existence of hostName

        public static Stopwatch stopwatch;
        private Timer timer;
        private double xValue;
        private double yValue;
        private double zValue;
        private IBandClient client;

        public string XString { get; set; }
        public string YString { get; set; }
        public string ZString { get; set; }

        /// <summary>
        /// MainPage initialize all asynchronous functions
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            stopwatch = new Stopwatch();
            stopwatch.Start();
            
            GetModeAndStartup();
        }
        
        public async void TryGetBand()
        {
            if (isRobot) return;
            try
            {
                // Get the list of Microsoft Bands paired to the phone.
                IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();

                if (pairedBands.Length < 1) return;

                // Connect to Microsoft Band.
                client = await BandClientManager.Instance.ConnectAsync(pairedBands[0]);

                UserConsent currentUserConsent = client.SensorManager.Accelerometer.GetCurrentUserConsent();

                if (currentUserConsent == UserConsent.NotSpecified)
                {
                    await client.SensorManager.Accelerometer.RequestUserConsentAsync();
                }
                else if (currentUserConsent == UserConsent.Declined)
                {
                    return;
                }

                client.SensorManager.Accelerometer.ReportingInterval = client.SensorManager.Accelerometer.SupportedReportingIntervals.ToList()[2];

                timer = new Timer(UpdateControls, null, 1000, 1000);
                client.SensorManager.Accelerometer.ReadingChanged += OnChanged;
                await client.SensorManager.Accelerometer.StartReadingsAsync();
            }
            catch (Exception ex)
            {
                string message = ex.Message;
            }
        }

        private void OnChanged(object sender, BandSensorReadingEventArgs<IBandAccelerometerReading> e)
        {
            double accelerationX = e.SensorReading.AccelerationX;
            double accelerationY = e.SensorReading.AccelerationY;
            double accelerationZ = e.SensorReading.AccelerationZ;

            xValue = accelerationX.Equals(0) ? xValue : accelerationX;
            yValue = accelerationY.Equals(0) ? yValue : accelerationY;
            zValue = accelerationZ.Equals(0) ? zValue : accelerationZ;
        }

        private async void UpdateControls(object state)
        {
            if (isRobot) return;
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, delegate
            {
                x.Text = xValue.ToString();
                y.Text = yValue.ToString();
                z.Text = zValue.ToString();

                bool left = yValue > 0.2;
                bool right = yValue < -0.2;
                bool forwards = xValue > 0.2;
                bool backwards = xValue < -0.2;

                if (forwards && left)
                {
                    TouchDir(Controllers.CtrlCmds.ForwardLeft);
                    return;
                }
                if (forwards && right)
                {
                    TouchDir(Controllers.CtrlCmds.ForwardRight);
                    return;
                }
                if (backwards && left)
                {
                    TouchDir(Controllers.CtrlCmds.BackLeft);
                    return;
                }
                if (backwards && right)
                {
                    TouchDir(Controllers.CtrlCmds.BackRight);
                    return;
                }

                if (left)
                {
                    TouchDir(Controllers.CtrlCmds.Left);
                    return;
                }

                if (right)
                {
                    TouchDir(Controllers.CtrlCmds.Right);
                    return;
                }

                if (forwards)
                {
                    TouchDir(Controllers.CtrlCmds.Forward);
                    return;
                }

                if (backwards)
                {
                    TouchDir(Controllers.CtrlCmds.Backward);
                    return;
                }

                TouchDir(Controllers.CtrlCmds.Stop);
            });
        }


        /// <summary>
        /// Show the current running mode
        /// </summary>
        public void ShowStartupStatus()
        {
            this.CurrentState.Text = "Robot-Kit Sample";
            this.Connection.Text = (isRobot ? ("Robot to " + serverHostName) : "Controller");
        }

        /// <summary>
        /// Switch and store the current running mode in local config file
        /// </summary>
        public async void SwitchRunningMode()
        {
            try
            {
                if (serverHostName.Length > 0) serverHostName = "";
                else serverHostName = defaultHostName;

                StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                StorageFile configFile = await storageFolder.CreateFileAsync("config.txt", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(configFile, serverHostName);

                isRobot = serverHostName.Length > 0;
                ShowStartupStatus();
                NetworkCmd.NetworkInit(serverHostName);
                
                TryGetBand();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetRunningMode() - " + ex.Message);
            }
        }

        /// <summary>
        /// Read the current running mode (controller host name) from local config file.
        /// Initialize accordingly
        /// </summary>
        public async void GetModeAndStartup()
        {
            try
            {
                StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
                StorageFile configFile = await storageFolder.GetFileAsync("config.txt");
                String fileContent = await FileIO.ReadTextAsync(configFile);

                serverHostName = fileContent;
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("GetRunningMode() - configuration does not exist yet.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GetRunningMode() - " + ex.Message);
            }

            isRobot = (serverHostName.Length > 0);
            ShowStartupStatus();

            Controllers.XboxJoystickInit();
            NetworkCmd.NetworkInit(serverHostName);
            if (isRobot) MotorCtrl.MotorsInit();
            Controllers.SetRobotDirection(Controllers.CtrlCmds.Stop, (int)Controllers.CtrlSpeeds.Max);
        }
    }
}
