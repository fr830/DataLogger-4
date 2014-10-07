using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DataLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 


    public static class Retry
    {
        public static void Do(
            Action action,
            TimeSpan retryInterval,
            int retryCount = 3)
        {
            Do<object>(() =>
            {
                action();
                return null;
            }, retryInterval, retryCount);
        }

        public static T Do<T>(
            Func<T> action,
            TimeSpan retryInterval,
            int retryCount = 3)
        {
            var exceptions = new List<Exception>();
            var exp1 = new Exception();
            for (int retry = 0; retry < retryCount; retry++)
            {
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    exp1 = ex;
                    exceptions.Add(ex);
                    Thread.Sleep(retryInterval);
                }
            }

            throw exp1;
        }
    }
    

    public partial class MainWindow : Window
    {

        class TimeData
        {
            public string TimeStamp { get; set; }
            public float  TestData { get; set; }
            public float Diff { get; set; }
            public float Elapse { get; set; }


            public TimeData(DateTime currentTime, float data1, float elapse, float diff)
            {
           
                TimeStamp = currentTime.ToString("HH:mm:ss.fff");
                TestData = data1;
                Diff = diff;
                Elapse = elapse;
    
            }

        }
        int interval;
        public delegate void TimeLoop(long TimeData, string data);
        public delegate void Calculation();
        public Calculation cal;
        public TimeLoop myDelegate;
        System.Timers.Timer timer = null;
        DateTime PrevTime;
        float prevelapse = 0;
        float currentelapse = 0;
        DateTime currentT;
        DateTime StartT;
        Stopwatch stopwatch;
        string[] delimited = {"\r","\n","#","@"};
        string[] delimitmarkup = { "\\r", "\\n", "#", "@" };
        string selectedDelimiter;
        SerialPort serialMonitor;
        private readonly MicroLibrary.MicroTimer _microTimer;
        MicroLibrary.MicroStopwatch _microStopwatch;
        List<double> testdatalst;
        List<TimeData> expData;
        System.Timers.Timer calTimer = null;

        public MainWindow()
        {
            InitializeComponent();
            myDelegate = new TimeLoop(updateData);
            cal = new Calculation(GetInstFlowRate);
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                cmbPorts.Items.Add(port);
            }

            foreach (string delimiter in delimitmarkup)
            {
                cmbDelimiter.Items.Add(delimiter);
            }

            cmbDelimiter.SelectedIndex = 0;
            expData = new List<TimeData>();
            stopwatch = new Stopwatch();

            serialMonitor = new SerialPort();
            serialMonitor.BaudRate = 9600;
            serialMonitor.Handshake = System.IO.Ports.Handshake.None;
            serialMonitor.Parity = Parity.None;
            serialMonitor.DataBits = 8;
            serialMonitor.StopBits = StopBits.One;
            serialMonitor.ReadTimeout = 200;
            serialMonitor.WriteTimeout = 50;
            
            calTimer = new System.Timers.Timer();
            calTimer.Interval = 1000;
            calTimer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);

            _microStopwatch = new MicroLibrary.MicroStopwatch();

            _microTimer = new MicroLibrary.MicroTimer();
        }

        private void Recieve(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            // Collecting the characters received to our 'buffer' (string).
            string received_data = "AAA";
            long start_time = 0;
            bool isError = false;
            try
            {
                received_data = serialMonitor.ReadTo(selectedDelimiter);
            }
            catch
            { isError = true; }

            if (isError == false)
            {
                if (_microStopwatch.IsRunning == false)
                {
                    _microStopwatch.Start();
                    Dispatcher.BeginInvoke(myDelegate, start_time, received_data);
                }
                else
                {
                    Dispatcher.BeginInvoke(myDelegate,_microStopwatch.ElapsedMicroseconds,received_data);
                }

            }


        }


          void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {

            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.SystemIdle, cal);
            

        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_microStopwatch.IsRunning)
            {
                _microStopwatch.Stop();    
            }

            this.Close();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            serialMonitor.DiscardInBuffer();
            serialMonitor.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(Recieve);

            calTimer.Start();
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
        }


        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
           // timer.Stop();
            prevelapse = 0;
            currentelapse = 0;
            serialMonitor.DiscardInBuffer();
            serialMonitor.DataReceived -= new System.IO.Ports.SerialDataReceivedEventHandler(Recieve);
            _microStopwatch.Stop();
            _microStopwatch.Reset();
            calTimer.Stop();
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
        }


        private void updateData(long elaspedTime, string data)
        {
            float data1 = float.Parse(data);

            prevelapse = currentelapse;

            currentelapse = (float)elaspedTime / 1000;
           
            

            if (currentelapse == 0)
            {
                StartT = DateTime.Now;
            }

            
            currentT = StartT.AddMilliseconds(currentelapse);

            expData.Add(new TimeData(currentT, data1, currentelapse, (currentelapse - prevelapse)));

            lblDataCount.Content = expData.Count;
            
            //if (expData.Count > 5)
            //{
            //    lblFlowRate.Content = GetInstFlowRate(expData);
            //}

 //           TestFunction();
            

//            this.lstData.Items.Add(new TimeData(currentT, data, currentelapse, (currentelapse - prevelapse)));

//            lstData.ScrollIntoView(lstData.Items.GetItemAt(lstData.Items.Count - 1));
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
//            lstData.Items.Clear();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            

            if (btnConnect.Content.ToString().Contains("Connect"))
            {

                try
                {
                    serialMonitor.PortName = cmbPorts.Text;
                    serialMonitor.Open();
                    btnConnect.Content = "Disconnect";
                    btnStart.IsEnabled = true;
                }
                catch
                {

                    System.Windows.MessageBox.Show("Cannot Open Serial Port " + cmbPorts.Text + "!");
                    btnConnect.Content = "Connect";
                }
            }
            else
            {
                try
                {
                    serialMonitor.Close();
                    btnConnect.Content = "Connect";
                    btnStart.IsEnabled = false;
                    btnStop.IsEnabled = false;
                    btnStop_Click(null,null);
                }
                catch
                {

                }

            }



        }


        private bool FileInUse(string path)
        {

            try
            {
                StreamWriter sw = new StreamWriter(path, false);
                sw.Close();

            }
            catch (IOException ex)
            {

                return true;
            }

            return false;
        }

        private void btnExp_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog
            {
                Title = "Choose file to save",
                FileName = "Data_" + DateTime.Today.ToString("MM_dd_yyyy") + "_" + DateTime.Now.ToString("HH_mm_ss") + ".csv",
                Filter = "CSV (*.csv)|*.csv",
                FilterIndex = 0,
                RestoreDirectory = true
            };


            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filename = sfd.FileName;



                if (FileInUse(filename) == false)
                {
                    StreamWriter sw = new StreamWriter(filename, false);

                    sw.Write("Time,Data,Elapse(ms),Diff(ms),");

                    sw.Write(sw.NewLine);

                    for (int i = 0; i < expData.Count - 1; i++)
                    {
                        TimeData dataitem = (TimeData)expData.ElementAt(i);

                        sw.Write(dataitem.TimeStamp.ToString());

                        sw.Write(",");

                        sw.Write(dataitem.TestData);

                        sw.Write(",");

                        sw.Write(dataitem.Elapse);
                        
                        sw.Write(",");

                        sw.Write(dataitem.Diff);

                        sw.Write(sw.NewLine);
                    }
                    sw.Close();
                }
                else
                {
                    MessageBox.Show("File Already Open!");
                }

            }

        }

        private void tbDelimit_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void cmbDelimiter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedDelimiter = delimited[cmbDelimiter.SelectedIndex];
        }

        private void GetInstFlowRate()
        {
            int count = expData.Count;

            float result = 0;

            result = (expData.ElementAt(count - 1).TestData - expData.ElementAt(count - 2).TestData) / (expData.ElementAt(count - 1).Elapse - expData.ElementAt(count - 2).Elapse);

            lblFlowRate.Content = result;
            
        }


        private void TestFunction()
        {
            long seed = Environment.TickCount;
            long result = seed;
            long count = 100000;
            for (int i = 0; i < count; ++i)
            {
                result ^= i ^ seed; // Some useless bit operations
            }

            Random rnd = new Random();

            double n = rnd.NextDouble();

            lblFlowRate.Content = n;
        }

    }
}
