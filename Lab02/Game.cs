using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// GameWindow apelează automat metodele On... de mai jos, în ordinea descrisă în laborator.
/// </summary>
public class Game : GameWindow
{
    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private Triangle _triangle = null!;

    private Quad _quad = null!;      // P6: dreptunghiul cu EBO
    private bool _showQuad;          // P6: tasta Q comută triunghi / dreptunghi

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private Color4 _clearColor = new(0.0f, 0.1f, 0.3f, 1.0f);   // culoarea de fundal
    private Color4 _tint = new(1.0f, 0.3f, 0.3f, 1.0f);   // nuanță roșiatică
    //private Vector3 _offset = Vector3.Zero;                       // deplasarea triunghiului
    private Vector3 _offset = new Vector3(0.3f, 0.2f, 0.0f);   // P4a: deplasare fixă

    // Contoare pentru exemplul cu bucla principală (afișate în titlul ferestrei).
    private readonly string _baseTitle;
    private double _elapsed;     // timpul total scurs (secunde)
    private double _secondTimer; // cronometru pentru actualizarea titlului o dată pe secundă
    private int _updates;        // câte OnUpdateFrame în ultima secundă
    private int _frames;         // câte OnRenderFrame în ultima secundă

    private bool _vertex2White;   // P2 e) starea culorii vârfului 2
    private bool _wireframe;   // P3 b) false = plin, true = contur


    public Game(int width, int height, string title)
        : base(GameWindowSettings.Default,
               new NativeWindowSettings
               {
                   ClientSize = new Vector2i(width, height),
                   Title = title,
                   APIVersion = new Version(3, 3),          // cerem OpenGL 3.3 ...
                   Profile = ContextProfile.Core,           // ... profil Core (fără modul imediat)
                   Flags = ContextFlags.ForwardCompatible
               })
    {
        _baseTitle = title;
        VSync = VSyncMode.Off;   // swap-ul bufferelor așteaptă reîmprospătarea monitorului
    }

    /// <summary>Apelată O SINGURĂ DATĂ, după crearea ferestrei și a contextului OpenGL.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        // Informații despre driverul și placa grafică folosite (vezi Problema 1 din Laboratorul 01).
        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Vendor  : {GL.GetString(StringName.Vendor)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");
        _triangle = new Triangle();

        _quad = new Quad();              // se creează în OnLoad: are nevoie de contextul OpenGL
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);
    }

    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        // args.Time = secundele scurse de la apelul anterior (delta time).
        _elapsed += args.Time;
        _updates++;

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // --- TODO (P1–P5): codul vostru de logică se adaugă aici ---
        // P1 b) Fundal animat: fiecare componentă oscilează între 0 și 1,
        // cu defazaje de 0, 2π/3 și 4π/3 radiani (0°, 120°, 240°).
        float t = (float)_elapsed;
        _clearColor = new Color4(
            0.5f + 0.5f * MathF.Sin(t),            // R
            0.5f + 0.5f * MathF.Sin(t + 2.094f),   // G (defazat cu 2π/3)
            0.5f + 0.5f * MathF.Sin(t + 4.189f),   // B (defazat cu 4π/3)
            1.0f);                                 // A (opac)

        // P2 b) Nuanța triunghiului din tastatură
        if (KeyboardState.IsKeyPressed(Keys.D1)) _tint = new Color4(1f, 0f, 0f, 1f);   // roșu
        if (KeyboardState.IsKeyPressed(Keys.D2)) _tint = new Color4(0f, 1f, 0f, 1f);   // verde
        if (KeyboardState.IsKeyPressed(Keys.D3)) _tint = new Color4(0f, 0f, 1f, 1f);   // albastru
        if (KeyboardState.IsKeyPressed(Keys.D0)) _tint = Color4.White;                 // revenire

        // P2 e) Tasta C comută culoarea vârfului 2 (sus) între alb și magenta
        if (KeyboardState.IsKeyPressed(Keys.C))
        {
            _vertex2White = !_vertex2White;

            if (_vertex2White)
                _triangle.SetColor(2, 1.0f, 1.0f, 1.0f);   // alb
            else
                _triangle.SetColor(2, 1.0f, 0.0f, 1.0f);   // magenta

            _triangle.UpdateVertices();                    // RAM -> VBO (altfel nu se vede nimic!)
        }

        // P3 b) Tasta F comută între contur și plin
        if (KeyboardState.IsKeyPressed(Keys.F))
        {
            _wireframe = !_wireframe;
        }

        // P4b: triunghiul oscilează stânga-dreapta, între -0.5 și 0.5
        _offset.X = 0.5f * MathF.Sin((float)_elapsed);

        // P4c: deplasare cu săgețile, viteză constantă de 1 unitate NDC pe secundă
        float dt = (float)args.Time;      // Δt: secundele de la cadrul anterior
        const float speed = 1.0f;         // unități NDC pe secundă
        if (KeyboardState.IsKeyDown(Keys.Left)) _offset.X -= speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) _offset.X += speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Down)) _offset.Y -= speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Up)) _offset.Y += speed * dt;

        // P5c: fiecare vârf se mișcă diferit
        _triangle.SetPosition(0, -0.5f + 0.2f * MathF.Sin(1.5f * t), -0.5f);   // vârful 0: stânga-dreapta
        _triangle.SetPosition(1, 0.5f, -0.5f + 0.2f * MathF.Sin(2.0f * t));   // vârful 1: sus-jos
        _triangle.SetPosition(2, 0.0f, 0.5f + 0.25f * MathF.Sin(3.3f * t));  // vârful 2: sus-jos, mai repede
        _triangle.UpdateVertices();

        // P6: tasta Q comută forma
        if (KeyboardState.IsKeyPressed(Keys.Q)) _showQuad = !_showQuad;


        // Exemplul bucla principală: o dată pe secundă afișăm în titlu câte
        // actualizări și câte cadre desenate au avut loc.
        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            Title = $"{_baseTitle} | update/s: {_updates} | render/s: {_frames}";
            _updates = 0;
            _frames = 0;
            _secondTimer -= 1.0;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer. GL.ClearColor SETEAZĂ starea (culoarea de ștergere),
        //    GL.Clear o FOLOSEȘTE. Aici o setăm la fiecare cadru pentru că vom putea s-o schimbăm (P1).
        GL.ClearColor(_clearColor.R, _clearColor.G, _clearColor.B, _clearColor.A);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Activăm programul shader și îi trimitem uniformele.
        _shader.Use();
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);
        _shader.SetVector3("uOffset", _offset);
        _shader.SetColor("uTint", _tint);

        // 3. Desenăm triunghiul.
        // P3 b) Setăm modul de rasterizare în funcție de starea aplicației
        //GL.PolygonMode(MaterialFace.FrontAndBack, _wireframe ? PolygonMode.Line : PolygonMode.Fill);
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
        // P6: desenăm forma aleasă
        if (_showQuad)
            _quad.Draw();
        else
            _triangle.Draw();

        // 4. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        _quad.Dispose();
        _triangle.Dispose();
        _shader.Dispose();

        base.OnUnload();
    }
}
