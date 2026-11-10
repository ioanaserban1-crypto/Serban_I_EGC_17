using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// TEMA: camera orbită. Privește mereu spre o țintă și se mișcă pe o sferă în jurul ei.
/// Poziția pe sferă = distanța (raza) + azimutul (în jurul axei Y) + elevația (deasupra orizontului).
/// </summary>
public class OrbitCamera : Camera
{
    // --- Starea camerei ---------------------------------------------------------

    public Vector3 Target { get; private set; }
    public float Distance { get; private set; }
    public float Azimuth { get; private set; }       // grade
    public float Elevation { get; private set; }     // grade, limitată la ±MaxElevation

    // --- Parametri ----------------------------------------------------------------

    public const float MaxElevation = 89.0f;         // ca la pitch: altfel "sus" devine paralel cu privirea
    public const float MinDistance = 2.0f;
    public const float MaxDistance = 30.0f;
    public float MouseSensitivity = 0.3f;            // grade pe pixel
    public float KeyRotateSpeed = 90.0f;             // grade pe secundă (săgeți)
    public float ZoomStep = 0.5f;                    // unități pentru un pas al rotiței
    public float TargetSpeed = 3.0f;                 // bonus: W A S D mută ținta

    private static readonly Vector3 WorldUp = Vector3.UnitY;

    public OrbitCamera()
    {
        Reset();
    }

    public override string Name => "orbită";

    /// <summary>Poziția = țintă + distanță · (cos el · cos az, sin el, cos el · sin az).</summary>
    public override Vector3 Position
    {
        get
        {
            float az = MathHelper.DegreesToRadians(Azimuth);
            float el = MathHelper.DegreesToRadians(Elevation);
            var offset = new Vector3(
                MathF.Cos(el) * MathF.Cos(az),
                MathF.Sin(el),
                MathF.Cos(el) * MathF.Sin(az));
            return Target + Distance * offset;
        }
    }

    /// <summary>Privirea merge mereu de la cameră spre țintă.</summary>
    public override Vector3 Front => Vector3.Normalize(Target - Position);

    public override Matrix4 ViewMatrix => Matrix4.LookAt(Position, Target, WorldUp);

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // Clic stânga ținut + mișcarea mouse-ului rotește camera
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Azimuth += mouseDelta.X * MouseSensitivity;
            Elevation += mouseDelta.Y * MouseSensitivity;
        }

        // Săgețile rotesc camera de la tastatură
        float step = KeyRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Left)) Azimuth += step;
        if (keyboard.IsKeyDown(Keys.Right)) Azimuth -= step;
        if (keyboard.IsKeyDown(Keys.Up)) Elevation += step;
        if (keyboard.IsKeyDown(Keys.Down)) Elevation -= step;

        Elevation = Math.Clamp(Elevation, -MaxElevation, MaxElevation);

        // Rotița schimbă distanța, nu FOV-ul
        if (mouse.ScrollDelta.Y != 0.0f)
        {
            Distance = Math.Clamp(Distance - mouse.ScrollDelta.Y * ZoomStep, MinDistance, MaxDistance);
        }

        // Bonus: W A S D mută ținta în planul orizontal
        Vector3 front = Front;
        Vector3 forward = new(front.X, 0.0f, front.Z);
        if (forward.LengthSquared > 0.0f)
        {
            forward.Normalize();
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, WorldUp));

            Vector3 move = Vector3.Zero;
            if (keyboard.IsKeyDown(Keys.W)) move += forward;
            if (keyboard.IsKeyDown(Keys.S)) move -= forward;
            if (keyboard.IsKeyDown(Keys.D)) move += right;
            if (keyboard.IsKeyDown(Keys.A)) move -= right;

            if (move.LengthSquared > 0.0f)
            {
                Target += Vector3.Normalize(move) * TargetSpeed * dt;
            }
        }
    }

    /// <summary>
    /// Bonus (FPS -> orbită fără salt): ținta e la <paramref name="distance"/> în fața camerei FPS,
    /// iar unghiurile vin din vectorul țintă -> cameră, o = -front.
    /// </summary>
    public void SetFromFps(Vector3 cameraPosition, Vector3 cameraFront, float distance)
    {
        Vector3 front = Vector3.Normalize(cameraFront);
        Target = cameraPosition + front * distance;
        Distance = distance;

        Vector3 o = -front;
        Elevation = Math.Clamp(MathHelper.RadiansToDegrees(MathF.Asin(Math.Clamp(o.Y, -1.0f, 1.0f))), -MaxElevation, MaxElevation);
        Azimuth = MathHelper.RadiansToDegrees(MathF.Atan2(o.Z, o.X));
    }

    public override void Reset()
    {
        Target = new Vector3(0.0f, 1.0f, -2.0f);   // cubul din centru
        Distance = 8.0f;
        Azimuth = 90.0f;                            // camera pe +Z față de țintă
        Elevation = 20.0f;
        FieldOfView = 60.0f;
    }

    public override IEnumerable<string> Describe()
    {
        Vector3 p = Position;
        yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
        yield return $"Distanță       : {Distance,6:F2}";
        yield return $"Azimut / elev. : {NormalizeAngle(Azimuth),7:F1}° / {Elevation,6:F1}°";
        yield return $"Poziție        : ({p.X,6:F2}, {p.Y,6:F2}, {p.Z,6:F2})";
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360.0f;
        if (degrees > 180.0f) degrees -= 360.0f;
        if (degrees <= -180.0f) degrees += 360.0f;
        return degrees;
    }
}
