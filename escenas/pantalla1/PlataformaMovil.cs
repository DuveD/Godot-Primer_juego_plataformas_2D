using System;
using Godot;
using PrimerjuegoPlataformas2D.escenas.entidades.jugador;

namespace PrimerjuegoPlataformas2D.escenas.pantalla1;

public partial class PlataformaMovil : Plataforma
{
    public Vector2 DeltaMovimiento { get; private set; }
    public Vector2 VelocidadActual { get; private set; }

    [Export]
    public Vector2 Inicio { get; set; } = Vector2.Zero;
    [Export]
    public Vector2 Fin { get; set; } = Vector2.Zero;

    [Export]
    public float DistanciaFrenado = 20f;
    [Export]
    public float VelocidadMaxima = 50f;
    [Export]
    public float Aceleracion = 30f;

    private Vector2 _posicionAnterior;
    private float _aceleracionActual = 0f;
    private bool _haciaFin = true;

    // --- Estados de la plataforma ---
    private enum EstadoPlataforma { Normal, EsperandoCaida, Cayendo, Reiniciando }
    private EstadoPlataforma _estado = EstadoPlataforma.Normal;

    private float _timer = 0f;

    [Export]
    public float VelocidadCaida = 150f; // velocidad vertical al caer
    [Export]
    public float TiempoEspera = 1f;     // tiempo antes de caer
    [Export]
    public float TiempoReaparecer = 3f; // tiempo hasta reaparecer

    private Area2D _sensorJugador;

    public override void _Ready()
    {
        _posicionAnterior = GlobalPosition;
        Position = Inicio; // asegurar posición inicial

        _sensorJugador = GetNode<Area2D>("SensorJugador");
        _sensorJugador.BodyEntered += OnSensorJugadorBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Movimiento horizontal independiente
        MoverHorizontal(delta);

        // Gestión de caída vertical según estado
        GestionarEstadoCaida(delta);

        // Guardar velocidad y delta
        Vector2 posicionActual = GlobalPosition;
        DeltaMovimiento = posicionActual - _posicionAnterior;
        VelocidadActual = DeltaMovimiento / (float)delta;
        _posicionAnterior = posicionActual;
    }

    private void MoverHorizontal(double delta)
    {
        Vector2 target = _haciaFin ? Fin : Inicio;
        Vector2 direccion = new Vector2(target.X - Position.X, 0);
        float distancia = Math.Abs(direccion.X);

        if (distancia < 0.01f)
        {
            Position = new Vector2(target.X, Position.Y);
            _haciaFin = !_haciaFin;
            _aceleracionActual = 0f;
        }
        else
        {
            _aceleracionActual += Aceleracion * (float)delta;
            _aceleracionActual = Mathf.Min(_aceleracionActual, VelocidadMaxima);

            float velocidad = _aceleracionActual;

            if (distancia < DistanciaFrenado)
            {
                float t = Mathf.Clamp(distancia / DistanciaFrenado, 0f, 1f);
                float factor = Mathf.SmoothStep(0f, 1f, t);
                velocidad *= factor;
                velocidad = Mathf.Max(velocidad, 10);
            }

            float movimientoX = Mathf.Min(velocidad * (float)delta, distancia);
            Position += new Vector2(Mathf.Sign(direccion.X) * movimientoX, 0);
        }
    }

    private void GestionarEstadoCaida(double delta)
    {
        switch (_estado)
        {
            case EstadoPlataforma.Normal:
                // No hacer nada; esperar activación
                break;

            case EstadoPlataforma.EsperandoCaida:
                _timer += (float)delta;
                if (_timer >= TiempoEspera)
                {
                    _estado = EstadoPlataforma.Cayendo;
                    _timer = 0f;
                }
                break;

            case EstadoPlataforma.Cayendo:
                _timer += (float)delta;
                Position += Vector2.Down * VelocidadCaida * (float)delta;

                if (_timer >= TiempoReaparecer)
                {
                    _estado = EstadoPlataforma.Reiniciando;
                }
                break;

            case EstadoPlataforma.Reiniciando:
                Position = Inicio;
                _estado = EstadoPlataforma.Normal;
                _timer = 0f;
                _aceleracionActual = 0f;
                _haciaFin = true;
                break;
        }
    }

    private void OnSensorJugadorBodyEntered(Node2D body)
    {
        if (body is Jugador jugador)
        {
            if (jugador.IsOnFloor())
            {
                KinematicCollision2D collider = jugador.GetLastSlideCollision();
                if (collider != null && collider.GetCollider() == this)
                {
                    ActivarCaida();
                }
            }
        }
    }


    // Llamar cuando el jugador pisa la plataforma
    public void ActivarCaida()
    {
        if (_estado == EstadoPlataforma.Normal)
        {
            _estado = EstadoPlataforma.EsperandoCaida;
            _timer = 0f;
        }
    }
}