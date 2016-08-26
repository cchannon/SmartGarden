using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SmartGarden
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        private const byte Sensor1Channel = 0;
        private const byte Sensor2Channel = 1;
        private const byte Sensor3Channel = 2;
        private const byte Sensor4Channel = 3;

        private const int SolenoidGPIO = 25;
        private readonly Solenoid _waterControl = new Solenoid(SolenoidGPIO);

        private const int FanPin = 24;
        private readonly Fan _fanControl = new Fan(FanPin);

        private readonly Mcp3008 _adc = new Mcp3008();
        public Timer Timer;

        private Bmp280 _bmp280;

        public MainPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes BMP280, ADC, Solenoid, Fan, and launches Processor
        /// </summary>
        /// <param name="navArgs"></param>
        protected override async void OnNavigatedTo(NavigationEventArgs navArgs)
        {
            Debug.WriteLine("Process Initialized");

            _bmp280 = new Bmp280();
            await _bmp280.Initialize();

            await _adc.Initialize();

            _fanControl.InitializeFan();

            _waterControl.InitializeSolenoid();

            Timer = new Timer(GardenProcessor, this, 0, 6000000);
        }

        /// <summary>
        /// Encapsulates the primary operation of the garden; invokes methods and classes to collect and save measurements, 
        /// compares against baselines, and makes the watering determination.
        /// </summary>
        /// <param name="state"></param>
        private async void GardenProcessor(object state)
        {
            var baseline = GetBaseline();

            var thisMeasure = await GetMeasures(0);
            
            await RecordMeasurements(thisMeasure);

            float sensorTotal = 0;
            var workingSensors = 0;

            foreach (float meas in baseline)
            {
                sensorTotal += meas;
                if (meas > 0) workingSensors += workingSensors;
            }

            float baselineAvg = sensorTotal / workingSensors;

            sensorTotal = 0;
            workingSensors = 0;
            float temp = 0;
            var currentMoisture = new float[] {};

            if (thisMeasure != null)
            {
                var mArray = thisMeasure.Split(new[] { "," }, StringSplitOptions.None);
                currentMoisture = new[]
                {
                    float.Parse(mArray[0]),
                    float.Parse(mArray[1]),
                    float.Parse(mArray[2]),
                    float.Parse(mArray[3])
                };
                temp = float.Parse(mArray[4]);
            }

            _fanControl.SetFan(temp > 32);

            foreach (float meas in currentMoisture)
            {
                sensorTotal += meas;
                if (meas > 0) workingSensors += workingSensors;
            }

            if (workingSensors == 0)
            {
                throw(new Exception("Sensors disconnected. Execution aborted."));
            }

            float currentAvg = sensorTotal / workingSensors;

            if (currentAvg/baselineAvg <= .1)
            {
                _waterControl?.WaterTheGarden();
                await Task.Delay(120000);
                await RecordMeasurements(await GetMeasures(1));
            }

            Debug.WriteLine("Sleeping for 60 seconds");
        }

        /// <summary>
        /// Method reads from local text to identify latest measurement with isbaseline == 1
        /// Returns only the moisture readouts from that baseline
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<float> GetBaseline()
        {
            try
            {
                var measurements = Convert.ToString(ReadStringFromLocalFile("Measurements.txt"));
                var lines = measurements.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                float[] baseline;
                string comparison = null;

                const string pattern = "[1]$";
                var isBaseline = new Regex(pattern);

                foreach (var line in lines)
                {
                    if (isBaseline.IsMatch(line)) comparison = line;
                }
                if (comparison != null)
                {
                    var bArray = comparison.Split(new[] {","}, StringSplitOptions.None);
                    baseline = new[]
                    {
                        float.Parse(bArray[0]),
                        float.Parse(bArray[1]),
                        float.Parse(bArray[2]),
                        float.Parse(bArray[3])
                    };
                }
                else baseline = new float[] {700, 700, 700, 700};
                return baseline;
            }
            catch (Exception)
            {
                Debug.WriteLine("Txt file not found. Using default values for baseline.");
                return new float[] {700, 700, 700, 700};
            }
        }

        /// <summary>
        /// Reads from ADC and BMP280 to return a single string with 4 moisture measurements, temp, pressure, datetime, and isBaseline
        /// </summary>
        /// <param name="isBaseline"></param>
        /// <returns></returns>
        public async Task<string> GetMeasures(int isBaseline)
        {
            Debug.WriteLine("Preparing Measurements");

            float temp = await _bmp280.ReadTemperature();

            float pressure = await _bmp280.ReadPreasure();

            Debug.WriteLine($"Current Temp: {temp}\nCurrent Pressure: {pressure}");

            var sensor1 = Convert.ToString(_adc.ReadAdc(Sensor1Channel));
            var sensor2 = Convert.ToString(_adc.ReadAdc(Sensor2Channel));
            var sensor3 = Convert.ToString(_adc.ReadAdc(Sensor3Channel));
            var sensor4 = Convert.ToString(_adc.ReadAdc(Sensor4Channel));

            Debug.WriteLine($"Moisture sensors currently reading: \n{sensor1}, {sensor2}, {sensor3}, {sensor4}");

            //four sensors + temp + pressure + datetime + bool value for isBaseline (always false when collecting outside watering)
            var thisMeasure = sensor1 + ","
                               + sensor2 + ","
                               + sensor3 + ","
                               + sensor4 + ","
                               + temp + ","
                               + pressure + ","
                               + DateTime.Now + ","
                               + isBaseline;
            return thisMeasure;
        }

        /// <summary>
        /// Appends a single string to the end of the measurements.txt file. If file is not found, creates the file.
        /// </summary>
        /// <param name="thisMeasure"></param>
        /// <returns></returns>
        public async Task RecordMeasurements(string thisMeasure)
        {
            try
            {
                var oldMeasurements = Convert.ToString(await ReadStringFromLocalFile("Measurements.txt"));
                oldMeasurements += "\n" + thisMeasure;
                await SaveStringToLocalFile("Measurements.txt", oldMeasurements);
            }
            catch
            {
                Debug.WriteLine("File Measurements.txt not found. Creating file without history.");
                await SaveStringToLocalFile("Measurements.txt", thisMeasure);
            }
        }

        /// <summary>
        /// Saves string object to a TXT file in the default application folder location
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        private static async Task SaveStringToLocalFile(string filename, string content)
        {
            // saves the string 'content' to a file 'filename' in the app's local storage folder
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(content.ToCharArray());

            // create a file with the given filename in the local folder; replace any existing file with the same name
            StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);

            // write the char array created from the content string into the file
            using (Stream stream = await file.OpenStreamForWriteAsync())
            {
                stream.Write(fileBytes, 0, fileBytes.Length);
            }
        }

        /// <summary>
        /// Reads a TXT file in the default application folder location in to memory as a single string
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static async Task<string> ReadStringFromLocalFile(string filename)
        {
            // reads the contents of file 'filename' in the app's local storage folder and returns it as a string

            // access the local folder
            StorageFolder local = ApplicationData.Current.LocalFolder;
            // open the file 'filename' for reading
            Stream stream = await local.OpenStreamForReadAsync(filename);
            string text;

            // copy the file contents into the string 'text'
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            return text;
        }
    }
}
