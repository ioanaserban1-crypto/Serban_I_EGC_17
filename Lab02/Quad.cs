using OpenTK.Graphics.OpenGL4;

namespace Egc;

/// <summary>
/// PROBLEMA 6: dreptunghi din 4 vârfuri (VBO) și 6 indici (EBO).
/// Este clasa Triangle copiată; diferă doar datele.
/// </summary>
public sealed class Quad : IDisposable
{
    private const int FloatsPerVertex = 6;   // x, y, z, r, g, b

    public float[] Vertices { get; } = new float[]
    {
        //  poziție (x, y, z)      culoare (r, g, b)
        -0.5f, -0.5f, 0.0f,      1.0f, 0.0f, 0.0f,   // vârful 0: stânga-jos, roșu
         0.5f, -0.5f, 0.0f,      0.0f, 1.0f, 0.0f,   // vârful 1: dreapta-jos, verde
         0.5f,  0.5f, 0.0f,      0.0f, 0.0f, 1.0f,   // vârful 2: dreapta-sus, albastru
        -0.5f,  0.5f, 0.0f,      1.0f, 1.0f, 0.0f    // vârful 3: stânga-sus, galben
    };

    // Două triunghiuri, ambele în sens trigonometric (CCW). Diagonala comună: 0–2.
    private readonly uint[] _indices = new uint[] { 0, 1, 2, 2, 3, 0 };

    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;

    public Quad()
    {
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);                    // tot ce urmează se înregistrează în VAO

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, Vertices.Length * sizeof(float),
                      Vertices, BufferUsageHint.StaticDraw);   // nu-l modificăm => StaticDraw

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);  // legarea EBO e reținută de VAO
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint),
                      _indices, BufferUsageHint.StaticDraw);

        int stride = FloatsPerVertex * sizeof(float);          // 24 de octeți
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);                 // poziția
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float)); // culoarea
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);                       // întâi dezlegăm VAO (NU EBO înaintea lui!)
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void Draw()
    {
        GL.BindVertexArray(_vao);
        // count = 6 INDICI (nu 4 vârfuri): 6 / 3 = 2 triunghiuri
        GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
    }
}