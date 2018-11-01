using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NolekMoxa.Lib;
using NolekMoxa.Model;

namespace NolekMoxa
{
    /// <summary>
    ///     A Dll class made for Nolek A/S to be used in conjunction with Moxa IO devices
    /// </summary>
    public class NolekMoxa_CS
    {
        private const ushort Port = 502;
        private const uint Timeout = 1000;

        private const string CONFIG_FILE_NAME = "Devices";
        private readonly Comparer _comparer = new Comparer();

        private readonly ushort[] _wIfCount = new ushort[1];


        private HttpClient _httpClient;

        private byte _ifIndex;

        //MOXA Lib properties
        private int _ret;

        /// <summary>
        ///     An array of known channel types
        /// </summary>
        public string[] ChannelTypes = {"relay", "di", "do", "ai", "ao", "rtd", "tc"};
        //Stopwatch timer;

        /// <summary>
        ///     A List of all connected devices
        /// </summary>
        public List<ConnectedDevice> ConnectedDevices;

        /// <summary>
        ///     Constructor. Runs the MXEIO_Init.
        /// </summary>
        public NolekMoxa_CS()
        {
            //timer = new Stopwatch();
            //timer.Start();
            _ret = MXIO_CS.MXEIO_Init();
        }

        /// <summary>
        ///     Returns a list of the currently available networkinterfaces with their name as element(0) in the array and the
        ///     description as element(1).
        ///     The name is only usefull for the user whereas the description is the one used for connecting to Moxa IO modules
        /// </summary>
        /// <returns>A List of string arrays with name as element(0) in the array and the description as element(1)</returns>
        public List<string[]> GetNetworkInterfaces()
        {
            return (from item in NetworkInterface.GetAllNetworkInterfaces()
                select new[]
                {
                    item.Name, item.Description
                }).ToList();
        }

        /// <summary>
        ///     Checks if a config file exists
        /// </summary>
        /// <returns>True if it exists</returns>
        public bool CheckForExistingConfig()
        {
            return File.Exists(CONFIG_FILE_NAME);
        }


        /// <summary>
        ///     Compares the configfile with the current setup
        /// </summary>
        /// <returns>string array with errors if any</returns>
        public string[] CompareConfigWithCurrentSettings()
        {
            var oldList = DeSerializeObject<List<ConnectedDevice>>(CONFIG_FILE_NAME);
            if (oldList == null)
                return new[] {"Config file was not found"};
            var errors = _comparer.CompareDeviceLists(ConnectedDevices, oldList);
            return errors;
        }

        /// <summary>
        ///     Creates a config file with the current setup. Overrides any existing file.
        /// </summary>
        public void CreateConfigFile()
        {
            SerializeObject(ConnectedDevices, CONFIG_FILE_NAME);
        }

        /// <summary>
        ///     Loads the config file and replaces the current device list with the loaded one
        /// </summary>
        /// <returns>The new list of connected devices</returns>
        public List<ConnectedDevice> LoadConfigFileAsDeviceList()
        {
            return ConnectedDevices = DeSerializeObject<List<ConnectedDevice>>(CONFIG_FILE_NAME);
        }

        /// <summary>
        ///     Gets a list of all connected devices
        /// </summary>
        /// <returns>Returns a list of ConnectedDevices</returns>
        public List<ConnectedDevice> GetConnectedDevices(string EthIFName)
        {
            SetupHTTPClient();
            SetConnectedDevices(EthIFName);
            SetDeviceInfo();
            SetChannelsForDevices();
            return ConnectedDevices;
        }

        /// <summary>
        ///     Opens up a connection to a device. Use this once per thread you might want to initiate and not inside a loop.
        /// </summary>
        /// <param name="device">The device you want to connect to</param>
        /// <returns>The connection as an int to be used with other methods</returns>
        public int GetNewConnectionToDevice(ConnectedDevice device)
        {
            var connection = new int[1];
            var ret = MXIO_CS.MXEIO_E1K_Connect(Encoding.UTF8.GetBytes(device.Ip), Port, Timeout, connection,
                Encoding.UTF8.GetBytes(device.Password));
            MxioSub.CheckErr(ret, "MXIO_Connect");
            return connection[0];
        }

        /// <summary>
        ///     Gets the statuses (1: on or 0: off) of a specific amount of DI channels starting from di0
        /// </summary>
        /// <param name="device">The device on which you want the channel statuses</param>
        /// <param name="amountOfChannels">
        ///     The amount of channels to get statuses from. 1 being only di0 and 0 meaning all DI
        ///     channels
        /// </param>
        /// <param name="connection">A connection (Use "GetNewConnection" to establish a new connection)</param>
        /// <returns>A dictionary where the key is the indexes of the channels and the value is the corresponding status</returns>
        public Dictionary<int, double> GetStatusOfDiChannels(ConnectedDevice device, int amountOfChannels,
            int connection)
        {
            return GetStatusOfChannels("di", device, amountOfChannels, connection, 2);
        }

        /// <summary>
        ///     Gets the value of a specific amount of AI channels starting from ai0
        /// </summary>
        /// <param name="device">The device on which you want the channel values</param>
        /// <param name="amountOfChannels">
        ///     The amount of channels to get statuses from. 1 being only ai0 and 0 meaning all AI
        ///     channels
        /// </param>
        /// <param name="connection">A connection (Use "GetNewConnection" to establish a new connection)</param>
        /// <returns>A dictionary where the key is the indexes of the channels and the value is the corresponding value</returns>
        public Dictionary<int, double> GetStatusOfAiChannels(ConnectedDevice device, int amountOfChannels,
            int connection)
        {
            return GetStatusOfChannels("ai", device, amountOfChannels, connection, 2);
        }

