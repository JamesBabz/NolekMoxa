using System.ComponentModel;

namespace NolekMoxa.Model
{
    /// <inheritdoc />
    /// <summary>
    /// The settings of the channel. A custom dictionary containing the setting and the value of that setting
    /// </summary>
    public class ChannelSetting : INotifyPropertyChanged
    {
        /// <summary>
        /// The name of the setting
        /// </summary>
        public string Key { get; set; }


        private string _value { get; set; }

        /// <summary>
        /// The value of the setting.
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                NotifyPropertyChanged("Value");
            }
        }

        /// <summary>
        /// Constructor for the channelsetting with key and value parameters
        /// </summary>
        /// <param name="k">The identifier (name) of the channel setting</param>
        /// <param name="v">The value for the channel setting</param>
        public ChannelSetting(string k, string v)
        {
            Key = k;
            Value = v;
        }

        /// <summary>
        /// Empty constructor for xml conversion
        /// </summary>
        public ChannelSetting(){ }

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
