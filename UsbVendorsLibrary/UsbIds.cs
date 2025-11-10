using System.Reflection;
using System.Runtime.CompilerServices;

namespace UsbVendorsLibrary;

/// <summary>
/// Provides fast, low-memory lookups of USB vendor and product names parsed from the embedded usb.ids.
/// Primary APIs are TryGetVendorName(vendorId) and TryGetProductName(vendorId, productId).
/// Optional reverse lookups (name -> id) are built lazily.
/// </summary>
public static class UsbIds
{
    private const string EmbeddedResourceName = "UsbVendorsLibrary.usb.ids";

    private static readonly Lazy<Data> _data = new(CreateData, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    // Reverse lookup indexes are optional and built lazily to minimize memory usage.
    private static readonly Lazy<Dictionary<string, ushort>> _vendorNameToId = new(() =>
    {
        var d = _data.Value;
        var map = new Dictionary<string, ushort>(d.Vendors.Length, StringComparer.OrdinalIgnoreCase);
        var vendors = d.Vendors;
        for (int i = 0; i < vendors.Length; i++)
        {
            var v = vendors[i];
            map.TryAdd(v.Name, v.Id); // prefer first occurrence if duplicates exist
        }
        return map;
    }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Dictionary<ushort, Dictionary<string, ushort>> _deviceNameToIdByVendor = new();
    private static readonly object _deviceReverseLock = new();

    /// <summary>Gets the usb.ids Version line if present in the embedded data.</summary>
    public static string? Version => _data.Value.Version;

    /// <summary>Gets the usb.ids Date line if present in the embedded data.</summary>
    public static string? Date => _data.Value.Date;

    /// <summary>Try to get the vendor name for a given vendor ID.</summary>
    public static bool TryGetVendorName(ushort vendorId, out string name)
    {
        var vendors = _data.Value.Vendors;
        var idx = BinarySearchVendor(vendors, vendorId);
        if (idx >= 0)
        {
            name = vendors[idx].Name;
            return true;
        }
        name = string.Empty;
        return false;
    }

    /// <summary>Try to get the product name for a given vendor + product ID.</summary>
    public static bool TryGetProductName(ushort vendorId, ushort productId, out string name)
    {
        var d = _data.Value;
        var vendors = d.Vendors;
        var idx = BinarySearchVendor(vendors, vendorId);
        if (idx >= 0)
        {
            var start = vendors[idx].DeviceStart;
            var count = vendors[idx].DeviceCount;
            if (count > 0)
            {
                var devices = d.Devices;
                var deviceIdx = BinarySearchProduct(devices, start, count, productId);
                if (deviceIdx >= 0)
                {
                    name = devices[deviceIdx].Name;
                    return true;
                }
            }
        }
        name = string.Empty;
        return false;
    }

    /// <summary>Try to resolve a vendor ID by vendor name (case-insensitive). Built lazily.</summary>
    public static bool TryGetVendorIdByName(string vendorName, out ushort vendorId)
    {
        if (string.IsNullOrWhiteSpace(vendorName))
        {
            vendorId = 0;
            return false;
        }
        return _vendorNameToId.Value.TryGetValue(vendorName, out vendorId);
    }

    /// <summary>Try to resolve a product ID by name for a given vendor (case-insensitive). Built lazily per vendor.</summary>
    public static bool TryGetProductIdByName(ushort vendorId, string productName, out ushort productId)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            productId = 0;
            return false;
        }

        var d = _data.Value;
        var vendors = d.Vendors;
        var idx = BinarySearchVendor(vendors, vendorId);
        if (idx < 0)
        {
            productId = 0;
            return false;
        }

        Dictionary<string, ushort> nameMap;
        lock (_deviceReverseLock)
        {
            if (!_deviceNameToIdByVendor.TryGetValue(vendorId, out nameMap!))
            {
                nameMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
                var start = vendors[idx].DeviceStart;
                var count = vendors[idx].DeviceCount;
                var devices = d.Devices;
                for (var i = 0; i < count; i++)
                {
                    var entry = devices[start + i];
                    nameMap.TryAdd(entry.Name, entry.ProductId); // prefer first occurrence if duplicates exist
                }
                _deviceNameToIdByVendor[vendorId] = nameMap;
            }
        }
        return nameMap.TryGetValue(productName, out productId);
    }

    /// <summary>Enumerate all vendor IDs and names (ordered as in usb.ids).</summary>
    public static IEnumerable<(ushort VendorId, string Name)> GetVendors()
    {
        var vendors = _data.Value.Vendors;
        for (int i = 0; i < vendors.Length; i++)
        {
            var v = vendors[i];
            yield return (v.Id, v.Name);
        }
    }

    /// <summary>Enumerate all (productId, name) for a given vendor ID (ordered as in usb.ids). Returns empty if vendor not found.</summary>
    public static IEnumerable<(ushort ProductId, string Name)> GetProducts(ushort vendorId)
    {
        var d = _data.Value;
        var vendors = d.Vendors;
        var idx = BinarySearchVendor(vendors, vendorId);
        if (idx < 0)
            yield break;

        var start = vendors[idx].DeviceStart;
        var count = vendors[idx].DeviceCount;
        var devices = d.Devices;
        for (var i = 0; i < count; i++)
        {
            var entry = devices[start + i];
            yield return (entry.ProductId, entry.Name);
        }
    }

