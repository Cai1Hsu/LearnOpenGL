using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

class Texture
{
    public uint Id { get; private set; }

    /// <summary>
    /// Remember to dispose image when the texture is created.
    /// </summary>
    public Texture(Image<Rgba32> image, GL gl)
    {
        Id = gl.GenTexture();
        gl.ActiveTexture(TextureUnit.Texture0);

        gl.BindTexture(TextureTarget.Texture2D, Id);
        {
            unsafe
            {
                if (image.DangerousTryGetSinglePixelMemory(out var single))
                {
                    fixed (void* data = single.Span)    
                        texImage(data);
                }
                // Not Contiguous rows
                else
                {
                    image.ProcessPixelRows(accessor =>
                    {
                        Rgba32* ContiguousPixels = stackalloc Rgba32[accessor.Width * accessor.Height];

                        for (int y = 0; y < accessor.Height; y++)
                        {
                            var row = accessor.GetRowSpan(y);

                            for (int x = 0; x < accessor.Width; x++)
                                ContiguousPixels[y * accessor.Height + x] = row[x];
                        }

                        texImage(ContiguousPixels);
                    });
                }

                void texImage(void* data)
                {
                    // allocate storage for the texture.
                    gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                        (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);

                    gl.GenerateMipmap(TextureTarget.Texture2D);
                }
            }

        }
    }
}
