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

        private L502Device _device;

        public ComboBox[] cbbChannels;
        public ComboBox[] cbbModes;
        public ComboBox[] cbbRanges;

        private delegate void updatedelegate();

        public MainWindow()
        {
            InitializeComponent();
            _device = new L502Device(this);
            refreshDevList();
            cbbInit();
        }

        /// <summary>
        /// Обновление элементов WPF
        /// </summary>
        public void updateControls()
        {
            btnOpen.Content = (_device.hndIsNull) ? "Установить соединение?" : "Разорвать соединение?";
            btnStart.IsEnabled = (!_device.hndIsNull) && (!_device.ThreadRunning);
            btnStop.IsEnabled = _device.ThreadRunning;
            btnRefreshSerialList.IsEnabled = _device.hndIsNull;
            data_lch_1.IsEnabled = _device.hndIsNull;
            data_lch_2.IsEnabled = _device.hndIsNull;
            data_lch_3.IsEnabled = _device.hndIsNull;
            data_lch_4.IsEnabled = _device.hndIsNull;
            data_lch_5.IsEnabled = _device.hndIsNull;
            set_adc_freq_textbox.IsEnabled = _device.hndIsNull;
            set_lch_freq_textbox.IsEnabled = _device.hndIsNull;
        }

        /// <summary>
        /// Обновления списка обнаруженных устройств на форме
        /// </summary>
        private void refreshDevList()
        {
            cbox_serial_list.Items.Clear();
            _device.RefreshDeviceList();
            for (int i = 0; i < _device.devrecs.Length; i++)
            {
                cbox_serial_list.Items.Add(_device.devrecs[i].DevName + ", " + _device.devrecs[i].Serial);
            }
            if (_device.devrecs.Length > 0)
            {
                cbox_serial_list.SelectedIndex = 0;
            }
            updateControls();
        }

        /// <summary>
        /// Инициализация всех ComboBox
        /// </summary>
        private void cbbInit()
        {
            cbbChannels = new ComboBox[]
            {
                cbox_chn_channel_1,
                cbox_chn_channel_2,
                cbox_chn_channel_3,
                cbox_chn_channel_4,
                cbox_chn_channel_5
            };
            cbbModes = new ComboBox[]
            {
                cbox_chn_mode_1,
                cbox_chn_mode_2,
                cbox_chn_mode_3,
                cbox_chn_mode_4,
                cbox_chn_mode_5
            };
            cbbRanges = new ComboBox[]
            {
                cbox_chn_range_1,
                cbox_chn_range_2,
                cbox_chn_range_3,
                cbox_chn_range_4,
                cbox_chn_range_5
            };
            string[] modes = { "COMM", "DIFF", "ZERO" };
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
            foreach (var cbb in cbbRanges)
            {
                cbb.ItemsSource = range;
                cbb.SelectedIndex = 0;
            }
        }

        /// <summary>
        ///  Структура, с помощью которой я передаю параметры из логических каналов
        /// </summary>
        public struct LChannelParams
        {
            public uint logicChannel;
            public uint selectedChannel;
            public X502.LchMode selectedMode;
            public X502.AdcRange selectedRange;
        }

        /// <summary>
        /// Запись установленных данных лог. каналов и частот сбора; передача их в класс устройства
        /// </summary>
        /// <returns></returns>
        public lpcie.Errs setParams()
        {
            //Запись установленных пользователем параметров логических каналов
            LChannelParams[] lchannels = new LChannelParams[5];

            X502.LchMode[] lchModes = new X502.LchMode[] { X502.LchMode.COMM, X502.LchMode.DIFF, X502.LchMode.ZERO };
            X502.AdcRange[] adcRanges = new X502.AdcRange[] {X502.AdcRange.RANGE_10, X502.AdcRange.RANGE_5, X502.AdcRange.RANGE_2,
                X502.AdcRange.RANGE_1, X502 .AdcRange.RANGE_05, X502.AdcRange.RANGE_02};

            for (int i = 0; i < lchannels.Length; i++)
            {
                lchannels[i] = new LChannelParams();
                lchannels[i].logicChannel = Convert.ToUInt32(i);
                lchannels[i].selectedChannel = Convert.ToUInt32(cbbChannels[i].SelectedIndex);
                lchannels[i].selectedMode = lchModes[cbbModes[i].SelectedIndex];
                lchannels[i].selectedRange = adcRanges[cbbRanges[i].SelectedIndex];
            }
            
            //Запись установленных пользователем значений частот сбора АЦП и обработки одного канала
            double freq_adc = Convert.ToDouble(set_adc_freq_textbox.Text);
            double freq_lch = Convert.ToDouble(set_lch_freq_textbox.Text);

            //Передача установленных параметров
            lpcie.Errs err = _device.SetParams(lchannels, ref freq_adc, ref freq_lch);

            if (err == lpcie.Errs.OK)
            {
                set_adc_freq_textbox.Text = freq_adc.ToString();
                set_lch_freq_textbox.Text = freq_lch.ToString();
            }
            return err;

        }

        /// <summary>
        /// Обновление считываемых данных. Вызывается из рабочего потока
        /// </summary>
        public void UpdateData()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new updatedelegate(UpdateData));
            }
            else
            {
                TextBox[] results = {data_lch_1, data_lch_2,
                data_lch_3,  data_lch_4,  data_lch_5 };

                for (uint i = 0; (i < 5) && (i < _device.AdcSize); i++)
                    results[(_device.FirstLch + i) % 5].Text = _device.Adc_data[i].ToString("F7");
            }
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            
            if (_device.hndIsNull)
            {
                lpcie.Errs res;
                int index = cbox_serial_list.SelectedIndex;
                if (index >= 0)
                {
                    res = _device.Open(index);
                    
                    if (res == lpcie.Errs.OK)
                    {
                        MessageBox.Show("Успешно");
                    }
                    else
                    {
                        MessageBox.Show(X502.GetErrorString(res), "Не удается установить соединение");
                        _device.Close();
                    }
                }
            }
            else
            {
                _device.Close();
            }
            updateControls();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            lpcie.Errs err = setParams();
            if (err == lpcie.Errs.OK)
            {
                err = _device.EnableStream();
            }
            if (err == lpcie.Errs.OK)
            {
                _device.StartThread();
                updateControls();
            }
            else
            {
                MessageBox.Show(X502.GetErrorString(err), "Ошибка настройки модуля");
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            _device.Stop();
            btnStop.IsEnabled = false;
        }

        private void btnRefreshSerialList_Click(object sender, RoutedEventArgs e)
        {
            refreshDevList();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _device.Close();
        }
    }
}