        /// <summary>
        ///     Gets the value of a specific amount of AI channels starting from ai0
        /// </summary>
        /// <param name="device">The device on which you want the channel values</param>
        /// <param name="amountOfChannels">
        ///     The amount of channels to get statuses from. 1 being only ai0 and 0 meaning all AI
        ///     channels
        /// </param>
        /// <param name="connection">A connection (Use "GetNewConnection" to establish a new connection)</param>
        /// <param name="rawOrScaledValue">Wether to return the value as the raw value(0) or the scaled value(1).</param>
        /// <returns>A dictionary where the key is the indexes of the channels and the value is the corresponding value</returns>
        public Dictionary<int, double> GetStatusOfAiChannels(ConnectedDevice device, int amountOfChannels,
            int connection, int rawOrScaledValue)
        {
            return GetStatusOfChannels("ai", device, amountOfChannels, connection, rawOrScaledValue);
        }


        /// <summary>
        /// The parent method to call the correct method depending on the type given.
        /// </summary>
        /// <param name="channelType">The type of channel to get</param>
        /// <param name="device">The device on which the channels are</param>
        /// <param name="amountOfChannels">Amount of channels from 0</param>
        /// <param name="connection">A connection to the device</param>
        /// <param name="rawOrScaledValue">if the type is AI the this is either 0(raw) or 1(scaled) otherwise its 2</param>
        /// <returns>Dictionary with index, value</returns>
        private Dictionary<int, double> GetStatusOfChannels(string channelType, ConnectedDevice device,
            int amountOfChannels, int connection, int rawOrScaledValue)
        {
            //The first channel of the type
            var firstChannel = device.Channels.Find(x => x.Type == channelType);
            if (firstChannel == null) //Check if channel type exists on device
            {
                Console.WriteLine($"No {channelType} channels detected on device");
                return null;
            }
            //All the channels of the type
            var allChannelsOfType = device.Channels.FindAll(x => x.Type == channelType);
            if (allChannelsOfType.Count < amountOfChannels) // Check if there are any
            {
                Console.WriteLine($"There are less {channelType} channels on the device than the amount specified");
                return null;
            }
            //If the amount given is 0 it should get all
            if (amountOfChannels == 0)
                amountOfChannels = allChannelsOfType.Count;

            var bytStartChannel = int.Parse(firstChannel.ChannelSettings[0].Value);
            var highestIndexWithOffset =
                int.Parse(allChannelsOfType[allChannelsOfType.Count - 1].ChannelSettings[0].Value) + 1;
            int ret;

            switch (channelType)
            {
                case "di":
                    var dwGetDiValue = new uint[1];
                    ret = MXIO_CS.E1K_DI_Reads(connection, BitConverter.GetBytes(bytStartChannel)[0],
                        BitConverter.GetBytes(highestIndexWithOffset)[0], dwGetDiValue);
                    MxioSub.CheckErr(ret, "DI_Reads");
                    return GetDiChannelStatusByIndex(ret, dwGetDiValue[0], highestIndexWithOffset, amountOfChannels,
                        allChannelsOfType);
                case "ai":
                    //If no prefered valuetype is given use the standard
                    if (rawOrScaledValue > 1)
                        rawOrScaledValue = int.Parse(firstChannel.ChannelSettings
                            .Find(x => x.Key == "aiPreferedGetValue").Value);
                    if (rawOrScaledValue == 0)
                    {
                        //Gets the raw value of the AI channels
                        var dwGetAiRawValue = new ushort[highestIndexWithOffset];
                        ret = MXIO_CS.E1K_AI_ReadRaws(connection, BitConverter.GetBytes(bytStartChannel)[0],
                            BitConverter.GetBytes(highestIndexWithOffset)[0], dwGetAiRawValue);
                        MxioSub.CheckErr(ret, "AI_ReadRaws");
                        return GetAiRawValueByIndex(ret, dwGetAiRawValue, amountOfChannels, allChannelsOfType);
                    }
                    else
                    {
                        //Gets the scaled value of the AI channels
                        var dwGetAiScaledValue = new double[highestIndexWithOffset];
                        ret = MXIO_CS.E1K_AI_Reads(connection, BitConverter.GetBytes(bytStartChannel)[0],
                            BitConverter.GetBytes(highestIndexWithOffset)[0], dwGetAiScaledValue);
                        MxioSub.CheckErr(ret, "AI_Reads");
                        return GetAiScaledValueByIndex(ret, dwGetAiScaledValue, amountOfChannels, allChannelsOfType);
                    }
                default:
                    Console.WriteLine("Something went wrong");
                    return null;
            }
        }

        /// <summary>
        /// Get the raw value of the AI channels
        /// </summary>
        /// <param name="ret">The returned request</param>
        /// <param name="dwValue">the returned value from the request</param>
        /// <param name="amountOfChannels">The amount of channels to return value for</param>
        /// <param name="allChannelsOfType">All the AI channels</param>
        /// <returns>Dictionary with index, value</returns>
        private Dictionary<int, double> GetAiRawValueByIndex(int ret, ushort[] dwValue, int amountOfChannels,
            List<Channel> allChannelsOfType)
        {
            var returnDict = new Dictionary<int, double>();
            if (ret != MXIO_CS.MXIO_OK) return returnDict;
            var skippedAmount = 0;
            for (var i = 0; i < amountOfChannels; i++)
            {
                var ch = allChannelsOfType.Find(x => x.ChannelSettings[0].Value == i + skippedAmount + "");
                if (ch != null)
                {
                    returnDict.Add(int.Parse(ch.ChannelSettings[0].Value), dwValue[i - skippedAmount]);
                }
                else //Skip if channel doesn't exist.
                {
                    skippedAmount++;
                    i--;
                }
            }
            return returnDict;
        }

