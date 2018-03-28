using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
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

        public SattiliteCom(string path,string portName,Form1 form1)
        {
            comPort = new SerialPort(portName, 38400, Parity.None, 8, StopBits.One);
            comPort.DataReceived += new SerialDataReceivedEventHandler(ComDataReceive);
            this.timer = new System.Timers.Timer(1000 * 60);
            this.timer.Elapsed += new ElapsedEventHandler(TimerTimeout);
            this.buffer = new byte[1024*4];
            this.locker = new Object();
            this.path = path;
            this.window = form1;
            this.portName = portName;
        }

        public void Start()
        {
            //创建新文件
            string fileName = DateTime.Now.ToString() + ".rtcm";
            fileName = fileName.Replace('/', '-');
            fileName = fileName.Replace(':', '-');

            string pathString = Path.Combine(this.path, fileName);

            this.fileStream = new FileStream(pathString, FileMode.Create);

            //this.window.Invoke((EventHandler)(
            //            delegate {
            //                window.AppendLog("Create File " + fileName);
            //            }));

            comPort.Open();
            this.timer.Start();
        }

        public void Stop()
        {
            if (comPort != null)
            {
                comPort.Close();
            }

            timer.Stop();

            if(this.fileStream != null)
            {
                if (this.fileStream.CanWrite)
                {
                    this.fileStream.Flush();
                    this.fileStream.Close();
                }
            }

        }

        private void TimerTimeout(object sender, System.Timers.ElapsedEventArgs e)
        {
            //创建新文件
            string fileName = DateTime.Now.ToString() + ".rtcm";
            fileName = fileName.Replace('/', '-');
            fileName = fileName.Replace(':', '-');

            string pathString = null;

            lock (this.locker)
            {
                //关闭文件
                this.fileStream.Flush();
                this.fileStream.Close();
                
                pathString = Path.Combine(this.path, fileName);

                this.fileStream = new FileStream(pathString, FileMode.Create);
            }

            this.window.Invoke((EventHandler)(
                        delegate
                        {
                            window.AppendLog(this.portName + " Create File " + pathString);
                        }));
        }

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
                        this.fileStream.Write(this.buffer, 0, bytesRead);
                    }

                    //this.window.Invoke((EventHandler)(
                    //            delegate
                    //            {
                    //                window.AppendLog(this.portName + " bytesRead :" + bytesRead);
                    //            }));
                }
                catch(Exception ex)
                {
                    this.Stop();
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
