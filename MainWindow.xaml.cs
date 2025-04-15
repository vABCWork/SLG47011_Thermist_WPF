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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Markup;
using System.Reflection;
using System.Security.Cryptography;
using ScottPlot;
using ScottPlot.Colormaps;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Drawing.Drawing2D;
using System.IO;


namespace test_ch347
{

    // 履歴(ヒストリ)データ　クラス
    // クラス名: HistoryData
    // メンバー:  double  data0
    //            double  data1
    //            double  data2
    //            double  data3
    //            double  dt
    //

    public class HistoryData
    {
        public double data0 { get; set; }       // ch0のデータ　
        public double data1 { get; set; }       // ch1のデータ
        public double data2 { get; set; }       // ch2のデータ
        public double data3 { get; set; }       // ch3のデータ
        public double dt { get; set; }         // 日時 (double型)
    }


    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {

        // Win32 API
        // CH347DLL_EN.H での定義
        //  HANDLE WINAPI CH347OpenDevice(ULONG DevI);   
        //
        //   BOOL WINAPI CH347CloseDevice(ULONG iIndex);
        //
        //BOOL WINAPI  CH347I2C_Set(ULONG iIndex,   // Specify the device number
        //                             ULONG iMode);  // See downlink for the specified mode 
        //                                            //bit 1-bit 0: I2C interface speed /SCL frequency, 00= low speed /20KHz,01= standard /100KHz(default),10= fast /400KHz,11= high speed /750KHz
        //                                            //Other reservations, must be 0
        //
        //Process I2C data stream, 2-wire interface, clock line for SCL pin, data line for SDA pin
        //BOOL WINAPI  CH347StreamI2C(ULONG iIndex,        // Specify the device number
        //                               ULONG iWriteLength,  // The number of bytes of data to write
        //                               PVOID iWriteBuffer,  // Points to a buffer to place data ready to be written out, the first byte is usually the I2C device address and read/write direction bit
        //                               ULONG iReadLength,   // Number of bytes of data to be read
        //                               PVOID oReadBuffer); // Points to a buffer to place data ready to be read in


        // DLLのインポート
        // CH347DLL.DLLは、ドライバをインストールすると、Windowsのシステム上にコピーされる。
        //

        [DllImport("CH347DLL.DLL")]      // 32bit版 dll
        private static extern IntPtr CH347OpenDevice(UInt32 DevI);

        [DllImport("CH347DLL.DLL")]
        private static extern bool CH347CloseDevice(UInt32 iIndex);

        [DllImport("CH347DLL.DLL")]
        private static extern bool CH347I2C_Set(UInt32 iIndex, UInt32 iMode);


        [DllImport("CH347DLL.DLL")]
        private static extern bool CH347StreamI2C(UInt32 iIndex, UInt32 iWriteLength, byte[] iWriteBuffer, UInt32 iReadLength, byte[] oReadBuffer);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]

        public delegate void mPCH347_NOTIFY_ROUTINE(UInt32 iEventStatus);
        
        [DllImport("CH347DLL.DLL")]
        public static extern bool CH347SetDeviceNotify(UInt32 iIndex, string iDeviceID, mPCH347_NOTIFY_ROUTINE iNotifyRoutine);


        public const int CH347_DEVICE_REMOVE = 0;
        public const int CH347_DEVICE_REMOVE_PEND = 1;
        public const int CH347_DEVICE_ARRIVE = 3;

        public static string usb_plug = "default";

        UInt32 in_dex; // CH347のUSBポートへの接続 index (例: 0 = 最初に接続した CH347, 1= 次に接続したCH347 ) 

        string ch347_dev_id = "VID_1A86&PID_55D\0";

        mPCH347_NOTIFY_ROUTINE NOTIFY_ROUTINE;



        Byte iic_slave_adrs;      // I2Cアドレス

        Byte[] iic_rcv_data;   // IIC受信データ
        Byte[] iic_sd_data;    // IIC送信データ

