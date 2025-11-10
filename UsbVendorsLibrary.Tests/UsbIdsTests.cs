using System;
using System.Globalization;
using System.Linq;
using UsbVendorsLibrary;
using Xunit;

namespace UsbVendorsLibrary.Tests;

public class UsbIdsTests
{
    [Fact]
    public void TryGetVendorName_ReturnsExpectedName_ForKnownVendor()
    {
        var found = UsbIds.TryGetVendorName(0x046d, out var name);

        Assert.True(found);
        Assert.Equal("Logitech, Inc.", name);
    }

    [Fact]
    public void TryGetVendorName_ReturnsFalse_ForUnknownVendor()
    {
        var found = UsbIds.TryGetVendorName(0x0000, out var name);

        Assert.False(found);
        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public void TryGetProductName_ReturnsExpectedName_ForKnownVendorAndProduct()
    {
        var found = UsbIds.TryGetProductName(0x046d, 0x0802, out var name);

        Assert.True(found);
        Assert.Equal("Webcam C200", name);
    }

    [Fact]
    public void TryGetProductName_ReturnsFalse_ForUnknownProduct()
    {
        var found = UsbIds.TryGetProductName(0x046d, 0xFFFF, out var name);

        Assert.False(found);
        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public void TryGetProductName_ReturnsFalse_ForUnknownVendor()
    {
        var found = UsbIds.TryGetProductName(0xFFFF, 0x0001, out var name);

        Assert.False(found);
        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public void TryGetVendorIdByName_IsCaseInsensitive()
    {
        var found = UsbIds.TryGetVendorIdByName("logitech, inc.", out var vendorId);

        Assert.True(found);
        Assert.Equal(0x046d, vendorId);
    }

    [Fact]
    public void TryGetVendorIdByName_ReturnsFalse_ForUnknownName()
    {
        var found = UsbIds.TryGetVendorIdByName("Not A Real Vendor", out var vendorId);

        Assert.False(found);
        Assert.Equal(0, vendorId);
    }

    [Fact]
    public void TryGetProductIdByName_IsCaseInsensitive()
    {
        var found = UsbIds.TryGetProductIdByName(0x046d, "webcam c200", out var productId);

        Assert.True(found);
        Assert.Equal(0x0802, productId);
    }

    [Fact]
    public void TryGetProductIdByName_ReturnsFalse_ForUnknownVendor()
    {
        var found = UsbIds.TryGetProductIdByName(0xFFFF, "Whatever", out var productId);

        Assert.False(found);
        Assert.Equal(0, productId);
    }

    [Fact]
    public void TryGetProductIdByName_ReturnsFalse_ForUnknownProductName()
    {
        var found = UsbIds.TryGetProductIdByName(0x046d, "Definitely Not Real", out var productId);

        Assert.False(found);
        Assert.Equal(0, productId);
    }

    [Fact]
    public void GetVendors_ReturnsSortedResults()
    {
        var vendors = UsbIds.GetVendors().ToArray();

        Assert.NotEmpty(vendors);
        Assert.Equal(vendors.Select(v => v.VendorId).OrderBy(v => v), vendors.Select(v => v.VendorId));
    }

    [Fact]
    public void GetProducts_ReturnsKnownProduct()
    {
        var products = UsbIds.GetProducts(0x046d).ToArray();

        Assert.Contains(((ushort)0x0802, "Webcam C200"), products);
    }

    [Fact]
    public void GetProducts_ReturnsEmpty_ForVendorWithoutProducts()
    {
        var products = UsbIds.GetProducts(0x0003).ToArray();

        Assert.Empty(products);
    }

    [Fact]
    public void TryGetProductIdByName_CachesPerVendor()
    {
        // First call should populate the reverse lookup cache for the vendor.
        var firstCall = UsbIds.TryGetProductIdByName(0x046d, "Webcam C200", out var productIdFirst);
        var secondCall = UsbIds.TryGetProductIdByName(0x046d, "Webcam C200", out var productIdSecond);

        Assert.True(firstCall);
        Assert.True(secondCall);
        Assert.Equal(productIdFirst, productIdSecond);
        Assert.Equal(0x0802, productIdFirst);
    }

    [Fact]
    public void VersionAndDate_AreParsedFromHeader()
    {
        Assert.False(string.IsNullOrWhiteSpace(UsbIds.Version));
        Assert.StartsWith("Version:", UsbIds.Version, StringComparison.Ordinal);
        var versionValue = UsbIds.Version["Version:".Length..].Trim();
        Assert.Matches(@"^\d{4}\.\d{2}\.\d{2}$", versionValue);

        Assert.False(string.IsNullOrWhiteSpace(UsbIds.Date));
        Assert.StartsWith("Date:", UsbIds.Date, StringComparison.Ordinal);
        var dateValue = UsbIds.Date["Date:".Length..].Trim();
        Assert.True(DateTime.TryParseExact(dateValue, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _));
    }
}
