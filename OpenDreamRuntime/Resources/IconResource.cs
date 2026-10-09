using System.IO;
using OpenDreamShared.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenDreamRuntime.Resources;

public sealed class IconResource : DreamResource {
    // Same formats DMIParser accepts; keeps other decoders (e.g. vulnerable TIFF) away from untrusted data.
    private static readonly DecoderOptions DecoderOptions = new() {
        Configuration = new Configuration(new PngConfigurationModule(), new BmpConfigurationModule())
    };

    public Image<Rgba32> Texture => _texture ??= Image.Load<Rgba32>(DecoderOptions, ResourceData);
    public DMIParser.ParsedDMIDescription DMI => _dmi ??= DMIParser.ParseDMI(new MemoryStream(ResourceData));

    private Image<Rgba32>? _texture;
    private DMIParser.ParsedDMIDescription? _dmi;

    public IconResource(int id, string? filePath, string? resourcePath) : base(id, filePath, resourcePath) { }

    public IconResource(int id, byte[] data) : base(id, data) { }

    public IconResource(int id, byte[] data, Image<Rgba32> texture, DMIParser.ParsedDMIDescription dmi) :
        base(id, data) {
        _texture = texture;
        _dmi = dmi;
    }
}
