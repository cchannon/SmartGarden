using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace SmartGarden
{
    internal class Fan
    {
        private GpioController _gpio;
        private GpioPin _fanPin;
        private readonly int _fanControlPin;

        public Fan(int fanControlPin)
        {
            Debug.WriteLine("Instantiating the Fan Class");

            _fanControlPin = fanControlPin;
        }

        public void InitializeFan()
        {
            Debug.WriteLine("Intiializing the Fan");

            _gpio = GpioController.GetDefault();

            _fanPin = _gpio.OpenPin(_fanControlPin);
            _fanPin.SetDriveMode(GpioPinDriveMode.Output);

            _fanPin.Write(GpioPinValue.Low);
            Debug.WriteLine("The pin is set to Low");
        }

        public void SetFan(bool fanState)
        {
            if (fanState == true)
            {
                _fanPin.Write(GpioPinValue.High);
                Debug.WriteLine("The fan pin is set to High");
            }
            else
            {
                _fanPin.Write(GpioPinValue.Low);
                Debug.WriteLine("The fan pin is set to Low");
            }
        }
    }
}
