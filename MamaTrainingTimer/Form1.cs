using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Speech.Synthesis;
using System.Reflection;

namespace MamaTrainingTimer
{
    public partial class Form1 : Form
    {
        private bool speakingEnabled;

        private DateTime totalStart;
        private TimeSpan phaseDuration;
        private TimeSpan breakDuration;
        int numOfPhasesProcessed = 0;
        int numOfBreaksProcessed = 0;
        int numOfPhases = 0;


        public Form1()
        {
            InitializeComponent();
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-notalk"))
                speakingEnabled = false;
            else
                speakingEnabled = true;

            // UI Setup
            breakDurationPicker.Format = DateTimePickerFormat.Time;
            breakDurationPicker.ShowUpDown = true;
            breakDurationPicker.Value = new DateTime(2016, 3, 19, 0, 0, 20);
            phaseDurationPicker.Value = new DateTime(2016, 3, 19, 0, 0, 30);
            phaseDurationPicker.Format = DateTimePickerFormat.Time;
            phaseDurationPicker.ShowUpDown = true;

            tabControl1.TabPages.Remove(ProgressTab);
        }

        private void Form1_Load(object sender, EventArgs e)
        {


        }


        public Prompt Speak(string textToSpeak, bool sync = false)
        {

            if (speakingEnabled)
            {
                if (sync)
                {
                    Speaker.Instance?.Speak(textToSpeak);
                }
                else
                    return Speaker.Instance?.SpeakAsync(textToSpeak);
            }
            return null;
        }



        private void btnStart_Click(object sender, EventArgs e)
        {
            tabControl1.TabPages.Add(ProgressTab);
            tabControl1.TabPages.Remove(SettingsTab);

            totalStart = DateTime.Now;
            phaseDuration = phaseDurationPicker.Value.TimeOfDay;
            breakDuration = breakDurationPicker.Value.TimeOfDay;
            numOfPhases = (int)numPhases.Value;
            numOfBreaksProcessed = numOfPhasesProcessed = 0;
            lblAction.Text = "Get ready!";
            lblTime.Text = "";

            var img = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
            Graphics g = Graphics.FromImage(img);
            g.Clear(Color.White);
            pictureBox1.Image = img;

            Speak("3 2 1, Go!", true);
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var elapsed = DateTime.Now - totalStart;
            var totalDuration = totalStart.Add(new TimeSpan(phaseDuration.Ticks * (numOfPhases) + breakDuration.Ticks * (numOfPhases - 1))) - totalStart;
            if (elapsed >= totalDuration)
            {
                Speak("You completed your training!");
                lblAction.Text = "You completed your training!";
                PlaySound("Completed.wav");
                timer1.Stop();

                var img = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
                Graphics g = Graphics.FromImage(img);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                g.FillEllipse(Brushes.Green, pictureBox1.ClientRectangle);
                pictureBox1.Image = img;

                return;
            }

            // determine the phase
            int numOfPhasesAndBreaks = (numOfPhases * 2) - 1;
            bool isCurrentPhase = false;
            int currentPhaseBreakIndex = 0;
            TimeSpan t = new TimeSpan(0);
            int counter = 0;
            while (t <= totalDuration)
            {
                if (t >= elapsed)
                {
                    isCurrentPhase = (counter - 1) % 2 == 0;
                    currentPhaseBreakIndex = (counter - 1) / 2;
                    lblTime.Text = $"Remaining to next phase/break: {(t - elapsed).TotalSeconds.ToString("f1")} secs";
                    break;
                }

                t = ++counter % 2 == 0 ? t = t.Add(breakDuration) : t = t.Add(phaseDuration);
            }

            float phaseBreakPercent;
            if (isCurrentPhase)
            {
                lblAction.Text = $"Phase: {currentPhaseBreakIndex + 1}";
                if (currentPhaseBreakIndex >= numOfPhasesProcessed)
                {
                    numOfPhasesProcessed++;
                    if (currentPhaseBreakIndex + 1 != 1)
                        PlaySound("Next.wav");
                    Speak(lblAction.Text);
                }
                phaseBreakPercent = 1 - ((float)(t - elapsed).Ticks / phaseDuration.Ticks);
            }
            else
            {
                lblAction.Text = $"Break: {currentPhaseBreakIndex + 1}";
                if (currentPhaseBreakIndex >= numOfBreaksProcessed)
                {
                    numOfBreaksProcessed++;
                    PlaySound("Next.wav");
                    Speak("Take a break!");
                }
                phaseBreakPercent = 1 - ((float)(t - elapsed).Ticks / breakDuration.Ticks);
            }


            DrawPicture(pictureBox1.Image, (float)elapsed.Ticks / totalDuration.Ticks, phaseBreakPercent);
        }

        private void PlaySound(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MamaTrainingTimer." + name);
            System.Media.SoundPlayer player = new System.Media.SoundPlayer(stream);
            player.Play();
        }

        private void DrawPicture(Image img, float totalPercent, float phasePercent)
        {
            var now = DateTime.Now;
            img?.Dispose();
            img = new Bitmap(pictureBox1.ClientSize.Width, pictureBox1.ClientSize.Height);
            Graphics g = Graphics.FromImage(img);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            g.FillEllipse(Brushes.Red, pictureBox1.ClientRectangle);
            g.FillPie(Brushes.Green, pictureBox1.ClientRectangle, -90, (float)(totalPercent * 360));

            Rectangle smallerRectangle = pictureBox1.ClientRectangle;
            smallerRectangle.Inflate((int)(-0.1 * pictureBox1.ClientRectangle.Width), (int)(-0.1 * pictureBox1.ClientRectangle.Height));
            g.FillEllipse(Brushes.Blue, smallerRectangle);
            g.FillPie(Brushes.Orange, smallerRectangle, -90, (float)(phasePercent * 360));

            pictureBox1.Image = img;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            timer1.Stop();
            tabControl1.TabPages.Add(SettingsTab);
            tabControl1.TabPages.Remove(ProgressTab);
        }
    }

    public class Speaker
    {
        private static SpeechSynthesizer synthesizer;
        private static bool possible = true;
        public static SpeechSynthesizer Instance
        {
            get
            {
                if (possible && synthesizer == null)
                {
                    synthesizer = new SpeechSynthesizer();
                    if (!IsEnglish(synthesizer.Voice))
                    {
                        foreach (var voice in synthesizer.GetInstalledVoices())
                        {
                            if (IsEnglish(voice.VoiceInfo))
                            {
                                synthesizer.SelectVoice(voice.VoiceInfo.Name);
                                possible = true;
                                break;
                            }
                            possible = false;
                        }
                        if (!possible) synthesizer = null;
                    }
                }
                return synthesizer;
            }
        }

        private static bool IsEnglish(VoiceInfo voice)
        {
            return voice.Culture.EnglishName.ToLower().StartsWith("english");
        }
    }

}


