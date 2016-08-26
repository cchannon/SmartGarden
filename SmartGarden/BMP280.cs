using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace SmartGarden
{
    public class Bmp280CalibrationData
    {
        //BMP280 Registers
        public ushort DigT1 { get; set; }
        public short DigT2 { get; set; }
        public short DigT3 { get; set; }

        public ushort DigP1 { get; set; }
        public short DigP2 { get; set; }
        public short DigP3 { get; set; }
        public short DigP4 { get; set; }
        public short DigP5 { get; set; }
        public short DigP6 { get; set; }
        public short DigP7 { get; set; }
        public short DigP8 { get; set; }
        public short DigP9 { get; set; }
    }

    public class Bmp280
    {
        //The BMP280 register addresses according the the datasheet: http://www.adafruit.com/datasheets/BST-BMP280-DS001-11.pdf
        private const byte Bmp280Address = 0x77;
        private const byte Bmp280Signature = 0x58;

        private enum ERegisters : byte
        {
            Bmp280RegisterDigT1 = 0x88,
            Bmp280RegisterDigT2 = 0x8A,
            Bmp280RegisterDigT3 = 0x8C,

            Bmp280RegisterDigP1 = 0x8E,
            Bmp280RegisterDigP2 = 0x90,
            Bmp280RegisterDigP3 = 0x92,
            Bmp280RegisterDigP4 = 0x94,
            Bmp280RegisterDigP5 = 0x96,
            Bmp280RegisterDigP6 = 0x98,
            Bmp280RegisterDigP7 = 0x9A,
            Bmp280RegisterDigP8 = 0x9C,
            Bmp280RegisterDigP9 = 0x9E,

            Bmp280RegisterChipid = 0xD0,
            //Bmp280RegisterVersion = 0xD1,
            //Bmp280RegisterSoftreset = 0xE0,

            //Bmp280RegisterCal26 = 0xE1,  // R calibration stored in 0xE1-0xF0

            Bmp280RegisterControlhumid = 0xF2,
            Bmp280RegisterControl = 0xF4,
            //Bmp280RegisterConfig = 0xF5,

            Bmp280RegisterPressuredataMsb = 0xF7,
            Bmp280RegisterPressuredataLsb = 0xF8,
            Bmp280RegisterPressuredataXlsb = 0xF9, // bits <7:4>

            Bmp280RegisterTempdataMsb = 0xFA,
            Bmp280RegisterTempdataLsb = 0xFB,
            Bmp280RegisterTempdataXlsb = 0xFC, // bits <7:4>

            //Bmp280RegisterHumiddataMsb = 0xFD,
            //Bmp280RegisterHumiddataLsb = 0xFE,
        };

        //String for the friendly name of the I2C bus 
        private const string I2CControllerName = "I2C1";
        //Create an I2C device
        private I2cDevice _bmp280;
        //Create new calibration data for the sensor
        private Bmp280CalibrationData _calibrationData;
        //Variable to check if device is initialized
        private bool _init;

        //Method to initialize the BMP280 sensor
        public async Task Initialize()
        {
            Debug.WriteLine("BMP280::Initialize");

            try
            {
                //Instantiate the I2CConnectionSettings using the device address of the BMP280
                var settings = new I2cConnectionSettings(Bmp280Address)
                {
                    BusSpeed = I2cBusSpeed.FastMode
                };
                //Set the I2C bus speed of connection to fast mode
                //Use the I2CBus device selector to create an advanced query syntax string
                string aqs = I2cDevice.GetDeviceSelector(I2CControllerName);
                //Use the Windows.Devices.Enumeration.DeviceInformation class to create a collection using the advanced query syntax string
                DeviceInformationCollection dis = await DeviceInformation.FindAllAsync(aqs);
                //Instantiate the the BMP280 I2C device using the device id of the I2CBus and the I2CConnectionSettings
                _bmp280 = await I2cDevice.FromIdAsync(dis[0].Id, settings);
                //Check if device was found
                if (_bmp280 == null)
                {
                    Debug.WriteLine("Device not found");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message + "\n" + e.StackTrace);
                throw;
            }

        }
        private async Task Begin()
        {
            Debug.WriteLine("BMP280::Begin");
            byte[] writeBuffer = { (byte)ERegisters.Bmp280RegisterChipid };
            byte[] readBuffer = { 0xFF };

            //Read the device signature
            _bmp280.WriteRead(writeBuffer, readBuffer);
            Debug.WriteLine("BMP280 Signature: " + readBuffer[0].ToString());

            //Verify the device signature
            if (readBuffer[0] != Bmp280Signature)
            {
                Debug.WriteLine("BMP280::Begin Signature Mismatch.");
                return;
            }

            //Set the initalize variable to true
            _init = true;

            //Read the coefficients table
            _calibrationData = await ReadCoefficeints();

            //Write control register
            await WriteControlRegister();

            //Write humidity control register
            await WriteControlRegisterHumidity();
        }

        //Method to write 0x03 to the humidity control register
        private async Task WriteControlRegisterHumidity()
        {
            byte[] writeBuffer = { (byte)ERegisters.Bmp280RegisterControlhumid, 0x03 };
            _bmp280.Write(writeBuffer);
            await Task.Delay(1);
        }

        //Method to write 0x3F to the control register
        private async Task WriteControlRegister()
        {
            byte[] writeBuffer = { (byte)ERegisters.Bmp280RegisterControl, 0x3F };
            _bmp280.Write(writeBuffer);
            await Task.Delay(1);
        }

        //Method to read a 16-bit value from a register and return it in little endian format
        private ushort ReadUInt16_LittleEndian(byte register)
        {
            ushort value;
            byte[] writeBuffer = { 0x00 };
            byte[] readBuffer = { 0x00, 0x00 };

            writeBuffer[0] = register;

            _bmp280.WriteRead(writeBuffer, readBuffer);
            int h = readBuffer[1] << 8;
            int l = readBuffer[0];
            value = (ushort)(h + l);
            return value;
        }

        //Method to read an 8-bit value from a register
        private byte ReadByte(byte register)
        {
            byte value;
            byte[] writeBuffer = { 0x00 };
            byte[] readBuffer = { 0x00 };

            writeBuffer[0] = register;

            _bmp280.WriteRead(writeBuffer, readBuffer);
            value = readBuffer[0];
            return value;
        }

        //Method to read the caliberation data from the registers
        private async Task<Bmp280CalibrationData> ReadCoefficeints()
        {
            // 16 bit calibration data is stored as Little Endian, the helper method will do the byte swap.
            _calibrationData = new Bmp280CalibrationData
            {
                DigT1 = ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigT1),
                DigT2 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigT2),
                DigT3 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigT3),
                DigP1 = ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP1),
                DigP2 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP2),
                DigP3 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP3),
                DigP4 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP4),
                DigP5 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP5),
                DigP6 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP6),
                DigP7 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP7),
                DigP8 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP8),
                DigP9 = (short) ReadUInt16_LittleEndian((byte) ERegisters.Bmp280RegisterDigP9)
            };

            // Read temperature calibration data

            // Read presure calibration data

            await Task.Delay(1);
            return _calibrationData;
        }


        //t_fine carries fine temperature as global value
        private int _tFine = int.MinValue;
        //Method to return the temperature in DegC. Resolution is 0.01 DegC. Output value of “5123” equals 51.23 DegC.
        private double BMP280_compensate_T_double(int adcT)
        {
            double var1, var2, T;

            //The temperature is calculated using the compensation formula in the BMP280 datasheet
            var1 = ((adcT / 16384.0) - (_calibrationData.DigT1 / 1024.0)) * _calibrationData.DigT2;
            var2 = ((adcT / 131072.0) - (_calibrationData.DigT1 / 8192.0)) * _calibrationData.DigT3;

            _tFine = (int)(var1 + var2);

            T = (var1 + var2) / 5120.0;
            return T;
        }


        //Method to returns the pressure in Pa, in Q24.8 format (24 integer bits and 8 fractional bits).
        //Output value of “24674867” represents 24674867/256 = 96386.2 Pa = 963.862 hPa
        private long BMP280_compensate_P_Int64(int adcP)
        {
            long var1, var2, p;

            //The pressure is calculated using the compensation formula in the BMP280 datasheet
            var1 = _tFine - 128000;
            var2 = var1 * var1 * _calibrationData.DigP6;
            var2 = var2 + ((var1 * _calibrationData.DigP5) << 17);
            var2 = var2 + ((long)_calibrationData.DigP4 << 35);
            var1 = ((var1 * var1 * _calibrationData.DigP3) >> 8) + ((var1 * _calibrationData.DigP2) << 12);
            var1 = (((((long)1 << 47) + var1)) * _calibrationData.DigP1) >> 33;
            if (var1 == 0)
            {
                Debug.WriteLine("BMP280_compensate_P_Int64 Jump out to avoid / 0");
                return 0; //Avoid exception caused by division by zero
            }
            //Perform calibration operations as per datasheet: http://www.adafruit.com/datasheets/BST-BMP280-DS001-11.pdf
            p = 1048576 - adcP;
            p = (((p << 31) - var2) * 3125) / var1;
            var1 = (_calibrationData.DigP9 * (p >> 13) * (p >> 13)) >> 25;
            var2 = (_calibrationData.DigP8 * p) >> 19;
            p = ((p + var1 + var2) >> 8) + ((long)_calibrationData.DigP7 << 4);
            return p;
        }


        public async Task<float> ReadTemperature()
        {
            //Make sure the I2C device is initialized
            if (!_init) await Begin();

            //Read the MSB, LSB and bits 7:4 (XLSB) of the temperature from the BMP280 registers
            byte tmsb = ReadByte((byte)ERegisters.Bmp280RegisterTempdataMsb);
            byte tlsb = ReadByte((byte)ERegisters.Bmp280RegisterTempdataLsb);
            byte txlsb = ReadByte((byte)ERegisters.Bmp280RegisterTempdataXlsb); // bits 7:4

            //Combine the values into a 32-bit integer
            int t = (tmsb << 12) + (tlsb << 4) + (txlsb >> 4);

            //Convert the raw value to the temperature in degC
            double temp = BMP280_compensate_T_double(t);

            //Return the temperature as a float value
            return (float)temp;
        }

        public async Task<float> ReadPreasure()
        {
            //Make sure the I2C device is initialized
            if (!_init) await Begin();

            //Read the temperature first to load the t_fine value for compensation
            if (_tFine == int.MinValue)
            {
                await ReadTemperature();
            }

            //Read the MSB, LSB and bits 7:4 (XLSB) of the pressure from the BMP280 registers
            byte tmsb = ReadByte((byte)ERegisters.Bmp280RegisterPressuredataMsb);
            byte tlsb = ReadByte((byte)ERegisters.Bmp280RegisterPressuredataLsb);
            byte txlsb = ReadByte((byte)ERegisters.Bmp280RegisterPressuredataXlsb); // bits 7:4

            //Combine the values into a 32-bit integer
            int t = (tmsb << 12) + (tlsb << 4) + (txlsb >> 4);

            //Convert the raw value to the pressure in Pa
            long pres = BMP280_compensate_P_Int64(t);

            //Return the temperature as a float value
            return ((float)pres) / 256;
        }

        //Method to take the sea level pressure in Hectopascals(hPa) as a parameter and calculate the altitude using current pressure.
        public async Task<float> ReadAltitude(float seaLevel)
        {
            //Make sure the I2C device is initialized
            if (!_init) await Begin();

            //Read the pressure first
            float pressure = await ReadPreasure();
            //Convert the pressure to Hectopascals(hPa)
            pressure /= 100;

            //Calculate and return the altitude using the international barometric formula
            return 44330.0f * (1.0f - (float)Math.Pow((pressure / seaLevel), 0.1903f));
        }
    }
}
