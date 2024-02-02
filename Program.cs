using System.Diagnostics;
using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

// internally call SDL.Init()
Sdl sdl = Sdl.GetApi();

Vector2D<int> size = new(1024, 768);

string vs = @"
#version 330 core

layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexUV;

out vec2 TexUV;

void main()
{
    gl_Position = vec4(aPos, 0.0f, 1.0f);
    TexUV = vec2(aTexUV);
}
";

string fs = @"
#version 330 core

in vec2 TexUV;
out vec4 ofColor;

uniform sampler2D uTexture;

void main()
{
    ofColor = texture(uTexture, TexUV);
    // ofColor = vec4(1.0f);
}
";

sdl.GLSetAttribute(GLattr.ContextMajorVersion, 3);
sdl.GLSetAttribute(GLattr.ContextMinorVersion, 3);
sdl.GLSetAttribute(GLattr.ContextProfileMask, (int)GLprofile.Core);

unsafe
{
    var window = sdl.CreateWindow("Window",
        (int)Sdl.WindowposUndefinedMask, (int)Sdl.WindowposUndefinedMask,
        size.X, size.Y,
        (uint)WindowFlags.Opengl);

    var context = sdl.GLCreateContext(window);

    GL gl = GL.GetApi(symbol => (nint)sdl.GLGetProcAddress(symbol));

    sdl.GLMakeCurrent(window, context);

    gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);

    float[] vertices =
    [
        // vertex     // texture
         0.9f,  0.9f, 1.0f, 1.0f,
         0.9f, -0.9f, 1.0f, 0.0f,
        -0.9f, -0.9f, 0.0f, 0.0f,
        -0.9f,  0.9f, 0.0f, 1.0f,
    ];

    uint[] indices =
    [
        0, 1, 2,
        0, 2, 3,
    ];

    Shader shader = new(vs, fs, gl);

    uint vao = gl.GenVertexArray();
    uint vbo = gl.GenBuffer();
    uint ebo = gl.GenBuffer();

    gl.BindVertexArray(vao);
    {
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (void* ptr = &vertices[0])
            gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)(sizeof(float) * vertices.Length), ptr, BufferUsageARB.StaticDraw);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (void* ptr = &indices[0])
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (uint)(sizeof(uint) * indices.Length), ptr, BufferUsageARB.StaticDraw);

        gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 4 * sizeof(float), (void*)null);
        gl.EnableVertexAttribArray(0);

        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        gl.EnableVertexAttribArray(1);

        // vbo is bound to vertex attribute's buffer, so we can safely unbind it.
        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
    }
    gl.BindVertexArray(0);
    // must do this when vao is not active.
    gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

    gl.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Line);
    gl.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Fill);

    Texture texture = null!;
    using (var img = Image.Load<Rgba32>("container.jpg"))
    {
        texture = new(img, gl);
    }

    Debug.Assert(texture != null);

    void Draw()
    {
        gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        gl.UseProgram(shader.Program);

        gl.BindTexture(TextureTarget.Texture2D, texture.Id);

        gl.BindVertexArray(vao);
        {
            // Since we've bound ebo in vao, we don't need to pass a indices array.
            gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Length, DrawElementsType.UnsignedInt, null);
        }
        gl.BindVertexArray(0);
    }

    bool running = true;

    Console.CancelKeyPress += (_, _) => running = false;

    while (running)
    {
        sdl.PumpEvents();

        Draw();

        sdl.GLSwapWindow(window);
    }
}

class Shader
{
    public uint Program;

    public Shader(string vShader, string fShader, GL gl)
    {
        uint vs = gl.CreateShader(GLEnum.VertexShader);
        gl.ShaderSource(vs, vShader);
        gl.CompileShader(vs);

        assertCreated("Vertex", vs);

        uint fs = gl.CreateShader(GLEnum.FragmentShader);
        gl.ShaderSource(fs, fShader);
        gl.CompileShader(fs);

        assertCreated("Fragment", fs);

        Program = gl.CreateProgram();
        gl.AttachShader(Program, vs);
        gl.AttachShader(Program, fs);
        gl.LinkProgram(Program);

        gl.GetProgram(Program, GLEnum.LinkStatus, out var link);

        if (link != 0)
        {
            gl.DetachShader(Program, vs);
            gl.DetachShader(Program, fs);

            gl.DeleteShader(vs);
            gl.DeleteShader(fs);

            return;
        }

        var link_failure = gl.GetProgramInfoLog(Program);

        throw new Exception($"Failed to link program, reason: \n{link_failure}");

        void assertCreated(string name, uint handle)
        {
            gl.GetShader(handle, GLEnum.CompileStatus, out var status);

            if (status != 0)
                return;

            gl.DeleteShader(handle);

            var reason = gl.GetShaderInfoLog(handle);

            throw new Exception($"Failed to compile {name} shader. Reason: \n{reason}");
        }
    }
}
