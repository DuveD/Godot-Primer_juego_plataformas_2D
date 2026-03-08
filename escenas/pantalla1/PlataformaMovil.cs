using Godot;

namespace PrimerjuegoPlataformas2D.escenas.pantalla1;

public partial class PlataformaMovil : Plataforma
{
    public Vector2 DeltaMovimiento { get; private set; }

    private Vector2 _posicionAnterior;

    public Vector2 VelocidadActual { get; private set; }

    [Export]
    public Vector2 Inicio { get; set; } = Vector2.Zero;

    [Export]
    public Vector2 Fin { get; set; } = Vector2.Zero;

    [Export]
    public float DistanciaFrenado = 20f;

    public float VelocidadMaxima = 50f;

    public float Aceleracion = 30f; // Cuánto aumenta la velocidad hacia la máxima.

    private bool _haciaFin = true;
    private float aceleracionActual = 0f; // Variable nueva para controlar la aceleración

    public override void _Ready()
    {
        _posicionAnterior = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 target = _haciaFin ? Fin : Inicio;
        Vector2 direccion = target - Position;
        float distancia = direccion.Length();

        if (distancia < 0.01f)
        {
            // Llegamos al objetivo
            Position = target;
            _haciaFin = !_haciaFin;
            aceleracionActual = 0f; // Reiniciamos aceleración al cambiar dirección
        }
        else
        {
            // Aceleración gradual hasta la velocidad máxima
            aceleracionActual += Aceleracion * (float)delta;
            aceleracionActual = Mathf.Min(aceleracionActual, VelocidadMaxima);

            float velocidad = aceleracionActual;

            // Frenado suave al acercarse
            if (distancia < DistanciaFrenado)
            {
                float t = Mathf.Clamp(distancia / DistanciaFrenado, 0f, 1f);
                float factor = Mathf.SmoothStep(0f, 1f, t); // Suavizado
                velocidad *= factor;
                velocidad = Mathf.Max(velocidad, 10);
            }

            // Movimiento real
            Vector2 movimiento = direccion.Normalized() * velocidad * (float)delta;

            // No sobrepasar el objetivo
            if (movimiento.Length() > distancia)
                Position = target;
            else
                Position += movimiento;
        }

        // Guardar velocidad y delta para otros sistemas
        Vector2 posicionActual = GlobalPosition;
        DeltaMovimiento = posicionActual - _posicionAnterior;
        VelocidadActual = DeltaMovimiento / (float)delta;
        _posicionAnterior = posicionActual;
    }
}