using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Renci.SshNet;

namespace RadioInterface
{
    public partial class RadioInterface : Form
    {
        private string[] _files;
        private string _fileName;
        private string _piIP = "192.168.1.142";
        private string _username = "pi";
        private string _password = "RASPBERRY";
        private string _directory = "Radio/musicMP3";
        private SshClient _client;
        private Thread _radioThread;
        private double _freq;

        public RadioInterface()
        {
            InitializeComponent();
            freqBar.ValueChanged += (sender, args) =>
            {
                var value = (double) freqBar.Value / 10;
                freqTextBox.Text = value +"";
            };
            freqTextBox.TextChanged += (sender, args) =>
            {
                int.TryParse(freqTextBox.Text, out var value);
                value = value * 10;
                if (value >= 800 && value <= 1100) freqBar.Value = value;
            };
        }

        private async void fileButton_Click(object sender, EventArgs e)
        {
            fileTextBox.Text = "";
            var result = openFileDialog.ShowDialog();
            if (result != DialogResult.OK) return;
            if(openFileDialog.SafeFileNames.Length == 1)
                _fileName = openFileDialog.SafeFileName;
            foreach (var name in openFileDialog.SafeFileNames)
            {
                fileTextBox.Text += "\"" + name +"\" ";
            }

            _files = openFileDialog.FileNames;

            await Task.Factory.StartNew(UploadFile);
            startButton.Enabled = true;
                
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            startButton.Enabled = false;
            startButton.Text = "Playing...";

            _freq = (double) freqBar.Value / 10;
            var newRef = new ThreadStart(SSHToRadio);
            _radioThread = new Thread(newRef);
            _radioThread.Start();

            stopButton.Enabled = true;
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            stopButton.Enabled = false;
            KillRadio();

            startButton.Text = "Start";
            startButton.Enabled = true;
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            //stops radio
            if(_radioThread.IsAlive)
                stopButton_Click(sender,e);

            Application.Restart();
        }

        private void SSHToRadio()
        {
            _client = new SshClient(_piIP,_username,_password);
            try
            {
                _client.Connect();
                if (_files.Length == 1)
                    _client.RunCommand("cd Radio; sox -t mp3 ~/Radio/musicMP3/\"" + _fileName +
                                       "\" -t wav - | sudo ./pi_fm_rds -freq "+ _freq +
                                       " -ps PirateRadio -audio -");
                
                else if (_files.Length > 1)
                    _client.RunCommand("cd Radio; sox -t mp3 ~/Radio/musicMP3/*.mp3 -t wav - | " +
                                       "sudo ./pi_fm_rds -freq " + _freq + 
                                       " -ps PirateRadio -audio -");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void KillRadio()
        {
            _client.RunCommand("killall -SIGKILL sox");
            _radioThread.Abort();
            _client.Disconnect();
        }

        private void UploadFile()
        {
            var uploadClient = new SftpClient(_piIP,_username,_password);
            try
            {
                uploadClient.Connect();
                uploadClient.ChangeDirectory(_directory);
                Console.WriteLine(uploadClient.WorkingDirectory);

                foreach (var file in _files)
                {
                    using (var fileStream = new FileStream(file, FileMode.Open))
                    {
                        uploadClient.BufferSize = 4 * 1024;
                        uploadClient.UploadFile(fileStream, Path.GetFileName(file));
                    }
                }
                

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                uploadClient.Disconnect();
            }
            
        }
    }
}
