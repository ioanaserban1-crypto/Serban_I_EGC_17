// Varianta C: roata panoramică
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Tema, varianta C: roata panoramică.
///  - ierarhie: butucul (pivot) poartă spițele, janta și cele 8 cabine; fiecare cabină atârnă
///    de un pivot de pe jantă și se rotește invers față de roată, ca să rămână verticală;
///  - săgețile sus / jos schimbă viteza țintă, iar viteza reală se apropie lin de ea:
///    v ← v + (v_țintă − v) · (1 − e^(−k·dt)), același rezultat la orice număr de cadre pe secundă;
///  - Space oprește roata lin (viteza țintă devine 0);
///  - tastele 1-8 aduc cabina aleasă jos, la peron, cu un Tween (EaseInOut) pe unghiul roții,
///    pe drumul cel mai scurt (cel mult 180°);
///  - bonus: cabinele se leagănă când roata accelerează sau frânează (resort amortizat).
/// Toate animațiile folosesc doar timpul de joc (dt), deci pauza, scara timpului și blocarea (B) merg.
/// </summary>
public sealed class RoataPanoramica : Scene
{
    // --- Acțiunile scenei (apar în meniul H) ----------------------------------------------------
    private const string Faster = "roata: viteza țintă mai mare";
    private const string Slower = "roata: viteza țintă mai mică";
    private const string Stop = "roata: oprire lină";
    private const string ResetWheel = "roata: revine la starea inițială";
    private static readonly string[] BringCabin =
        Enumerable.Range(1, 8).Select(n => $"cabina {n} la peron").ToArray();

    // --- Dimensiuni și parametri --------------------------------------------------------------
    private const int CabinCount = 8;
    private const float Radius = 4.5f;                 // raza jantei
    private static readonly Vector3 HubPosition = new(0.0f, 6.0f, 0.0f);
    private const float SpeedStep = 5.0f;              // grade/s la o apăsare pe săgeată
    private const float MaxSpeed = 45.0f;              // grade/s
    private const float Smoothing = 1.5f;              // k din formula de apropiere (1/s)

    // Leagănul cabinelor (bonus): resort amortizat, acționat de accelerația roții.
    private const float SwingStiffness = 30.0f;
    private const float SwingDamping = 3.0f;
    private const float SwingFromAcceleration = 3.0f;
    private const float MaxSwing = 25.0f;              // grade

    // --- Obiectele care se animă --------------------------------------------------------------
    private SceneObject _hub = null!;                              // pivotul roții
    private readonly SceneObject[] _cabinMounts = new SceneObject[CabinCount];   // pivoții cabinelor

    // --- Starea animației ---------------------------------------------------------------------
    private float _angle;            // unghiul roții, în grade (în jurul axei Z)
    private float _speed;            // viteza reală, grade/s
    private float _targetSpeed;      // viteza țintă, grade/s
    private Tween? _bring;           // animația "cabina k la peron" (X = unghiul roții)
    private int _bringCabin = -1;

    private float _lastAngle;        // pentru viteza și accelerația măsurate (leagăn)
    private float _lastVelocity;
    private float _swing;            // unghiul de leagăn al cabinelor, grade
    private float _swingVelocity;

    public override string Name => "tema C: roata panoramică";

    public override IEnumerable<string> ActionNames =>
        new[] { Faster, Slower, Stop, ResetWheel }.Concat(BringCabin);

    public override string Status =>
        _bring != null
            ? $"cabina {_bringCabin + 1} vine la peron"
            : $"v = {_speed:F0}°/s (țintă {_targetSpeed:F0}°/s) | jos: cabina {CabinAtPlatform() + 1}";

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(4.0f, 5.5f, 15.0f);
        CameraTarget = new Vector3(0.0f, 5.0f, 0.0f);

        // Input DOAR prin acțiuni; tastele se leagă aici, o singură dată.
        input.Bind(Faster, Keys.Up);
        input.Bind(Slower, Keys.Down);
        input.Bind(Stop, Keys.Space);
        input.Bind(ResetWheel, Keys.R);
        for (int k = 0; k < CabinCount; k++)
        {
            input.Bind(BringCabin[k], Keys.D1 + k, Keys.KeyPad1 + k);
        }

