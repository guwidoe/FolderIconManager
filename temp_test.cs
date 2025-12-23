using FolderIconManager.Core.Services;

var imageresPath = @"C:\Windows\System32\imageres.dll";
using var extractor = new IconExtractor(imageresPath);

Console.WriteLine($"Total icons: {extractor.IconCount}");
Console.WriteLine($"First 10 resource IDs: {string.Join(", ", extractor.ResourceIds.Take(10))}");
Console.WriteLine($"Resource IDs around 266:");
Console.WriteLine($"  Has ID 266: {extractor.HasResourceId(266)}");
Console.WriteLine($"  Has ID 265: {extractor.HasResourceId(265)}");
Console.WriteLine($"  Has ID 267: {extractor.HasResourceId(267)}");

// Check what index 266 would give us
if (extractor.IconCount > 266)
{
    Console.WriteLine($"  ResourceId at index 266: {extractor.ResourceIds[266]}");
}
Console.WriteLine($"  Index of ResourceId 266 (if exists): {extractor.ResourceIds.ToList().IndexOf(266)}");