        /// <summary>
        /// Get the status of DI channels
        /// </summary>
        /// <param name="ret">The returned request</param>
        /// <param name="dwValue">the returned value from the request</param>
        /// <param name="highestIndexWithOffset">The index of the last DI channel</param>
        /// <param name="amountOfChannels">The amount of channels to return value for</param>
        /// <param name="allChannelsOfType">All the AI channels</param>
        /// <returns>Dictionary with index, value</returns>
        private Dictionary<int, double> GetDiChannelStatusByIndex(int ret, uint dwValue, int highestIndexWithOffset,
            int amountOfChannels, List<Channel> allChannelsOfType)
        {
            var returnDict = new Dictionary<int, double>();
            if (ret != MXIO_CS.MXIO_OK) return returnDict;
            var binary = Convert.ToString(dwValue, 2);
            while (binary.Length != highestIndexWithOffset) //Add 0s to the binary string
                binary = "0" + binary;
            var binaryArray = binary.ToCharArray();
            var skippedAmount = 0;
            for (var i = 0; i < amountOfChannels; i++)
            {
                var ch = allChannelsOfType.Find(x => x.ChannelSettings[0].Value == i + skippedAmount + "");
                if (ch != null)
                {
                    returnDict.Add(int.Parse(ch.ChannelSettings[0].Value),
                        binaryArray[binaryArray.Length - 1 - i - skippedAmount] - '0');
                }
                else //Skip if channel doesn't exist
                {
                    skippedAmount++;
                    i--;
                }
            }
            return returnDict;
        }

        /// <summary>
        /// Get the scaled value of the AI channels
        /// </summary>
        /// <param name="ret">The returned request</param>
        /// <param name="dwValue">the returned value from the request</param>
        /// <param name="amountOfChannels">The amount of channels to return value for</param>
        /// <param name="allChannelsOfType">All the AI channels</param>
        /// <returns>Dictionary with index, value</returns>
        private Dictionary<int, double> GetAiScaledValueByIndex(int ret, double[] dwValue, int amountOfChannels,
            List<Channel> allChannelsOfType)
        {
            var returnDict = new Dictionary<int, double>();
            if (ret != MXIO_CS.MXIO_OK) return returnDict;
            var skippedAmount = 0;
            for (var i = 0; i < amountOfChannels; i++)
            {
                var ch = allChannelsOfType.Find(x => x.ChannelSettings[0].Value == i + skippedAmount + "");
                if (ch != null)
                {
                    returnDict.Add(int.Parse(ch.ChannelSettings[0].Value), dwValue[i - skippedAmount]);
                }
                else
                {
                    skippedAmount++;
                    i--;
                }
            }
            return returnDict;
        }

        /// <summary>
        /// Sets the status for one of the relays
        /// </summary>
        /// <param name="device">The device with the relay to be set</param>
        /// <param name="index">The index of the relay to be set</param>
        /// <param name="on">True if it should turn on and false if it should turn off</param>
        public void SetStatusForRelay(ConnectedDevice device,int index, bool on)
        {
            SetRelayOrDOStatus(device, index, on);
        }

        /// <summary>
        /// Sets the status for one of the DOs
        /// </summary>
        /// <param name="device">The device with the DO to be set</param>
        /// <param name="index">The index of the DO to be set</param>
        /// <param name="on">True if it should turn on and false if it should turn off</param>
        public void SetStatusForDO(ConnectedDevice device, int index, bool on)
        {
            SetRelayOrDOStatus(device, index, on);
        }

        /// <summary>
        /// Sets a relay or DO status (same method for both)
        /// </summary>
        /// <param name="device">The device with the channel</param>
        /// <param name="index">The index of the desired channel</param>
        /// <param name="on">on or off?</param>
        private void SetRelayOrDOStatus(ConnectedDevice device, int index, bool on)
        {

            var con = GetNewConnectionToDevice(device);
            var value = (uint) (on ? 1:0);
            var ret = MXIO_CS.E1K_DO_Writes(con, BitConverter.GetBytes(index)[0], 1, value);
            MxioSub.CheckErr(ret, "DO_WRITE");
            Console.WriteLine(value);
        }

        //TODO Test! Only works in theory
        /// <summary>
        /// Sets the raw value for one of the AOs
        /// </summary>
        /// <param name="device">The device with the AO to be set</param>
        /// <param name="index">The index of the AO to be set</param>
        /// <param name="value">The value to be set on the AO</param>
        public void SetRawForAO(ConnectedDevice device, int index, int value)
        {
            var con = GetNewConnectionToDevice(device);
            UInt16[] wAoRawValues = new UInt16[1];
            wAoRawValues[0] = (ushort) value;
            var ret = MXIO_CS.E1K_AO_WriteRaws(con, BitConverter.GetBytes(index)[0], 1, wAoRawValues);
            MxioSub.CheckErr(ret, "AO_WriteRaws");
            Console.WriteLine(value);
        }

