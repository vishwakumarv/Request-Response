namespace Edj20Tester.Models
{
    public enum ModbusFunction
    {
        FC01_ReadCoils = 1,
        FC02_ReadDiscreteInputs = 2,
        FC03_ReadHoldingRegisters = 3,
        FC04_ReadInputRegisters = 4,
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