        UInt32 iic_sd_num;	    // 送信データ数(スレーブアドレスを含む)
        UInt32 iic_rcv_num;     // 受信データ数

        DateTime receiveDateTime;   // 受信完了日時

    
        UInt32 i2c_clk; // 0x00=20[KHz], 0x01=100[KHz], 0x02 = 400{KHz], 0x03 = 750[KHz]

        double temp_data0;          // 測定温度

        uint trend_data_item_max;             // 各リアルタイム　トレンドデータの保持数 

        double[] trend_data0;                 // トレンドデータ 0 
        double[] trend_data1;                 // トレンドデータ 1              
        double[] trend_data2;                 // トレンドデータ 2  
        double[] trend_data3;                 // トレンドデータ 3 

        double[] trend_dt;                    // トレンドデータ　収集日時

        ScottPlot.Plottables.Scatter trend_scatter_0; // トレンドデータ0  
        ScottPlot.Plottables.Scatter trend_scatter_1; // トレンドデータ1  
        ScottPlot.Plottables.Scatter trend_scatter_2; // トレンドデータ2  
        ScottPlot.Plottables.Scatter trend_scatter_3; // トレンドデータ3  


        public List<HistoryData> historyData_list;          // ヒストリデータ　データ収集時に使用


        double y_axis_top;                      // Y軸 温度目盛りの上限値
        double y_axis_bottom;                   // Y軸 温度目盛りの下限値

        public static DispatcherTimer SendIntervalTimer;  // タイマ　モニタ用　電文送信間隔   


        public MainWindow()
        {

            InitializeComponent();

            in_dex = 0;                     // CH347の使用は１つと仮定

            NOTIFY_ROUTINE = new mPCH347_NOTIFY_ROUTINE(Disp_plug_status);  // USB接続検知用、コールバック関数の作成

            bool flg_notify = CH347SetDeviceNotify(in_dex, ch347_dev_id, NOTIFY_ROUTINE);  // USB plug and unplug monitor


            IntPtr intPtr = CH347OpenDevice(in_dex);         // ドライバのハンドルを得る

            Int32 pt_val = intPtr.ToInt32();

            if (pt_val == -1)   // ハンドルが取れない場合 
            {
                Dis_connect();   // 未接続で終了
            }
            else
            {
                USB_plug_TextBox.Text = "Attached";
            }

            iic_slave_adrs = 0x08;  // SLG47011 スレーブアドレス = 0x08 
            i2c_clk = 2;            // I2Cスピード 400[KHz]  
            Boolean f_sta =  CH347I2C_Set(in_dex, i2c_clk);  // I2C通信スピード(SCL周波数)設定 

            iic_sd_data = new byte[16];     // 送信バッファ領域  
            iic_rcv_data = new byte[16];    // 受信バッファ領域

            historyData_list = new List<HistoryData>();     // モニタ時のトレンドデータ 記録用　


            SendIntervalTimer = new System.Windows.Threading.DispatcherTimer();　　// タイマーの生成(定周期モニタ用)
            SendIntervalTimer.Tick += new EventHandler(SendIntervalTimer_Tick);  // タイマーイベント
            SendIntervalTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);         // タイマーイベント発生間隔 1sec(コマンド送信周期)


