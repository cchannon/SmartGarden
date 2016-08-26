using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace SmartGarden
{
    internal class Solenoid
    {
        private GpioController _gpio;
        private GpioPin _solenoidPin;
        private readonly int _solenoidControlPin;

        private const int WaterflowDuration = 60000;

        public Solenoid(int solenoidControlPin)
        {
            Debug.WriteLine("Instantiating the Solenoid Class");

            _solenoidControlPin = solenoidControlPin;
        }

        public void InitializeSolenoid()
        {
            Debug.WriteLine("Intiializing the Solenoid");

            _gpio = GpioController.GetDefault();

            _solenoidPin = _gpio.OpenPin(_solenoidControlPin);
            _solenoidPin.SetDriveMode(GpioPinDriveMode.Output);

            _solenoidPin.Write(GpioPinValue.Low);
            Debug.WriteLine("The pin is set to Low");
        }

        public async void WaterTheGarden()
        {
            _solenoidPin.Write(GpioPinValue.High);
            Debug.WriteLine("The pin is set to High");

            await Task.Delay(WaterflowDuration);
            _solenoidPin.Write(GpioPinValue.Low);
        }
    }
}