        //TODO Test! Only works in theory
        /// <summary>
        /// Sets the scaled value for one of the AOs
        /// </summary>
        /// <param name="device">The device with the AO to be set</param>
        /// <param name="index">The index of the AO to be set</param>
        /// <param name="value">The value to be set on the AO</param>
        public void SetScaledForAO(ConnectedDevice device, int index, double value)
        {
            double[] dAoValues = new double[1];
            dAoValues[0] = value;
            var con = GetNewConnectionToDevice(device);
            var ret = MXIO_CS.E1K_AO_Writes(con, BitConverter.GetBytes(index)[0], 1, dAoValues);
            MxioSub.CheckErr(ret, "AO_Writes");
            Console.WriteLine(value);


        }

        /// <summary>
        /// Sets firmware and name of all connected devices
        /// </summary>
        private void SetDeviceInfo()
        {
            foreach (var device in ConnectedDevices)
            {
                var response = _httpClient.GetStringAsync("http://" + device.Ip + "/api/slot/0/sysInfo").Result;

                device.FirmwareVersion = GetValueOfSearchwordInHTTPResponse(response, "firmwareVersion");
                device.Name = GetValueOfSearchwordInHTTPResponse(response, "deviceName");
            }
        }

        /// <summary>
        /// Get value from a JSON string based on key
        /// </summary>
        /// <param name="response">The entire JSON string</param>
        /// <param name="searchword">The key for which to get the value</param>
        /// <returns>The value of the given searchword(key)</returns>
        private string GetValueOfSearchwordInHTTPResponse(string response, string searchword)
        {
            searchword = "\"" + searchword + "\": \"";
            var value = response.Substring(response.IndexOf(searchword, StringComparison.Ordinal) + searchword.Length);
            value = value.Substring(0, value.IndexOf("\"", StringComparison.Ordinal));
            return value;
        }

        /// <summary>
        /// Set all channels for devices using Moxas RESTApi
        /// </summary>
        private void SetChannelsForDevices()
        {
            foreach (var device in ConnectedDevices)
            foreach (var channelType in ChannelTypes)
                try
                {
                    var response = _httpClient.GetAsync("http://" + device.Ip + "/api/slot/0/io/" + channelType).Result;
                    if (!response.IsSuccessStatusCode) continue;
                    var responseCont = response.Content.ReadAsStringAsync().Result;
                    var channelArray = ResponseToArray(responseCont);
                    AddChannelOfTypeWithSettingsToDevice(device, channelType, channelArray);
                }
                catch (Exception e)
                {
                    switch (e)
                    {
                        case WebException _:
                            var we = (WebException) e;
                            if (we.Response is HttpWebResponse errorResponse &&
                                errorResponse.StatusCode == HttpStatusCode.NotFound)
                                continue;
                            Console.WriteLine(we);
                            break;
                        case AggregateException _:
                            Console.WriteLine(e.Message);
                            break;
                    }
                }
        }

        /// <summary>
        /// Adds a channel to the device and all the current settings for that channel
        /// </summary>
        /// <param name="device">The device to which you want to add the channel</param>
        /// <param name="channelType">The type of channel to add</param>
        /// <param name="channelArray">All the settings for the channel</param>
        private static void AddChannelOfTypeWithSettingsToDevice(ConnectedDevice device, string channelType,
            string[] channelArray)
        {
            foreach (var fullChannel in channelArray)
            {

                var channel = new Channel(channelType);
                var channelSetting = fullChannel.Split(',');
                foreach (var kvChannelSetting in channelSetting) //All the settings
                {
                    var kvSplit = kvChannelSetting.Split(':');
                    channel.ChannelSettings.Add(new ChannelSetting(kvSplit[0], kvSplit[1])); //Key, value pair for the settings
                }
                if (channelType.Equals("ai"))
                    channel.ChannelSettings.Add(new ChannelSetting("aiPreferedGetValue", "0")); // prefered raw or scaled for ai
                device.Channels.Add(channel);
            }
        }

        /// <summary>
        /// Converts JSON resposne from a httpRequest to a string array
        /// </summary>
        /// <param name="response">The JSON string</param>
        /// <returns>string array of the response</returns>
        private string[] ResponseToArray(string response)
        {
            var subResponse = response.Substring(response.IndexOf("[", StringComparison.Ordinal) + 1);
            subResponse = subResponse.Remove(subResponse.LastIndexOf(']'));
            subResponse = subResponse.Replace(" ", string.Empty);
            subResponse = subResponse.Replace("\n", string.Empty);
            subResponse = subResponse.Replace("},{", "-");
            subResponse = subResponse.Replace("\"", string.Empty);
            subResponse = subResponse.Replace("{", string.Empty);
            subResponse = subResponse.Replace("}", string.Empty);
            return subResponse.Split('-');
        }

        /// <summary>
        /// Sets up the HTTPCLient with headers used for the Moxa devices
        /// </summary>
        private void SetupHTTPClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Connection.Clear();
            _httpClient.DefaultRequestHeaders.ConnectionClose = false;
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vdn.dac.v1"));
        }