    // Internal data parsed from usb.ids
    private sealed class Data
    {
        public readonly VendorEntry[] Vendors;
        public readonly DeviceEntry[] Devices;
        public readonly string? Version;
        public readonly string? Date;

        public Data(VendorEntry[] vendors, DeviceEntry[] devices, string? version, string? date)
        {
            Vendors = vendors;
            Devices = devices;
            Version = version;
            Date = date;
        }
    }

    private readonly record struct VendorEntry(ushort Id, string Name, int DeviceStart, int DeviceCount);
    private readonly record struct DeviceEntry(ushort ProductId, string Name);

    private static Data CreateData()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName)
                         ?? throw new InvalidOperationException($"Embedded resource '{EmbeddedResourceName}' not found.");
        using var reader = new StreamReader(stream);

        var vendorBuilders = new List<VendorBuilder>(capacity: 4096);
        var devices = new List<DeviceEntry>(capacity: 16384);

        string? version = null;
        string? date = null;

        VendorBuilder? currentVendor = null;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
                continue;

            // Header info
            if (line[0] == '#')
            {
                // Capture Version/Date if present
                if (version is null && line.StartsWith("# Version:", StringComparison.Ordinal))
                    version = line.AsSpan(1).Trim().ToString();
                else if (date is null && line.StartsWith("# Date:", StringComparison.Ordinal))
                    date = line.AsSpan(1).Trim().ToString();
                continue;
            }

            // Device line: starts with a single tab, not two
            if (line[0] == '\t')
            {
                if (line.Length >= 2 && line[1] == '\t')
                {
                    // Interface/class info – ignore for this library
                    continue;
                }

                if (currentVendor is null)
                    continue; // malformed order

                var span = line.AsSpan(1).TrimStart();
                if (!TryParseHexId(span, out var productId, out var nameSpan))
                    continue;

                var productName = nameSpan.Trim().ToString();
                devices.Add(new DeviceEntry(productId, productName));
                continue;
            }

            // Vendor line: must start with 4 hex chars and a space/tab
            if (IsHex(line[0]) && line.Length >= 6)
            {
                var span = line.AsSpan();
                if (!TryParseHexId(span, out var vendorId, out var nameSpan))
                    continue;

                var vendorName = nameSpan.Trim().ToString();

                // finalize previous vendor's device count
                if (currentVendor is VendorBuilder vbPrev)
                {
                    var finalized = vbPrev with { DeviceCount = devices.Count - vbPrev.DeviceStart };
                    vendorBuilders[^1] = finalized;
                }

                var vb = new VendorBuilder(vendorId, vendorName, devices.Count, 0);
                vendorBuilders.Add(vb);
                currentVendor = vb;
                continue;
            }
        }

        // finalize last vendor
        if (currentVendor is VendorBuilder vbLast && vendorBuilders.Count > 0)
        {
            vendorBuilders[^1] = vbLast with { DeviceCount = devices.Count - vbLast.DeviceStart };
        }

        // Convert to arrays. usb.ids is sorted; keep order for binary search.
        var vendorsArr = new VendorEntry[vendorBuilders.Count];
        for (int i = 0; i < vendorBuilders.Count; i++)
        {
            var vb = vendorBuilders[i];
            vendorsArr[i] = new VendorEntry(vb.Id, vb.Name, vb.DeviceStart, vb.DeviceCount);
        }

        var devicesArr = devices.ToArray();

        return new Data(vendorsArr, devicesArr, version, date);
    }

    private static int BinarySearchVendor(ReadOnlySpan<VendorEntry> vendors, ushort vendorId)
    {
        int lo = 0, hi = vendors.Length - 1;
        while (lo <= hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            var cmp = vendors[mid].Id.CompareTo(vendorId);
            if (cmp == 0) return mid;
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        return -1;
    }

    private static int BinarySearchProduct(ReadOnlySpan<DeviceEntry> devices, int start, int count, ushort productId)
    {
        int lo = start, hi = start + count - 1;
        while (lo <= hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            var cmp = devices[mid].ProductId.CompareTo(productId);
            if (cmp == 0) return mid;
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        return -1;
    }

    private static bool TryParseHexId(ReadOnlySpan<char> line, out ushort id, out ReadOnlySpan<char> name)
    {
        // Expect: "hhhh  name..." or "hhhh\tname..."
        id = 0;
        name = default;

        int i = 0;
        // skip leading whitespace
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        if (i + 4 > line.Length) return false;

        var hexSpan = line.Slice(i, 4);
        if (!IsHex(hexSpan[0]) || !IsHex(hexSpan[1]) || !IsHex(hexSpan[2]) || !IsHex(hexSpan[3]))
            return false;

        if (!ushort.TryParse(hexSpan, System.Globalization.NumberStyles.HexNumber, null, out id))
            return false;

        i += 4;
        // skip whitespace to name
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        name = i < line.Length ? line.Slice(i) : ReadOnlySpan<char>.Empty;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHex(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private readonly record struct VendorBuilder(ushort Id, string Name, int DeviceStart, int DeviceCount);
}
