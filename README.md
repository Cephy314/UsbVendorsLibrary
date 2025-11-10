# UsbVendorsLibrary

Fast, low‑memory lookups of USB vendor and product names sourced from `usb.ids`.

## Features
- O( log n ) lookups via binary search over compact arrays
- Very small memory footprint (no per‑vendor dictionary overhead)
- Primary API: vendorId -> name, (vendorId, productId) -> name
- Optional reverse lookups (name -> id) built lazily on first use

## Basic Usage
```csharp
using UsbVendorsLibrary;

// Vendor name by ID
if (UsbIds.TryGetVendorName(0x03E7, out var vendor))
{
    Console.WriteLine(vendor);
}

// Product name by Vendor + Product ID
if (UsbIds.TryGetProductName(0x03E7, 0x2150, out var product))
{
    Console.WriteLine(product);
}

// Reverse lookups (lazy index build)
if (UsbIds.TryGetVendorIdByName("Intel", out var vendorId))
{
    Console.WriteLine($"Vendor 0x{vendorId:X4}");
}
if (UsbIds.TryGetProductIdByName(0x03E7, "Myriad VPU [Movidius Neural Compute Stick]", out var productId))
{
    Console.WriteLine($"Product 0x{productId:X4}");
}

Console.WriteLine($"Data version: {UsbIds.Version} ({UsbIds.Date})");
```

## Data Source
- Embedded copy of `usb.ids` (Linux USB IDs) from [**Linux-usb.org**](http://www.linux-usb.org/). The file is also packed as `contentFiles/any/any/usb.ids` for reference. 

## Notes
- Reverse lookup maps are built on demand to keep memory usage low.
- Interface/class code sections in `usb.ids` are intentionally ignored; only vendors and products are indexed.