        /// <summary>
        ///     Sets the list of connected devices on the given ethernet port.
        ///     Note this methods takes a description and not a name.
        /// </summary>
        /// <param name="EthIFName">The first word of the DESCRIPTION of the ethernet port</param>
        public void SetConnectedDevices(string EthIFName)
        {
            ConnectedDevices = new List<ConnectedDevice>();
            _ret = MXIO_CS.MXIO_GetDllVersion();
            Console.WriteLine("MXIO_GetDllVersion:{0}.{1}.{2}.{3}", (_ret >> 12) & 0xF, (_ret >> 8) & 0xF,
                (_ret >> 4) & 0xF, _ret & 0xF);
            _ret = MXIO_CS.MXIO_GetDllBuildDate();
            Console.WriteLine("MXIO_GetDllBuildDate:{0:x}/{1:x}/{2:x}", _ret >> 16, (_ret >> 8) & 0xFF, _ret & 0xFF);
            //--------------------------------------------------------------------------
            _ret = MXIO_CS.MXEIO_Init();
            Console.WriteLine("MXEIO_Init return {0}", _ret);
            //--------------------------------------------------------------------------
            _wIfCount[0] = 0;
            _ret = MXIO_CS.MXIO_ListIF(_wIfCount);
            MxioSub.CheckErr(_ret, "MXIO_ListIF");
            if (_ret == MXIO_CS.MXIO_OK)
                Console.WriteLine("MXIO_ListIF return device = {0}", _wIfCount[0]);
            //found IF
            if (_wIfCount[0] <= 0) return;
            var IFInfo = new byte[_wIfCount[0] * MXIO_CS.MX_IF_ONE_IF_SIZE];
            _ret = MXIO_CS.MXIO_GetIFInfo(_wIfCount[0], IFInfo);
            MxioSub.CheckErr(_ret, "MXIO_GetIFInfo");
            if (_ret == MXIO_CS.MXIO_OK)
            {
                Console.WriteLine("MXIO_GetIFInfo succeed");
                var LinkIFInfo = new string[_wIfCount[0]];
                for (var i = 0; i < _wIfCount[0]; i++)
                {
                    var szIPData = "";
                    for (var g = 0; g < 16; g++) // IP Length
                        szIPData += $"{(char) IFInfo[g + i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_IP]}";
                    //
                    var szDescData = "";
                    for (var g = 0; g < 256; g++) // Descriptor
                        szDescData +=
                            $"{(char) IFInfo[g + i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_DESC]}";

                    /* Show the device name and IP address */
                    LinkIFInfo[i] =
                        $"[{IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_NUM]}] {szIPData}, {IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC]:X2}:{IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 1]:X2}:{IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 2]:X2}:{IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 3]:X2}:{IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 4]:X2}:{IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 5]:X2},{szDescData}";
                    Console.WriteLine(LinkIFInfo[i]);
                    //check which one network IF that we want.//save user specific IF information
                    var iCheck = 0;
                    for (var g = 0; g < 6; g++) // Descriptor
                    {
                        if (IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_DESC + g] !=
                            EthIFName.ToCharArray()[g])
                            break;
                        iCheck++;
                    }
                    Console.WriteLine(iCheck + "       ICHECK");
                    if (iCheck == EthIFName.Length)
                        _ifIndex = IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_NUM];
                }
            }
            _ret = MXIO_CS.MXIO_SelectIF(_wIfCount[0], IFInfo, _ifIndex);
            MxioSub.CheckErr(_ret, "MXIO_SelectIF");
            if (_ret == MXIO_CS.MXIO_OK)
                Console.WriteLine("MXIO_SelectIF() success. select Index = {0} network Interface.\n", _ifIndex);
            //

            const ushort MAX_SUPPORT_DEVICE = 256;
            var bytTemp = new byte[MAX_SUPPORT_DEVICE * MXIO_CS.MX_ML_MODULE_LIST_SIZE];
            var iDeviceCount = new uint[1];
            //
            _ret = MXIO_CS.MXIO_AutoSearch(MXIO_CS.E1200_SERIES, 1, 2000, iDeviceCount, bytTemp);
            MxioSub.CheckErr(_ret, "MXIO_AutoSearch");
            if (_ret != MXIO_CS.MXIO_OK) return;
            {
                Console.WriteLine("MXIO_AutoSearch Success.");
                var linkDeviceInfo = new string[iDeviceCount[0]];
                //MXIO_CS._MODULE_LIST[] ModuleList = new MXIO_CS._MODULE_LIST[iDeviceCount[0]];
                var MID = new ushort[iDeviceCount[0]];
                for (var i = 0; i < iDeviceCount[0]; i++)
                {
                    var szIPConv = "";
                    var BytMACAddr = new byte[6];
                    MID[i] = (ushort) (bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + 1] << 8); //High Byte
                    MID[i] |= bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE]; //Low Byte
                    ConnectedDevices.Add(new ConnectedDevice {Module = GetModuleType(MID[i], linkDeviceInfo[i])});
                    if (bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_BYTLANUSE] == 1)
                    {
                        //set IP address to string
                        for (var j = 0; j < 16; j++)
                            if (bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZIP1 + j] > 0)
                                szIPConv +=
                                    $"{(char) bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZIP1 + j]}";
                        //Set MAC address to string
                        for (var j = 0; j < 6; j++)
                            BytMACAddr[j] =
                                bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + j];
                        //
                        linkDeviceInfo[i] +=
                            $" [{i}] IP={szIPConv}, MAC={bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 1]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 2]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 3]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 4]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 5]:X2}\n";
                    }
                    else
                    {
                        //set IP address to string
                        for (var j = 0; j < 16; j++)
                            if (bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZIP0 + j] > 0)
                                szIPConv +=
                                    $"{(char) bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZIP0 + j]}";
                        //Set MAC address to string
                        for (var j = 0; j < 6; j++)
                            BytMACAddr[j] =
                                bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + j];
                        //
                        linkDeviceInfo[i] =
                            $"IP={szIPConv}:MAC={bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 1]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 2]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 3]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 4]:X2}-{bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 5]:X2}\n";
                    }
                    SetIPAndMac(i, szIPConv, BytMACAddr);
                }
            }
        }

        /// <summary>
        /// Sets the IP and Mac address for the connected device
        /// </summary>
        /// <param name="index">index of the connected device in the list</param>
        /// <param name="ip">The ip to set</param>
        /// <param name="bytMACAddr">The mac address to set as byte array</param>
        private void SetIPAndMac(int index, string ip, byte[] bytMACAddr)
        {
            ConnectedDevices[index].Ip = ip;
            ConnectedDevices[index].Mac = bytMACAddr;
        }

        /// <summary>
        /// Returns the module name of the device
        /// </summary>
        /// <param name="MID">Info from their device DB</param>
        /// <param name="linkDeviceInfo">Info from their device DB</param>
        /// <returns>A string value containing the module name</returns>
        private string GetModuleType(ushort MID, string linkDeviceInfo)
        {
            return MxioSub.CheckModuleType(MID, linkDeviceInfo)
                ? Enum.GetName(typeof(MXIO_CS.MXIO_ActiveTagModuleType), MID)?.Split('_')[1]
                : "";
        }

        /// <summary>
        ///     Serializes an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializableObject">The object to serialize</param>
        /// <param name="fileName">The name of the file where the serialized object should be saved to</param>
        private void SerializeObject<T>(T serializableObject, string fileName)
        {
            if (serializableObject == null) return;

            try
            {
                var xmlDocument = new XmlDocument();
                var serializer = new XmlSerializer(serializableObject.GetType());
                using (var stream = new MemoryStream())
                {
                    serializer.Serialize(stream, serializableObject);
                    stream.Position = 0;
                    xmlDocument.Load(stream);
                    xmlDocument.Save(fileName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION: " + ex.Message);
            }
        }


        /// <summary>
        ///     Deserializes an xml file into an object list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName">The file to desrialize</param>
        /// <returns>The deserialized object</returns>
        private T DeSerializeObject<T>(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return default(T);

            var objectOut = default(T);
            try
            {
                var xmlDocument = new XmlDocument();
                xmlDocument.Load(fileName);
                var xmlString = xmlDocument.OuterXml;
                using (var read = new StringReader(xmlString))
                {
                    var outType = typeof(T);
                    var serializer = new XmlSerializer(outType);
                    using (XmlReader reader = new XmlTextReader(read))
                    {
                        objectOut = (T) serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION: " + ex.Message);
            }
            return objectOut;
        }

        /// <summary>
        ///     Checks the specified connection. Best used in a loop
        /// </summary>
        /// <param name="connection">A connection previously established</param>
        /// <returns>True if the connection is ok</returns>
        public bool CheckConnection(int connection)
        {
            var bytCheckStatus = new byte[1];
            var ret = MXIO_CS.MXEIO_CheckConnection(connection, Timeout, bytCheckStatus);
            MxioSub.CheckErr(ret, "MXEIO_CheckConnection");
            if (ret != MXIO_CS.MXIO_OK) return false;
            switch (bytCheckStatus[0])
            {
                case MXIO_CS.CHECK_CONNECTION_OK:
                    Console.WriteLine("MXEIO_CheckConnection: Check connection ok => {0}", bytCheckStatus[0]);
                    return true;
                case MXIO_CS.CHECK_CONNECTION_FAIL:
                    Console.WriteLine("MXEIO_CheckConnection: Check connection fail => {0}", bytCheckStatus[0]);
                    break;
                case MXIO_CS.CHECK_CONNECTION_TIME_OUT:
                    Console.WriteLine("MXEIO_CheckConnection: Check connection time out => {0}", bytCheckStatus[0]);
                    break;
                default:
                    Console.WriteLine("MXEIO_CheckConnection: Check connection status unknown => {0}",
                        bytCheckStatus[0]);
                    break;
            }
            return false;
        }


        //public int ChangeIP(ConnectedDevice device, string newIp)
        //{
        //    Console.WriteLine("MXIO_ChangeDupIP from {0} to new IP address {1}", device.Ip, newIp);
        //    //change IP        
        //    _ret = MXIO_CS.MXIO_ChangeDupIP(
        //        Encoding.UTF8.GetBytes(device.Ip),
        //        Port,
        //        device.Mac,
        //        Timeout,
        //        Encoding.UTF8.GetBytes(Password),
        //        Encoding.UTF8.GetBytes(newIp));
        //    MxioSub.CheckErr(_ret, "MXIO_ChangeDupIP");
        //    if (_ret == MXIO_CS.MXIO_OK)
        //        Console.WriteLine("MXIO_ChangeDupIP Success.");
        //    return _ret;
        //}


        //public void TEST()
        //{
        //    int ret;
        //    Int32[] hConnection = new Int32[1];
        //    string IPAddr = "192.168.127.254";
        //    string NewIPAddr = "192.168.127.123";
        //    string Password = "";
        //    UInt32 Timeout = 5000;
        //    UInt32 i, j, g, iCheck;
        //    UInt16[] wIFCount = new UInt16[1];
        //    UInt32 _IF_INDEX = 0;
        //    string EthIFName = "Killer";
        //    {
        //        ret = MXIO_CS.MXIO_GetDllVersion();
        //        Console.WriteLine("MXIO_GetDllVersion:{0}.{1}.{2}.{3}", (ret >> 12) & 0xF, (ret >> 8) & 0xF, (ret >> 4) & 0xF, (ret) & 0xF);

        //        ret = MXIO_CS.MXIO_GetDllBuildDate();
        //        Console.WriteLine("MXIO_GetDllBuildDate:{0:x}/{1:x}/{2:x}", (ret >> 16), (ret >> 8) & 0xFF, (ret) & 0xFF);
        //        //--------------------------------------------------------------------------
        //        ret = MXIO_CS.MXEIO_Init();
        //        Console.WriteLine("MXEIO_Init return {0}", ret);
        //        //--------------------------------------------------------------------------
        //        wIFCount[0] = 0;
        //        ret = MXIO_CS.MXIO_ListIF(wIFCount);
        //        MxioSub.CheckErr(ret, "MXIO_ListIF");
        //        if (ret == MXIO_CS.MXIO_OK)
        //            Console.WriteLine("MXIO_ListIF return device = {0}", wIFCount[0]);
        //        //found IF
        //        if (wIFCount[0] > 0)
        //        {
        //            byte[] IFInfo = new byte[wIFCount[0] * MXIO_CS.MX_IF_ONE_IF_SIZE];
        //            ret = MXIO_CS.MXIO_GetIFInfo(wIFCount[0], IFInfo);
        //            MxioSub.CheckErr(ret, "MXIO_GetIFInfo");
        //            if (ret == MXIO_CS.MXIO_OK)
        //            {
        //                Console.WriteLine("MXIO_GetIFInfo succeed");
        //                string[] LinkIFInfo = new string[wIFCount[0]];
        //                for (i = 0; i < wIFCount[0]; i++)
        //                {
        //                    string szIPData = "";
        //                    for (g = 0; g < 16; g++)// IP Length
        //                        szIPData += String.Format("{0}", (char)IFInfo[g + i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_IP]);
        //                    //
        //                    string szDescData = "";
        //                    for (g = 0; g < 256; g++)// Descriptor
        //                        szDescData += String.Format("{0}", (char)IFInfo[g + i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_DESC]);

        //                    /* Show the device name and IP address */
        //                    LinkIFInfo[i] = String.Format("[{0}] {1}, {2:X2}:{3:X2}:{4:X2}:{5:X2}:{6:X2}:{7:X2},{8}",
        //                        IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_NUM],
        //                        szIPData,
        //                        IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC],
        //                        IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 1],
        //                        IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 2],
        //                        IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 3],
        //                        IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 4],
        //                        IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_MAC + 5],
        //                        szDescData);
        //                    Console.WriteLine(LinkIFInfo[i]);
        //                    //check which one network IF that we want.//save user specific IF information
        //                    for (g = 0, iCheck = 0; g < 6; g++)// Descriptor
        //                    {
        //                        if (IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_DESC + g] != EthIFName.ToCharArray()[g])
        //                            break;
        //                        else
        //                            iCheck++;
        //                    }
        //                    if (iCheck == EthIFName.Length)
        //                        _IF_INDEX = IFInfo[i * MXIO_CS.MX_IF_ONE_IF_SIZE + MXIO_CS.MX_IF_INDEX_NUM];
        //                }
        //            }
        //            if (IFInfo != null)
        //            {
        //                ret = MXIO_CS.MXIO_SelectIF(wIFCount[0], IFInfo, _IF_INDEX);
        //                MxioSub.CheckErr(ret, "MXIO_SelectIF");
        //                if (ret == MXIO_CS.MXIO_OK)
        //                    Console.WriteLine("MXIO_SelectIF() success. select Index = {0} network Interface.\n", _IF_INDEX);
        //                //
        //            }

        //            const UInt16 MAX_SUPPORT_DEVICE = 256;
        //            byte[] bytTemp = new byte[MAX_SUPPORT_DEVICE * MXIO_CS.MX_ML_MODULE_LIST_SIZE];
        //            UInt32[] iDeviceCount = new UInt32[1];
        //            //
        //            ret = MXIO_CS.MXIO_AutoSearch(MXIO_CS.E1200_SERIES, 1, 2000, iDeviceCount, bytTemp);
        //            MxioSub.CheckErr(ret, "MXIO_AutoSearch");
        //            if (ret == MXIO_CS.MXIO_OK)
        //            {
        //                Console.WriteLine("MXIO_AutoSearch Success.");
        //                string[] LinkDeviceInfo = new string[iDeviceCount[0]];
        //                //MXIO_CS._MODULE_LIST[] ModuleList = new MXIO_CS._MODULE_LIST[iDeviceCount[0]];
        //                UInt16[] MID = new UInt16[iDeviceCount[0]];
        //                for (i = 0; i < iDeviceCount[0]; i++)
        //                {
        //                    String szIPConv = "";
        //                    Byte[] BytMACAdddr = new Byte[6];
        //                    MID[i] = (UInt16)(bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + 1] << 8);        //High Byte
        //                    MID[i] |= bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE];                          //Low Byte
        //                    MxioSub.CheckModuleType(MID[i], LinkDeviceInfo[i]);
        //                    //
        //                    if (bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_BYTLANUSE] == 1)
        //                    {
        //                        //set IP address to string
        //                        for (j = 0; j < 16; j++)
        //                        {
        //                            if (bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZIP1 + j] > 0)
        //                                szIPConv += String.Format("{0}", (char)bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZIP1 + j]);
        //                        }
        //                        //Set MAC address to string
        //                        for (j = 0; j < 6; j++)
        //                            BytMACAdddr[j] = bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + j];
        //                        //
        //                        LinkDeviceInfo[i] += String.Format(" [{0}] IP={1}, MAC={2:X2}-{3:X2}-{4:X2}-{5:X2}-{6:X2}-{7:X2}\n",
        //                            i, szIPConv,
        //                            bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1], bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 1],
        //                            bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 2], bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 3],
        //                            bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 4], bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC1 + 5]);
        //                    }
        //                    else
        //                    {
        //                        //set IP address to string
        //                        for (j = 0; j < 16; j++)
        //                        {
        //                            if (bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZIP0 + j] > 0)
        //                                szIPConv += String.Format("{0}", (char)bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZIP0 + j]);
        //                        }
        //                        //Set MAC address to string
        //                        for (j = 0; j < 6; j++)
        //                            BytMACAdddr[j] = bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + j];
        //                        //
        //                        LinkDeviceInfo[i] = String.Format("[{0}] IP={1}, MAC={2:X2}-{3:X2}-{4:X2}-{5:X2}-{6:X2}-{7:X2}\n",
        //                            i, szIPConv,
        //                            bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0], bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 1],
        //                            bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 2], bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 3],
        //                            bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 4], bytTemp[i * MXIO_CS.MX_ML_MODULE_LIST_SIZE + MXIO_CS.MX_ML_INDEX_SZMAC0 + 5]);
        //                    }
        //                    Console.WriteLine(LinkDeviceInfo[i]);
        //                    //
        //                    if (szIPConv.CompareTo(IPAddr) == 0)
        //                    {
        //                        Console.WriteLine("MXIO_ChangeDupIP from {0} to new IP address {1}", IPAddr, NewIPAddr);
        //                        //change Duplicate IP  
        //                        ret = MXIO_CS.MXIO_ChangeDupIP(
        //                            System.Text.Encoding.UTF8.GetBytes(IPAddr),
        //                            Port,
        //                            BytMACAdddr,
        //                            Timeout,
        //                            System.Text.Encoding.UTF8.GetBytes(Password),
        //                            System.Text.Encoding.UTF8.GetBytes(NewIPAddr));
        //                        Console.WriteLine(ret);
        //                        MxioSub.CheckErr(ret, "MXIO_ChangeDupIP");
        //                        if (ret == MXIO_CS.MXIO_OK)
        //                            Console.WriteLine("MXIO_ChangeDupIP Success.");
        //                    }
        //                }
        //            }
        //        }
        //        //--------------------------------------------------------------------------
        //        MXIO_CS.MXEIO_Exit();
        //        Console.WriteLine("MXEIO_Exit, Press Enter To Exit.");
        //        Console.ReadLine();
        //    }
        //}
    }

    [SuppressMessage("ReSharper", "RedundantAssignment")]
    internal class MxioSub
    {
        //===================================================================
        public static void CheckErr(int iRet, string szFunctionName)
        {
            if (Enum.IsDefined(typeof(MXIO_CS.MXIO_ErrorCode), iRet))
                Console.WriteLine("Function = \"{0}\". Return Message : {1}", szFunctionName,
                    Enum.GetName(typeof(MXIO_CS.MXIO_ErrorCode), iRet));
            else
                Console.WriteLine("Function = \"{0}\". Return Unknown Message ID: {1}", szFunctionName, iRet);
        }

        //===================================================================
        public static bool CheckModuleType(ushort wHWID, string szModuleName)
        {
            var success = false;
            szModuleName = "Unknown_Module_ID.";
            if (Enum.IsDefined(typeof(MXIO_CS.MXIO_ActiveTagModuleType), (int) wHWID))
            {
                szModuleName = Enum.GetName(typeof(MXIO_CS.MXIO_ActiveTagModuleType), wHWID);
                success = true;
            }
            Console.WriteLine("wHWID = 0x{0:X2}. ID : \"{1}\"", wHWID, szModuleName);
            return success;
        }

        //===================================================================
        public static void CheckMsgType(byte bytType, string szMsgTypeName)
        {
            szMsgTypeName = "Unknown_Msg_Type.";
            if (Enum.IsDefined(typeof(MXIO_CS.MXIO_ActiveTagMsgType), (int) bytType))
                szMsgTypeName = Enum.GetName(typeof(MXIO_CS.MXIO_ActiveTagMsgType), bytType);
            Console.WriteLine("BytType = {0}. Message Type : \"{1}\"", bytType, szMsgTypeName);
        }

        //===================================================================
        public static void CheckSubMsgType(ushort nType, string szMsgTypeName)
        {
            szMsgTypeName = "Unknown_SubMsg_Type.";
            if (Enum.IsDefined(typeof(MXIO_CS.MXIO_ActiveTagMsgSubType), (int) nType))
                szMsgTypeName = Enum.GetName(typeof(MXIO_CS.MXIO_ActiveTagMsgSubType), nType);
            Console.WriteLine("nType = {0}. Sub Message Type :\"{1}\"", nType, szMsgTypeName);
        }

        //===================================================================
        public static void CheckChType(byte bytType, string[] szChTypeName)
        {
            szChTypeName[0] = "Unknown_Ch_Type.";
            if (Enum.IsDefined(typeof(MXIO_CS.MXIO_ActiveTagChType), (int) bytType))
                szChTypeName[0] = Enum.GetName(typeof(MXIO_CS.MXIO_ActiveTagChType), bytType);
        }

        //===================================================================
    }
}