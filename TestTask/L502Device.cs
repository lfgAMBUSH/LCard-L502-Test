using lpcieapi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using x502api;


namespace TestTask
{
    public class L502Device
    {
        public const uint RECV_BUF_SIZE = 8 * 1024 * 1024;
        public const uint RECV_TOUT = 250;

        public MainWindow main;

        private X502 hnd;
        public bool hndIsNull
        {
            get
            {
                if (hnd == null)
                    return true;
                else
                    return false;
            }
        }
        public X502.DevRec[] devrecs { get; private set; }
        public bool ThreadRunning { get; private set; }
        public bool StopRequest { get; private set; }

        public UInt32[] Rcv_buf { get; private set; }
        public double[] Adc_data { get; private set; }

        private UInt32 adcSize;
        public UInt32 AdcSize { get; private set; }
        public UInt32 FirstLch { get; private set; }

        private Thread thread;

        public L502Device(MainWindow main)
        {
            Rcv_buf = new UInt32[RECV_BUF_SIZE];
            Adc_data = new double[RECV_BUF_SIZE];
            ThreadRunning = false;
            this.main = main;
        }

        /// <summary>
        /// Открытие соединения из devrecs по выбранному индексу
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public lpcie.Errs Open(int num)
        {
            lpcie.Errs err;
            hnd = X502.Create(devrecs[num].DevName);
            err = hnd.Open(devrecs[num]);
            return err;
        }

        /// <summary>
        /// Закрытие соединения и остановка сбора данных. 
        /// </summary>
        public void Close()
        {
            if (hnd != null)
            {
                if (ThreadRunning)
                {
                    StopRequest = true;
                    while (ThreadRunning)
                        main.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(() => { }));
                }
                hnd.Close();
                hnd = null;
            }
        }

        /// <summary>
        /// Метод, реализующий работу потока считывания данных
        /// </summary>
        private void threadFunc()
        {
            StopRequest = false;
            lpcie.Errs err = hnd.StreamsStart();
            if (err == lpcie.Errs.OK)
            {
                while (StopRequest == false)
                {
                    Int32 rcv_size = hnd.Recv(Rcv_buf, RECV_BUF_SIZE, RECV_TOUT);
                    if (rcv_size < 0)
                    {
                        err = (lpcie.Errs)(rcv_size);
                    }
                    else if (rcv_size > 0)
                    {
                        adcSize = RECV_BUF_SIZE;
                        FirstLch = hnd.NextExpectedLchNum;
                        err = hnd.ProcessAdcData(Rcv_buf, Adc_data, ref adcSize, X502.ProcFlags.VOLT);

                        if (err == lpcie.Errs.OK)
                        {
                            main.UpdateData();
                        }
                    }
                }
            }
            lpcie.Errs stop_err = hnd.StreamsStop();
            if (err == lpcie.Errs.OK)
            {
                err = stop_err;
            }
            if (err != lpcie.Errs.OK)
            {
                MessageBox.Show(X502.GetErrorString(err), "Сбор данных завершен с ошибкой");
            }
            main.updateControls();
            ThreadRunning = false;

        }
        
        /// <summary>
        /// Метод, активирующий потоковый режим 
        /// </summary>
        /// <returns></returns>
        public lpcie.Errs EnableStream()
        {
            return hnd.StreamsEnable(X502.Streams.ADC);
        }

        /// <summary>
        /// Метод, реализующий запуск потока сбора данных
        /// </summary>
        public void StartThread()
        {
            thread = new Thread(() => threadFunc());
            thread.Start();
            ThreadRunning = true;
        }

        /// <summary>
        /// Метод, останавливающий поток
        /// </summary>
        public void Stop()
        {
            if (ThreadRunning)
                StopRequest = true;
        }

        /// <summary>
        /// Метод, определяющий параметры логических каналов и частоты сбора АЦП
        /// </summary>
        /// <param name="lchannels"></param>
        /// <param name="freq_adc"></param>
        /// <param name="freq_lch"></param>
        /// <returns></returns>
        public lpcie.Errs SetParams(MainWindow.LChannelParams[] lchannels, ref double freq_adc, ref double freq_lch)
        {
            hnd.LChannelCount = Convert.ToUInt32(lchannels.Length);
            
            X502.LchMode[] lchMode = new X502.LchMode[] { X502.LchMode.COMM, X502.LchMode.DIFF, X502.LchMode.ZERO };
            X502.AdcRange[] adcRanges = new X502.AdcRange[] {X502.AdcRange.RANGE_10, X502.AdcRange.RANGE_5, X502.AdcRange.RANGE_2,
                X502.AdcRange.RANGE_1, X502 .AdcRange.RANGE_05, X502.AdcRange.RANGE_02};

            lpcie.Errs err = lpcie.Errs.OK;
            uint i = 0;
            while (err == lpcie.Errs.OK && i < lchannels.Length)
            {
                err = hnd.SetLChannel(lchannels[i].logicChannel, lchannels[i].selectedChannel,
                    lchannels[i].selectedMode, lchannels[i].selectedRange, 0);
                i++;
            }
            if(err == lpcie.Errs.OK)
            {
                err = hnd.SetAdcFreq(ref freq_adc, ref freq_lch);
            }
            if(err == lpcie.Errs.OK)
            {
                err = hnd.Configure(0);
            }
            return err;
        }

        /// <summary>
        /// Обновление списка обнаруженных устройств и запись в devresc
        /// </summary>
        public void RefreshDeviceList()
        {
            L502.GetDevRecordsList(out var pci_devices, 0);
            if(pci_devices.Length > 0)
            {
                devrecs = new X502.DevRec[pci_devices.Length];
            }
            else
            {
                devrecs = new X502.DevRec[0];
            }
            pci_devices.CopyTo(devrecs, 0);
        } 
    }
}
