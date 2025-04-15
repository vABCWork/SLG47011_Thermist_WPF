using Microsoft.Win32;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;

namespace test_ch347
{
    /// <summary>
    /// HistoryWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class HistoryWindow : Window
    {

        public List<HistoryData> historyData_file_list;     // ヒストリデータ　ファイルからの読み出し時に使用

        ScottPlot.Plottables.Scatter history_scatter_0;   // ヒストリデータ 0   
        ScottPlot.Plottables.Scatter history_scatter_1;   // ヒストリデータ 1   
        ScottPlot.Plottables.Scatter history_scatter_2;   // ヒストリデータ 2   
        ScottPlot.Plottables.Scatter history_scatter_3;   // ヒストリデータ 3  



        public HistoryWindow()
        {
            InitializeComponent();


            historyData_file_list = new List<HistoryData>(); // ファイルからの読み出し時に使用
        }

        private void Open_Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();   // ダイアログのインスタンスを生成

            dialog.Filter = "csvファイル (*.csv)|*.csv|全てのファイル (*.*)|*.*";  //  // ファイルの種類を設定

            dialog.RestoreDirectory = true;                 //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする


            if (dialog.ShowDialog() == false)     // ダイアログを表示する
            {
                return;                          // キャンセルの場合、リターン
            }


            try
            {
                historyData_file_list.Clear();            // ヒストリデータのクリア

                StreamReader sr = new StreamReader(dialog.FileName, Encoding.GetEncoding("SHIFT_JIS"));    //  CSVファイルを読みだし

                FileNameTextBox.Text = dialog.FileName;                // ファイル名の表示

                DataMemoTextBox.Text = sr.ReadLine();           // 先頭行の Memoを読み出し、表示

                sr.ReadLine();              // 読み飛ばし (2行目は、日時、ch名の項目名のため)

                while (!sr.EndOfStream)     // ファイル最終行まで、繰り返し
                {
                    HistoryData historyData = new HistoryData();        // 読み出しデータを格納するクラス

                    string line = sr.ReadLine();        // 1行の読み出し

                    string[] items = line.Split(',');       // 1行を、,(カンマ)毎に items[]に格納 

                    DateTime dateTime;
                    DateTime.TryParse(items[0], out dateTime);  // 日付の文字列を DateTime型へ変換

                    historyData.dt = dateTime.ToOADate();       // DateTiem型を double型へ変換


                    double.TryParse(items[1], out double d0); // ch0の値　文字列を double型へ変換
                    historyData.data0 = d0;                   // クラスのメンバーへ格納

                    double.TryParse(items[2], out double d1); // ch1の値　文字列を double型へ変換
                    historyData.data1 = d1;                   // クラスのメンバーへ格納

                    double.TryParse(items[3], out double d2); // ch2の値　文字列を double型へ変換
                    historyData.data2 = d2;

                    double.TryParse(items[4], out double d3); // ch3の値　文字列を double型へ変換
                    historyData.data3 = d3;                   // クラスのメンバーへ格納


                    historyData_file_list.Add(historyData);      // Listへ追加

                }


                disp_history_graph();       // ヒストリトレンドデータのグラフ表示

                set_check_box_true();       // チェックボックスをtrue
            }

            catch (Exception ex) when (ex is IOException || ex is IndexOutOfRangeException)
            {

                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        //
        //  ヒストリトレンドデータのグラフ表示
        private void disp_history_graph()
        {
            wpfPlot_History.Plot.Clear();

            int cnt_max = historyData_file_list.Count;       // 行数分の配列

            double[] t_data0 = new double[cnt_max];   // データ 0  
            double[] t_data1 = new double[cnt_max];   // データ 1  
            double[] t_data2 = new double[cnt_max];   // データ 2  
            double[] t_data3 = new double[cnt_max];   // データ 3  

            double[] t_dt = new double[cnt_max];       //  date time


            for (int i = 0; i < cnt_max; i++)                   // List化された、historyDataクラスの情報をグラフ表示用の配列にコピー 
            {
                t_data0[i] = historyData_file_list[i].data0;       // PV
                t_data1[i] = historyData_file_list[i].data1;       // SV
                t_data2[i] = historyData_file_list[i].data2;       // ch2
                t_data3[i] = historyData_file_list[i].data3;       // ch3 

                t_dt[i] = historyData_file_list[i].dt;           // data tiem
            }

            history_scatter_0 = wpfPlot_History.Plot.Add.Scatter(t_dt, t_data0, ScottPlot.Colors.Blue);    // 散布図へ表示　t_data0
            history_scatter_1 = wpfPlot_History.Plot.Add.Scatter(t_dt, t_data1, ScottPlot.Colors.Gainsboro);
            history_scatter_2 = wpfPlot_History.Plot.Add.Scatter(t_dt, t_data2, ScottPlot.Colors.Orange);
            history_scatter_3 = wpfPlot_History.Plot.Add.Scatter(t_dt, t_data3, ScottPlot.Colors.Green);


            // wpfPlot_History.UserInputProcessor.IsEnabled = false;     // マウスによるパン(グラフの移動)、ズーム(グラフの拡大、縮小)の操作禁止

            wpfPlot_History.Plot.Axes.AutoScale();          //全データがチャートに入るように、X軸,Y軸の範囲を調整

            wpfPlot_History.Plot.Axes.DateTimeTicksBottom();  //  tell the plot to display dates on the bottom axis

            wpfPlot_History.Plot.Axes.Bottom.TickLabelStyle.FontSize = 24;      //  X軸　目盛りのフォントサイズ

            wpfPlot_History.Plot.Axes.Left.TickLabelStyle.FontSize = 24;        //  Y軸　目盛りのフォントサイズ

            // 凡例の表示
            // 参考:scottplot.net/cookbook/5.0/Legend/
            //
            wpfPlot_History.Plot.Legend.FontSize = 24;

            history_scatter_0.LegendText = "ch0";
            history_scatter_1.LegendText = "ch1";
            history_scatter_2.LegendText = "ch2";
            history_scatter_3.LegendText = "ch3";

            wpfPlot_History.Plot.ShowLegend(Alignment.UpperRight, ScottPlot.Orientation.Vertical);


            wpfPlot_History.Refresh();       // Refresh

        }


        // チェックボックスによるトレンド線の表示 
        private void CH_N_Show(object sender, RoutedEventArgs e)
        {

            if (history_scatter_0 is null) return;
            if (history_scatter_1 is null) return;
            if (history_scatter_2 is null) return;
            if (history_scatter_3 is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "Ch0_CheckBox")
            {
                history_scatter_0.IsVisible = true;
            }
            else if (checkBox.Name == "Ch1_CheckBox")
            {
                history_scatter_1.IsVisible = true;
            }
            else if (checkBox.Name == "Ch2_CheckBox")
            {
                history_scatter_2.IsVisible = true;
            }
            else if (checkBox.Name == "Ch3_CheckBox")
            {
                history_scatter_3.IsVisible = true;
            }


            wpfPlot_History.Refresh();   // グラフの更新

        }

        // チェックボックスによるトレンド線の非表示
        private void CH_N_Hide(object sender, RoutedEventArgs e)
        {
            if (history_scatter_0 is null) return;
            if (history_scatter_1 is null) return;
            if (history_scatter_2 is null) return;
            if (history_scatter_3 is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "Ch0_CheckBox")
            {
                history_scatter_0.IsVisible = false;
            }
            else if (checkBox.Name == "Ch1_CheckBox")
            {
                history_scatter_1.IsVisible = false;
            }
            else if (checkBox.Name == "Ch2_CheckBox")
            {
                history_scatter_2.IsVisible = false;
            }
            else if (checkBox.Name == "Ch3_CheckBox")
            {
                history_scatter_3.IsVisible = false;
            }

            wpfPlot_History.Refresh();   // グラフの更新
        }


        //  グラフデータのオープン時には、
        //  表示するグラフのチェックボックスを全てチェック済みにする。
        private void set_check_box_true()
        {
            Ch0_CheckBox.IsChecked = true;
            Ch1_CheckBox.IsChecked = true;
            Ch2_CheckBox.IsChecked = true;
            Ch3_CheckBox.IsChecked = true;
        }


    }
}
