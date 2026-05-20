using System;
using System.Threading.Tasks;

namespace Edj20Tester
{
    public class ModbusPacket
    {
        public byte[] RawBytes { get; set; }

        // Parsed fields
        public byte SlaveAddress { get; set; }
        public byte FunctionCode { get; set; }
        public ushort StartAddress { get; set; }
        public ushort Quantity { get; set; }
        public byte[] DataBytes { get; set; }
        public ushort Crc { get; set; }

        public bool IsResponse { get; set; }
        public byte ByteCount { get; set; }    // only in response
    }

    public class DeviceResponse
    {
        public string Raw { get; }
        public ModbusPacket Request { get; set; }
        public ModbusPacket Response { get; set; }
        public bool IsError => Raw == "ERROR";

        public DeviceResponse(string raw) => Raw = raw;
    }

    public class DeviceClient
    {
        private const byte SlaveId = 0x01;

        // CRC-16/MODBUS
        private static ushort ComputeCrc(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
            }
            return crc;
        }

        public async Task<DeviceResponse> SendAsync(string command)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (command == "READ_INPUT_REGISTERS")
                    {
                        // FC 04 — Read Input Registers, starting at 30001 (addr 0x0000), qty 2
                        byte fc = 0x04;
                        byte addrHi = 0x00;
                        byte addrLo = 0x00;
                        byte qtyHi = 0x00;
                        byte qtyLo = 0x02;

                        //CRC Calculation for request
                        byte[] reqCore = { SlaveId, fc, addrHi, addrLo, qtyHi, qtyLo };
                        ushort reqCrc = ComputeCrc(reqCore);
                        byte crcLo = (byte)(reqCrc & 0xFF);
                        byte crcHi = (byte)(reqCrc >> 8);
                        byte[] reqFull = { SlaveId, fc, addrHi, addrLo, qtyHi, qtyLo, crcLo, crcHi };

                        // Simulated response: register 1 = 0x0006, register 2 = 0x0005
                        byte rfc = 0x04;
                        byte byteCount = 0x04;
                        byte d1Hi = 0x00; byte d1Lo = 0x06;
                        byte d2Hi = 0x00; byte d2Lo = 0x05;

                        byte[] resCore = { SlaveId, rfc, byteCount, d1Hi, d1Lo, d2Hi, d2Lo };
                        ushort resCrc = ComputeCrc(resCore);
                        byte rCrcLo = (byte)(resCrc & 0xFF);
                        byte rCrcHi = (byte)(resCrc >> 8);
                        byte[] resFull = { SlaveId, rfc, byteCount, d1Hi, d1Lo, d2Hi, d2Lo, rCrcLo, rCrcHi };

                        var req = new ModbusPacket
                        {
                            RawBytes = reqFull,
                            SlaveAddress = SlaveId,
                            FunctionCode = fc,
                            StartAddress = (ushort)((addrHi << 8) | addrLo),
                            Quantity = (ushort)((qtyHi << 8) | qtyLo),
                            Crc = reqCrc,
                            IsResponse = false
                        };

                        var res = new ModbusPacket
                        {
                            RawBytes = resFull,
                            SlaveAddress = SlaveId,
                            FunctionCode = rfc,
                            ByteCount = byteCount,
                            DataBytes = new byte[] { d1Hi, d1Lo, d2Hi, d2Lo },
                            Crc = resCrc,
                            IsResponse = true
                        };

                        return new DeviceResponse("TP2=6,TP3=5")
                        {
                            Request = req,
                            Response = res
                        };
                    }

                    return new DeviceResponse("ERROR");
                }
                catch (Exception ex)
                {
                    return new DeviceResponse($"ERROR: {ex.Message}");
                }
            });
        }
    }
}
