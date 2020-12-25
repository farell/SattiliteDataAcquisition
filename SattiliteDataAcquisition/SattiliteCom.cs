using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace SattiliteDataAcquisition
{
    class SattiliteCom
    {
        private SerialPort comPort;
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
        //private ConcurrentQueue<> dataQueue;

        public SattiliteCom(string identifier,string path,string portName,int baudrate,Form1 form1,int timeout)
        {
            this.prefix = identifier;
            this.sem = new Semaphore(1, 1);
            int milliseconds = timeout * 60 * 1000;
            comPort = new SerialPort(portName, baudrate, Parity.None, 8, StopBits.One);
            //comPort.DataReceived += new SerialDataReceivedEventHandler(ComDataReceive);
            this.timer = new System.Timers.Timer(milliseconds);
            this.timer.Elapsed += new ElapsedEventHandler(TimerTimeout);
            this.buffer = new byte[1024*4];
            this.locker = new Object();
            this.path = path;
            this.window = form1;
            this.portName = portName;
        }

        private void StartAcquisit()
        {
            //创建新文件
            string fileName = this.prefix+DateTime.Now.ToString("yyyyMMddHHmmss");

            fileName = fileName + ".rt27";

            string pathString = Path.Combine(this.path, fileName);

            this.fileStream = new FileStream(pathString, FileMode.Append);

            comPort.Open();
            receiveTask = Task.Factory.StartNew(() => {
                int bytesRead = 0;
                while (true)
                {
                    try
                    {
                        bytesRead = this.comPort.Read(this.buffer, 0, this.comPort.ReadBufferSize);
                        if (bytesRead > 0)
                        {
                            this.sem.WaitOne();
                            if (this.fileStream.CanWrite)
                            {
                                this.fileStream.Write(this.buffer, 0, bytesRead);
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
            StartAcquisit();
            timer.Start();
        }

        private void StopAcquisit()
        {
            if (comPort != null)
            {
                comPort.Close();
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

        //private void TimerTimeout(object sender, System.Timers.ElapsedEventArgs e)
        //{

        //    StopAcquisit();

        //    this.window.Invoke((EventHandler)(
        //                    delegate
        //                    {
        //                        window.AppendLog(this.portName +" "+ this.prefix+DateTime.Now.ToString("yyyyMMddHHmmss") + " 文件已经保存 \r\n");
        //                    }));
        //    StartAcquisit();
        //}

        //private void TimerTimeout(object sender, System.Timers.ElapsedEventArgs e)
        //{
        //    DateTime dt = DateTime.Now;
        //    if (dt.Date != currentDate.Date)
        //    {
        //        currentDate = dt;
        //        //创建新文件
        //        string fileName = dt.ToString();
        //        fileName = fileName.Replace('/', '-');
        //        fileName = fileName.Split(' ')[0] + ".rt27";

        //        string pathString = null;

        //        lock (this.locker)
        //        {
        //            //关闭文件
        //            this.fileStream.Flush();
        //            this.fileStream.Close();

        //            pathString = Path.Combine(this.path, fileName);

        //            this.fileStream = new FileStream(pathString, FileMode.Append);
        //        }

        //        this.window.Invoke((EventHandler)(
        //                    delegate
        //                    {
        //                        window.AppendLog(this.portName + " Create File " + pathString);
        //                    }));
        //    }
        //}

        private void ComDataReceive(object sender, SerialDataReceivedEventArgs e)
        {
            lock (this.locker)
            {
                //int bytesToRead = this.comPort.BytesToRead;
                int bytesRead = 0;
                try {
                    bytesRead = this.comPort.Read(this.buffer, 0, this.comPort.ReadBufferSize);
                    if (bytesRead > 0)
                    {
                        if (this.fileStream.CanWrite)
                        {
                            //receivedCount++;
                            this.fileStream.Write(this.buffer, 0, bytesRead);

                            //if(receivedCount > 10)
                            {
                                this.fileStream.Flush();
                                //receivedCount = 0;
                            }
                        }
                        
                    }

                    //this.window.Invoke((EventHandler)(
                    //            delegate
                    //            {
                    //                window.AppendLog(this.portName + " bytesRead :" + bytesRead);
                    //            }));
                }
                catch(Exception ex)
                {
                    using (StreamWriter sw = new StreamWriter(@"ErrLog\ErrLog.txt", true))
                    {
                        sw.WriteLine( this.portName +" bytesToRead :" + bytesRead+"\n");
                        sw.WriteLine(ex.ToString());
                        sw.WriteLine("---------------------------------------------------------");
                        sw.Close();
                    }

                    MessageBox.Show("产生异常，请看日志");

                    this.window.Invoke((EventHandler)(
                                delegate
                                {
                                    window.AppendLog("产生异常，请看日志 " );
                                    window.AppendLog(this.portName + " bytesRead :" + bytesRead);
                                    window.AppendLog(ex.ToString());
                                }));
                }

            }
        }
    }
}
