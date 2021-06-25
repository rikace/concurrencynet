using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Helpers
{
    public class ImageProcessingHelpers
    {
        public struct ImageInfo
        {
            public string Name { get; set; }
            public string Source { get; set; }
            public string Destination { get; set; }
            public Image<Rgba32> Image { get; set; }
        }

        private readonly string destination;

        public ImageProcessingHelpers(string destination)
        {
            this.destination = destination;


            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            LoadImage_Step1 = async path =>
            {
                Console.WriteLine($"Loading Image {Path.GetFileName(path)}...");
                var image = await LoadImageAsync(path); // Image.Load<Rgba32>(path);
                return new ImageInfo
                {
                    Name = Path.GetFileName(path),
                    Source = path,
                    Destination = this.destination,
                    Image = image
                };
            };

            ScaleImage_Step2 = async imageInfo =>
            {
                Console.WriteLine($"Scaling Image {Path.GetFileName(imageInfo.Name)}...");
                var scale = 200;
                var image = imageInfo.Image;
                var resizedImage = ImageHandler.Resize(image, scale, scale);
                imageInfo.Image = resizedImage;
                return imageInfo;
            };

            ConvertTo3D_Step3 = async imageInfo =>
            {
                Console.WriteLine($"Converting to 3D Image {Path.GetFileName(imageInfo.Name)}...");
                var image = imageInfo.Image;
                var converted3DImage = ImageHandler.ConvertTo3D(image);
                imageInfo.Image = converted3DImage;
                return imageInfo;
            };

            SaveImage_Step4 = async imageInfo =>
            {
                var filePathDestination = Path.Combine(imageInfo.Destination, imageInfo.Name);
                Console.WriteLine($"Saving Image {filePathDestination}...");

                imageInfo.Image.Save(filePathDestination);
                imageInfo.Image.Dispose();
                return filePathDestination;
            };
        }

        public Func<string, Task<ImageInfo>> LoadImage_Step1 { get; }

        public Func<ImageInfo, Task<ImageInfo>> ScaleImage_Step2 { get; }

        public Func<ImageInfo, Task<ImageInfo>> ConvertTo3D_Step3 { get; }

        public Func<ImageInfo, Task<string>> SaveImage_Step4 { get; }

        public static async Task<Image<Rgba32>> LoadImageAsync(string filePath)
        {
            byte[] result;
            using (FileStream sourceStream =
                new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, true))
            {
                result = new byte[sourceStream.Length];
                await sourceStream.ReadAsync(result, 0, (int) sourceStream.Length);
            }

            return Image.Load<Rgba32>(result);
        }

        public static Image<Rgba32> LoadImage(string filePath)
        {
            byte[] result;
            using (FileStream sourceStream = File.Open(filePath, FileMode.Open))
            {
                result = new byte[sourceStream.Length];
                sourceStream.Read(result, 0, (int) sourceStream.Length);
            }

            return Image.Load<Rgba32>(result);
        }
    }
}
