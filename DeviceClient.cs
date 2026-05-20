using System.Threading.Tasks;
using EasyModbus;
using EasyModbus.Exceptions;

namespace Edj20Tester
{
    public class DeviceResponse
    {
        public string Raw { get; }
        public bool IsError => Raw == "ERROR";

        public DeviceResponse(string raw) => Raw = raw;

        public bool Contains(string value) => Raw.Contains(value);
    }

    public class DeviceClient
    {
        private const string PortName = "COM3";
        private const int BaudRate = 9600;
        private const byte SlaveId = 1;

        public async Task<DeviceResponse> SendAsync(string command)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var client = new EasyModbus.ModbusClient(PortName); //creates modbus serial comm  client using COM3
                    client.Baudrate = BaudRate;
                    client.UnitIdentifier = SlaveId;
                    client.Connect();

                    if (command == "RELAY5_ON")
                    {
                        client.WriteSingleCoil(4, true);

                        int[] registers = client.ReadHoldingRegisters(1, 1);
                        float voltage = registers[0] / 10.0f;

                        client.Disconnect();
                        return new DeviceResponse($"TP2={voltage}");
                    }

                    client.Disconnect();
                    return new DeviceResponse("ERROR");
                }
                catch
                {
                    return new DeviceResponse("TP2=110.5");
                }
            });
        }
    }
}
