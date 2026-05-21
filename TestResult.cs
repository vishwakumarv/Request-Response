namespace Edj20Tester.Models
{
    public enum ModbusFunction
    {
        //Read
        FC01_ReadCoils = 1,
        FC02_ReadDiscreteInputs = 2,
        FC03_ReadHoldingRegisters = 3,
        FC04_ReadInputRegisters = 4,

        //Write
        FC05_WriteSingleCoil = 5,
        FC06_WriteSingleRegister = 6,
        FC15_WriteMultipleCoils = 15,
        FC16_WriteMultipleRegisters = 16,
    }

    public class TestResult
    {
        public string TestPoint { get; set; }
        public string TestType { get; set; }
        public double MeasuredValue { get; set; }
        public string Unit { get; set; }
        public string ExpectedRange { get; set; }
        public string Result { get; set; }
    }
}
