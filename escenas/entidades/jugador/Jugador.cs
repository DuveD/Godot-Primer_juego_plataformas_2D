using Godot;
using PrimerjuegoPlataformas2D.escenas.pantalla1;

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
    private const float MAXIMA_VELOCIDAD_CAIDA = 350f;

    private int _coyoteFrames = 0;
    private const int MAX_COYOTE_FRAMES = 6;

    private int _jumpBufferFrames = 0;
    private const int MAX_JUMP_BUFFER = 6;

    private const float ACELERACION_SUELO = 1000f;
    private const float ACELERACION_AIRE = 500f;

    private const float GRAVEDAD_EXTRA_JUMP_CUT = 2f;

    private AnimatedSprite2D _animatedSprite2D;
    public CollisionShape2D CollisionShape2D;
    public float Gravedad = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");

    private SistemaPlataformas _sistemaPlataformas;
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
            OnAterrizar();
        }
    }

    private void OnAterrizar()
    {
        GD.Print("Aterrizaje.");
    }

    public Jugador()
    {
        _sistemaPlataformas = new SistemaPlataformas(this);
        AddChild(_sistemaPlataformas);
    }

    public override void _Ready()
    {
        this._animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        this.CollisionShape2D = GetNode<CollisionShape2D>("CollisionShape2D");
    }

    public override void _PhysicsProcess(double delta)
    {
        EstadoLocomocion = CalcularEstadoLocomocion();
        ActualizarCoyoteTime();
        ActualizarBufferDeSalto();

        Vector2 velocidad = Velocity;

        velocidad = AplicarGravedad(delta, velocidad);
        velocidad = GestionarSalto(delta, velocidad);
        velocidad = GestionarMovimiento(delta, velocidad);

        Velocity = velocidad;

        MoveAndSlide();

        EstadoLocomocion = CalcularEstadoLocomocion();
    }

    private void ActualizarCoyoteTime()
    {
        if (IsOnFloor())
            _coyoteFrames = MAX_COYOTE_FRAMES;
        else if (_coyoteFrames > 0)
            _coyoteFrames--;
    }

    private void ActualizarBufferDeSalto()
    {
        if (Input.IsActionJustPressed("ui_accept"))
            _jumpBufferFrames = MAX_JUMP_BUFFER;
        else if (_jumpBufferFrames > 0)
            _jumpBufferFrames--;
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
            if (velocidad.Y > MAXIMA_VELOCIDAD_CAIDA)
                velocidad.Y = MAXIMA_VELOCIDAD_CAIDA;
        }
        return velocidad;
    }

    private Vector2 GestionarSalto(double delta, Vector2 velocidad)
    {
        bool puedeSaltar = _coyoteFrames > 0;

        if (EstadoLocomocion != EstadoLocomocionJugador.Saltando)
        {
            if ((_jumpBufferFrames > 0) && puedeSaltar)
            {
                if (Input.IsActionPressed("ui_down"))
                    velocidad = _sistemaPlataformas.AtravesarPlataformasDebajo(delta, velocidad);
                else
                    velocidad.Y = -VELOCIDAD_SALTO;

                _coyoteFrames = 0;
                _jumpBufferFrames = 0;
            }
        }
        else
        {
            if (!Input.IsActionPressed("ui_accept") && velocidad.Y < 0)
                velocidad.Y += Gravedad * GRAVEDAD_EXTRA_JUMP_CUT * (float)delta;
        }

        return velocidad;
    }

    private Vector2 GestionarMovimiento(double delta, Vector2 velocidad)
    {
        float direccion = Input.GetAxis("ui_left", "ui_right");
        float aceleracion = IsOnFloor() ? ACELERACION_SUELO : ACELERACION_AIRE;
        float objetivoX = direccion * VELOCIDAD;

        velocidad.X = Mathf.MoveToward(velocidad.X, objetivoX, aceleracion * (float)delta);

        if (direccion != 0)
            _animatedSprite2D.FlipH = !(direccion > 0);

        return velocidad;
    }
}