using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace SerialCommunication
{
    public partial class Form1 : Form
    {
        private SerialPort serialPort = null;
        private Timer timerOefening5;
        private bool lastLedState = false;

        public Form1()
        {
            InitializeComponent();

            // Timer for oefening 5
            timerOefening5 = new Timer();
            timerOefening5.Interval = 1000; // 1000 ms
            timerOefening5.Tick += TimerOefening5_Tick;

            // Start/stop timer when tab selection changes
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            // Start immediately if the oefening5 tab is already selected
            if (tabControl.SelectedTab == tabPageOefening5)
                timerOefening5.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                string[] portNames = SerialPort.GetPortNames().Distinct().ToArray();
                comboBoxPoort.Items.Clear();
                comboBoxPoort.Items.AddRange(portNames);
                if (comboBoxPoort.Items.Count > 0) comboBoxPoort.SelectedIndex = 0;

                comboBoxBaudrate.SelectedIndex = comboBoxBaudrate.Items.IndexOf("115200");
            }
            catch (Exception)
            { }
        }

        private void cboPoort_DropDown(object sender, EventArgs e)
        {
            try
            {
                string selected = (string)comboBoxPoort.SelectedItem;
                string[] portNames = SerialPort.GetPortNames().Distinct().ToArray();

                comboBoxPoort.Items.Clear();
                comboBoxPoort.Items.AddRange(portNames);

                comboBoxPoort.SelectedIndex = comboBoxPoort.Items.IndexOf(selected);
            }
            catch (Exception)
            {
                if (comboBoxPoort.Items.Count > 0) comboBoxPoort.SelectedIndex = 0;
            }
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            // Robust open/close for serial connection (works with Arduino Leonardo)
            try
            {
                if (serialPort == null || !serialPort.IsOpen)
                {
                    if (comboBoxPoort.SelectedItem == null)
                    {
                        MessageBox.Show("Selecteer eerst een seriële poort.");
                        return;
                    }

                    string port = comboBoxPoort.SelectedItem.ToString();
                    int baud = 115200;
                    if (comboBoxBaudrate.SelectedItem != null)
                        int.TryParse(comboBoxBaudrate.SelectedItem.ToString(), out baud);

                    // Create and configure port. For Leonardo avoid toggling DTR before open to prevent unwanted reset.
                    serialPort = new SerialPort(port, baud)
                    {
                        NewLine = "\n",
                        ReadTimeout = 500,
                        WriteTimeout = 500,
                        DtrEnable = false,
                        RtsEnable = false
                    };

                    serialPort.Open();

                    // Update UI and disable port selection while connected
                    UpdateConnectionUI(true);
                    buttonConnect.Text = "Disconnect";
                }
                else
                {
                    // Explicitly stop timer and ensure clean close/dispose
                    try
                    {
                        if (timerOefening5 != null && timerOefening5.Enabled)
                            timerOefening5.Stop();
                    }
                    catch { }
                    try                    {                        if (serialPort != null)                        {                            if (serialPort.IsOpen)                            {                                try { serialPort.DiscardInBuffer(); } catch { }                                serialPort.Close();                            }                            try { serialPort.Dispose(); } catch { }                        }                    }                    catch (Exception exClose)                    {                        Console.WriteLine("Fout bij sluiten seriële poort: " + exClose.Message);                    }                    finally                    {                        serialPort = null;                        UpdateConnectionUI(false);                        buttonConnect.Text = "Connect";                    }                }            }            catch (Exception ex)            {                UpdateConnectionUI(false);                MessageBox.Show("Kan geen seriële verbinding maken: " + ex.Message);            }        }

        private void UpdateConnectionUI(bool connected)
        {
            try { radioButtonVerbonden.Checked = connected; } catch { }
            try { comboBoxPoort.Enabled = !connected; } catch { }
            try { comboBoxBaudrate.Enabled = !connected; } catch { }
            // If disconnected, ensure timer doesn't run
            if (!connected)            {                try { if (timerOefening5 != null) timerOefening5.Stop(); } catch { }            }        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab == tabPageOefening5)
                timerOefening5.Start();
            else
                timerOefening5.Stop();
        }

        private void TimerOefening5_Tick(object sender, EventArgs e)
        {
            // Check serial connection presence
            if (serialPort == null || !serialPort.IsOpen)
            {
                // Optionally clear or indicate no connection
                return;
            }

            try
            {
                int rawPot = ReadAnalogValue(0);
                int rawLM35 = ReadAnalogValue(1);

                // Gewenste temperatuur: 0..1023 -> 5..45 °C
                double m1 = 40.0 / 1023.0; // slope
                double desired = m1 * rawPot + 5.0;

                // Huidige temperatuur (LM35): 0..1023 -> 0..500 °C
                double m2 = 500.0 / 1023.0;
                double current = m2 * rawLM35;

                // Update UI (rounded to 1 decimal, include unit)
                labelGewensteTemp.Text = desired.ToString("F1") + " °C";
                labelHuidigeTemp.Text = current.ToString("F1") + " °C";

                // LED control: led on when current < desired (pin 2)
                bool ledShouldBeOn = current < desired;
                if (ledShouldBeOn != lastLedState)
                {
                    SetDigital(2, ledShouldBeOn);
                    lastLedState = ledShouldBeOn;
                }
            }
            catch (TimeoutException)
            {
                // ignore temporary read timeouts
            }
            catch (Exception ex)
            {
                // Log or show error once; do not crash timer
                Console.WriteLine("Oefening5 error: " + ex.Message);
            }
        }

        private int ReadAnalogValue(int channel)
        {
            if (serialPort == null || !serialPort.IsOpen)
                throw new InvalidOperationException("Geen seriële verbinding.");

            // try a few common command formats — the Arduino sketch used with this project may accept one of these
            string[] candidates = new string[] { $"a{channel}", $"A{channel}", $"ANALOG {channel}", $"READ {channel}", $"R{channel}" };
            foreach (var cmd in candidates)
            {
                try
                {
                    serialPort.DiscardInBuffer();
                    serialPort.WriteLine(cmd);
                    string line = serialPort.ReadLine().Trim();
                    if (int.TryParse(line, out int value))
                        return Math.Max(0, Math.Min(1023, value));

                    // If response contains digits, try to extract first number
                    var digits = new string(line.Where(c => char.IsDigit(c)).ToArray());
                    if (int.TryParse(digits, out value))
                        return Math.Max(0, Math.Min(1023, value));
                }
                catch (TimeoutException)
                {
                    // try next command
                }
                catch
                {
                    // try next command
                }
            }

             throw new TimeoutException("Geen geldige analoge waarde ontvangen van kanaal " + channel);
        }

        private void SetDigital(int pin, bool on)
        {
            if (serialPort == null || !serialPort.IsOpen)
                return;

            // send a simple digital command - the Arduino sketch used in class should understand one of these
            string[] commands = new string[] { $"d{pin}:{(on ? 1 : 0)}", $"D{pin}:{(on ? 1 : 0)}", $"DIGITAL {pin} {(on ? 1 : 0)}", $"SET {pin} {(on ? 1 : 0)}" };
            foreach (var cmd in commands)
            {
                try
                {
                    serialPort.WriteLine(cmd);
                    return;
                }
                catch { }
            }
        }
    }
}
