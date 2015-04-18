using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

//http://www.codeproject.com/Tips/112274/FFmpeg-Tutorial
//http://www.codeproject.com/Articles/332123/AAC-Encode

namespace VideoEncoder
{
    public class EncodeProgressEventArgs : EventArgs
    {
        public string RawOutputLine { get; set; }
        public short FPS { get; set; }
        public short Percentage { get; set; }
        public long CurrentFrame { get; set; }
        public long TotalFrames { get; set; }
    }

    public class EncodeFinishedEventArgs : EventArgs
    {
        public EncodedVideo EncodedVideoInfo { get; set; }
    }

    public delegate void EncodeProgressEventHandler(object sender, EncodeProgressEventArgs e);
    public delegate void EncodeFinishedEventHandler(object sender, EncodeFinishedEventArgs e);

    public class Encoder
    {
        EncodedVideo tempEncodedVideo = null;
        System.Windows.Forms.Control tempCaller = null;
        VideoFile tempVideoFile = null;

        int iProgressErrorCount = 0;
        const int PROGRESS_ERROR_LIMIT = 100;

        public event EncodeProgressEventHandler OnEncodeProgress;
        public event EncodeFinishedEventHandler OnEncodeFinished;

        protected virtual void DoEncodeProgress(EncodeProgressEventArgs e)
        {
            if (OnEncodeProgress != null)
                OnEncodeProgress(this, e);
        }

        protected virtual void DoEncodeFinished(EncodeFinishedEventArgs e)
        {
            if (OnEncodeFinished != null)
                OnEncodeFinished(this, e);
        }

        public EncodedVideo EncodeVideo(VideoFile input, string encodingCommand, string outputFile, bool getVideoThumbnail)
        {
            EncodedVideo encoded = new EncodedVideo();

            Params = string.Format("-i \"{0}\" {1} \"{2}\"", input.Path, encodingCommand, outputFile);
            string output = RunProcess(Params);
            encoded.EncodingLog = output;
            encoded.EncodedVideoPath = outputFile;
            
            if (File.Exists(outputFile))
            {
                encoded.Success = true;

                if (getVideoThumbnail)
                {
                    string saveThumbnailTo = outputFile + "_thumb.jpg";

                    if (GetVideoThumbnail(input, saveThumbnailTo))
                    {
                        encoded.ThumbnailPath = saveThumbnailTo;
                    }
                }
            }
            else
            {
                encoded.Success = false;
            }

            return encoded;
        }
        public void EncodeVideoAsync(VideoFile input, string encodingCommand, string outputFile, int threadCount)
        {
            EncodeVideoAsync(input, encodingCommand, outputFile, null, threadCount);
        }

        public void EncodeVideoAsync(VideoFile input, string encodingCommand, string outputFile, System.Windows.Forms.Control caller, int threadCount)
        {
            //Gather info
            if (!input.infoGathered)
            {
                GetVideoInfo(input);
            }

            //encoded video oluşturuyorum
            tempEncodedVideo = new EncodedVideo();
            tempEncodedVideo.EncodedVideoPath = outputFile;

            tempCaller = caller;

            tempVideoFile = input;

            //Create parameters
            if (threadCount.Equals(1))
                Params = string.Format("-i \"{0}\" {1} \"{2}\"", input.Path, encodingCommand, outputFile);
            else
                Params = string.Format("-i \"{0}\" -threads {1} {2} \"{3}\"", input.Path, threadCount.ToString(), encodingCommand, outputFile);

            //Execute ffmpeg async
            RunProcessAsync(Params);
        }

        public void EncodeVideoAsyncAutoCommand(VideoFile input, string outputFile, int threadCount)
        {
            EncodeVideoAsyncAutoCommand(input, outputFile, null, threadCount);
        }

