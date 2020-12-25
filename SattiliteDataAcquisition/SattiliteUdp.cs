using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace SattiliteDataAcquisition
{
    class SattiliteUdp
    {
        private UdpClient udpServer;
        private System.Timers.Timer timer;
        private string path;
        private FileStream fileStream;
        private byte[] buffer;
        private Object locker;
        private Form1 window;
        private string portName;
        private DateTime currentDate;
        private Task receiveTask;
        private string prefix;
        private Semaphore sem;
        private int portNumber;
        //private ConcurrentQueue<> dataQueue;

        public SattiliteUdp(string identifier,string path,int portNumber,Form1 form1,int timeout)
        {
            this.prefix = identifier;
            this.sem = new Semaphore(1, 1);
            int milliseconds = timeout * 60 * 1000;
            //udpServer = new UdpClient();
            //comPort.DataReceived += new SerialDataReceivedEventHandler(ComDataReceive);
            this.timer = new System.Timers.Timer(milliseconds);
            this.timer.Elapsed += new ElapsedEventHandler(TimerTimeout);
            this.buffer = new byte[1024*4];
            this.locker = new Object();
            this.path = path;
            this.window = form1;
            this.portNumber = portNumber;
        }

        private void StartAcquisit()
        {
            //创建新文件
            string fileName = this.prefix+DateTime.Now.ToString("yyyyMMddHHmmss");

            fileName = fileName + ".rt27";

            string pathString = Path.Combine(this.path, fileName);

            this.fileStream = new FileStream(pathString, FileMode.Append);

            //comPort.Open();
            receiveTask = Task.Factory.StartNew(() => {
                int bytesRead = 0;
                IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    try
                    {
                        Byte[] receiveBytes = udpServer.Receive(ref remoteIpEndPoint);
                        //bytesRead = receiveBytes.Length;
                        if (receiveBytes.Length > 0)
                        {
                            this.sem.WaitOne();
                            if (this.fileStream.CanWrite)
                            {
                                this.fileStream.Write(receiveBytes, 0, receiveBytes.Length);
                            }
                            this.sem.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        this.window.Invoke((EventHandler)(
                            delegate
                            {
                                window.AppendLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + this.portName + ex.Message);
                            }));
                        break;
                    }
                }
            });
        }

        public void Start()
        {
            udpServer = new UdpClient(portNumber);
            StartAcquisit();
            timer.Start();
        }

        private void StopAcquisit()
        {
            //if (udpServer.Client.Connected)
            {
                udpServer.Close();
            }
           
            if (this.fileStream != null)
            {
                try
                {
                    this.fileStream.Flush();
                    this.fileStream.Close();
                }
                catch (Exception ex)
                {

                }
            }
        }

        public void Stop()
        {
            timer.Stop();
            StopAcquisit();
        }

        private void TimerTimeout(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.sem.WaitOne();

            if (this.fileStream != null)
            {
                try
                {
                    this.fileStream.Flush();
                    this.fileStream.Close();
                }
                catch (Exception ex)
                {

                }
            }

            this.window.Invoke((EventHandler)(
                            delegate
                            {
                                window.AppendLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+ " "+this.portName  + " 文件已经保存 \r\n");
                            }));

            //创建新文件
            string fileName = this.prefix + DateTime.Now.ToString("yyyyMMddHHmmss");

            fileName = fileName + ".rt27";

            string pathString = Path.Combine(this.path, fileName);

            this.fileStream = new FileStream(pathString, FileMode.Append);
            this.sem.Release();
        }
    }
}