        Mesh grid = AddMesh(Shapes.Grid(10, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh cylinder = AddMesh(Shapes.ShadedCylinder(0.5f, 1.0f, 24));

        AddObject("podea", grid);
        BuildSupport(cube, cylinder);
        BuildWheel(cube, cylinder);
        Reset();
    }

    // --- Construcția scenei -------------------------------------------------------------------

    /// <summary>Picioarele (în formă de A, de o parte și de alta a roții), axul și peronul. Nu se mișcă.</summary>
    private void BuildSupport(Mesh cube, Mesh cylinder)
    {
        var steel = new Color4(0.55f, 0.58f, 0.65f, 1.0f);
        const float footX = 2.4f;
        float legLength = MathF.Sqrt(footX * footX + HubPosition.Y * HubPosition.Y);
        float legAngle = MathHelper.RadiansToDegrees(MathF.Atan2(footX, HubPosition.Y));

        foreach (float z in new[] { -0.9f, 0.9f })
        {
            foreach (float side in new[] { -1.0f, 1.0f })
            {
                // un segment vertical, înclinat ca vârful să ajungă în butuc
                SceneObject leg = AddObject("picior", cube);
                leg.Transform.Position = new Vector3(side * footX / 2.0f, HubPosition.Y / 2.0f, z);
                leg.Transform.Rotation = new Vector3(0.0f, 0.0f, side * legAngle);
                leg.Transform.Scale = new Vector3(0.25f, legLength, 0.25f);
                leg.Tint = steel;
            }
        }

        SceneObject axle = AddObject("ax", cylinder);          // cilindrul e pe Y: îl culcăm pe Z
        axle.Transform.Position = HubPosition;
        axle.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
        axle.Transform.Scale = new Vector3(0.3f, 2.0f, 0.3f);
        axle.Tint = steel;

        SceneObject platform = AddObject("peron", cube);
        platform.Transform.Position = new Vector3(0.0f, 0.15f, 0.0f);
        platform.Transform.Scale = new Vector3(2.2f, 0.3f, 1.6f);
        platform.Tint = new Color4(0.75f, 0.55f, 0.35f, 1.0f);
    }

    /// <summary>Partea care se rotește: butucul (pivot) cu spițele, janta și cabinele, toate copiii lui.</summary>
    private void BuildWheel(Mesh cube, Mesh cylinder)
    {
        var white = new Color4(0.92f, 0.92f, 0.95f, 1.0f);

        _hub = AddObject("butuc (pivot)", null);
        _hub.Transform.Position = HubPosition;

        SceneObject hubDisk = AddObject("butuc", cylinder, _hub);
        hubDisk.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
        hubDisk.Transform.Scale = new Vector3(1.0f, 0.8f, 1.0f);
        hubDisk.Tint = new Color4(0.9f, 0.3f, 0.3f, 1.0f);

        foreach (float z in new[] { -0.35f, 0.35f })   // două fețe ale roții, cabinele atârnă între ele
        {
            // Spițele: un cub lung, rotit și mutat la jumătatea razei. Scalarea neuniformă stă pe
            // forma-copil, nu pe un pivot cu copii (altfel i-ar deforma și pe aceștia).
            for (int k = 0; k < CabinCount; k++)
            {
                float a = CabinBaseAngle(k);
                SceneObject spoke = AddObject("spiță", cube, _hub);
                spoke.Transform.Position = OnCircle(a, Radius / 2.0f, z);
                spoke.Transform.Rotation = new Vector3(0.0f, 0.0f, a);
                spoke.Transform.Scale = new Vector3(Radius, 0.08f, 0.08f);
                spoke.Tint = white;
            }

            // Janta: 24 de segmente scurte, tangente la cerc.
            const int segments = 24;
            float chord = 2.0f * Radius * MathF.Sin(MathF.PI / segments) + 0.05f;
            for (int j = 0; j < segments; j++)
            {
                float a = (j + 0.5f) * 360.0f / segments;
                SceneObject rim = AddObject("jantă", cube, _hub);
                rim.Transform.Position = OnCircle(a, Radius, z);
                rim.Transform.Rotation = new Vector3(0.0f, 0.0f, a + 90.0f);
                rim.Transform.Scale = new Vector3(chord, 0.12f, 0.12f);
                rim.Tint = white;
            }
        }

        // Cabinele: un pivot pe jantă (copil al butucului, deci se rotește cu roata),
        // iar de el atârnă tija și cabina propriu-zisă.
        for (int k = 0; k < CabinCount; k++)
        {
            SceneObject mount = AddObject($"pivot cabina {k + 1}", null, _hub);
            mount.Transform.Position = OnCircle(CabinBaseAngle(k), Radius, 0.0f);
            _cabinMounts[k] = mount;

            SceneObject rod = AddObject("tijă", cube, mount);
            rod.Transform.Position = new Vector3(0.0f, -0.2f, 0.0f);
            rod.Transform.Scale = new Vector3(0.06f, 0.4f, 0.06f);
            rod.Tint = white;

            SceneObject cabin = AddObject($"cabina {k + 1}", cube, mount);
            cabin.Transform.Position = new Vector3(0.0f, -0.75f, 0.0f);
            cabin.Transform.Scale = new Vector3(0.9f, 0.7f, 0.55f);
            cabin.Tint = ColorUtil.FromHsv(k / (float)CabinCount, 0.6f, 0.95f);
        }
    }

    // --- Animația -----------------------------------------------------------------------------

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);   // Spin (aici nu e folosit, dar păstrăm comportamentul de bază)

