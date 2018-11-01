using System.Collections.Generic;
using System.ComponentModel;

namespace NolekMoxa.Model
{
    /// <inheritdoc />
    /// <summary>
    /// The Connected device (Module)
    /// </summary>
    public class ConnectedDevice : INotifyPropertyChanged
    {
        /// <summary>
        /// The name of the device. This is not set by default on the device
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The module Type eg. E1242
        /// </summary>
        public string Module { get; set; }
        /// <summary>
        /// the IP-address for the device
        /// </summary>
        public string Ip { get; set; }
        /// <summary>
        /// The password for the device.
        /// </summary>
        public string Password { get; set; } = "";
        /// <summary>
        /// The Mac-address in byte array
        /// </summary>
        public byte[] Mac { get; set; }
        /// <summary>
        /// A List of all the channels on the device
        /// </summary>
        public List<Channel> Channels { get; set; }
        /// <summary>
        /// The firware version read from the device
        /// </summary>
        public string FirmwareVersion { get; set; }

        /// <summary>
        /// Constructor. Initialises channel list
        /// </summary>
        public ConnectedDevice()
        {
            Channels = new List<Channel>();
        }

        #region INotify
        /// <inheritdoc />
        /// <summary>
        /// PropertyChanged
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Notify property changed
        /// </summary>
        /// <param name="prop">property name</param>
        public void NotifyPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
        #endregion
    }
}