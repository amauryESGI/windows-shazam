using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using SharedCoreLib.AudioProcessing;

namespace MusicAnalyzer
{
    public partial class MainForm : Form
    {
        ShazamClient _client;
        Microphone _microphone;
        byte[] _audioData;
        int _bytesRead;
        int _counter = 10;

        public MainForm()
        {
            InitializeComponent();
            _client = new ShazamClient();
            _client.OnRecongnitionStateChanged += ShazamStateChanged;
            _microphone = Microphone.Default;
            if (Microphone.All.Count == 0)
            {
                MessageBox.Show("There are no recording devices on this computer.", "Error");
                Application.Exit();
            }
            FrameworkDispatcher.Update();
        }

        public void ShazamStateChanged(ShazamRecognitionState state, ShazamResponse response)
        {
            switch (state)
            {
                case ShazamRecognitionState.Sending:
                    statusLabel.Text = "Sending...";
                    break;
                case ShazamRecognitionState.Matching:
                    statusLabel.Text = "Matching...";
                    break;
                case ShazamRecognitionState.Done:
                    statusLabel.Text = "Click to Recognize";
                    button1.Enabled = true;
                    if (response.Tag != null)
                    {
                        if (response.Tag.Track != null)
                            MessageBox.Show("Title: " + response.Tag.Track.Title + "\r\nArtist: " + response.Tag.Track.Artist, "Hey!");
                        else
                            MessageBox.Show("Song not found :-(", "Hey!");
                    }
                    else
                        MessageBox.Show("Song not found :-(", "Hey!");
                    break;
                case ShazamRecognitionState.Failed:
                    button1.Enabled = true;
                    if (response.Exception.Message != null && response.Exception.Message != "")
                        statusLabel.Text = "Failed! Message: " + response.Exception.Message;
                    else
                        statusLabel.Text = "Failed!";
                    break;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _audioData = new byte[Microphone.Default.GetSampleSizeInBytes(TimeSpan.FromSeconds(10.0))];
            _bytesRead = 0;
            _counter = 10;
            _microphone.Start();
            timer.Start();
            recordTimer.Start();
            statusLabel.Text = "Listening... "+_counter;
            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.MarqueeAnimationSpeed = 10;
            button1.Enabled = false;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            ProcessPCMAudio(_microphone.SampleRate,16,1);
            string str = Encoding.UTF8.GetString(_audioData);
            _microphone.Stop();
            _client.DoRecognition(_audioData, MicrophoneRecordingOutputFormatType.PCM);
            _microphone.Stop();
            timer.Stop();
            recordTimer.Stop();
            progressBar1.Style = ProgressBarStyle.Continuous;
            progressBar1.MarqueeAnimationSpeed = 0;
        }


        private void ProcessPCMAudio(int sampleRate, short numBitsPerSample, short numChennels)
        {
                int length = _audioData.Length;
                WaveFile.WaveHeader waveHeader = new WaveFile.WaveHeader(length, sampleRate, numBitsPerSample, numChennels, false);
                MemoryStream memoryStream = WaveFile.WriteWaveFile(waveHeader, _audioData);
                _audioData = memoryStream.GetBuffer();
        }

        private void recordTimer_Tick(object sender, EventArgs e)
        {
            _counter--;
            statusLabel.Text = "Listening... " + _counter;
            _bytesRead += Microphone.Default.GetData(_audioData, _bytesRead, (_audioData.Length - _bytesRead));
        }
    }
}
