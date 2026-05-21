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

            PacketPanel.Children.Clear();
            ShowPackets(response);
            PacketScroller.ScrollToTop();

            StatusDot.Fill = response.IsError ? Brushes.Red : Brushes.Lime;
            StatusText.Text = response.IsError ? "STATUS : ERROR" : "STATUS : PASS";
            btnStart.IsEnabled = true;
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            PacketPanel.Children.Clear();
            StatusDot.Fill = Brushes.Lime;
            StatusText.Text = "STATUS : READY";
        }

        // ── Render ────────────────────────────────────────────────────────────

        private void ShowPackets(DeviceResponse response)
        {
            if (response.Request != null)
                PacketPanel.Children.Add(BuildRequestTable(response.Request));
            if (response.Response != null)
                PacketPanel.Children.Add(BuildResponseTable(response.Response));
        }

        // ── REQUEST table ─────────────────────────────────────────────────────

        private UIElement BuildRequestTable(ModbusPacket pkt)
        {
            var outerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            outerStack.Children.Add(SectionLabel("REQUEST", "#00FFFF"));

            var grid = MakeTableGrid();
            AddHeaderRow(grid, 0);
            int row = 1;

            AddRow(grid, row++, "Header", "None", "None", "None");
            AddRow(grid, row++, "Slave Address", $"{pkt.SlaveAddress:X2}", $"0 {pkt.SlaveAddress:X}", $"{pkt.SlaveAddress:X2}");
            AddRow(grid, row++, "Function", $"{pkt.FunctionCode:X2}", $"0 {pkt.FunctionCode:X}", $"{pkt.FunctionCode:X2}");

            switch (pkt.Function)
            {
                // ── FC01 / FC02 ──────────────────────────────────────────────
                case ModbusFunction.FC01_ReadCoils:
                case ModbusFunction.FC02_ReadDiscreteInputs:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", $"{(pkt.StartAddress >> 8):X2}");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", $"{(pkt.StartAddress & 0xFF):X2}");
                    AddRow(grid, row++, "Quantity of Coils Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", $"{(pkt.Quantity >> 8):X2}");
                    AddRow(grid, row++, "Quantity of Coils Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", $"{(pkt.Quantity & 0xFF):X2}");
                    break;

                // ── FC03 / FC04 ──────────────────────────────────────────────
                case ModbusFunction.FC03_ReadHoldingRegisters:
                case ModbusFunction.FC04_ReadInputRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", $"{(pkt.StartAddress >> 8):X2}");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", $"{(pkt.StartAddress & 0xFF):X2}");
                    AddRow(grid, row++, "No. of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", $"{(pkt.Quantity >> 8):X2}");
                    AddRow(grid, row++, "No. of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", $"{(pkt.Quantity & 0xFF):X2}");
                    break;

                // ── FC05 ─────────────────────────────────────────────────────
                case ModbusFunction.FC05_WriteSingleCoil:
                    {
                        byte valHi = pkt.DataBytes?[0] ?? 0x00;
                        byte valLo = pkt.DataBytes?[1] ?? 0x00;
                        string state = valHi == 0xFF ? "ON (0xFF00)" : "OFF (0x0000)";
                        byte expHi = valHi == 0xFF ? (byte)0xFF : (byte)0x00;
                        AddRow(grid, row++, "Output Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", $"{(pkt.StartAddress >> 8):X2}");
                        AddRow(grid, row++, "Output Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", $"{(pkt.StartAddress & 0xFF):X2}");
                        AddRow(grid, row++, $"Output Value Hi  [{state}]", $"{valHi:X2}", $"0 {valHi:X}", $"{expHi:X2}");
                        AddRow(grid, row++, "Output Value Lo", $"{valLo:X2}", $"0 {valLo:X}", "00");
                        break;
                    }

                // ── FC06 ─────────────────────────────────────────────────────
                case ModbusFunction.FC06_WriteSingleRegister:
                    {
                        byte valHi = pkt.DataBytes?[0] ?? 0x00;
                        byte valLo = pkt.DataBytes?[1] ?? 0x00;
                        ushort val = (ushort)((valHi << 8) | valLo);
                        AddRow(grid, row++, "Register Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", $"{(pkt.StartAddress >> 8):X2}");
                        AddRow(grid, row++, "Register Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", $"{(pkt.StartAddress & 0xFF):X2}");
                        AddRow(grid, row++, $"Register Value Hi  [= {val} decimal]", $"{valHi:X2}", $"0 {valHi:X}", $"{valHi:X2}");
                        AddRow(grid, row++, "Register Value Lo", $"{valLo:X2}", $"0 {valLo:X}", $"{valLo:X2}");
                        break;
                    }

                // ── FC15 ─────────────────────────────────────────────────────
                case ModbusFunction.FC15_WriteMultipleCoils:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", $"{(pkt.StartAddress >> 8):X2}");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", $"{(pkt.StartAddress & 0xFF):X2}");
                    AddRow(grid, row++, "Quantity of Outputs Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", $"{(pkt.Quantity >> 8):X2}");
                    AddRow(grid, row++, "Quantity of Outputs Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", $"{(pkt.Quantity & 0xFF):X2}");
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", $"{pkt.ByteCount:X2}");
                    if (pkt.DataBytes != null)
                        for (int i = 0; i < pkt.DataBytes.Length; i++)
                        {
                            byte b = pkt.DataBytes[i];
                            AddRow(grid, row++,
                                $"Outputs Value (Byte {i + 1})  [bits: {Convert.ToString(b, 2).PadLeft(8, '0')}]",
                                $"{b:X2}", $"0 {b:X}", $"{b:X2}");
                        }
                    break;

                // ── FC16 ─────────────────────────────────────────────────────
                case ModbusFunction.FC16_WriteMultipleRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", $"{(pkt.StartAddress >> 8):X2}");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", $"{(pkt.StartAddress & 0xFF):X2}");
                    AddRow(grid, row++, "Quantity of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", $"{(pkt.Quantity >> 8):X2}");
                    AddRow(grid, row++, "Quantity of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", $"{(pkt.Quantity & 0xFF):X2}");
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", $"{pkt.ByteCount:X2}");
                    if (pkt.DataBytes != null)
                        for (int i = 0; i < pkt.DataBytes.Length; i += 2)
                        {
                            int regNum = (i / 2) + 1;
                            byte hi = pkt.DataBytes[i];
                            byte lo = pkt.DataBytes[i + 1];
                            ushort val = (ushort)((hi << 8) | lo);
                            AddRow(grid, row++, $"Registers Value Hi (Reg {regNum})  [= {val} decimal]", $"{hi:X2}", $"0 {hi:X}", $"{hi:X2}");
                            AddRow(grid, row++, $"Registers Value Lo (Reg {regNum})", $"{lo:X2}", $"0 {lo:X}", $"{lo:X2}");
                        }
                    break;
            }

            byte crcLo = (byte)(pkt.Crc & 0xFF);
            byte crcHi = (byte)(pkt.Crc >> 8);
            AddRow(grid, row++, "Error Check Lo (CRC)", $"{crcLo:X2}", $"LRC ({crcLo:X2})", $"{crcLo:X2}");
            AddRow(grid, row++, "Error Check Hi (CRC)", $"{crcHi:X2}", "None", $"{crcHi:X2}");
            AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", $"{pkt.RawBytes.Length}");

            outerStack.Children.Add(WrapTable(grid));
            outerStack.Children.Add(RawHexBlock(pkt.RawBytes, "#00FFFF"));
            return outerStack;
        }

        // ── RESPONSE table ────────────────────────────────────────────────────

        private UIElement BuildResponseTable(ModbusPacket pkt)
        {
            var outerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            outerStack.Children.Add(SectionLabel("RESPONSE", "#00FF00"));

            var grid = MakeTableGrid();
            AddHeaderRow(grid, 0);
            int row = 1;

            AddRow(grid, row++, "Header", "None", "None", "None");
            AddRow(grid, row++, "Slave Address", $"{pkt.SlaveAddress:X2}", $"0 {pkt.SlaveAddress:X}", $"{pkt.SlaveAddress:X2}");
            AddRow(grid, row++, "Function", $"{pkt.FunctionCode:X2}", $"0 {pkt.FunctionCode:X}", $"{pkt.FunctionCode:X2}");

            switch (pkt.Function)
            {
                // ── FC01 / FC02 ──────────────────────────────────────────────
                case ModbusFunction.FC01_ReadCoils:
                case ModbusFunction.FC02_ReadDiscreteInputs:
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", $"{pkt.ByteCount:X2}");
                    if (pkt.DataBytes != null)
                        for (int i = 0; i < pkt.DataBytes.Length; i++)
                        {
                            byte b = pkt.DataBytes[i];
                            AddRow(grid, row++,
                                $"Coil Status (Byte {i + 1})",
                                $"{b:X2}",
                                $"0 {b:X}  [bits: {Convert.ToString(b, 2).PadLeft(8, '0')}]",
                                $"{b:X2}");
                        }
                    break;

                // ── FC03 / FC04 ──────────────────────────────────────────────
                case ModbusFunction.FC03_ReadHoldingRegisters:
                case ModbusFunction.FC04_ReadInputRegisters:
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"0 {pkt.ByteCount:X}", $"{pkt.ByteCount:X2}");
                    if (pkt.DataBytes != null)
                        for (int i = 0; i < pkt.DataBytes.Length; i += 2)
                        {
                            int regNum = (i / 2) + 1;
                            AddRow(grid, row++, $"Data Hi (Reg {regNum})", $"{pkt.DataBytes[i]:X2}", $"0 {pkt.DataBytes[i]:X}", $"{pkt.DataBytes[i]:X2}");
                            AddRow(grid, row++, $"Data Lo (Reg {regNum})", $"{pkt.DataBytes[i + 1]:X2}", $"0 {pkt.DataBytes[i + 1]:X}", $"{pkt.DataBytes[i + 1]:X2}");
                        }
                    break;

                // ── FC05 / FC06 – echo response ──────────────────────────────
                case ModbusFunction.FC05_WriteSingleCoil:
                case ModbusFunction.FC06_WriteSingleRegister:
                    {
                        string addrLabel = pkt.Function == ModbusFunction.FC05_WriteSingleCoil ? "Output Address" : "Register Address";
                        string valLabel = pkt.Function == ModbusFunction.FC05_WriteSingleCoil ? "Output Value" : "Register Value";
                        byte hi = pkt.DataBytes?[0] ?? 0x00;
                        byte lo = pkt.DataBytes?[1] ?? 0x00;
                        string annotation = pkt.Function == ModbusFunction.FC05_WriteSingleCoil
                            ? (hi == 0xFF ? "  [ON confirmed]" : "  [OFF confirmed]")
                            : $"  [= {(ushort)((hi << 8) | lo)} decimal]";

                        AddRow(grid, row++, $"{addrLabel} Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", $"{(pkt.StartAddress >> 8):X2}");
                        AddRow(grid, row++, $"{addrLabel} Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", $"{(pkt.StartAddress & 0xFF):X2}");
                        AddRow(grid, row++, $"{valLabel} Hi{annotation}", $"{hi:X2}", $"0 {hi:X}", $"{hi:X2}");
                        AddRow(grid, row++, $"{valLabel} Lo", $"{lo:X2}", $"0 {lo:X}", $"{lo:X2}");
                        break;
                    }

                // ── FC15 / FC16 – short confirmation response ────────────────
                case ModbusFunction.FC15_WriteMultipleCoils:
                case ModbusFunction.FC16_WriteMultipleRegisters:
                    {
                        string qtyLabel = pkt.Function == ModbusFunction.FC15_WriteMultipleCoils
                            ? "Quantity of Outputs"
                            : "Quantity of Registers";
                        AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"0 {(pkt.StartAddress >> 8):X}", $"{(pkt.StartAddress >> 8):X2}");
                        AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"0 {(pkt.StartAddress & 0xFF):X}", $"{(pkt.StartAddress & 0xFF):X2}");
                        AddRow(grid, row++, $"{qtyLabel} Hi", $"{(pkt.Quantity >> 8):X2}", $"0 {(pkt.Quantity >> 8):X}", $"{(pkt.Quantity >> 8):X2}");
                        AddRow(grid, row++, $"{qtyLabel} Lo", $"{(pkt.Quantity & 0xFF):X2}", $"0 {(pkt.Quantity & 0xFF):X}", $"{(pkt.Quantity & 0xFF):X2}");
                        break;
                    }
            }

            byte crcLo = (byte)(pkt.Crc & 0xFF);
            byte crcHi = (byte)(pkt.Crc >> 8);
            AddRow(grid, row++, "Error Check Lo (CRC)", $"{crcLo:X2}", $"LRC ({crcLo:X2})", $"{crcLo:X2}");
            AddRow(grid, row++, "Error Check Hi (CRC)", $"{crcHi:X2}", "None", $"{crcHi:X2}");
            AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", $"{pkt.RawBytes.Length}");

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