        public void EncodeVideoAsyncAutoCommand(VideoFile input, string outputFile, System.Windows.Forms.Control caller, int treadCount)
        {
            if (!input.infoGathered)
            {
                GetVideoInfo(input);
            }

            
            if (input.VideoBitRate == 0)
            {
                
                int h = input.Height;

                if (h < 180) input.VideoBitRate = 400;
                else if (h < 260) input.VideoBitRate = 1000;
                else if (h < 400) input.VideoBitRate = 2000;
                else if (h < 800) input.VideoBitRate = 5000;
                else input.VideoBitRate = 8000;
            }

          
            if (input.AudioBitRate == 0) input.AudioBitRate = 128;

            
            string encodingCommand = String.Format("-threads {0} -y -b {1} -ab {2}", treadCount.ToString(), input.VideoBitRate.ToString() + "k", input.AudioBitRate.ToString() + "k");

            
            tempEncodedVideo = new EncodedVideo();
            tempEncodedVideo.EncodedVideoPath = outputFile;

           
            tempCaller = caller;

           
            tempVideoFile = input;

          
            Params = string.Format("-i \"{0}\" {1} \"{2}\"", input.Path, encodingCommand, outputFile);

           
            RunProcessAsync(Params);
        }

        private void RunProcessAsync(string Parameters)
        {
            
            ProcessStartInfo oInfo = new ProcessStartInfo(this.FFmpegPath, Parameters);

            oInfo.UseShellExecute = false;
            oInfo.CreateNoWindow = true;
            oInfo.RedirectStandardOutput = false;
            oInfo.RedirectStandardError = true;
            
         
            Process proc = new Process();


            proc.StartInfo = oInfo;

       
            proc.EnableRaisingEvents = true;
            proc.ErrorDataReceived += new DataReceivedEventHandler(proc_ErrorDataReceived);
            proc.Exited += new EventHandler(proc_Exited);

          
            proc.Start();

         
            proc.BeginErrorReadLine();
        }

    
        void proc_Exited(object sender, EventArgs e)
        {
          
            Process proc = (Process)sender;

        
            int iExitCode = proc.ExitCode;

            
            bool blFileExists = File.Exists(tempEncodedVideo.EncodedVideoPath);

  
            tempEncodedVideo.Success = (iExitCode.Equals(0) && blFileExists);

     
            EncodeFinishedEventArgs efe = new EncodeFinishedEventArgs();

            efe.EncodedVideoInfo = tempEncodedVideo;


            if (tempCaller != null)
                tempCaller.BeginInvoke(new EncodeFinishedEventHandler(OnEncodeFinished), tempCaller, efe);
            else
                DoEncodeFinished(efe);

            iProgressErrorCount = 0;


            proc.Close();
        }


        void proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                tempEncodedVideo.EncodingLog += e.Data + Environment.NewLine;

                if (e.Data.StartsWith("frame"))
                {
                  
                    iProgressErrorCount = 0;

                
                    EncodeProgressEventArgs epe = new EncodeProgressEventArgs();

            
                    epe.RawOutputLine = e.Data;

                    epe.TotalFrames = tempVideoFile.TotalFrames;

             
                    string[] parts = e.Data.Split(new string[] { " ", "=" }, StringSplitOptions.RemoveEmptyEntries);


                    long lCurrentFrame = 0L;
                    Int64.TryParse(parts[1], out lCurrentFrame);
                    epe.CurrentFrame = lCurrentFrame;


                    short sFPS = 0;
                    Int16.TryParse(parts[3], out sFPS);
                    epe.FPS = sFPS;

             
                    double dCurrentFrame = (double)epe.CurrentFrame;
                    double dTotalFrames = (double)epe.TotalFrames;
                    short sPercentage = (short)Math.Round(dCurrentFrame * 100 / dTotalFrames, 0);
                    epe.Percentage = sPercentage;

         
                    if (tempCaller != null)
                        tempCaller.BeginInvoke(new EncodeProgressEventHandler(OnEncodeProgress), tempCaller, epe);
                    else
                        DoEncodeProgress(epe);
                }
                else
             
                    iProgressErrorCount++;
            }
            else
           
                iProgressErrorCount++;


