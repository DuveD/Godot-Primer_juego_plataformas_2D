using Godot;

namespace PrimerjuegoPlataformas2D.escenas.entidades.jugador;

public partial class Jugador : CharacterBody2D
{
    /// <summary>
    /// Describe el estado físico vertical del jugador.
    /// </summary>
    public enum EstadoLocomocionJugador
    {
        EnSuelo,
        Saltando,
        Cayendo
    }

    public float VELOCIDAD = 130.0f;

    public float VELOCIDAD_SALTO = 300.0f;

    private AnimatedSprite2D _animatedSprite2D;

    public CollisionShape2D CollisionShape2D;

    public float Gravedad = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");

    private SistemaAtravesarPlataformas _sistemaAtravesarPlataformas;

    public EstadoLocomocionJugador? EstadoLocomocionAnterior;
    private EstadoLocomocionJugador? _estadoLocomocion;

    public EstadoLocomocionJugador EstadoLocomocion
    {
        get => _estadoLocomocion ??= CalcularEstadoLocomocion();
        private set
        {
            if (_estadoLocomocion.HasValue && _estadoLocomocion.Value == value)
                return;

            EstadoLocomocionAnterior = _estadoLocomocion;
            _estadoLocomocion = value;

            OnEstadoLocomocionChanged(EstadoLocomocionAnterior, value);
        }
    }

    private void OnEstadoLocomocionChanged(EstadoLocomocionJugador? anterior, EstadoLocomocionJugador actual)
    {
        switch (actual)
        {
            case EstadoLocomocionJugador.EnSuelo:
                GD.Print("Jugador en el suelo.");
                break;

            case EstadoLocomocionJugador.Saltando:
                GD.Print("Jugador saltando.");
                break;

            case EstadoLocomocionJugador.Cayendo:
                GD.Print("Jugador cayendo.");
                break;
        }

        if (anterior == EstadoLocomocionJugador.Cayendo &&
            actual == EstadoLocomocionJugador.EnSuelo)
        {
            GD.Print("Aterrizaje.");
        }
    }

    public Jugador()
    {
        _sistemaAtravesarPlataformas = new SistemaAtravesarPlataformas(this);
        AddChild(_sistemaAtravesarPlataformas);
    }

    public override void _Ready()
    {
        this._animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        this.CollisionShape2D = GetNode<CollisionShape2D>("CollisionShape2D");
    }

    public override void _PhysicsProcess(double delta)
    {
        EstadoLocomocion = CalcularEstadoLocomocion();

        Vector2 velocidad = Velocity;

        velocidad = AplicarGravedad(delta, velocidad);

        velocidad = GestionarSalto(delta, velocidad);

        velocidad = GestionarMovimiento(velocidad);

        this.Velocity = velocidad;

        MoveAndSlide();
    }

    public EstadoLocomocionJugador CalcularEstadoLocomocion()
    {
        if (IsOnFloor())
        {
            return EstadoLocomocionJugador.EnSuelo;
        }
        else if (Velocity.Y < 0)
        {
            return EstadoLocomocionJugador.Saltando;
        }
        else
        {
            return EstadoLocomocionJugador.Cayendo;
        }
    }

    private Vector2 AplicarGravedad(double delta, Vector2 velocidad)
    {
        // Aplicamos gravedad al jugador si no está en el suelo.

        if (!IsOnFloor())
        {
            velocidad.Y += Gravedad * (float)delta;
        }

        return velocidad;
    }

    private Vector2 GestionarSalto(double delta, Vector2 velocidad)
    {
        if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
        {
            if (Input.IsActionPressed("ui_down"))
                velocidad = _sistemaAtravesarPlataformas.AtravesarPlataformasDebajo(delta, velocidad);
            else
                velocidad.Y = -VELOCIDAD_SALTO;
        }

        return velocidad;
    }

    private Vector2 GestionarMovimiento(Vector2 velocidad)
    {
        float direccion = Input.GetAxis("ui_left", "ui_right");
        if (direccion != 0)
        {
            velocidad.X = direccion * VELOCIDAD;
            _animatedSprite2D.FlipH = !(direccion > 0);
        }
        else
        {
            velocidad.X = Mathf.MoveToward(velocidad.X, 0f, VELOCIDAD * 10);
        }

        return velocidad;
    }
}