using System;
using System.Threading.Tasks;
using Edj20Tester.Models;

namespace Edj20Tester
{
    public class ModbusPacket
    {
        public byte[] RawBytes { get; set; }
        public byte SlaveAddress { get; set; }
        public byte FunctionCode { get; set; }
        public ushort StartAddress { get; set; }
        public ushort Quantity { get; set; }
        public byte[] DataBytes { get; set; }
        public ushort Crc { get; set; }
        public bool IsResponse { get; set; }
        public byte ByteCount { get; set; }
        public ModbusFunction Function { get; set; }
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

        private static ushort ComputeCrc(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc & 1) != 0
                        ? (ushort)((crc >> 1) ^ 0xA001)
                        : (ushort)(crc >> 1);
            }
            return crc;
        }

        private static byte[] AppendCrc(byte[] core)
        {
            ushort crc = ComputeCrc(core);
            byte crcLo = (byte)(crc & 0xFF);
            byte crcHi = (byte)(crc >> 8);
            var full = new byte[core.Length + 2];
            Array.Copy(core, full, core.Length);
            full[core.Length] = crcLo;
            full[core.Length + 1] = crcHi;
            return full;
        }

        public async Task<DeviceResponse> SendAsync(ModbusFunction function)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return function switch
                    {
                        ModbusFunction.FC01_ReadCoils or
                        ModbusFunction.FC02_ReadDiscreteInputs => BuildReadCoilResponse(function),

                        ModbusFunction.FC03_ReadHoldingRegisters or
                        ModbusFunction.FC04_ReadInputRegisters => BuildReadRegisterResponse(function),

                        ModbusFunction.FC05_WriteSingleCoil => BuildWriteSingleCoilResponse(),
                        ModbusFunction.FC06_WriteSingleRegister => BuildWriteSingleRegisterResponse(),
                        ModbusFunction.FC15_WriteMultipleCoils => BuildWriteMultipleCoilsResponse(),
                        ModbusFunction.FC16_WriteMultipleRegisters => BuildWriteMultipleRegistersResponse(),

                        _ => new DeviceResponse("ERROR")
                    };
                }
                catch (Exception ex)
                {
                    return new DeviceResponse($"ERROR: {ex.Message}");
                }
            });
        }

        //FC01/FC02–Read Coils/Discrete Inputs
        private DeviceResponse BuildReadCoilResponse(ModbusFunction function)
        {
            byte fc = (byte)function;
            byte addrHi = 0x00; byte addrLo = 0x00;
            byte qtyHi = 0x00; byte qtyLo = 0x02;

            byte[] reqCore = { SlaveId, fc, addrHi, addrLo, qtyHi, qtyLo };
            ushort reqCrc = ComputeCrc(reqCore);
            byte[] reqFull = AppendCrc(reqCore);

            var req = new ModbusPacket
            {
                RawBytes = reqFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = function,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                Quantity = (ushort)((qtyHi << 8) | qtyLo),
                Crc = reqCrc,
                IsResponse = false
            };

            // 2 coils packed into 1 byte: coil1=ON(1), coil2=OFF(0) → 0x01
            byte byteCount = 0x01;
            byte coilData = 0x01;
            byte[] resCore = { SlaveId, fc, byteCount, coilData };
            ushort resCrc = ComputeCrc(resCore);
            byte[] resFull = AppendCrc(resCore);

            var res = new ModbusPacket
            {
                RawBytes = resFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = function,
                ByteCount = byteCount,
                DataBytes = new byte[] { coilData },
                Crc = resCrc,
                IsResponse = true
            };

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC03 / FC04 – Read Holding / Input Registers ──────────────────────
        private DeviceResponse BuildReadRegisterResponse(ModbusFunction function)
        {
            byte fc = (byte)function;
            byte addrHi = 0x00; byte addrLo = 0x00;
            byte qtyHi = 0x00; byte qtyLo = 0x02;

            byte[] reqCore = { SlaveId, fc, addrHi, addrLo, qtyHi, qtyLo };
            ushort reqCrc = ComputeCrc(reqCore);
            byte[] reqFull = AppendCrc(reqCore);

            var req = new ModbusPacket
            {
                RawBytes = reqFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = function,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                Quantity = (ushort)((qtyHi << 8) | qtyLo),
                Crc = reqCrc,
                IsResponse = false
            };

            // Two 16-bit registers: Reg1 = 6, Reg2 = 5
            byte byteCount = 0x04;
            byte d1Hi = 0x00; byte d1Lo = 0x06;
            byte d2Hi = 0x00; byte d2Lo = 0x05;
            byte[] resCore = { SlaveId, fc, byteCount, d1Hi, d1Lo, d2Hi, d2Lo };
            ushort resCrc = ComputeCrc(resCore);
            byte[] resFull = AppendCrc(resCore);

            var res = new ModbusPacket
            {
                RawBytes = resFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = function,
                ByteCount = byteCount,
                DataBytes = new byte[] { d1Hi, d1Lo, d2Hi, d2Lo },
                Crc = resCrc,
                IsResponse = true
            };

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC05 – Write Single Coil ───────────────────────────────────────────
        // Request : [SlaveId][05][AddrHi][AddrLo][ValHi][ValLo][CRC-Lo][CRC-Hi]
        //           0xFF00 = ON, 0x0000 = OFF
        // Response: echo of the request
        private DeviceResponse BuildWriteSingleCoilResponse()
        {
            const byte fc = (byte)ModbusFunction.FC05_WriteSingleCoil;
            byte addrHi = 0x00; byte addrLo = 0x00;
            byte valHi = 0xFF; byte valLo = 0x00;   // ON

            byte[] reqCore = { SlaveId, fc, addrHi, addrLo, valHi, valLo };
            ushort reqCrc = ComputeCrc(reqCore);
            byte[] reqFull = AppendCrc(reqCore);

            var req = new ModbusPacket
            {
                RawBytes = reqFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = ModbusFunction.FC05_WriteSingleCoil,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                DataBytes = new byte[] { valHi, valLo },
                Crc = reqCrc,
                IsResponse = false
            };

            // Response is an echo of the request
            var res = new ModbusPacket
            {
                RawBytes = (byte[])reqFull.Clone(),
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = ModbusFunction.FC05_WriteSingleCoil,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                DataBytes = new byte[] { valHi, valLo },
                Crc = reqCrc,
                IsResponse = true
            };

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC06 – Write Single Register ──────────────────────────────────────
        // Request : [SlaveId][06][AddrHi][AddrLo][ValHi][ValLo][CRC-Lo][CRC-Hi]
        // Response: echo of the request
        private DeviceResponse BuildWriteSingleRegisterResponse()
        {
            const byte fc = (byte)ModbusFunction.FC06_WriteSingleRegister;
            byte addrHi = 0x00; byte addrLo = 0x01;   // register 0x0001
            byte valHi = 0x00; byte valLo = 0x03;   // value = 3

            byte[] reqCore = { SlaveId, fc, addrHi, addrLo, valHi, valLo };
            ushort reqCrc = ComputeCrc(reqCore);
            byte[] reqFull = AppendCrc(reqCore);

            var req = new ModbusPacket
            {
                RawBytes = reqFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = ModbusFunction.FC06_WriteSingleRegister,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                DataBytes = new byte[] { valHi, valLo },
                Crc = reqCrc,
                IsResponse = false
            };

            // Response is an echo of the request
            var res = new ModbusPacket
            {
                RawBytes = (byte[])reqFull.Clone(),
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = ModbusFunction.FC06_WriteSingleRegister,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                DataBytes = new byte[] { valHi, valLo },
                Crc = reqCrc,
                IsResponse = true
            };

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC15 – Write Multiple Coils ───────────────────────────────────────
        // Request : [SlaveId][0F][AddrHi][AddrLo][QtyHi][QtyLo][ByteCount][CoilData...][CRC-Lo][CRC-Hi]
        // Response: [SlaveId][0F][AddrHi][AddrLo][QtyHi][QtyLo][CRC-Lo][CRC-Hi]
        private DeviceResponse BuildWriteMultipleCoilsResponse()
        {
            const byte fc = (byte)ModbusFunction.FC15_WriteMultipleCoils;
            byte addrHi = 0x00; byte addrLo = 0x00;
            byte qtyHi = 0x00; byte qtyLo = 0x0A;   // 10 coils
            byte byteCount = 0x02;                        // ceil(10/8) = 2 bytes
            byte coilByte1 = 0x55;   // 0101 0101 → coils 0,2,4,6 ON
            byte coilByte2 = 0x01;   // 0000 0001 → coil 8 ON, coil 9 OFF

            byte[] reqCore = { SlaveId, fc, addrHi, addrLo, qtyHi, qtyLo, byteCount, coilByte1, coilByte2 };
            ushort reqCrc = ComputeCrc(reqCore);
            byte[] reqFull = AppendCrc(reqCore);

            var req = new ModbusPacket
            {
                RawBytes = reqFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = ModbusFunction.FC15_WriteMultipleCoils,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                Quantity = (ushort)((qtyHi << 8) | qtyLo),
                ByteCount = byteCount,
                DataBytes = new byte[] { coilByte1, coilByte2 },
                Crc = reqCrc,
                IsResponse = false
            };

            // Response: SlaveId + FC + StartAddr + Quantity + CRC
            byte[] resCore = { SlaveId, fc, addrHi, addrLo, qtyHi, qtyLo };
            ushort resCrc = ComputeCrc(resCore);
            byte[] resFull = AppendCrc(resCore);

            var res = new ModbusPacket
            {
                RawBytes = resFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = ModbusFunction.FC15_WriteMultipleCoils,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                Quantity = (ushort)((qtyHi << 8) | qtyLo),
                Crc = resCrc,
                IsResponse = true
            };

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        //FC16 – Write Multiple Registers
        private DeviceResponse BuildWriteMultipleRegistersResponse()
        {
            const byte fc = (byte)ModbusFunction.FC16_WriteMultipleRegisters;
            byte addrHi = 0x00; byte addrLo = 0x00;
            byte qtyHi = 0x00; byte qtyLo = 0x02;   // 2 registers
            byte byteCount = 0x04;                        // 2 registers × 2 bytes
            byte r1Hi = 0x00; byte r1Lo = 0x0A;   // Reg1 = 10
            byte r2Hi = 0x01; byte r2Lo = 0x02;   // Reg2 = 258

            byte[] reqCore = { SlaveId, fc, addrHi, addrLo, qtyHi, qtyLo, byteCount, r1Hi, r1Lo, r2Hi, r2Lo };
            ushort reqCrc = ComputeCrc(reqCore);
            byte[] reqFull = AppendCrc(reqCore);

            var req = new ModbusPacket
            {
                RawBytes = reqFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = ModbusFunction.FC16_WriteMultipleRegisters,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                Quantity = (ushort)((qtyHi << 8) | qtyLo),
                ByteCount = byteCount,
                DataBytes = new byte[] { r1Hi, r1Lo, r2Hi, r2Lo },
                Crc = reqCrc,
                IsResponse = false
            };

            // Response: SlaveId + FC + StartAddr + Quantity + CRC
            byte[] resCore = { SlaveId, fc, addrHi, addrLo, qtyHi, qtyLo };
            ushort resCrc = ComputeCrc(resCore);
            byte[] resFull = AppendCrc(resCore);

            var res = new ModbusPacket
            {
                RawBytes = resFull,
                SlaveAddress = SlaveId,
                FunctionCode = fc,
                Function = ModbusFunction.FC16_WriteMultipleRegisters,
                StartAddress = (ushort)((addrHi << 8) | addrLo),
                Quantity = (ushort)((qtyHi << 8) | qtyLo),
                Crc = resCrc,
                IsResponse = true
            };

            return new DeviceResponse("OK") { Request = req, Response = res };
        }
    }
}
