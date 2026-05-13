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
        private SerialPort serialPortArduino = null;

        public Form1()
        {
            InitializeComponent();
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
            // Make or break connection to Arduino Leonardo per user-selected settings
            try
            {
                if (serialPortArduino == null || !serialPortArduino.IsOpen)
                {
                    // Ensure a port is selected
                    if (comboBoxPoort.SelectedItem == null)
                    {
                        MessageBox.Show("Selecteer eerst een seriële poort.");
                        return;
                    }

                    // Configure SerialPort according to UI
                    serialPortArduino = new SerialPort()
                    {
                        PortName = comboBoxPoort.SelectedItem.ToString(),
                        NewLine = "\n",
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };

                    // Baud
                    if (comboBoxBaudrate.SelectedItem != null)
                        int.TryParse(comboBoxBaudrate.SelectedItem.ToString(), out int baud);
                    else
                        int.TryParse("115200", out int baud);
                    serialPortArduino.BaudRate = baud;

                    // Data bits
                    try { serialPortArduino.DataBits = (int)numericUpDownDatabits.Value; } catch { }

                    // Parity
                    if (radioButtonParityEven.Checked) serialPortArduino.Parity = Parity.Even;
                    else if (radioButtonParityOdd.Checked) serialPortArduino.Parity = Parity.Odd;
                    else if (radioButtonParityNone.Checked) serialPortArduino.Parity = Parity.None;
                    else if (radioButtonParityMark.Checked) serialPortArduino.Parity = Parity.Mark;
                    else if (radioButtonParitySpace.Checked) serialPortArduino.Parity = Parity.Space;

                    // Stop bits
                    if (radioButtonStopbitsNone.Checked) serialPortArduino.StopBits = StopBits.None;
                    else if (radioButtonStopbitsOne.Checked) serialPortArduino.StopBits = StopBits.One;
                    else if (radioButtonStopbitsOnePointFive.Checked) serialPortArduino.StopBits = StopBits.OnePointFive;
                    else if (radioButtonStopbitsTwo.Checked) serialPortArduino.StopBits = StopBits.Two;
                    // Handshake
                    if (radioButtonHandshakeNone.Checked) serialPortArduino.Handshake = Handshake.None;
                    else if (radioButtonHandshakeRTS.Checked) serialPortArduino.Handshake = Handshake.RequestToSend;
                    else if (radioButtonHandshakeRTSXonXoff.Checked) serialPortArduino.Handshake = Handshake.RequestToSendXOnXOff;
                    else if (radioButtonHandshakeXonXoff.Checked) serialPortArduino.Handshake = Handshake.XOnXOff;
                    // RTS/DTR enables                    serialPortArduino.RtsEnable = checkBoxRtsEnable.Checked;
                    serialPortArduino.DtrEnable = checkBoxDtrEnable.Checked;
                    // Open port                    serialPortArduino.Open();
                    // Quick sanity ping/pong check                    try                    {                        serialPortArduino.DiscardInBuffer();                        serialPortArduino.WriteLine("ping");                        string resp = serialPortArduino.ReadLine().Trim();                        if (!string.Equals(resp, "pong", StringComparison.OrdinalIgnoreCase))                        {                            // Unexpected response — close and inform user                            serialPortArduino.Close();                            serialPortArduino.Dispose();                            serialPortArduino = null;                            MessageBox.Show($"Geen geldige reactie op ping ontvangen (ontvangen: '{resp}')");                            UpdateConnectedUI(false);                            return;                        }                    }                    catch (TimeoutException)                    {                        // no response — treat as failure to connect                        try { serialPortArduino.Close(); } catch { }                        try { serialPortArduino.Dispose(); } catch { }                        serialPortArduino = null;                        MessageBox.Show("Geen antwoord op ping ontvangen binnen timeout.");                        UpdateConnectedUI(false);                        return;                    }                    // Success: update UI                    UpdateConnectedUI(true);                    labelStatus.Text = $"Verbonden op {comboBoxPoort.SelectedItem}";                    buttonConnect.Text = "Disconnect";                }                else                {                    // Disconnect gracefully                    try                    {                        if (serialPortArduino.IsOpen)                        {                            try { serialPortArduino.DiscardInBuffer(); } catch { }                            serialPortArduino.Close();                        }                        try { serialPortArduino.Dispose(); } catch { }                    }                    catch (Exception exClose)                    {                        MessageBox.Show("Fout bij verbreken verbinding: " + exClose.Message);                    }                    finally                    {                        serialPortArduino = null;                        UpdateConnectedUI(false);                        labelStatus.Text = "Niet verbonden";                        buttonConnect.Text = "Connect";                    }                }            }            catch (Exception ex)            {                // Ensure safe cleanup on error                try { if (serialPortArduino != null && serialPortArduino.IsOpen) serialPortArduino.Close(); } catch { }                try { serialPortArduino?.Dispose(); } catch { }                serialPortArduino = null;                UpdateConnectedUI(false);                MessageBox.Show("Fout bij verbinding: " + ex.Message);            }        }

        private void UpdateConnectedUI(bool connected)
        {
            try { radioButtonVerbonden.Checked = connected; } catch { }
            try { comboBoxPoort.Enabled = !connected; } catch { }
            try { comboBoxBaudrate.Enabled = !connected; } catch { }
            try { numericUpDownDatabits.Enabled = !connected; } catch { }
            try { radioButtonParityEven.Enabled = !connected; radioButtonParityOdd.Enabled = !connected; radioButtonParityNone.Enabled = !connected; radioButtonParityMark.Enabled = !connected; radioButtonParitySpace.Enabled = !connected; } catch { }
            try { radioButtonStopbitsNone.Enabled = !connected; radioButtonStopbitsOne.Enabled = !connected; radioButtonStopbitsOnePointFive.Enabled = !connected; radioButtonStopbitsTwo.Enabled = !connected; } catch { }
            try { radioButtonHandshakeNone.Enabled = !connected; radioButtonHandshakeRTS.Enabled = !connected; radioButtonHandshakeRTSXonXoff.Enabled = !connected; radioButtonHandshakeXonXoff.Enabled = !connected; } catch { }
            try { checkBoxRtsEnable.Enabled = !connected; checkBoxDtrEnable.Enabled = !connected; } catch { }
        }
    }
}