        if (input.WasPressed(ResetWheel)) Reset();

        // Săgețile schimbă viteza țintă (o apăsare = un pas); anulează aducerea unei cabine.
        if (input.WasPressed(Faster)) ChangeTarget(+SpeedStep);
        if (input.WasPressed(Slower)) ChangeTarget(-SpeedStep);
        if (input.WasPressed(Stop))
        {
            _targetSpeed = 0.0f;   // viteza reală scade lin spre 0
        }

        // Tastele 1-8: cabina aleasă vine la peron, pe drumul cel mai scurt.
        for (int k = 0; k < CabinCount; k++)
        {
            if (input.WasPressed(BringCabin[k])) StartBringing(k);
        }

        if (_bring != null)
        {
            // Unghiul roții vine din Tween (EaseInOut: pornește și oprește lin).
            _bring.Update(dt);
            _angle = _bring.Value.X;
            if (_bring.IsFinished)
            {
                _angle = NormalizeAngle(_angle);
                _bring = null;
                _bringCabin = -1;
            }
        }
        else
        {
            // Viteza reală se apropie exponențial de viteza țintă. Factorul (1 − e^(−k·dt))
            // dă același rezultat la orice număr de cadre pe secundă; în pauză dt = 0, deci nimic nu se schimbă.
            _speed += (_targetSpeed - _speed) * (1.0f - MathF.Exp(-Smoothing * dt));
            _angle = NormalizeAngle(_angle + _speed * dt);
        }

