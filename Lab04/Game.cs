using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// Față de laboratorul 3: o scenă cu mai multe obiecte, fiecare cu matricea lui model,
/// o cameră cu matricele view și projection, tastatura și mouse-ul.
/// </summary>
public class Game : GameWindow
{
    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private readonly List<SceneObject> _scene = new();
    private readonly List<Transform2D> _initialTransforms = new();   // pentru tasta R

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private int _selected;            // indexul obiectului selectat în _scene (tasta Tab)
    private bool _wireframe;          // contur (true) sau plin (false)

    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;

    private Vector2 _pivot = Vector2.Zero;   // T4: pivotul ales cu clic dreapta

    private Mesh _satellite = null!;   // T5: forma satelitului
    private float _orbitAngle;         // T5: unghiul orbitei, în grade

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
        VSync = VSyncMode.On;   // swap-ul bufferelor așteaptă reîmprospătarea monitorului
    }

    /// <summary>Apelată O SINGURĂ DATĂ, după crearea ferestrei și a contextului OpenGL.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _camera.Resize(ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        // Scena: aceleași forme din laboratorul 3, acum plasate în lume prin Transform2D.
        // Depth (z) contează doar în perspectivă (tasta O, secțiunea 7.2).
        AddObject("pătrat", Shapes.Quad(),
                  new Transform2D { Position = new Vector2(-1.8f, -1.0f), Depth = -3.0f });
        AddObject("triunghi", Shapes.Triangle(),
          new Transform2D
          {
              Position = new Vector2(-1.2f, 1.0f),
              Rotation = 45,
              Scale = new Vector2(1.5f, 0.5f)
          });
        AddObject("hexagon", Shapes.RegularPolygon(6, 0.5f),
                  new Transform2D { Position = new Vector2(2.0f, 1.0f) });
        AddObject("cerc", Shapes.RegularPolygon(64, 0.5f),
                  new Transform2D { Position = new Vector2(0.8f, -0.6f), Depth = 2.0f });

        // T5: satelitul nu e în _scene, ca să nu poată fi selectat cu Tab
        _satellite = Shapes.Triangle();
    }

    /// <summary>Adaugă un obiect în scenă și reține transformarea lui inițială (pentru tasta R).</summary>
    private void AddObject(string name, Mesh mesh, Transform2D transform)
    {
        _scene.Add(new SceneObject(name, mesh, transform));
        _initialTransforms.Add(transform.Clone());
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);      // proiecția se adaptează la noul raport de aspect
    }

    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // --- Obiectul selectat: tastatura (secțiunea 8) ------------------------------

        float dt = (float)args.Time;
        SceneObject sel = _scene[_selected];

        // T2: cu Shift, mișcările sunt de 3 ori mai rapide
        float speed = KeyboardState.IsKeyDown(Keys.LeftShift) ? 3.0f : 1.0f;

        const float moveSpeed = 2.0f;   // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.Left)) sel.Transform.Position.X -= moveSpeed * speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) sel.Transform.Position.X += moveSpeed * speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Down)) sel.Transform.Position.Y -= moveSpeed * speed * dt;
        if (KeyboardState.IsKeyDown(Keys.Up)) sel.Transform.Position.Y += moveSpeed * speed * dt;

        // T2: Q = sens trigonometric, E = sens orar
        const float rotSpeed = 90.0f;   // grade pe secundă
        if (KeyboardState.IsKeyDown(Keys.Q)) sel.Transform.Rotation += rotSpeed * speed * dt;
        if (KeyboardState.IsKeyDown(Keys.E)) sel.Transform.Rotation -= rotSpeed * speed * dt;

        // T2: Z micșorează, X mărește, cu factorul 1,5 pe secundă
        float factor = MathF.Pow(1.5f, speed * dt);
        if (KeyboardState.IsKeyDown(Keys.Z)) sel.Transform.Scale /= factor;
        if (KeyboardState.IsKeyDown(Keys.X)) sel.Transform.Scale *= factor;
        sel.Transform.Scale.X = Math.Clamp(sel.Transform.Scale.X, 0.1f, 10.0f);
        sel.Transform.Scale.Y = Math.Clamp(sel.Transform.Scale.Y, 0.1f, 10.0f);

        // T2: scalarea se face prin înmulțire pentru că e un raport: la fiecare cadru mărimea
        // crește cu același procent, deci viteza pare aceeași la orice dimensiune.
        // Prin adunare, scara ar putea ajunge la 0 sau negativă, iar obiectul s-ar inversa.

        if (KeyboardState.IsKeyPressed(Keys.Tab)) _selected = (_selected + 1) % _scene.Count;
        if (KeyboardState.IsKeyPressed(Keys.O)) _camera.IsPerspective = !_camera.IsPerspective;
        if (KeyboardState.IsKeyPressed(Keys.F)) _wireframe = !_wireframe;
        if (KeyboardState.IsKeyPressed(Keys.R)) ResetScene();

        if (KeyboardState.IsKeyPressed(Keys.Tab) || KeyboardState.IsKeyPressed(Keys.O) ||
            KeyboardState.IsKeyPressed(Keys.R))
        {
            _titleDirty = true;
        }

        // --- Obiectul selectat: mouse-ul (secțiunea 9) -------------------------------

        // TODO (T3): la clic stânga, obiectul selectat se mută în punctul din lume aflat sub cursor
        //            (folosiți ScreenToWorld; Depth nu se schimbă)
        // T3: clic stânga mută obiectul selectat sub cursor (Depth rămâne neschimbat)
        if (MouseState.IsButtonPressed(MouseButton.Left))
        {
            sel.Transform.Position = ScreenToWorld(MouseState.Position);
        }

        // TODO (T4, facultativ): clic dreapta = pivot; tasta P ținută = rotație în jurul pivotului
        // T4: clic dreapta alege pivotul
        if (MouseState.IsButtonPressed(MouseButton.Right))
        {
            _pivot = ScreenToWorld(MouseState.Position);
        }

        // T4: cu P ținut, obiectul se rotește în jurul pivotului cu 90° pe secundă
        if (KeyboardState.IsKeyDown(Keys.P))
        {
            float angle = 90.0f * dt;
            float rad = MathHelper.DegreesToRadians(angle);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);

            // T4: se rotește poziția față de pivot ...
            Vector2 d = sel.Transform.Position - _pivot;
            sel.Transform.Position = _pivot + new Vector2(d.X * cos - d.Y * sin,
                                                          d.X * sin + d.Y * cos);

            // T4: ... iar unghiul propriu crește la fel, ca obiectul să privească mereu spre pivot
            sel.Transform.Rotation += angle;
        }

        // T5: o rotație completă la 4 secunde = 90° pe secundă
        _orbitAngle = (_orbitAngle + 90.0f * dt) % 360.0f;

        // --- Camera: tastatura și rotița (secțiunea 6.2) -----------------------------

        float camSpeed = 3.0f / _camera.Zoom;                    // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.Position.Y -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.W)) _camera.Position.Y += camSpeed * dt;

        _camera.Zoom *= MathF.Pow(1.1f, MouseState.ScrollDelta.Y);   // un pas al rotiței = ±10 %
        _camera.Zoom = Math.Clamp(_camera.Zoom, 0.1f, 10.0f);

        // --- Titlul ferestrei: obiectul selectat, proiecția, cadre pe secundă --------

        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            _lastFrames = _frames;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            string projection = _camera.IsPerspective ? "perspectivă" : "ortografică";
            Title = $"{_baseTitle} | selectat: {_scene[_selected].Name} | proiecție {projection} | FPS: {_lastFrames}";
            _titleDirty = false;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer.
        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Starea de rasterizare și programul shader.
        SetWireframe(_wireframe);
        _shader.Use();

        // 3. View și projection sunt aceleași pentru toate obiectele: le trimitem o singură dată.
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);

        // 4. Fiecare obiect: matricea lui model, nuanța, apoi desenarea.
        foreach (SceneObject obj in _scene)
        {
            _shader.SetMatrix4("uModel", obj.Transform.ModelMatrix);
            _shader.SetColor("uTint", obj.Tint);
            obj.Mesh.Draw();
        }

        // TODO (T5, facultativ): satelitul obiectului selectat
        // T5: local = scalare 0,3 -> translație (1,2; 0) -> rotația orbitei,
        // apoi înmulțit cu matricea model a obiectului selectat (îl urmează pe acesta)
        Matrix4 local = Matrix4.CreateScale(0.3f, 0.3f, 1.0f)
                      * Matrix4.CreateTranslation(1.2f, 0.0f, 0.0f)
                      * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(_orbitAngle));
        _shader.SetMatrix4("uModel", local * _scene[_selected].Transform.ModelMatrix);
        _shader.SetColor("uTint", Color4.White);
        _satellite.Draw();

        // T5: când obiectul selectat e scalat, se scalează și orbita, pentru că matricea lui
        // model se aplică după translația cu 1,2. Raza devine 1,2 * scara, iar la o scalare
        // neuniformă orbita devine elipsă. Satelitul însuși se mărește sau se micșorează la fel.

        // 5. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        foreach (SceneObject obj in _scene)
        {
            obj.Dispose();
        }
        _satellite.Dispose();
        _shader.Dispose();

        base.OnUnload();
    }

    /// <summary>Transformă poziția mouse-ului (pixeli, origine stânga-sus) în coordonate de lume.</summary>
    private Vector2 ScreenToWorld(Vector2 mousePixels)
    {
        // T3: pasul 1, pixel -> NDC
        float xNdc = 2.0f * mousePixels.X / ClientSize.X - 1.0f;
        float yNdc = 1.0f - 2.0f * mousePixels.Y / ClientSize.Y;

        // T3: la mouse y crește în jos, iar în NDC y crește în sus, de aceea axa y
        // trebuie întoarsă (1 − ...): sus în fereastră (y = 0) devine 1 în NDC.

        // T3: pasul 2, NDC -> lume, cu inversa lui view * projection
        Matrix4 inverse = (_camera.ViewMatrix * _camera.ProjectionMatrix).Inverted();
        Vector4 world = new Vector4(xNdc, yNdc, 0.0f, 1.0f) * inverse;

        return new Vector2(world.X, world.Y) / world.W;
    }

    /// <summary>Tasta R: obiectele revin la transformările inițiale, camera la starea inițială.</summary>
    private void ResetScene()
    {
        for (int i = 0; i < _scene.Count; i++)
        {
            _scene[i].Transform = _initialTransforms[i].Clone();
        }
        _camera.Reset();
    }

    /// <summary>
    /// Contur / plin. Dacă Visual Studio semnalează o eroare la primul parametru,
    /// folosiți aici tipul acceptat de versiunea voastră (vezi laboratorul 2, Problema 3):
    /// TriangleFace.FrontAndBack sau MaterialFace.FrontAndBack.
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }
}
