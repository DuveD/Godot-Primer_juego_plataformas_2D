using System;
using Godot;
using PrimerjuegoPlataformas2D.escenas.entidades.jugador;

namespace PrimerjuegoPlataformas2D.escenas.pantalla1;

public partial class PlataformaMovil : Plataforma
{
    public Vector2 DeltaMovimiento { get; private set; }
    public Vector2 VelocidadActual { get; private set; }

    [Export]
    public bool Movimiento { get; set; } = false;

    public Vector2 PosicionInicial { get; set; }

    [Export]
    public Vector2 PosicionA { get; set; } = Vector2.Zero;
    [Export]
    public Vector2 PosicionB { get; set; } = Vector2.Zero;

    [Export]
    public float DistanciaFrenado = 20f;
    [Export]
    public float VelocidadMaxima = 50f;
    [Export]
    public float Aceleracion = 30f;

    private Vector2 _posicionAnterior;
    private float _aceleracionActual = 0f;
    private bool _haciaFin = true;

    [Export]
    public bool Caida { get; set; } = false;

    [Export]
    public float TiempoEsperaCaida = 1f;     // tiempo antes de caer
    [Export]
    public float TiempoReaparecer = 3f; // tiempo hasta reaparecer
    [Export]
    public float VelocidadCaida = 150f; // velocidad vertical al caer

    // --- Estados de la plataforma ---
    private enum EstadoPlataforma { Normal, EsperandoCaida, Cayendo, Reiniciando }
    private EstadoPlataforma _estado = EstadoPlataforma.Normal;

    private float _timer = 0f;

    private Area2D _sensorJugador;

    public override void _Ready()
    {
        PosicionInicial = _posicionAnterior = GlobalPosition;
        _sensorJugador = GetNode<Area2D>("SensorJugador");

        if (Caida)
            _sensorJugador.BodyEntered += OnSensorJugadorBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Movimiento horizontal independiente
        GestionarMovimiento(delta);

        // Gestión de caída vertical según estado
        GestionarCaida(delta);

        // Guardar velocidad y delta
        Vector2 posicionActual = GlobalPosition;
        DeltaMovimiento = posicionActual - _posicionAnterior;
        VelocidadActual = DeltaMovimiento / (float)delta;
        _posicionAnterior = posicionActual;
    }

    private void GestionarMovimiento(double delta)
    {
        if (!Movimiento && (PosicionA == Vector2.Zero || PosicionB == Vector2.Zero))
            return;

        Vector2 target = _haciaFin ? PosicionB : PosicionA;
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

    private void GestionarCaida(double delta)
    {
        if (!Caida)
            return;

        switch (_estado)
        {
            case EstadoPlataforma.Normal:
                // No hacer nada; esperar activación
                break;

            case EstadoPlataforma.EsperandoCaida:
                _timer += (float)delta;
                if (_timer >= TiempoEsperaCaida)
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
                Position = PosicionInicial;
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