            Loaded += LoadEvent;      // LoadEvent実行

        }

        //  CH347と未接続時の処理
        private void Dis_connect()
        {
            var msg = "CH347と接続されていません。\r\n";

            MessageBox.Show(msg, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning); // メッセージボックスの表示

            Close();            // アプリ終了  
        }


        //  
        //  CH347 USB plug/unplug 時の処理
        // 
        private void Disp_plug_status(UInt32 status)
        {
            if (status == CH347_DEVICE_REMOVE)    // USBケーブルが外れた
            {
                SendIntervalTimer.Stop();         // データ収集用コマンド送信タイマー停止
                
                CH347CloseDevice(in_dex);   　　　// CH347 デバイス close

                USB_plug_TextBox.Text = "Removed";
            }
            else if (status == CH347_DEVICE_ARRIVE)  // USBケーブルが接続された
            {
                USB_plug_TextBox.Text = "Attached";
                
                IntPtr intPtr = CH347OpenDevice(in_dex);    // ドライバのハンドルを得る

                SendIntervalTimer.Start();  　　　　 // 定周期　送信用タイマの開始
            }
        }


        //
        // 要素のレイアウトやレンダリングが完了し、操作を受け入れる準備が整ったときに発生
        //
        private void LoadEvent(object sender, EventArgs e)
        {
            Chart_Ini();    // チャートの初期表示
        }

        //
        // 定周期モニタ用
        //  
        private void SendIntervalTimer_Tick(object sender, EventArgs e)
        {
            iic_sd_data[0] = (byte)((iic_slave_adrs << 1));  // スレーブアドレスへ書き込み
            iic_sd_data[1] = 0x22;               // SLG47011V (Buffer1 result) の開始アドレス 0x2224 
            iic_sd_data[2] = 0x24;

            iic_sd_num = 3;     // 送信データ数 
            iic_rcv_num = 0;    // 受信データ数

            bool status = CH347StreamI2C(in_dex, iic_sd_num, iic_sd_data, iic_rcv_num, iic_rcv_data); // I2C通信


            iic_sd_data[0] = (byte)((iic_slave_adrs << 1) | 0x01);  // スレーブアドレスから読み出し
            iic_sd_num = 1;     // 送信データ数 
            iic_rcv_num = 2;    // 受信データ数

            bool status1 = CH347StreamI2C(in_dex, iic_sd_num, iic_sd_data, iic_rcv_num, iic_rcv_data); // I2C通信

            receiveDateTime = DateTime.Now;   // 受信完了時刻を得る

            Disp_monitor_data();              // 温度と受信データの表示

            Store_History();                // ヒストリデータとして保持

            Chart_update();                 // チャートの更新
        }


        //
        //  ヒストリデータとして保持
        //
        private void Store_History()
        {

            HistoryData historyData = new HistoryData();     // 保存用ヒストリデータ

            historyData.data0 = temp_data0;
            historyData.data1 = 0;
            historyData.data2 = 0;
            historyData.data3 = 0; 

            historyData.dt = receiveDateTime.ToOADate();   // 受信日時を deouble型で格納

            historyData_list.Add(historyData);          // Listへ保持

        }


        //
        // 表示
        //
        private void Disp_monitor_data()
        {

            string rcv_str = "";

            for (int i = 0; i < iic_rcv_num; i++)   // 受信データ 表示用の文字列作成
            {
                rcv_str = rcv_str + iic_rcv_data[i].ToString("X2") + " ";
            }

            UInt16 t = (UInt16)(iic_rcv_data[0] << 8);  // 上位バイト
            t = (UInt16)(t | (iic_rcv_data[1]));

            temp_data0 = t ;                            // 受信データは、温度


            Ch0_TextBox.Text = temp_data0.ToString("f1");         // 温度の表示

            RcvTextBox.Text =  rcv_str + "(" + receiveDateTime.ToString("HH:mm:ss") + ")";         // 受信データの表示


        }

        //
        //   チャートの更新
        private void Chart_update()
        {

            // 1スキャン前のデータを移動後、最新のデータを入れる
            Array.Copy(trend_data0, 1, trend_data0, 0, trend_data_item_max - 1);
            trend_data0[trend_data_item_max - 1] = temp_data0;

            Array.Copy(trend_data1, 1, trend_data1, 0, trend_data_item_max - 1);
            trend_data1[trend_data_item_max - 1] = 0;

            Array.Copy(trend_data2, 1, trend_data2, 0, trend_data_item_max - 1);
            trend_data2[trend_data_item_max - 1] = 0;

            Array.Copy(trend_data3, 1, trend_data3, 0, trend_data_item_max - 1);
            trend_data3[trend_data_item_max - 1] = 0;


            Array.Copy(trend_dt, 1, trend_dt, 0, trend_data_item_max - 1);
            trend_dt[trend_data_item_max - 1] = receiveDateTime.ToOADate();    // 受信日時 double型に変換して、格納


            Axis_make();            // 軸の作成

            wpfPlot_Trend.Refresh();   // リアルタイム グラフの更新


        }



        //
        // 　チャートの初期化(リアルタイム　チャート用)
        //
        private void Chart_Ini()
        {
            trend_data_item_max = 30;             // 各リアルタイム　トレンドデータの保持数(=30 ) 1秒毎に収集すると、30秒分のデータ

            trend_data0 = new double[trend_data_item_max];
            trend_data1 = new double[trend_data_item_max];
            trend_data2 = new double[trend_data_item_max];
            trend_data3 = new double[trend_data_item_max];

            trend_dt = new double[trend_data_item_max];

            DateTime datetime = DateTime.Now;   // 現在の日時

            DateTime[] myDates = new DateTime[trend_data_item_max];  // 日時型



            for (int i = 0; i < trend_data_item_max; i++)  // 初期値の設定
            {
                trend_data0[i] = 30 + i;
                trend_data1[i] = 20 + i;
                trend_data2[i] = 10 + i;
                trend_data3[i] = 0 + i;

                myDates[i] = datetime + new TimeSpan(0, 0, i);  // i秒増やす

                trend_dt[i] = myDates[i].ToOADate();   // (現在の日時 + i 秒)をdouble型に変換
            }


            trend_scatter_0 = wpfPlot_Trend.Plot.Add.Scatter(trend_dt, trend_data0, ScottPlot.Colors.Blue); // プロット plot the data array only once
            trend_scatter_1 = wpfPlot_Trend.Plot.Add.Scatter(trend_dt, trend_data1, ScottPlot.Colors.Gainsboro);
            trend_scatter_2 = wpfPlot_Trend.Plot.Add.Scatter(trend_dt, trend_data2, ScottPlot.Colors.Orange);
            trend_scatter_3 = wpfPlot_Trend.Plot.Add.Scatter(trend_dt, trend_data3, ScottPlot.Colors.Green);


            wpfPlot_Trend.UserInputProcessor.IsEnabled = false;     // マウスによるパン(グラフの移動)、ズーム(グラフの拡大、縮小)の操作禁止

            Axis_make();            // 軸の作成

            // 凡例の表示
            // 参考:scottplot.net/cookbook/5.0/Legend/
            //
            wpfPlot_Trend.Plot.Legend.FontSize = 24;

            trend_scatter_0.LegendText = "ch0";
            trend_scatter_1.LegendText = "ch1";
            trend_scatter_2.LegendText = "ch2";
            trend_scatter_3.LegendText = "ch3";

            wpfPlot_Trend.Plot.ShowLegend(Alignment.UpperRight, ScottPlot.Orientation.Vertical);


            wpfPlot_Trend.Refresh();        // データ変更後のリフレッシュ


        }


        //
        // 軸の作成
        //
        private void Axis_make()
        {
            y_axis_top = 250;                       // Y軸　上限値
            y_axis_bottom = 0;                      // Y軸　下限値

            // X軸の日時リミットを、最終日時+1秒にする
            DateTime dt_end = DateTime.FromOADate(trend_dt[trend_data_item_max - 1]); // double型を　DateTime型に変換
            TimeSpan dt_sec = new TimeSpan(0, 0, 1);    // 1 秒
            DateTime dt_limit = dt_end + dt_sec;      // DateTime型(最終日時+ 1秒) 
            double dt_ax_limt = dt_limit.ToOADate();   // double型(最終日時+ 1秒) 


            wpfPlot_Trend.Plot.Axes.SetLimits(trend_dt[0], dt_ax_limt, 0, 250);  // X軸の最小=現在の時間 ,X軸の最大=最終日時+1秒,Y軸下限=0[℃], Y軸上限=250 [℃]

            custom_ticks();                             // X軸の目盛りのカスタマイズ

            //wpfPlot_Trend.Plot.Axes.Left.Label.FontSize = 24;                 // Y軸   ラベルのフォントサイズ変更  :
            //wpfPlot_Trend.Plot.Axes.Left.Label.Text = "[C] celsius";          // Y軸のラベル (scottplot.net/cookbook/5.0/Styling/AxisCustom/)

        }

        //
        //  目盛りのカスタマイズ 
        // 参考: scottplot.net/cookbook/5.0/CustomizingTicks/
        //
        //       Custom Tick DateTimes
        // Users may define custom ticks using DateTime units
        // 
        private void custom_ticks()
        {
            DateTime dt;
            string label;

            // create a manual DateTime tick generator and add ticks
            ScottPlot.TickGenerators.DateTimeManual ticks = new ScottPlot.TickGenerators.DateTimeManual();

            //for (int i = 0; i < trend_data_item_max; i++)  // 1秒毎に目盛りのラベル表示
            //{
            //    DateTime dt = DateTime.FromOADate(trend_dt[i]);
            //    string label = dt.ToString("HH:mm:ss");
            //    ticks.AddMajor(dt, label);
            //}

           
            dt = DateTime.FromOADate(trend_dt[1]);  // 先頭 + 1の時刻　目盛りのラベル表示
            label = dt.ToString("HH:mm:ss");
            ticks.AddMajor(dt, label);

            UInt16 t = (ushort)(trend_data_item_max / 2); 
            dt = DateTime.FromOADate(trend_dt[t]);  // 中間の時刻　目盛りのラベル表示
            label = dt.ToString("HH:mm:ss");
            ticks.AddMajor(dt, label);

            dt = DateTime.FromOADate(trend_dt[trend_data_item_max - 1]);  // 最後の時刻　目盛りのラベル表示
            label = dt.ToString("HH:mm:ss");
            ticks.AddMajor(dt, label);

            wpfPlot_Trend.Plot.Axes.Bottom.TickGenerator = ticks;    　　　　// tell the horizontal axis to use the custom tick generator

            wpfPlot_Trend.Plot.Axes.Bottom.TickLabelStyle.FontSize = 24;      //  X軸　目盛りのフォントサイズ


            wpfPlot_Trend.Plot.Axes.Left.TickLabelStyle.FontSize = 24;        //  Y軸　目盛りのフォントサイズ
        }



        //
        // Windowを閉じる時の処理
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
             CH347CloseDevice(in_dex);  // CH347 デバイス close
        }

        // モニタ開始ボタン
        private void Start_Monitor_Button_Click(object sender, RoutedEventArgs e)
        {
            SendIntervalTimer.Start();   // 定周期　送信用タイマの開始
        }

        // モニタ停止ボタン
        private void Stop_Monitor_Button_Click(object sender, RoutedEventArgs e)
        {
            SendIntervalTimer.Stop();     // データ収集用コマンド送信タイマー停止
        }



        // チェックボックスによるトレンド線の表示 
        private void CH_N_Show(object sender, RoutedEventArgs e)
        {

            if (trend_scatter_0 is null) return;
            if (trend_scatter_1 is null) return;
            if (trend_scatter_2 is null) return;
            if (trend_scatter_3 is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "Ch0_CheckBox")
            {
                trend_scatter_0.IsVisible = true;
            }
            else if (checkBox.Name == "Ch1_CheckBox")
            {
                trend_scatter_1.IsVisible = true;
            }
            else if (checkBox.Name == "Ch2_CheckBox")
            {
                trend_scatter_2.IsVisible = true;
            }
            else if (checkBox.Name == "Ch3_CheckBox")
            {
                trend_scatter_3.IsVisible = true;
            }


            wpfPlot_Trend.Refresh();   // グラフの更新

        }

        // チェックボックスによるトレンド線の非表示
        private void CH_N_Hide(object sender, RoutedEventArgs e)
        {
            if (trend_scatter_0 is null) return;
            if (trend_scatter_1 is null) return;
            if (trend_scatter_2 is null) return;
            if (trend_scatter_3 is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "Ch0_CheckBox")
            {
                trend_scatter_0.IsVisible = false;
            }
            else if (checkBox.Name == "Ch1_CheckBox")
            {
                trend_scatter_1.IsVisible = false;
            }
            else if (checkBox.Name == "Ch2_CheckBox")
            {
                trend_scatter_2.IsVisible = false;
            }
            else if (checkBox.Name == "Ch3_CheckBox")
            {
                trend_scatter_3.IsVisible = false;
            }

            wpfPlot_Trend.Refresh();   // グラフの更新
        }

    

        // 保持しているデータをファイルへ保存
        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            string path;

            string str_one_line;

            SaveFileDialog sfd = new SaveFileDialog();           //　SaveFileDialogクラスのインスタンスを作成 

            sfd.FileName = "temp_trend.csv";                              //「ファイル名」で表示される文字列を指定する

            sfd.Title = "保存先のファイルを選択してください。";        //タイトルを設定する 

            sfd.RestoreDirectory = true;                 //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする

            if (sfd.ShowDialog() == true)            //ダイアログを表示する
            {
                path = sfd.FileName;

                try
                {
                    System.IO.StreamWriter sw = new System.IO.StreamWriter(path, false, System.Text.Encoding.Default);

                    str_one_line = DataMemoTextBox.Text; // メモ欄
                    sw.WriteLine(str_one_line);         // 1行保存


                    str_one_line = "DateTime" + "," + "ch0[℃]" + "," + "ch1[℃]" + "," + "ch2[℃]" + "," + "ch3[℃]";
                    sw.WriteLine(str_one_line);         // 1行保存


                    foreach (HistoryData historyData in historyData_list)         // historyData_listの内容を保存
                    {
                        DateTime dateTime = DateTime.FromOADate(historyData.dt); // 記録されている日時(double型)を　DateTime型に変換

                        string st_dateTime = dateTime.ToString("yyyy/MM/dd HH:mm:ss.fff");             // DateTime型を文字型に変換　（2021/10/22 11:09:06.125 )

                        string st_dt0 = historyData.data0.ToString("F1");       //データ(ch0) 文字型に変換 (25.0)
                        string st_dt1 = historyData.data1.ToString("F1");       // 
                        string st_dt2 = historyData.data2.ToString("F1");       // 
                        string st_dt3 = historyData.data3.ToString("F1");       // 


                        str_one_line = st_dateTime + "," + st_dt0 + "," + st_dt1 + "," + st_dt2 + "," + st_dt3;

                        sw.WriteLine(str_one_line);         // 1行保存
                    }

                    sw.Close();
                }

                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }
        }


        // 収集済みのデータをクリアの確認
        private void Clear_Button_Click(object sender, RoutedEventArgs e)
        {
            string messageBoxText = "収集済みのデータがクリアされます。";
            string caption = "Check clear";

            MessageBoxButton button = MessageBoxButton.YesNoCancel;
            MessageBoxImage icon = MessageBoxImage.Warning;
            MessageBoxResult result;

            result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

            switch (result)
            {
                case MessageBoxResult.Yes:      // Yesを押した場合
                    historyData_list.Clear();   // 収集済みのデータのクリア
                    break;

                case MessageBoxResult.No:
                    break;

                case MessageBoxResult.Cancel:
                    break;
            }
        }

        // トレンド 履歴画面
        private void History_Button_Click(object sender, RoutedEventArgs e)
        {

            var window = new HistoryWindow();      // 注意メッセージのダイアログを開く
            window.Owner = this;
            window.Show();
        }

    }

}