        UpdateSwing(dt);
        ApplyAngles();
    }

    /// <summary>Copiază unghiurile în transformări: roata se rotește, cabinele invers (rămân verticale).</summary>
    private void ApplyAngles()
    {
        _hub.Transform.Rotation = new Vector3(0.0f, 0.0f, _angle);
        foreach (SceneObject mount in _cabinMounts)
        {
            // Pivotul cabinei moștenește rotația roții; rotația lui proprie o anulează (-_angle),
            // iar leagănul se adaugă peste.
            mount.Transform.Rotation = new Vector3(0.0f, 0.0f, -_angle + _swing);
        }
    }

    /// <summary>
    /// Bonus: leagănul cabinelor, un resort amortizat. Accelerația roții (măsurată din unghi)
    /// împinge cabinele, resortul le readuce la verticală, iar amortizarea oprește oscilația.
    /// </summary>
    private void UpdateSwing(float dt)
    {
        if (dt <= 0.0f) return;   // pauză: totul rămâne pe loc

        float velocity = AngleDifference(_lastAngle, _angle) / dt;
        float acceleration = (velocity - _lastVelocity) / dt;
        _lastAngle = _angle;
        _lastVelocity = velocity;

        // Euler semi-implicit, ca la gravitație: întâi viteza, apoi poziția.
        float force = -SwingStiffness * _swing - SwingDamping * _swingVelocity - SwingFromAcceleration * acceleration;
        _swingVelocity += force * dt;
        _swing = Math.Clamp(_swing + _swingVelocity * dt, -MaxSwing, MaxSwing);
    }

    private void ChangeTarget(float delta)
    {
        if (_bring != null)
        {
            // Aducerea unei cabine se întrerupe: roata pornește din repaus din unghiul curent.
            _angle = NormalizeAngle(_angle);
            _bring = null;
            _bringCabin = -1;
            _speed = 0.0f;
        }
        _targetSpeed = Math.Clamp(_targetSpeed + delta, -MaxSpeed, MaxSpeed);
    }

    /// <summary>Pornește Tween-ul care aduce cabina <paramref name="k"/> jos, pe drumul cel mai scurt.</summary>
    private void StartBringing(int k)
    {
        // Cabina k este jos când unghiul ei total (de bază + al roții) este -90°.
        float target = -90.0f - CabinBaseAngle(k);
        float delta = AngleDifference(_angle, target);   // între -180° și 180°: drumul cel mai scurt
        float duration = 0.8f + MathF.Abs(delta) / 60.0f; // drum mai lung = durată mai mare

        _bring = new Tween(new Vector3(_angle, 0.0f, 0.0f), new Vector3(_angle + delta, 0.0f, 0.0f),
                           duration, Easing.EaseInOut);
        _bringCabin = k;
        _speed = 0.0f;
        _targetSpeed = 0.0f;   // după sosire roata rămâne oprită la peron
    }

    private void Reset()
    {
        _angle = 0.0f;
        _speed = 0.0f;
        _targetSpeed = 10.0f;   // pornește încet
        _bring = null;
        _bringCabin = -1;
        _lastAngle = 0.0f;
        _lastVelocity = 0.0f;
        _swing = 0.0f;
        _swingVelocity = 0.0f;
        ApplyAngles();
    }

    // --- Funcții ajutătoare -------------------------------------------------------------------

    /// <summary>Unghiul cabinei k pe roată, cu roata în poziția 0: cabina 1 jos (-90°), apoi la fiecare 45°.</summary>
    private static float CabinBaseAngle(int k) => -90.0f + k * 360.0f / CabinCount;

    /// <summary>Punctul de pe cercul de rază <paramref name="radius"/> (în planul XY), la unghiul dat în grade, la adâncimea z.</summary>
    private static Vector3 OnCircle(float degrees, float radius, float z)
    {
        float r = MathHelper.DegreesToRadians(degrees);
        return new Vector3(radius * MathF.Cos(r), radius * MathF.Sin(r), z);
    }

    /// <summary>Aduce un unghi în intervalul (-180°, 180°].</summary>
    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360.0f;
        if (degrees > 180.0f) degrees -= 360.0f;
        if (degrees <= -180.0f) degrees += 360.0f;
        return degrees;
    }

    /// <summary>Cel mai scurt drum de la unghiul <paramref name="from"/> la <paramref name="to"/> (-180°..180°).</summary>
    private static float AngleDifference(float from, float to) => NormalizeAngle(to - from);

    /// <summary>Cabina aflată cel mai aproape de peron (de unghiul -90°).</summary>
    private int CabinAtPlatform()
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int k = 0; k < CabinCount; k++)
        {
            float distance = MathF.Abs(AngleDifference(CabinBaseAngle(k) + _angle, -90.0f));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = k;
            }
        }
        return best;
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Unghiul roții  : {_angle,7:F1}°";
        yield return $"Viteză reală   : {_speed,7:F1}°/s   țintă: {_targetSpeed:F1}°/s";
        yield return $"La peron       : cabina {CabinAtPlatform() + 1}";
        yield return _bring != null
            ? $"Tween          : cabina {_bringCabin + 1}, {_bring.Elapsed:F2} / {_bring.Duration:F2} s (EaseInOut)"
            : "Tween          : -";
        yield return $"Leagăn cabine  : {_swing,7:F1}°";
    }
}