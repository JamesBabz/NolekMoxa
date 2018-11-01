using System;
using System.Collections.Generic;
using System.Linq;
using NolekMoxa.Model;

namespace NolekMoxa
{

    /// <summary>
    /// Class made for comparing objects with eachother e.g. Channel settings on devices
    /// </summary>
    public class Comparer
    {
        private const string DEVICE_AMOUNT_ERROR = "Amount of connected devices ({0}) does not match the expected amount of connected devices ({1}).";
        private const string WRONG_MAC_ERROR = "{0} of the device(s) is not the same as previously configured.";
        //private const string DEVICE_NAME_ERROR = "The name of the device ({0}) does not match the expected name ({1})";
        //private const string DEVICE_IP_ERROR = "The ip of the device ({0}) does not match the expected ip ({1})";
        //private const string CHANNEL_ERROR_HEADER = "The following channels are not configured according to previous configurations:";
        //private const string CHANNEL_VALUE_ERROR = "The channel setting \"{0}\" was expected to provide {1} but provided {2} instead";
        //private const string CHANNEL_TYPE_ERROR = "A channel was the wrong type. This could be cause by a jumper inside a module with DIO channels";

        private readonly ErrorHandler _eHandler;
        
        /// <summary>
        /// Creates a new comparer with an errorhandler attached to it
        /// </summary>
        public Comparer()
        {
            _eHandler = new ErrorHandler();
        }

        /// <summary>
        /// Compares all device in a list with another. Useful when getting saved configurations
        /// </summary>
        /// <param name="newList">The freshly generated list to compare</param>
        /// <param name="oldList">The list from a config file to compare against</param>
        /// <returns>An array of errors if any</returns>
        public string[] CompareDeviceLists(List<ConnectedDevice> newList, List<ConnectedDevice> oldList)
        {
            var sizeDiff = newList.Count.CompareTo(oldList.Count);
            var sortedNewList = newList.OrderBy(d => BitConverter.ToString(d.Mac)).ToList();
            var sortedOldList = oldList.OrderBy(d => BitConverter.ToString(d.Mac)).ToList();
            if (sizeDiff == 0)
            {
                //Same size
                if (!CompareMac(sortedNewList, sortedOldList)) return _eHandler.GetErrors();
            }
            else
            {
                _eHandler.AddError(string.Format(DEVICE_AMOUNT_ERROR, newList.Count, oldList.Count));
            }
            return _eHandler.GetErrors();
        }

        private bool CompareMac(List<ConnectedDevice> newList, List<ConnectedDevice> oldList)
        {
            var amountNotFound = newList.Where((t, i) => !BitConverter.ToString(t.Mac).Equals(BitConverter.ToString(oldList[i].Mac))).Count();

            if (amountNotFound <= 0) return true;
            _eHandler.AddError(string.Format(WRONG_MAC_ERROR, amountNotFound));
            return false;
        }

        //private void CompareDeviceProperties(ConnectedDevice device1, ConnectedDevice device2)
        //{
        //    CompareDeviceName(device1, device2);
        //    CompareDeviceIP(device1, device2);
        //    for (var i = 0; i < device1.Channels.Count; i++)
        //    {
        //        CompareChannel(device1.Channels[i], device2.Channels[i]);
        //    }

        //}

        //private void CompareDeviceName(ConnectedDevice device1, ConnectedDevice device2)
        //{
        //    if (!device1.Name.Equals(device2.Name))
        //    {
        //        _eHandler.AddError(string.Format(DEVICE_NAME_ERROR, device1.Name, device2.Name));
        //    }
        //}

        //private void CompareDeviceIP(ConnectedDevice device1, ConnectedDevice device2)
        //{
        //    if (!device1.Ip.Equals(device2.Ip))
        //    {
        //        _eHandler.AddError(string.Format(DEVICE_IP_ERROR, device1.Ip, device2.Ip));
        //    }
        //}

        //private void CompareChannel(Channel channel1, Channel channel2)
        //{
        //    var isFirst = true;
        //    for (var i = 0; i < channel1.ChannelSettings.Count; i++)
        //    {
        //        if (!channel1.Type.Equals(channel2.Type))
        //        {
        //            if (isFirst)
        //            {
        //                _eHandler.AddError(CHANNEL_ERROR_HEADER);
        //                isFirst = false;
        //            }
        //            _eHandler.AddError(CHANNEL_TYPE_ERROR);
        //        }
        //        if (channel1.ChannelSettings[i].Key.ToLower().Contains("value")
        //            || channel1.ChannelSettings[i].Key.ToLower().Contains("count")
        //            || channel1.ChannelSettings[i].Key.ToLower().Contains("status"))
        //        {
        //            continue;
        //        }
        //        if (channel1.ChannelSettings[i].Value.Equals(channel2.ChannelSettings[i].Value)) continue;
        //        if (isFirst)
        //        {
        //            _eHandler.AddError(CHANNEL_ERROR_HEADER);
        //            isFirst = false;
        //        }
        //        _eHandler.AddError(string.Format(CHANNEL_VALUE_ERROR, channel1.ChannelSettings[i].Key, channel2.ChannelSettings[i].Value, channel1.ChannelSettings[i].Value));
        //    }
        //}

    }
}
