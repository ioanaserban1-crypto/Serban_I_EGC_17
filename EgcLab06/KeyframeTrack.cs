using OpenTK.Mathematics;

namespace Egc;

/// <summary>
/// NOU: o pistă de cadre-cheie (secțiunea 4): o listă de valori-cheie, la intervale EGALE de timp
/// (SecondsPerKey). Între două chei valoarea se interpolează. După ultima cheie pista revine
/// la prima și o ia de la capăt (bucla se închide singură).
/// </summary>
public sealed class KeyframeTrack
{
    private readonly List<Vector3> _keys = new();

    /// <summary>Timpul dintre două chei consecutive, în secunde.</summary>
    public float SecondsPerKey { get; }

    /// <summary>Easing-ul aplicat între două chei (implicit liniar).</summary>
    public EasingFunction Ease = Easing.Linear;

    public KeyframeTrack(float secondsPerKey)
    {
        SecondsPerKey = MathF.Max(secondsPerKey, 0.001f);
    }

    public int Count => _keys.Count;
    public IReadOnlyList<Vector3> Keys => _keys;

    /// <summary>Durata unui tur complet (inclusiv drumul de la ultima cheie înapoi la prima).</summary>
    public float Duration => _keys.Count * SecondsPerKey;

    /// <summary>Adaugă o cheie la sfârșitul pistei.</summary>
    public KeyframeTrack Add(Vector3 value)
    {
        _keys.Add(value);
        return this;   // permite scrierea în lanț: track.Add(...).Add(...)
    }

    public void Clear() => _keys.Clear();

    /// <summary>Valoarea pistei la momentul <paramref name="time"/> (în secunde).</summary>
    public Vector3 Sample(float time)
    {
        if (_keys.Count == 0) return Vector3.Zero;
        if (_keys.Count == 1) return _keys[0];

        // TODO 6.3 (secțiunea 4.2):
        // 1. câte intervale au trecut, adus în [0, Count) cu % pentru buclă (de exemplu 1.5)
        float position = time / SecondsPerKey % _keys.Count;
        if (position < 0.0f) position += _keys.Count;   // siguranță pentru timp negativ

        // 2. partea întreagă = cheia de plecare, partea fracționară = cât din drum s-a parcurs
        int i = Math.Min((int)position, _keys.Count - 1);
        float u = position - i;

        // 3. lerp între cheia i și următoarea; după ultima cheie urmează prima: (i + 1) % Count
        int next = (i + 1) % _keys.Count;
        return Vector3.Lerp(_keys[i], _keys[next], Ease(u));
    }
}