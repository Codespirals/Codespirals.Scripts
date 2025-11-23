using QRCoder;
using System;
using System.IO;

namespace QR
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string saveFilePath = $"{Directory.GetDirectoryRoot(Directory.GetCurrentDirectory())}\\Codes";
            Directory.CreateDirectory(saveFilePath);

            while (true)
            {
                Console.WriteLine("Enter the URL you want the QR code to link to:");
                // What you want to encode
                var payload = Console.ReadLine() ?? throw new Exception("No url set.");

                // ECC: L (low), M (medium), Q (quartile), H (high)
                var generator = new QRCodeGenerator();
                QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.H, forceUtf8: true);

                Console.WriteLine("Enter a file name:");
                var name = Console.ReadLine();

                Console.WriteLine("Save as png or svg?");

                var choice = Console.ReadLine();
                if (choice == "svg")
                    SaveAsSvg(saveFilePath, name, data);
                else
                    SaveAsPng(saveFilePath, name, data);

                Console.WriteLine("Another? y/n");
                var k = Console.ReadKey();
                Console.WriteLine();
                if (k.KeyChar == 'n')
                {
                    break;
                }
            }
        }
        
        static void SaveAsPng(string saveFilePath, string name, QRCodeData data)
        {
            // PNG bytes (no System.Drawing required)
            var pngQr = new PngByteQRCode(data);
            byte[] pngBytes = pngQr.GetGraphic(pixelsPerModule: 10); // scale

            File.WriteAllBytes($"{saveFilePath}/{name}.png", pngBytes);
            System.Console.WriteLine($"Saved {name}.png");
        }
        static void SaveAsSvg(string saveFilePath, string name, QRCodeData data)
        {
            var svgQr = new SvgQRCode(data);
            string svg = svgQr.GetGraphic(pixelsPerModule: 10);
            File.WriteAllText($"{saveFilePath}/{name}.svg", svg);
            System.Console.WriteLine($"Saved {name}.svg");
        }
    }
}
