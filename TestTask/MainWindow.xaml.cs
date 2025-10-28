using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using x502api;
using lpcieapi;
using System.Threading;
using System.Windows.Threading;

namespace TestTask
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const uint RECV_BUF_SIZE = 8 * 1024 * 1024;
        const uint RECV_TOUT = 250;


        X502 hnd = null;
        X502.DevRec[] devrecs = null;
        Thread thread;
        bool threadRunning;
        bool stopRequest;

        UInt32[] rcv_buf;
        double[] adc_data;
        UInt32 adcSize;
        UInt32 firstLch;

        private delegate void updatedelegate();
        private delegate lpcie.Errs finishdelegate(lpcie.Errs err);
        public MainWindow()
        {
            InitializeComponent();
            threadRunning = false;
            refreshDevList();
            cbbInit();
        }
       
        
        private lpcie.Errs setAdcFreq()
        {
            lpcie.Errs err;
            double freq_adc = Convert.ToDouble(set_adc_freq_textbox.Text);
            double freq_lch = Convert.ToDouble(set_lch_freq_textbox.Text);
            err = hnd.SetAdcFreq(ref freq_adc, ref freq_lch);
            if(err == lpcie.Errs.OK)
            {
                set_adc_freq_textbox.Text = freq_adc.ToString();
                set_lch_freq_textbox.Text = freq_lch.ToString();
            }
            return err;
        }

        private lpcie.Errs setParams()
        {
            hnd.LChannelCount = 5;

            X502.LchMode[] lchMode = new X502.LchMode[] {X502.LchMode.DIFF, X502.LchMode.COMM, X502.LchMode.ZERO};
            X502.AdcRange[] adcRanges = new X502.AdcRange[] {X502.AdcRange.RANGE_10, X502.AdcRange.RANGE_5, X502.AdcRange.RANGE_2,
            X502.AdcRange.RANGE_1, X502 .AdcRange.RANGE_05, X502.AdcRange.RANGE_02};

            lpcie.Errs err = hnd.SetLChannel(0, Convert.ToUInt32(cbox_chn_channel_1.SelectedIndex + 1),
                lchMode[cbox_chn_mode_1.SelectedIndex], adcRanges[cbox_chn_range_1.SelectedIndex], 0); 
            if(err == lpcie.Errs.OK)
            {
                err = hnd.SetLChannel(1, Convert.ToUInt32(cbox_chn_channel_2.SelectedIndex + 1), 
                    lchMode[cbox_chn_mode_2.SelectedIndex], adcRanges[cbox_chn_range_2.SelectedIndex], 0);
            }
            if(err == lpcie.Errs.OK)
            {
                err = hnd.SetLChannel(1, Convert.ToUInt32(cbox_chn_channel_3.SelectedIndex + 1), 
                    lchMode[cbox_chn_mode_3.SelectedIndex], adcRanges[cbox_chn_range_3.SelectedIndex], 0);
            }
            if(err == lpcie.Errs.OK)
            {
                err = hnd.SetLChannel(1, Convert.ToUInt32(cbox_chn_channel_5.SelectedIndex + 1), 
                    lchMode[cbox_chn_mode_4.SelectedIndex], adcRanges[cbox_chn_range_4.SelectedIndex], 0);
            }
            if(err == lpcie.Errs.OK)
            {
                err = hnd.SetLChannel(1, Convert.ToUInt32(cbox_chn_channel_5.SelectedIndex + 1), 
                    lchMode[cbox_chn_mode_5.SelectedIndex], adcRanges[cbox_chn_range_5.SelectedIndex], 0);
            }

            if(err == lpcie.Errs.OK)
            {
                err = setAdcFreq();
            }
            if(err == lpcie.Errs.OK)
            {
                err = hnd.Configure(0);
            }
            
            return err;
            
        }
        
        private void treadFunc()
        {
            stopRequest = false;
            lpcie.Errs err = hnd.StreamsStart();
            if(err == lpcie.Errs.OK)
            {
                while (stopRequest == false)
                {
                    Int32 rcv_size = hnd.Recv(rcv_buf, RECV_BUF_SIZE, RECV_TOUT);
                    if(rcv_size < 0)
                    {
                        err = (lpcie.Errs)(rcv_size);
                    }
                    else if(rcv_size > 0)
                    {
                        adcSize = RECV_BUF_SIZE;
                        firstLch = hnd.NextExpectedLchNum;
                        err = hnd.ProcessAdcData(rcv_buf, adc_data, ref adcSize, X502.ProcFlags.VOLT);

                        if(err == lpcie.Errs.OK)
                        {
                            UpdateData();
                        }
                    }
                }
            }
            lpcie.Errs stop_err = hnd.StreamsStop();
            if(err == lpcie.Errs.OK)
            {
                err = stop_err;
            }
            finishThread(err);
           
        }

        private void finishThread(lpcie.Errs err)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new updatedelegate(UpdateData));
            }
            else
            {
                if (err == lpcie.Errs.OK)
                {
                    threadRunning = false;
                    updateControls();
                }
                else
                {
                    MessageBox.Show(X502.GetErrorString(err), "Сбор данных завершен с ошибкой");
                }
            }
        }

        private void UpdateData()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new updatedelegate(UpdateData));
            }
            else
            {
                TextBox[] results = {data_lch_1, data_lch_2,
                data_lch_3,  data_lch_4,  data_lch_5 };
                for(uint i = 0; (i < 5) && (i < adcSize); i++)
                    results[(firstLch + i) % 5].Text = adc_data[i].ToString("F7");
            }
        }

        private void deviceClose()
        {
            if (hnd != null)
            {
                if (threadRunning)
                {
                    stopRequest = true;
                    while (threadRunning)
                        Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(() => { }));
                }
                hnd.Close();
                hnd = null;
            }
        }

        private void updateControls()
        {
            btnOpen.Content = (hnd == null) ? "Установить соединение?" : "Разорвать соединение?";
            btnStart.IsEnabled = (hnd != null);
            btnStop.IsEnabled = (hnd != null);
            btnRefreshSerialList.IsEnabled = hnd == null;
            data_lch_1.IsEnabled = (hnd == null);
            data_lch_2.IsEnabled = (hnd == null);
            data_lch_3.IsEnabled = (hnd == null);
            data_lch_4.IsEnabled = (hnd == null);
            data_lch_5.IsEnabled = (hnd == null);
            set_adc_freq_textbox.IsEnabled = (hnd == null);
            set_lch_freq_textbox.IsEnabled = (hnd == null);
        }

        private void refreshDevList()
        {
            cbox_serial_list.Items.Clear();
            L502.GetDevRecordsList(out devrecs, 0);
            for (int i = 0; i < devrecs.Length; i++)
            {
                cbox_serial_list.Items.Add(devrecs[i].DevName + ", " + devrecs[i].Serial);
            }
            if (devrecs.Length > 0)
            {
                cbox_serial_list.SelectedItem = 0;
            }
            updateControls();
        }

        private void cbbInit()
        {
            var cbbChannels = new ComboBox[]
            {
                cbox_chn_channel_1,
                cbox_chn_channel_2,
                cbox_chn_channel_3,
                cbox_chn_channel_4,
                cbox_chn_channel_5
            };
            var cbbModes = new ComboBox[]
            {
                cbox_chn_mode_1,
                cbox_chn_mode_2,
                cbox_chn_mode_3,
                cbox_chn_mode_4,
                cbox_chn_mode_5
            };
            var cbbRange = new ComboBox[]
            {
                cbox_chn_range_1,
                cbox_chn_range_2,
                cbox_chn_range_3,
                cbox_chn_range_4,
                cbox_chn_range_5
            };
            string[] modes = { "DIFF", "COMM", "ZERO" };
            double[] range = { 10.0, 5.0, 2.0, 1.0, 0.5, 0.2 };
            int i = 0;
            foreach (var cbb in cbbChannels)
            {
                cbb.ItemsSource = Enumerable.Range(1, 16);
                cbb.SelectedIndex = i;
                i++;
            }
            foreach (var cbb in cbbModes)
            {
                cbb.ItemsSource = modes;
                cbb.SelectedIndex = 0;
            }
            foreach (var cbb in cbbRange)
            {
                cbb.ItemsSource = range;
                cbb.SelectedIndex = 0;
            }
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (hnd == null)
            {
                lpcie.Errs res;
                int index = cbox_serial_list.SelectedIndex;
                if (index >= 0)
                {
                    hnd = X502.Create(devrecs[index].DevName);
                    res = hnd.Open(devrecs[index]);
                    if (res == lpcie.Errs.OK)
                    {
                        MessageBox.Show("Успешно");
                    }
                    else
                    {
                        MessageBox.Show(X502.GetErrorString(res), "Не удается установить соединение");
                        hnd = null;
                    }
                }
            }
            else
            {
                deviceClose();
            }
            updateControls();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            lpcie.Errs err = setParams();
            if (err == lpcie.Errs.OK)
            {
                err = hnd.StreamsEnable(L502.Streams.ADC);
            }
            if (err == lpcie.Errs.OK)
            {
                thread = new Thread(this.treadFunc);
                thread.Start();
                threadRunning = true;
                updateControls();
            }
            else
            {
                MessageBox.Show(X502.GetErrorString(err), "Ошибка настройки модуля");
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (threadRunning)
                stopRequest = true;
            btnStop.IsEnabled = false;
        }

        private void btnRefreshSerialList_Click(object sender, RoutedEventArgs e)
        {
            refreshDevList();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            deviceClose();
        }
    }
}
