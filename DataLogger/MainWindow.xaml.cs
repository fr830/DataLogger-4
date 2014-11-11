using MathNet.Numerics.LinearRegression;
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
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;

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
        ObservableDataSource<Point> source1 = null;

        class TimeData
        {
            public string TimeStamp { get; set; }
            public double  TestData { get; set; }
            public double Diff { get; set; }
            public double Elapse { get; set; }


            public TimeData(DateTime currentTime, double data1, double elapse, double diff)
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
        double prevelapse = 0;
        double currentelapse = 0;
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

            source1 = new ObservableDataSource<Point>();
            source1.SetXYMapping(p => p);
            plotter.AddLineGraph(source1);
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
            double data1 = double.Parse(data);

            prevelapse = currentelapse;

            currentelapse = (double)elaspedTime / 1000;
           
            

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
            //double[] ytestdata = new double[] { 2.0430489, 2.0430446, 2.0430402, 2.0430358, 2.0430245 };
            //double[] xtestdata = new double[] { 0.19, 0.29, 0.39, 0.49, 0.59 };


            double Delta_m;
            double Delta_t;
            int startpos = 1;
            int endpos = 0;
            int count = expData.Count;
            endpos = count;
            double n = (double) (count - 1);
            double Umrdg = 0, Um = 0, Usigma = 0, Udeltam = 0, OneOverDeltaT = 0, UdeltaT = 0.002, DTDM = 0, Uimpurity = 0.004, Uhumidity = 0.054, Umassrep = 0, Utimerep = 0.008, Usysrep = 0.128;
            double U_eccentricity = 0.0007;
            double U_linearity = 0.0005;
            double U_sensitivity = 0.0008;
            double U_caluncer = 0.0006;
            double U_final = 0;

            if (count > 3)
            {

                double result = 0;

                double S_x =0, S_y = 0, S_xx = 0, S_xy = 0, S_yy = 0, beta = 0, alpha = 0, Se_beta2 = 0, Se_sigma2 = 0, Se_sigma = 0;

                for (int i = startpos; i < endpos; i++)
                {
                    S_x += expData.ElementAt(i).Elapse / 1000;
                    S_xx += Math.Pow(expData.ElementAt(i).Elapse / 1000, 2);
                    S_y += expData.ElementAt(i).TestData;
                    S_yy += Math.Pow(expData.ElementAt(i).TestData, 2);
                    S_xy += expData.ElementAt(i).Elapse * expData.ElementAt(i).TestData / 1000;
                }

                double density = double.Parse(tbDensity.Text);

                result = (expData.ElementAt(count - 1).TestData - expData.ElementAt(count - 2).TestData) / ((expData.ElementAt(count - 1).Elapse - expData.ElementAt(count - 2).Elapse) / 1000);

                //result = (ytestdata[ytestdata.Count() - 1] - ytestdata[ytestdata.Count() - 2]) / (xtestdata[xtestdata.Count() - 1] - xtestdata[xtestdata.Count() - 2]);

                //result = 15850.323141489 * System.Math.Abs(result / density);

                Delta_m = expData.ElementAt(endpos - 1).TestData - expData.ElementAt(startpos).TestData;

                Delta_t = (expData.ElementAt(endpos - 1).Elapse - expData.ElementAt(startpos).Elapse) / 1000;

                lblFlowRate.Content = result.ToString("G4");

                //double[] xdata = new double[count - 1];
                //double[] ydata = new double[count - 1];

                //for (int i = 1; i < count; i++)
                //{
                //    xdata[i - 1] = expData.ElementAt(i).Elapse / 1000;
                //    ydata[i - 1] = expData.ElementAt(i).TestData;
                //}


                //MathNet.Numerics.Tuple<double, double> p = SimpleRegression.Fit(xdata, ydata);
                //double a = p.Item1; // == 10; intercept
                //double b = p.Item2; // == 0.5; slope

                beta = (n * S_xy - S_x * S_y) / (n * S_xx - Math.Pow(S_x, 2));
                alpha = S_y / n - beta * S_x / n;

                Se_sigma2 = (1 / (n * (n - 2))) * (n * S_yy - Math.Pow(S_y, 2) - Math.Pow(beta, 2) * (n * S_xx - Math.Pow(S_x, 2)));

                Se_beta2 = n * Se_sigma2 / (n * S_xx - Math.Pow(S_x, 2));

                Se_sigma = Math.Sqrt(Se_beta2);

                lblAvgFlowRate.Content = beta.ToString("G4");

                lblStdev.Content = Se_sigma.ToString("G4");

                Umassrep = (2 * Math.Sqrt(2) * (0.09 / 1000000) * 100) / Math.Abs(Delta_m);

                Udeltam = (Math.Sqrt(Math.Pow(U_caluncer, 2) + Math.Pow(U_linearity, 2) + Math.Pow(U_sensitivity, 2) + Math.Pow(U_eccentricity, 2))) / 1000;

                OneOverDeltaT = 1 / Delta_t;

                Usigma = Se_sigma;

                DTDM = -1 * Delta_m / (Math.Pow(Delta_t, 2));

                Um = 2 * Math.Sqrt(Math.Pow((Udeltam * OneOverDeltaT),2) + Math.Pow((UdeltaT * DTDM) , 2) + Math.Pow(Usigma ,2));

                Umrdg = 100 * (Um / Math.Abs(beta));
                //   15850.323141489 * System.Math.Abs(b / density);

                U_final = Math.Sqrt(Math.Pow(Umrdg, 2) + Math.Pow(Uimpurity, 2) + Math.Pow(Uhumidity, 2) + Math.Pow(Umassrep, 2) + Math.Pow(Utimerep, 2) + Math.Pow(Usysrep, 2));

                lbluncer.Content = U_final.ToString("G4");
            }
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

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog dlg = new System.Windows.Forms.OpenFileDialog();
            dlg.DefaultExt = ".DAT";
            dlg.Filter = "DAT Files|*.DAT";

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {

                 string filename = dlg.FileName;

                 expData.Clear();
                 
                    string[] readText = File.ReadAllLines(filename);
                    int strCount = 0;
                    double currentElapse = 0;
                    double prevElapse = 0;
                    DateTime StartT = DateTime.Now;
                    DateTime dT1;
                    double mass;


                    foreach(string s in readText)
                    {
                        string[] split1 = s.Split(new char[] { ',' });


                        bool readStatus = true;
                        if (split1.Count() > 1)
                        {
                            string[] split2 = split1[1].Split(new char[] { ' ' });

                            if (split2.Count() > 4)
                            {

                                if (DateTime.TryParse(split1[0], out dT1) && double.TryParse(split2[3], out mass))
                                {
                                    if (strCount == 0)
                                    {
                                        StartT = dT1;
                                    }

                                    TimeSpan ds = dT1 - StartT;
                                    prevElapse = currentElapse;
                                    currentElapse = ds.TotalMilliseconds;

                                    expData.Add(new TimeData(dT1, mass, ds.TotalMilliseconds, (currentElapse - prevElapse)));

                                    Point p1 = new Point(ds.TotalMilliseconds / 1000, mass);
                                    source1.AppendAsync(Dispatcher, p1);
                                }
                                else
                                {
                                    readStatus = false;
                                }
                            }
                            else { readStatus = false; }
                        }
                        else { readStatus = false; }

                        if (readStatus)
                        {
                            strCount++;
                        }
                    }

                     GetInstFlowRate();

                     lblDataCount.Content = expData.Count;
            }

        }


        

    }
}
