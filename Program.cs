using System;
using System.IO;

namespace IconExtractor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Check arguments length
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Invalid arguments. Please provide the icon reference in the format: path,index");
                return;
            }

            // Find the last comma in the input string
            int lastCommaIndex = args[0].LastIndexOf(',');
            if (lastCommaIndex == -1)
            {
                Console.Error.WriteLine("Invalid icon reference format. Please use format: path,index");
                return;
            }

            // Split the string into path and index parts
            var path = args[0].Substring(0, lastCommaIndex);
            var indexPart = args[0].Substring(lastCommaIndex + 1);

            if (!int.TryParse(indexPart, out int index))
            {
                Console.Error.WriteLine("Invalid index value in icon reference");
                return;
            }

            ExtractIcon(path, index);
        }

        static void ExtractIcon(string path, int index)
        {
            try
            {
                // Create an instance of IconExtractor
                var iconExtractor = new TsudaKageyu.IconExtractor(path);
                var icon = iconExtractor.GetIcon(index);
                if (icon == null)
                {
                    Console.Error.WriteLine("No icons were extracted. Please ensure the path and index are correct.");
                    return;
                }

                // Save the extracted icon to a file
                var iconPath = Path.Combine(Environment.CurrentDirectory, $"icon_{index}.ico");
                using (var fileStream = new FileStream(iconPath, FileMode.Create))
                {
                    icon.Save(fileStream);
                }
                Console.WriteLine($"Icon extracted to: {iconPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error extracting icon: {ex.Message}");
            }
        }
    }
}