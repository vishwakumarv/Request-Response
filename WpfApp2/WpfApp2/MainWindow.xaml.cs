using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Edj20Tester.Models;

namespace Edj20Tester
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            StatusDot.Fill = Brushes.Yellow;
            StatusText.Text = "STATUS : RUNNING...";

            ModbusFunction function = ModbusFunction.FC01_ReadCoils;
            if (FunctionSelector?.SelectedItem is ComboBoxItem item && item.Tag is ModbusFunction fn)
                function = fn;

            var client = new DeviceClient();
            var response = await client.SendAsync(function);

            RequestPanel.Children.Clear();
            ResponsePanel.Children.Clear();

            if (response.Request != null)
                RequestPanel.Children.Add(BuildRequestTable(response.Request));

            if (response.Response != null)
                ResponsePanel.Children.Add(BuildResponseTable(response.Response));

            RequestScroller.ScrollToTop();
            ResponseScroller.ScrollToTop();

            StatusDot.Fill = response.IsError ? Brushes.Red : Brushes.Lime;
            StatusText.Text = response.IsError ? "STATUS : ERROR" : "STATUS : PASS";
            btnStart.IsEnabled = true;
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            RequestPanel.Children.Clear();
            ResponsePanel.Children.Clear();
            StatusDot.Fill = Brushes.Lime;
            StatusText.Text = "STATUS : READY";
        }

        // ── REQUEST TABLE ─────────────────────────────────────────────────────

        private UIElement BuildRequestTable(ModbusPacket pkt)
        {
            var outerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

            var grid = MakeTableGrid();
            AddHeaderRow(grid, 0);
            int row = 1;

            string expSlaveAddr = "01";
            string expFc = $"{pkt.FunctionCode:X2}";

            AddRow(grid, row++, "Header", "None", "None", "None");
            AddRow(grid, row++, "Slave Address", $"{pkt.SlaveAddress:X2}", $"0 {pkt.SlaveAddress:X}", expSlaveAddr);
            AddRow(grid, row++, "Function", $"{pkt.FunctionCode:X2}", $"0 {pkt.FunctionCode:X}", expFc);

            switch (pkt.Function)
            {
                //FC01 – Read Coils
                case ModbusFunction.FC01_ReadCoils:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                    AddRow(grid, row++, "Quantity of Coils Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", "00");
                    AddRow(grid, row++, "Quantity of Coils Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", "02");
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "BD");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "CB");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                    break;

                //FC02 – Read Discrete Inputs
                case ModbusFunction.FC02_ReadDiscreteInputs:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                    AddRow(grid, row++, "Quantity of Coils Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", "00");
                    AddRow(grid, row++, "Quantity of Coils Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", "02");
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "F9");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "CB");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                    break;

                //FC03 – Read Holding Registers
                case ModbusFunction.FC03_ReadHoldingRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                    AddRow(grid, row++, "No. of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", "00");
                    AddRow(grid, row++, "No. of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", "02");
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "C4");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "0B");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                    break;

                //FC04 – Read Input Registers
                case ModbusFunction.FC04_ReadInputRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                    AddRow(grid, row++, "No. of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", "00");
                    AddRow(grid, row++, "No. of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", "02");
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "71");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "CB");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                    break;

                //FC05 – Write Single Coil
                case ModbusFunction.FC05_WriteSingleCoil:
                    {
                        byte valHi = pkt.DataBytes?[0] ?? 0x00;
                        byte valLo = pkt.DataBytes?[1] ?? 0x00;
                        string state = valHi == 0xFF ? "ON (0xFF00)" : "OFF (0x0000)";
                        AddRow(grid, row++, "Output Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                        AddRow(grid, row++, "Output Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                        AddRow(grid, row++, $"Output Value Hi  [{state}]", $"{valHi:X2}", $"0 {valHi:X}", "FF");
                        AddRow(grid, row++, "Output Value Lo", $"{valLo:X2}", $"0 {valLo:X}", "00");
                        AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "8C");
                        AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "3A");
                        AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                        break;
                    }

                //FC06 – Write Single Register
                case ModbusFunction.FC06_WriteSingleRegister:
                    {
                        byte valHi = pkt.DataBytes?[0] ?? 0x00;
                        byte valLo = pkt.DataBytes?[1] ?? 0x00;
                        ushort val = (ushort)((valHi << 8) | valLo);
                        AddRow(grid, row++, "Register Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                        AddRow(grid, row++, "Register Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "01");
                        AddRow(grid, row++, $"Register Value Hi  [= {val} decimal]", $"{valHi:X2}", $"0 {valHi:X}", "00");
                        AddRow(grid, row++, "Register Value Lo", $"{valLo:X2}", $"0 {valLo:X}", "03");
                        AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "98");
                        AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "0B");
                        AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                        break;
                    }

                //FC15 – Write Multiple Coils
                case ModbusFunction.FC15_WriteMultipleCoils:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                    AddRow(grid, row++, "Quantity of Outputs Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", "00");
                    AddRow(grid, row++, "Quantity of Outputs Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", "0A");
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", "02");
                    if (pkt.DataBytes != null)
                    {
                        string[] expCoilBytes = { "55", "01" };
                        for (int i = 0; i < pkt.DataBytes.Length; i++)
                        {
                            byte b = pkt.DataBytes[i];
                            string expByte = i < expCoilBytes.Length ? expCoilBytes[i] : "—";
                            AddRow(grid, row++,
                                $"Outputs Value (Byte {i + 1})  [bits: {Convert.ToString(b, 2).PadLeft(8, '0')}]",
                                $"{b:X2}", $"0 {b:X}", expByte);
                        }
                    }
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "1B");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "A8");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "11");
                    break;

                //FC16 – Write Multiple Registers
                case ModbusFunction.FC16_WriteMultipleRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                    AddRow(grid, row++, "Quantity of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", "00");
                    AddRow(grid, row++, "Quantity of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", "02");
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", "04");
                    if (pkt.DataBytes != null)
                    {
                        string[] expRegBytes = { "00", "0A", "01", "02" };
                        for (int i = 0; i < pkt.DataBytes.Length; i += 2)
                        {
                            int regNum = (i / 2) + 1;
                            byte hi = pkt.DataBytes[i];
                            byte lo = pkt.DataBytes[i + 1];
                            ushort val = (ushort)((hi << 8) | lo);
                            AddRow(grid, row++, $"Reg {regNum} Hi", $"{hi:X2}", $"0 {hi:X}", expRegBytes[i]);
                            AddRow(grid, row++, $"Reg {regNum} Lo", $"{lo:X2}", $"0 {lo:X}", expRegBytes[i + 1]);
                        }
                    }
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "53");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "FC");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "13");
                    break;
            }

            outerStack.Children.Add(WrapTable(grid));
            outerStack.Children.Add(RawHexBlock(pkt.RawBytes, "#00FFFF"));
            return outerStack;
        }

        // ── RESPONSE TABLE ────────────────────────────────────────────────────

        private UIElement BuildResponseTable(ModbusPacket pkt)
        {
            var outerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

            var grid = MakeTableGrid();
            AddHeaderRow(grid, 0);
            int row = 1;

            AddRow(grid, row++, "Header", "None", "None", "None");
            AddRow(grid, row++, "Slave Address", $"{pkt.SlaveAddress:X2}", $"0 {pkt.SlaveAddress:X}", "01");
            AddRow(grid, row++, "Function", $"{pkt.FunctionCode:X2}", $"0 {pkt.FunctionCode:X}", $"{pkt.FunctionCode:X2}");

            switch (pkt.Function)
            {
                //FC01 – Read Coils
                case ModbusFunction.FC01_ReadCoils:
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", "01");
                    if (pkt.DataBytes != null)
                        for (int i = 0; i < pkt.DataBytes.Length; i++)
                        {
                            byte b = pkt.DataBytes[i];
                            AddRow(grid, row++, $"Coil Status (Byte {i + 1})", $"{b:X2}",
                                $"0 {b:X}  [bits: {Convert.ToString(b, 2).PadLeft(8, '0')}]", "01");
                        }
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "90");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "48");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "6");
                    break;

                //FC02 – Read Discrete Inputs
                case ModbusFunction.FC02_ReadDiscreteInputs:
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", "01");
                    if (pkt.DataBytes != null)
                        for (int i = 0; i < pkt.DataBytes.Length; i++)
                        {
                            byte b = pkt.DataBytes[i];
                            AddRow(grid, row++, $"Coil Status (Byte {i + 1})", $"{b:X2}",
                                $"0 {b:X}  [bits: {Convert.ToString(b, 2).PadLeft(8, '0')}]", "01");
                        }
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "60");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "48");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "6");
                    break;

                //FC03 – Read Holding Registers
                case ModbusFunction.FC03_ReadHoldingRegisters:
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", "04");
                    if (pkt.DataBytes != null)
                    {
                        string[] expRegBytes = { "00", "06", "00", "05" };
                        for (int i = 0; i < pkt.DataBytes.Length; i += 2)
                        {
                            int regNum = (i / 2) + 1;
                            AddRow(grid, row++, $"Reg {regNum} Hi", $"{pkt.DataBytes[i]:X2}", $"0 {pkt.DataBytes[i]:X}", expRegBytes[i]);
                            AddRow(grid, row++, $"Reg {regNum} Lo", $"{pkt.DataBytes[i + 1]:X2}", $"0 {pkt.DataBytes[i + 1]:X}", expRegBytes[i + 1]);
                        }
                    }
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "DA");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "31");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "9");
                    break;

                //FC04 – Read Input Registers
                case ModbusFunction.FC04_ReadInputRegisters:
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", "04");
                    if (pkt.DataBytes != null)
                    {
                        string[] expRegBytes = { "00", "06", "00", "05" };
                        for (int i = 0; i < pkt.DataBytes.Length; i += 2)
                        {
                            int regNum = (i / 2) + 1;
                            AddRow(grid, row++, $"Data Hi (Reg {regNum})", $"{pkt.DataBytes[i]:X2}", $"0 {pkt.DataBytes[i]:X}", expRegBytes[i]);
                            AddRow(grid, row++, $"Data Lo (Reg {regNum})", $"{pkt.DataBytes[i + 1]:X2}", $"0 {pkt.DataBytes[i + 1]:X}", expRegBytes[i + 1]);
                        }
                    }
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "DB");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "86");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "9");
                    break;

                //FC05 – Write Single Coil (echo response)
                case ModbusFunction.FC05_WriteSingleCoil:
                    {
                        byte hi = pkt.DataBytes?[0] ?? 0x00;
                        byte lo = pkt.DataBytes?[1] ?? 0x00;
                        string annotation = hi == 0xFF ? "  [ON confirmed]" : "  [OFF confirmed]";
                        AddRow(grid, row++, "Output Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                        AddRow(grid, row++, "Output Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                        AddRow(grid, row++, $"Output Value Hi{annotation}", $"{hi:X2}", $"0 {hi:X}", "FF");
                        AddRow(grid, row++, "Output Value Lo", $"{lo:X2}", $"0 {lo:X}", "00");
                        AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "8C");
                        AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "3A");
                        AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                        break;
                    }

                //FC06 – Write Single Register (echo response)
                case ModbusFunction.FC06_WriteSingleRegister:
                    {
                        byte hi = pkt.DataBytes?[0] ?? 0x00;
                        byte lo = pkt.DataBytes?[1] ?? 0x00;
                        ushort val = (ushort)((hi << 8) | lo);
                        AddRow(grid, row++, "Register Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                        AddRow(grid, row++, "Register Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "01");
                        AddRow(grid, row++, $"Register Value Hi  [= {val} decimal]", $"{hi:X2}", $"0 {hi:X}", "00");
                        AddRow(grid, row++, "Register Value Lo", $"{lo:X2}", $"0 {lo:X}", "03");
                        AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "98");
                        AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "0B");
                        AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                        break;
                    }

                //FC15 – Write Multiple Coils (confirmation response)
                case ModbusFunction.FC15_WriteMultipleCoils:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                    AddRow(grid, row++, "Quantity of Outputs Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", "00");
                    AddRow(grid, row++, "Quantity of Outputs Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", "0A");
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "D5");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "CC");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                    break;

                //FC16 – Write Multiple Registers (confirmation response)
                case ModbusFunction.FC16_WriteMultipleRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", "00");
                    AddRow(grid, row++, "Quantity of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", "00");
                    AddRow(grid, row++, "Quantity of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", "02");
                    AddRow(grid, row++, "Error Check Lo (CRC)", $"{(byte)(pkt.Crc & 0xFF):X2}", $"LRC ({(byte)(pkt.Crc & 0xFF):X2})", "41");
                    AddRow(grid, row++, "Error Check Hi (CRC)", $"{(byte)(pkt.Crc >> 8):X2}", "None", "C8");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "8");
                    break;
            }

            outerStack.Children.Add(WrapTable(grid));
            outerStack.Children.Add(RawHexBlock(pkt.RawBytes, "#00FF00"));
            return outerStack;
        }

        // ── Table helpers ─────────────────────────────────────────────────────

        private static readonly string[] ColHeaders = { "Field Name", "RTU (hex)", "ASCII Characters", "Expected" };

        private Grid MakeTableGrid()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            return g;
        }

        private void AddHeaderRow(Grid g, int rowIndex)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < ColHeaders.Length; c++)
            {
                var cell = TableCell(ColHeaders[c], "#CCCCCC", isHeader: true);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, c);
                g.Children.Add(cell);
            }
        }

        private void AddRow(Grid g, int rowIndex, string field, string rtu, string ascii, string expected)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] vals = { field, rtu, ascii, expected };
            for (int c = 0; c < vals.Length; c++)
            {
                var cell = TableCell(vals[c], "#DDDDDD", isHeader: false);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, c);
                g.Children.Add(cell);
            }
        }

        private Border TableCell(string text, string fgHex, bool isHeader)
        {
            return new Border
            {
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#333333"),
                BorderThickness = new Thickness(0.5),
                Background = isHeader
                    ? (Brush)new BrushConverter().ConvertFromString("#1A1A2E")
                    : (Brush)new BrushConverter().ConvertFromString("#111111"),
                Padding = new Thickness(8, 5, 8, 5),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = (Brush)new BrushConverter().ConvertFromString(fgHex),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private Border WrapTable(Grid g)
        {
            return new Border
            {
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#444444"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 10, 0, 8),
                ClipToBounds = true,
                Child = g
            };
        }

        private UIElement RawHexBlock(byte[] bytes, string colorHex)
        {
            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                parts.Append(bytes[i].ToString("X2"));
                if (i < bytes.Length - 1)
                {
                    bool doubleSpace = (i == 0) || (i == 1) || (i == bytes.Length - 3);
                    parts.Append(doubleSpace ? "  " : " ");
                }
            }
            return new TextBlock
            {
                Text = parts.ToString(),
                Foreground = (Brush)new BrushConverter().ConvertFromString(colorHex),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 8, 0, 6)
            };
        }

        private UIElement SectionLabel(string text, string colorHex)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = (Brush)new BrushConverter().ConvertFromString(colorHex),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
        }
    }
}
