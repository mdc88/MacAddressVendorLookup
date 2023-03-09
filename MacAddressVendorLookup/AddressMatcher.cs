using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace MacAddressVendorLookup
{
    public class AddressMatcher
    {

        Dictionary<byte, Dictionary<long, MacVendorInfo>> _dicts = new Dictionary<byte, Dictionary<long, MacVendorInfo>>();

        public AddressMatcher(IMacVendorInfoProvider ouiEntryProvider)
        {
            BuildEntryDictionaries(ouiEntryProvider);
        }

        void BuildEntryDictionaries(IMacVendorInfoProvider ouiEntryProvider)
        {
            foreach (var entry in ouiEntryProvider.GetEntries())
            {
                Dictionary<long, MacVendorInfo> entryDict;
                if (!_dicts.TryGetValue(entry.MaskLength, out entryDict))
                {
                    entryDict = new Dictionary<long, MacVendorInfo>();
                    _dicts.Add(entry.MaskLength, entryDict);
                }

                entryDict[entry.Identifier] = entry;
            }
        }

        const long MAX_LONG = unchecked((long)ulong.MaxValue);

        public MacVendorInfo FindInfo(PhysicalAddress macAddress)
        {
            MacVendorInfo savedEntry = null;
            var longBytes = new byte[8];
            var macAddrBytes = macAddress.GetAddressBytes();
            macAddrBytes.CopyTo(longBytes, 0);
            var identifier = IPAddress.HostToNetworkOrder(BitConverter.ToInt64(longBytes, 0));

            foreach (var dict in _dicts)
            {
                //    GH-1:  Sub-ranges are being ignored
                //    There is a bug here where if the identifier is found in the first dictionary (/24 mask) 
                //    it will not look at any of the other dictionaries
                //    this example is 18:9B:A5 (IEEE Registration Authority) takes precedence over
                //                    18:9B:A5:50:00:00/28 (Starfire)
                //    Because of this bug, IEEE Registration Authority will always be returned
                
                int mask = dict.Key;

                var maskedIdent = identifier & (MAX_LONG << (64 - mask));

                MacVendorInfo entry;
                if (dict.Value.TryGetValue(maskedIdent, out entry))
                {
                    // GH-1: We want to check ALL of the mask dictionaries and pick the entry found with the most specific mask
                    if (savedEntry is null)
                    {
                        savedEntry = entry;
                    }
                    else
                    {
                        // GH-1: typically the problem will be that IEEE Reg Authority will own the /24,
                        //       and there will be numerous /28 or /36 entries within
                        if (savedEntry.MaskLength < entry.MaskLength)
                        {
                            savedEntry = entry;
                        }
                    }
                    // return entry;
                }
            }

            return savedEntry;
            // return null;
        }
    }
}
