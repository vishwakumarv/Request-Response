using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Net.Sockets;
using Edj20Tester.Models;

namespace Edj20Tester
{
    public partial class MainWindow : Window
    {
        private TcpClient _tcpClient;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ── Button Handlers ───────────────────────────────────────────────────

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = TxtIpAddress.Text.Trim();
                int port = int.Parse(TxtPort.Text.Trim());

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ip, port);

                TcpDot.Fill = Brushes.Lime;
                TcpStatusText.Text = "TCP : CONNECTED";
                TcpStatusText.Foreground = Brushes.Lime;

                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
                btnStart.IsEnabled = true;
            }
            catch (Exception ex)
            {
                TcpDot.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));
                TcpStatusText.Text = "TCP : DISCONNECTED";
                TcpStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));

                btnConnect.IsEnabled = true;
                btnDisconnect.IsEnabled = false;
                btnStart.IsEnabled = false;

                MessageBox.Show($"Connection failed:\n{ex.Message}", "Connection Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _tcpClient?.Close();
            _tcpClient = null;

            TcpDot.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));
            TcpStatusText.Text = "TCP : DISCONNECTED";
            TcpStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));

            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            btnStart.IsEnabled = false;
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            StatusDot.Fill = Brushes.Yellow;
            StatusText.Text = "STATUS : RUNNING...";

            ModbusFunction function = ModbusFunction.FC03_ReadHoldingRegisters;
            if (FunctionSelector?.SelectedItem is ComboBoxItem item && item.Tag is ModbusFunction fn)
                function = fn;

            var client = new DeviceClient();
            var response = await client.SendAsync(function);

            RequestPanel.Children.Clear();
            ResponsePanel.Children.Clear();

            // 1. TCP handshake section
            RequestPanel.Children.Add(BuildTcpHandshakeBlock(isRequestSide: true));
            ResponsePanel.Children.Add(BuildTcpHandshakeBlock(isRequestSide: false));

            // 2. Modbus packet tables
            if (response.Request != null) RequestPanel.Children.Add(BuildRequestTable(response.Request));
            if (response.Response != null) ResponsePanel.Children.Add(BuildResponseTable(response.Response));

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

        // ── TCP Handshake block ───────────────────────────────────────────────

        private UIElement BuildTcpHandshakeBlock(bool isRequestSide)
        {
            string clientIp = TxtIpAddress.Text.Trim();
            string serverIp = TxtIpAddress.Text.Trim();
            string port = TxtPort.Text.Trim();

            var outer = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };

            outer.Children.Add(MakeSectionHeading("TCP CONNECTION  ( 3-Way Handshake )", "#FFD700"));

            outer.Children.Add(MakeInfoRow(
                isRequestSide
                    ? $"Client  {clientIp}  →  Server  {clientIp} : {port}"
                    : $"Server  {clientIp} : {port}  ←  Client  {clientIp}",
                "#AAAAAA"));

            outer.Children.Add(BuildHandshakeTable(isRequestSide));

            string banner = isRequestSide
                ? "▶  TCP socket open — Modbus request ready to send"
                : "▶  TCP socket accepted — awaiting Modbus request";
            outer.Children.Add(MakeBanner(banner, "#FFD700", "#2A2000"));

            outer.Children.Add(MakeSectionHeading(
                "MODBUS  FRAME",
                isRequestSide ? "#00FFFF" : "#00FF00"));

            return outer;
        }

        private UIElement BuildHandshakeTable(bool isRequestSide)
        {
            string clientIp = TxtIpAddress.Text.Trim();
            string serverIp = TxtIpAddress.Text.Trim();

            var g = new Grid();
            int[] colWidths = { 50, 120, 80, 80, 80, 210 };
            foreach (int w in colWidths)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });

            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] headers = { "#", "Direction", "Flag", "SEQ", "ACK", "Description" };
            for (int c = 0; c < headers.Length; c++)
                AddCellToGrid(g, 0, c, headers[c], "#CCCCCC", isHeader: true, bg: "#1A1A2E");

            var rows = new (string step, string dir, string flag, string seq, string ack, string desc)[]
            {
                ("1",
                 isRequestSide ? $"{clientIp} →" : $"← {clientIp}",
                 "SYN",    "1000", "—",    "Client opens connection"),
                ("2",
                 isRequestSide ? $"← {serverIp}" : $"{serverIp} →",
                 "SYN-ACK","2000", "1001", "Server accepts, sends own SYN"),
                ("3",
                 isRequestSide ? $"{clientIp} →" : $"← {clientIp}",
                 "ACK",    "1001", "2001", "Client acknowledges — link UP"),
            };

            for (int r = 0; r < rows.Length; r++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                int row = r + 1;
                var (step, dir, flag, seq, ack, desc) = rows[r];

                string flagColor = flag switch
                {
                    "SYN" => "#FFD700",
                    "SYN-ACK" => "#FFA500",
                    "ACK" => "#00FF88",
                    _ => "#CCCCCC"
                };

                AddCellToGrid(g, row, 0, step, "#AAAAAA", false);
                AddCellToGrid(g, row, 1, dir, "#CCCCCC", false);
                AddCellToGrid(g, row, 2, flag, flagColor, false);
                AddCellToGrid(g, row, 3, seq, "#88CCFF", false);
                AddCellToGrid(g, row, 4, ack, "#88CCFF", false);
                AddCellToGrid(g, row, 5, desc, "#DDDDDD", false);
            }

            return new Border
            {
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#444444"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 6, 0, 8),
                ClipToBounds = true,
                Child = g
            };
        }

        // ── REQUEST TABLE ─────────────────────────────────────────────────────

        private UIElement BuildRequestTable(ModbusPacket pkt)
        {
            var outerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            var grid = MakeTableGrid();
            AddHeaderRow(grid, 0);
            int row = 1;

            AddMbapRows(grid, ref row, pkt);

            switch (pkt.Function)
            {
                case ModbusFunction.FC03_ReadHoldingRegisters:
                case ModbusFunction.FC04_ReadInputRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"{pkt.StartAddress >> 8}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"{pkt.StartAddress & 0xFF}", "00");
                    AddRow(grid, row++, "No. of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"{pkt.Quantity >> 8}", "00");
                    AddRow(grid, row++, "No. of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"{pkt.Quantity & 0xFF}", "02");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "12");
                    break;

                case ModbusFunction.FC06_WriteSingleRegister:
                    {
                        byte valHi = pkt.DataBytes?[0] ?? 0x00;
                        byte valLo = pkt.DataBytes?[1] ?? 0x00;
                        ushort val = (ushort)((valHi << 8) | valLo);
                        AddRow(grid, row++, "Register Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"{pkt.StartAddress >> 8}", "00");
                        AddRow(grid, row++, "Register Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"{pkt.StartAddress & 0xFF}", "01");
                        AddRow(grid, row++, $"Register Value Hi  [= {val} decimal]", $"{valHi:X2}", $"{valHi}", "00");
                        AddRow(grid, row++, "Register Value Lo", $"{valLo:X2}", $"{valLo}", "03");
                        AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "12");
                        break;
                    }

                case ModbusFunction.FC16_WriteMultipleRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"{pkt.StartAddress >> 8}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"{pkt.StartAddress & 0xFF}", "00");
                    AddRow(grid, row++, "Quantity of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"{pkt.Quantity >> 8}", "00");
                    AddRow(grid, row++, "Quantity of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"{pkt.Quantity & 0xFF}", "02");
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"{pkt.ByteCount}", "04");
                    if (pkt.DataBytes != null)
                    {
                        string[] expRegBytes = { "00", "0A", "01", "02" };
                        for (int i = 0; i + 1 < pkt.DataBytes.Length; i += 2)
                        {
                            int regNum = (i / 2) + 1;
                            byte hi = pkt.DataBytes[i];
                            byte lo = pkt.DataBytes[i + 1];
                            ushort val = (ushort)((hi << 8) | lo);
                            AddRow(grid, row++, $"Reg {regNum} Hi  [= {val} decimal]", $"{hi:X2}", $"{hi}", i < expRegBytes.Length ? expRegBytes[i] : "—");
                            AddRow(grid, row++, $"Reg {regNum} Lo", $"{lo:X2}", $"{lo}", i + 1 < expRegBytes.Length ? expRegBytes[i + 1] : "—");
                        }
                    }
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "17");
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

            AddMbapRows(grid, ref row, pkt);

            switch (pkt.Function)
            {
                case ModbusFunction.FC03_ReadHoldingRegisters:
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"{pkt.ByteCount}", "04");
                    if (pkt.DataBytes != null)
                    {
                        string[] expRegBytes = { "00", "06", "00", "05" };
                        for (int i = 0; i + 1 < pkt.DataBytes.Length; i += 2)
                        {
                            int regNum = (i / 2) + 1;
                            byte hi = pkt.DataBytes[i];
                            byte lo = pkt.DataBytes[i + 1];
                            ushort val = (ushort)((hi << 8) | lo);
                            AddRow(grid, row++, $"Reg {regNum} Hi  [= {val} decimal]", $"{hi:X2}", $"{hi}", i < expRegBytes.Length ? expRegBytes[i] : "—");
                            AddRow(grid, row++, $"Reg {regNum} Lo", $"{lo:X2}", $"{lo}", i + 1 < expRegBytes.Length ? expRegBytes[i + 1] : "—");
                        }
                    }
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "13");
                    break;

                case ModbusFunction.FC04_ReadInputRegisters:
                    AddRow(grid, row++, "Byte Count", $"{pkt.ByteCount:X2}", $"{pkt.ByteCount}", "04");
                    if (pkt.DataBytes != null)
                    {
                        string[] expRegBytes = { "00", "06", "00", "05" };
                        for (int i = 0; i + 1 < pkt.DataBytes.Length; i += 2)
                        {
                            int regNum = (i / 2) + 1;
                            byte hi = pkt.DataBytes[i];
                            byte lo = pkt.DataBytes[i + 1];
                            ushort val = (ushort)((hi << 8) | lo);
                            AddRow(grid, row++, $"Data Hi (Reg {regNum})  [= {val} decimal]", $"{hi:X2}", $"{hi}", i < expRegBytes.Length ? expRegBytes[i] : "—");
                            AddRow(grid, row++, $"Data Lo (Reg {regNum})", $"{lo:X2}", $"{lo}", i + 1 < expRegBytes.Length ? expRegBytes[i + 1] : "—");
                        }
                    }
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "13");
                    break;

                case ModbusFunction.FC06_WriteSingleRegister:
                    {
                        byte hi = pkt.DataBytes?[0] ?? 0x00;
                        byte lo = pkt.DataBytes?[1] ?? 0x00;
                        ushort val = (ushort)((hi << 8) | lo);
                        AddRow(grid, row++, "Register Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"{pkt.StartAddress >> 8}", "00");
                        AddRow(grid, row++, "Register Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"{pkt.StartAddress & 0xFF}", "01");
                        AddRow(grid, row++, $"Register Value Hi  [= {val} decimal]", $"{hi:X2}", $"{hi}", "00");
                        AddRow(grid, row++, "Register Value Lo", $"{lo:X2}", $"{lo}", "03");
                        AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "12");
                        break;
                    }

                case ModbusFunction.FC16_WriteMultipleRegisters:
                    AddRow(grid, row++, "Starting Address Hi", $"{(pkt.StartAddress >> 8):X2}", $"{pkt.StartAddress >> 8}", "00");
                    AddRow(grid, row++, "Starting Address Lo", $"{(pkt.StartAddress & 0xFF):X2}", $"{pkt.StartAddress & 0xFF}", "00");
                    AddRow(grid, row++, "Quantity of Registers Hi", $"{(pkt.Quantity >> 8):X2}", $"{pkt.Quantity >> 8}", "00");
                    AddRow(grid, row++, "Quantity of Registers Lo", $"{(pkt.Quantity & 0xFF):X2}", $"{pkt.Quantity & 0xFF}", "02");
                    AddRow(grid, row++, "Total Bytes", pkt.RawBytes.Length.ToString(), "—", "12");
                    break;
            }

            outerStack.Children.Add(WrapTable(grid));
            outerStack.Children.Add(RawHexBlock(pkt.RawBytes, "#00FF00"));
            return outerStack;
        }

        // ── Shared MBAP header rows ───────────────────────────────────────────

        private void AddMbapRows(Grid grid, ref int row, ModbusPacket pkt)
        {
            AddRow(grid, row++, "Transaction ID Hi", $"{(pkt.TransactionId >> 8):X2}", $"{pkt.TransactionId >> 8}", "—");
            AddRow(grid, row++, "Transaction ID Lo", $"{(pkt.TransactionId & 0xFF):X2}", $"{pkt.TransactionId & 0xFF}", "—");
            AddRow(grid, row++, "Protocol ID Hi", "00", "0", "00");
            AddRow(grid, row++, "Protocol ID Lo", "00", "0", "00");
            AddRow(grid, row++, "Length Hi", $"{(pkt.Length >> 8):X2}", $"{pkt.Length >> 8}", "00");
            AddRow(grid, row++, "Length Lo", $"{(pkt.Length & 0xFF):X2}", $"{pkt.Length & 0xFF}", "—");
            AddRow(grid, row++, "Unit ID", $"{pkt.UnitId:X2}", $"{pkt.UnitId}", "01");
            AddRow(grid, row++, "Function Code", $"{pkt.FunctionCode:X2}", $"{pkt.FunctionCode}", $"{pkt.FunctionCode:X2}");
        }

        // ── Small UI helpers ──────────────────────────────────────────────────

        private UIElement MakeSectionHeading(string text, string colorHex)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = (Brush)new BrushConverter().ConvertFromString(colorHex),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 6, 0, 4)
            };
        }

        private UIElement MakeInfoRow(string text, string colorHex)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = (Brush)new BrushConverter().ConvertFromString(colorHex),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private UIElement MakeBanner(string text, string fgHex, string bgHex)
        {
            return new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(bgHex),
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(fgHex),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 4, 0, 8),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = (Brush)new BrushConverter().ConvertFromString(fgHex),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        // ── Grid / cell helpers ───────────────────────────────────────────────

        private void AddCellToGrid(Grid g, int row, int col, string text,
                                   string fgHex, bool isHeader, string bg = "#111111")
        {
            var cell = new Border
            {
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#333333"),
                BorderThickness = new Thickness(0.5),
                Background = (Brush)new BrushConverter().ConvertFromString(isHeader ? "#1A1A2E" : bg),
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
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            g.Children.Add(cell);
        }

        private static readonly string[] ColHeaders = { "Field Name", "TCP (hex)", "Decoded", "Expected" };

        private Grid MakeTableGrid()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
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

        private void AddRow(Grid g, int rowIndex, string field, string tcp, string decoded, string expected)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] vals = { field, tcp, decoded, expected };
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
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
                if (i < bytes.Length - 1)
                    sb.Append(i == 5 ? "  " : " ");
            }
            return new TextBlock
            {
                Text = sb.ToString(),
                Foreground = (Brush)new BrushConverter().ConvertFromString(colorHex),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 8, 0, 6)
            };
        }
    }
}