            if (iProgressErrorCount > PROGRESS_ERROR_LIMIT)
            {
              
                Process proc = (Process)sender;
                
 
                try { proc.Kill(); }
                catch { }
            }
        }

        public bool GetVideoThumbnail(VideoFile input, string saveThumbnailTo)
        {
            if (!input.infoGathered)
            {
                GetVideoInfo(input);
            }

          
            int secs;
            secs = (int)Math.Round(TimeSpan.FromTicks(input.Duration.Ticks / 3).TotalSeconds, 0);
            if (secs.Equals(0)) secs = 1;

            string Params = string.Format("-i \"{0}\" \"{1}\" -vcodec mjpeg -ss {2} -vframes 1 -an -f rawvideo", input.Path, saveThumbnailTo, secs);
            string output = RunProcess(Params);

            if (File.Exists(saveThumbnailTo))
            {
                return true;
            }
            else
            {
          
                Params = string.Format("-i \"{0}\" \"{1}\" -vcodec mjpeg -ss {2} -vframes 1 -an -f rawvideo", input.Path, saveThumbnailTo, 1);
                output = RunProcess(Params);

                if (File.Exists(saveThumbnailTo))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private string RunProcess(string Parameters)
        {
          
            ProcessStartInfo oInfo = new ProcessStartInfo(this.FFmpegPath, Parameters);
            oInfo.UseShellExecute = false;
            oInfo.CreateNoWindow = true;
            oInfo.RedirectStandardOutput = true;
            oInfo.RedirectStandardError = true;

      
            string output = null;

       
            try
            {
               
                Process proc = System.Diagnostics.Process.Start(oInfo);

                //
                // WaitForExit() kaynak: http://msdn.microsoft.com/en-us/library/system.diagnostics.process.standardoutput%28v=VS.80%29.aspx
                output = proc.StandardError.ReadToEnd();

                proc.WaitForExit();
	 
     
	            proc.Close();
            }
            catch (Exception)
            {
                output = string.Empty;
            }

            return output;
        }

        public void GetVideoInfo(VideoFile input)
        {
            string Params = string.Format("-i \"{0}\"", input.Path);
            string output = RunProcess(Params);

            input.RawInfo = output;
            input.Duration = ExtractDuration(input.RawInfo);
            input.BitRate = ExtractBitrate(input.RawInfo);
            input.RawAudioFormat = ExtractRawAudioFormat(input.RawInfo);
            input.AudioFormat = ExtractAudioFormat(input.RawAudioFormat);
            input.RawVideoFormat = ExtractRawVideoFormat(input.RawInfo);
            input.VideoFormat = ExtractVideoFormat(input.RawVideoFormat);
            input.Width = ExtractVideoWidth(input.RawInfo);
            input.Height = ExtractVideoHeight(input.RawInfo);
            input.FrameRate = ExtractFrameRate(input.RawVideoFormat);
            input.TotalFrames = ExtractTotalFrames(input.Duration, input.FrameRate);
            input.AudioBitRate = ExtractAudioBitRate(input.RawAudioFormat);
            input.VideoBitRate = ExtractVideoBitRate(input.RawVideoFormat);

            input.infoGathered = true;
        }

        public string FFmpegPath { get; set; }
        private string Params { get; set; }

        #region Extraction methods
        private TimeSpan ExtractDuration(string rawInfo)
        {
            TimeSpan t = new TimeSpan(0);
            Regex re = new Regex("[D|d]uration:.((\\d|:|\\.)*)", RegexOptions.Compiled);
            Match m = re.Match(rawInfo);

            if (m.Success)
            {
                string duration = m.Groups[1].Value;
                string[] timepieces = duration.Split(new char[] { ':', '.' });
                if (timepieces.Length == 4)
                {
                    t = new TimeSpan(0, Convert.ToInt16(timepieces[0]), Convert.ToInt16(timepieces[1]), Convert.ToInt16(timepieces[2]), Convert.ToInt16(timepieces[3]));
                }
            }

            return t;
        }
        private double ExtractBitrate(string rawInfo)
        {
            Regex re = new Regex("[B|b]itrate:.((\\d|:)*)", RegexOptions.Compiled);
            Match m = re.Match(rawInfo);
            double kb = 0.0;
            if (m.Success)
            {
                Double.TryParse(m.Groups[1].Value, out kb);
            }
            return kb;
        }
        private string ExtractRawAudioFormat(string rawInfo)
        {
            string a = string.Empty;
            Regex re = new Regex("[A|a]udio:.*", RegexOptions.Compiled);
            Match m = re.Match(rawInfo);
            if (m.Success)
            {
                a = m.Value;
            }
            return a.Replace("Audio: ", "");
        }
        private string ExtractAudioFormat(string rawAudioFormat)
        {
            string[] parts = rawAudioFormat.Split(new string[] { ", " }, StringSplitOptions.None);
            return parts[0].Replace("Audio: ", "");
        }
        private string ExtractRawVideoFormat(string rawInfo)
        {
            string v = string.Empty;
            Regex re = new Regex("[V|v]ideo:.*", RegexOptions.Compiled);
            Match m = re.Match(rawInfo);
            if (m.Success)
            {
                v = m.Value;
            }
            return v.Replace("Video: ", ""); ;
        }
        private string ExtractVideoFormat(string rawVideoFormat)
        {
            string[] parts = rawVideoFormat.Split(new string[] { ", " }, StringSplitOptions.None);
            return parts[0].Replace("Video: ", "");
        }
        private int ExtractVideoWidth(string rawInfo)
        {
            int width = 0;
            Regex re = new Regex("(\\d{2,4})x(\\d{2,4})", RegexOptions.Compiled);
            Match m = re.Match(rawInfo);
            if (m.Success)
            {
                int.TryParse(m.Groups[1].Value, out width);
            }
            return width;
        }
        private int ExtractVideoHeight(string rawInfo)
        {
            int height = 0;
            Regex re = new Regex("(\\d{2,4})x(\\d{2,4})", RegexOptions.Compiled);
            Match m = re.Match(rawInfo);
            if (m.Success)
            {
                int.TryParse(m.Groups[2].Value, out height);
            }
            return height;
        }
        private double ExtractFrameRate(string rawVideoFormat)
        {
            string[] parts = rawVideoFormat.Split(new string[] { ", " }, StringSplitOptions.None);

            double dFPS = 0;

            foreach (string p in parts)
            {
                if (p.ToLower().Contains("fps"))
                {
                    Double.TryParse(p.ToLower().Replace("fps", "").Replace(".", ",").Trim(), out dFPS);
                    
                    break;
                }
                else if (p.ToLower().Contains("tbr"))
                {
                    Double.TryParse(p.ToLower().Replace("tbr", "").Replace(".", ",").Trim(), out dFPS);

                    break;
                }
            }

            //Audio: mp3, 44100 Hz, 2 channels, s16, 140 kb/s

            return dFPS;
        }
        private double ExtractAudioBitRate(string rawAudioFormat)
        {
            string[] parts = rawAudioFormat.Split(new string[] { ", " }, StringSplitOptions.None);

            double dABR = 0;

            foreach (string p in parts)
            {
                if (p.ToLower().Contains("kb/s"))
                {
                    Double.TryParse(p.ToLower().Replace("kb/s", "").Replace(".", ",").Trim(), out dABR);

                    break;
                }
            }

            return dABR;
        }
        private double ExtractVideoBitRate(string rawVideoFormat)
        {
            string[] parts = rawVideoFormat.Split(new string[] { ", " }, StringSplitOptions.None);

            double dVBR = 0;

            foreach (string p in parts)
            {
                if (p.ToLower().Contains("kb/s"))
                {
                    Double.TryParse(p.ToLower().Replace("kb/s", "").Replace(".", ",").Trim(), out dVBR);

                    break;
                }
            }

            return dVBR;
        }
        private long ExtractTotalFrames(TimeSpan duration, double frameRate)
        {
            return (long)Math.Round(duration.TotalSeconds * frameRate, 0);
        }
        #endregion
    }
}