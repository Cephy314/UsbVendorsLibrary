using System;
using UsbVendorsLibrary;

int failures = 0;
void Check(bool condition, string message)
{
    if (!condition)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {message}");
        Console.ResetColor();
        failures++;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[ OK ] {message}");
        Console.ResetColor();
    }
}

try
{
    Console.WriteLine($"usb.ids Version: {UsbIds.Version ?? "<unknown>"}");
    Console.WriteLine($"usb.ids Date:    {UsbIds.Date ?? "<unknown>"}\n");

    // Basic vendor lookup
    Check(UsbIds.TryGetVendorName(0x03E7, out var vendorName), "Vendor 0x03E7 should exist");
    if (vendorName is not null)
        Console.WriteLine($"Vendor 0x03E7 = '{vendorName}'");

    // Basic product lookup under vendor 0x03E7
    Check(UsbIds.TryGetProductName(0x03E7, 0x2150, out var productName), "Product 0x2150 under vendor 0x03E7 should exist");
    if (productName is not null)
        Console.WriteLine($"Product 0x2150 = '{productName}'");

    // Reverse lookups using names obtained from forward lookups for robustness
    if (!string.IsNullOrWhiteSpace(vendorName))
    {
        Check(UsbIds.TryGetVendorIdByName(vendorName, out var vendorIdBack) && vendorIdBack == 0x03E7,
            "Reverse vendor lookup should match 0x03E7");
    }
    if (!string.IsNullOrWhiteSpace(productName))
    {
        Check(UsbIds.TryGetProductIdByName(0x03E7, productName, out var productIdBack) && productIdBack == 0x2150,
            "Reverse product lookup should match 0x2150");
    }

    // Enumeration sanity checks
    var anyVendor = false;
    foreach (var (vendorId, name) in UsbIds.GetVendors())
    {
        anyVendor = true;
        // probe first vendor's product list briefly
        foreach (var _ in UsbIds.GetProducts(vendorId))
        {
            break;
        }
        break;
    }
    Check(anyVendor, "GetVendors should enumerate at least one vendor");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Unhandled exception: {ex}");
    Console.ResetColor();
    failures++;
}

if (failures > 0)
{
    Console.WriteLine($"\nSmoke test completed with {failures} failure(s).");
    return failures;
}
else
{
    Console.WriteLine("\nSmoke test passed.");
    return 0;
}

