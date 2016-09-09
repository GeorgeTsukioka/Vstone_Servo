using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//追加したクラス
using Windows.Devices.SerialCommunication;  //シリアル通信
using Windows.Devices.Enumeration;          //デバイス情報
using Windows.Storage.Streams;              //
using Windows.UI.Popups;                    //メッセージダイアログ
using System.Collections.ObjectModel;       //
using System.Threading.Tasks;               //非同期メソッド

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください
namespace VS_Servo_Serial_Test_for_raspi3
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //変数宣言
        private ObservableCollection<DeviceInformation> listOfDevices;  //UART0ポート情報格納用
        private SerialDevice serialPort = null;                         //シリアルポート設定
        DataWriter dataWriteObject = null;                              //

        short SID = 0x00;             //サーボID
        byte[] buf = new byte[256]; //データ送信

        /*!
         * @brief メインページ
         */
        public MainPage()
        {
            this.InitializeComponent();

            //サーボID(0~10)の追加
            var items = Enumerable.Range(0, 10).Select(i => i).ToList();
            ServoID.ItemsSource = items;

            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();
        }

        /*!
         * @brief SerialDevice.GetDeviceSelectorを用いてすべてのシリアルデバイスを列挙し，リストボックスにソースデバイス情報を表示させる
         */
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }

                DeviceListSource.Source = listOfDevices;
                ConnectDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message.ToString(), "Error").ShowAsync();
            }

        }

        /*!
         * @brief 接続ボタンがクリックされた時に行うアクション
         * 選択したデバイスのIDとインデックスを使用してSerialDevice objectを作成
         * シリアルポートの設定構成
         */
        private async void comPort_connect_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            if (selection.Count <= 0)
            {
                await new MessageDialog("Select a device and connect", "Error").ShowAsync();
                return;
            }

            try
            {
                DeviceInformation entry = (DeviceInformation)selection[0];
                serialPort = await SerialDevice.FromIdAsync(entry.Id);

                // Configure serial settings
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.BaudRate = 115200;
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message.ToString(), "Error").ShowAsync();
            }
        }

        /*!
         * @brief 接続切断ボタンがクリックされた時に行うアクション
         * SerialDevice objectを閉じる
         * 接続されているデバイスを再度表示
         */
        private async void comPort_disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serialPort != null)
                {
                    serialPort.Dispose();
                }
                serialPort = null;

                listOfDevices.Clear();
                ListAvailablePorts();
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message.ToString(), "Error").ShowAsync();
            }

        }

        /*!
         * @brief ServoIDが変更されたときに行う処理
         * 各サーボIDのアンロックを行う
         */
        private async void ServoID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SID = Convert.ToInt16(ServoID.SelectedItem.ToString());

            buf[0] = (byte)(0x80 | SID);
            buf[1] = 0x40 | 0x00 | 1;
            buf[2] = 0x14;
            buf[3] = 0x55;

            await MemoryWrite();
        }

        /*!
         * @brief Servo電源ON
         */
        private async void servo_on_Click(object sender, RoutedEventArgs e)
        {
            buf[0] = (byte)(0x80 | SID);
            buf[1] = 0x40 | 0x00 | 1;
            buf[2] = 0x3b;
            buf[3] = 1;

            await MemoryWrite();
        }

        /*!
 * @brief Servo電源ON
 */
        private async void servo_off_Click(object sender, RoutedEventArgs e)
        {
            buf[0] = (byte)(0x80 | SID);
            buf[1] = 0x40 | 0x00 | 1;                                // write 1byte
            buf[2] = 0x3b;                                           // SYS_ULK
            buf[3] = 0;

            await MemoryWrite();
        }

        /*!
         * @brief スライダーを変更時にサーボの位置を追従させる処理
         */
        private async void Servo_Pos_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            float target_pos = 3072 / 180 * float.Parse((Servo_Pos_Slider.Value.ToString())) + 512;
            servo_pos_analog.Text = target_pos.ToString();

            buf[0] = (byte)(0x80 | SID);
            buf[1] = 0x40 | 0x00 | 2;                  // write 2byte
            buf[2] = 0x30;                             // FB_TPOS
            buf[3] = 0x00;                             // low byte
            buf[4] = (byte)((short)target_pos >> 7);   // high byte                

            await MemoryWrite();
        }

        /*!
         * @brief 非同期でOutputStreamにbufからデータを書き込む処理
         */
        private async Task MemoryWrite()
        {
            Task<UInt32> storeAsyncTask = null;
            dataWriteObject = new DataWriter(serialPort.OutputStream);

            try
            {
                dataWriteObject.WriteBytes(buf);
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();
            }

            catch (Exception ex)
            {
                await new MessageDialog(ex.Message.ToString(), "Error").ShowAsync();
            }


            if (storeAsyncTask != null)
            {
                await storeAsyncTask;
            }

            if (dataWriteObject != null)
            {
                dataWriteObject.DetachStream();
                dataWriteObject = null;
            }
        }
    }

}
