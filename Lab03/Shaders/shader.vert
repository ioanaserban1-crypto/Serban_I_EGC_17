#version 330 core

// Atributele vertexului (din VBO, descrise in VAO)
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor;

// Uniforme din laboratorul 2
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uOffset;

// Uniforme noi (laboratorul 3)
uniform float uTime;           // timpul animatiei, in secunde (sta pe loc cand aplicatia e in pauza)
uniform float uWaveAmplitude;  // amplitudinea undei; 0 = unda oprita (tasta W)
uniform float uWaveFrequency;  // cat de "deasa" este unda
uniform int uColorMode;        // 0 = culoarea din VBO, 1 = culoare calculata din pozitie (tasta C)

// Iesire catre fragment shader (valoarea va fi interpolata la rasterizare)
flat out vec3 vColor;   // P7: fara interpolare

void main()
{
    vec3 position = aPosition;

    // P5a: unda. Fiecare varf urca/coboara cu o sinusoida care depinde
    // de pozitia lui pe orizontala (x) si de timp.
    position.y += uWaveAmplitude * sin(uWaveFrequency * position.x - uTime);

    // P5c: culoarea (completam la punctul c)
    // P5c: daca uColorMode este 1, culoarea se calculeaza din pozitie.
    // aPosition este in [-1, 1]; * 0.5 + 0.5 o aduce in [0, 1].
    if (uColorMode == 1)
    {
        vColor = aPosition * 0.5 + 0.5;
    }
    else
    {
        vColor = aColor;
    }

    gl_Position = uProjection * uView * vec4(position + uOffset, 1.0);
}
