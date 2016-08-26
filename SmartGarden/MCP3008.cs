using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;

namespace SmartGarden
{
    internal class Mcp3008
    {
        // Constants for the SPI controller chip interface
        public SpiDevice McpDevice3008;
        private const int SpiChipSelectLine = 0;  // SPI0 CS0 pin 24

        // ADC chip operation constants
        private const byte Mcp3008SingleEnded = 0x08;
        //private const byte Mcp3008Differential = 0x00;

        public const uint Min = 0;
        public const uint Max = 1023;

        public Mcp3008()
        {
            Debug.WriteLine("MCP3008::New MCP3008");
        }

        /// <summary>
        /// This method is used to configure the Pi2 to communicate over the SPI bus to the MCP3008 ADC chip.
        /// </summary>
       public async Task Initialize()
        {
            Debug.WriteLine("MCP3008::Initialize");
            try
            {
                // Setup the SPI bus configuration
                var settings = new SpiConnectionSettings(SpiChipSelectLine)
                {
                    ClockFrequency = 3600000, // 3.6MHz is the rated speed of the MCP3008 at 5v
                    Mode = SpiMode.Mode0
                };

                // Ask Windows for the list of SpiDevices

                // Get a selector string that will return all SPI controllers on the system 
                string aqs = SpiDevice.GetDeviceSelector();

                // Find the SPI bus controller devices with our selector string           
                DeviceInformationCollection dis = await DeviceInformation.FindAllAsync(aqs);

                // Create an SpiDevice with our bus controller and SPI settings           
                McpDevice3008 = await SpiDevice.FromIdAsync(dis[0].Id, settings);

                if (McpDevice3008 == null)
                {
                    Debug.WriteLine(
                        "SPI Controller {0} is currently in use by another application. Please ensure that no other applications are using SPI.",
                        dis[0].Id);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message + "\n" + e.StackTrace);
                throw;
            }
        }

        /// <summary> 
        /// This method does the actual work of communicating over the SPI bus with the chip.
        /// To line everything up for ease of reading back (on byte boundary) we 
        /// will pad the command start bit with 7 leading "0" bits
        ///
        /// Write 0000 000S GDDD xxxx xxxx xxxx
        /// Read  ???? ???? ???? ?N98 7654 3210
        /// S = start bit
        /// G = Single / Differential
        /// D = Chanel data 
        /// ? = undefined, ignore
        /// N = 0 "Null bit"
        /// 9-0 = 10 data bits
        /// </summary>
        public int ReadAdc(byte whichChannel)
        {
            var sample = 0;
            byte command = whichChannel;
            command |= Mcp3008SingleEnded;
            command <<= 4;

            byte[] commandBuf = new byte[] { 0x01, command, 0x00 };

            byte[] readBuf = new byte[] { 0x00, 0x00, 0x00 };

            if (McpDevice3008 == null) return sample;
            McpDevice3008.TransferFullDuplex(commandBuf, readBuf);

            sample = readBuf[2] + ((readBuf[1] & 0x03) << 8);
            int s2 = sample & 0x3FF;
            Debug.Assert(sample == s2);
            return sample;
        }
    }
}
