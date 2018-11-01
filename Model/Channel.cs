using System.Collections.Generic;
using System.Linq;

namespace NolekMoxa.Model
{
    /// <summary>
    /// A business entity to contain the different IO channels on the device and their type
    /// </summary>
    public class Channel
    {
        /// <summary>
        /// The type of the channel e.g DI, DO, Relay
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Is this an input(true) or an output(false) channel. Null if type isnt declared
        /// </summary>
        public bool? IsInput { get; set; }

        /// <summary>
        /// The tag connected in the frontend to this channel
        /// </summary>
        public string ConnectedTag { get; set; }

        /// <summary>
        /// A list of all the possible settings for the channel e.g status: on or off, DIO in or out
        /// </summary>
        public List<ChannelSetting> ChannelSettings { get; set; }

        /// <summary>
        /// One of the channels on a given device
        /// </summary>
        /// <param name="type">The type of channel (di/do etc.)</param>
        public Channel(string type)
        {
            SetIsInput(type);
            Type = type;
            ChannelSettings = new List<ChannelSetting>();
        }

        private void SetIsInput(string type)
        {
            var inputTypes = new[] { "ai", "relay", "di" };
            var outputTypes = new[] { "do", "ao" };
            if (inputTypes.Any(type.Equals))
            {
                IsInput = true;
            }
            else if (outputTypes.Any(type.Equals))
            {
                IsInput = false;
            }
            else
                IsInput = null;
        }

        /// <summary>
        /// Empty constructor for xml conversion
        /// </summary>
        public Channel() { }
    }
}
