using System;
using Godot;

namespace PrimerjuegoPlataformas2D.escenas.entidades.jugador;

public partial class Jugador : CharacterBody2D
{
    public const string NOMBRE_ANIMACION_ATERRIZAR = "aterrizar";
    public const string NOMBRE_ANIMACION_CAER = "caer";
    public const string NOMBRE_ANIMACION_CORRER = "correr";
    public const string NOMBRE_ANIMACION_GOLPEADO = "golpeado";
    public const string NOMBRE_ANIMACION_IDLE = "idle";
    public const string NOMBRE_ANIMACION_MUERTE = "muerte";
    public const string NOMBRE_ANIMACION_RODAR = "rodar";
    public const string NOMBRE_ANIMACION_RODANDO = "rodando";
    public const string NOMBRE_ANIMACION_SALTAR = "saltar";

    private int _framesEstadoTemporal = 0;
    private const int FRAMES_ATERRIZAR = 6;

    private const int FRAMES_RODAR = 16;          // duración del rodar

    private bool _mantenerAnimacionRodar = false;

    [Export]
    public float VELOCIDAD_RODAR = 200; // velocidad horizontal durante el rodar

    private float _velocidadInicialRodar = 0f;

    /// <summary>
    /// Describe el estado físico vertical del jugador.
    /// </summary>
    public enum EstadoLocomocionJugador
    {
        EnSuelo,
        Saltando,
        Cayendo,
        Aterrizando,
        Rodando
    }

    [Export]
    public float VELOCIDAD = 130f;

    [Export]
    public float VELOCIDAD_SALTO = 300.0f;

    [Export]
    public float MAXIMA_VELOCIDAD_CAIDA = 350f;

    private const float MULTIPLICADOR_CAIDA = 1.8f;
    private const float MULTIPLICADOR_CAIDA_RAPIDA = 2.5f;

    private int _coyoteFrames = 0;
    private const int MAX_COYOTE_FRAMES = 10;

    private int _jumpBufferFrames = 0;
    private const int MAX_JUMP_BUFFER = 6;

    private const float ACELERACION_SUELO = 1000f;
    private const float ACELERACION_AIRE = 500f;

    private const float GRAVEDAD_EXTRA_CORTE_SALTO = 2f;

    private const float UMBRAL_APEX = 40f;
    private const float MULTIPLICADOR_CONTROL_APEX = 1.5f;
    private const float MULTIPLICADOR_GRAVEDAD_APEX = 0.5f;

    private int _framesCaidaRapida = 0;
    private const int FRAMES_CAIDA_RAPIDA_MIN = 2;

    private AnimatedSprite2D _animatedSprite2D;
    public CollisionShape2D CollisionShape2D;
    public Area2D SensorSuelo;
    public float Gravedad = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");

    public SistemaPlataformas SistemaPlataformas;
    public EstadoLocomocionJugador EstadoLocomocionAnterior;
    public EstadoLocomocionJugador EstadoLocomocion;

    private bool? CaidaRapida;

    public override void _Ready()
    {
        this._animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        this.CollisionShape2D = GetNode<CollisionShape2D>("CollisionShape2D");
        this.SensorSuelo = GetNode<Area2D>("SensorSuelo");

        this.SistemaPlataformas = new SistemaPlataformas(this);
        AddChild(SistemaPlataformas);

        this.EstadoLocomocion = CalcularEstadoLocomocion();
        this.EstadoLocomocionAnterior = this.EstadoLocomocion;
        this.ActualizarAnimacion();
    }

    public override void _PhysicsProcess(double delta)
    {
        ActualizarInputs();

        Vector2 velocidad = Velocity;

        velocidad = GestionarMovimientoVertical(delta, velocidad);
        velocidad = GestionarSalto(delta, velocidad);
        velocidad = GestionarMovimientoHorizontal(delta, velocidad);

        Velocity = velocidad;

        MoveAndSlide();

        EvaluarEstadoLocomocion();
        ActualizarAnimacion();
    }

    private void ActualizarInputs()
    {
        DetectarRodar();

        ActualizarCoyoteTime();
        ActualizarBufferDeSalto();
    }

    private void DetectarRodar()
    {
        if (IsOnFloor() && Input.IsActionJustPressed("rodar"))
        {
            // Si no estaba rodando, o si queremos encadenar rodadas, reiniciamos el contador
            if (EstadoLocomocion != EstadoLocomocionJugador.Rodando)
            {
                CambiarEstadoLocomocion(EstadoLocomocionJugador.Rodando);

                // Guardamos la dirección
                float direccionRodar = _animatedSprite2D.FlipH ? -1 : 1;
                _velocidadInicialRodar = direccionRodar * VELOCIDAD_RODAR;
            }
            else
            {
                float direccionInput = Input.GetAxis("ui_left", "ui_right");
                float direccionActual = _animatedSprite2D.FlipH ? -1 : 1;

                if (Mathf.Sign(direccionInput) != 0 && Mathf.Sign(direccionInput) != Mathf.Sign(direccionActual))
                {
                    _animatedSprite2D.FlipH = direccionInput < 0;
                    _velocidadInicialRodar = (direccionInput < 0 ? -1 : 1) * VELOCIDAD_RODAR;
                }
            }

            // Reiniciamos duración de rodar
            _framesEstadoTemporal = FRAMES_RODAR;
        }
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
            if (EstadoLocomocion == EstadoLocomocionJugador.Cayendo)
            {
                return EstadoLocomocionJugador.Aterrizando;
            }

            return EstadoLocomocionJugador.EnSuelo;
        }

        if (Velocity.Y < 0)
        {
            return EstadoLocomocionJugador.Saltando;
        }

        return EstadoLocomocionJugador.Cayendo;
    }

    private Vector2 GestionarMovimientoVertical(double delta, Vector2 velocidad)
    {
        if (IsOnFloor())
        {
            CaidaRapida = null;
            _framesCaidaRapida = 0;
            return velocidad;
        }

        bool subiendo = velocidad.Y < 0;
        if (subiendo)
        {
            // Cancelar salto con ↓
            if (Input.IsActionJustPressed("ui_down"))
            {
                velocidad = CancelarSalto(velocidad);
            }
            // Jump cut: soltar salto antes de tiempo
            else if (!Input.IsActionPressed("ui_accept"))
            {
                velocidad = FrenarSalto(delta, velocidad);
            }
        }

        bool bajando = velocidad.Y > 0;
        if (bajando)
        {
            ActualizarCaidaRapida();
        }

        // Aplicar gravedad vertical
        velocidad = AplicarGravedad(delta, velocidad);

        // Limitar velocidad máxima de caída
        if (velocidad.Y > MAXIMA_VELOCIDAD_CAIDA)
            velocidad.Y = MAXIMA_VELOCIDAD_CAIDA;

        return velocidad;
    }

    private void ActualizarCaidaRapida()
    {
        // Si estamos presionando ↓ y no estamos atravesando plataformas, activamos la caída rápida mínima
        if (Input.IsActionPressed("ui_down") && !SistemaPlataformas.AtravesandoPlataformas())
            _framesCaidaRapida = FRAMES_CAIDA_RAPIDA_MIN;

        // Activar la caída rápida mientras queden frames
        if (_framesCaidaRapida > 0)
        {
            CaidaRapida = true;
            _framesCaidaRapida--;
        }
        else
        {
            CaidaRapida = false;
        }
    }

    private Vector2 CancelarSalto(Vector2 velocidad)
    {
        GD.Print("Salto interrumpido.");
        velocidad.Y = Mathf.Max(velocidad.Y, 0);
        return velocidad;
    }

    private Vector2 FrenarSalto(double delta, Vector2 velocidad)
    {
        GD.Print("Frenando salto.");
        velocidad.Y += Gravedad * GRAVEDAD_EXTRA_CORTE_SALTO * (float)delta;
        return velocidad;
    }

    private Vector2 AplicarGravedad(double delta, Vector2 velocidad)
    {
        bool subiendo = velocidad.Y < 0;
        bool cayendo = velocidad.Y > 0;

        float gravedadAplicada = Gravedad;

        if (subiendo && Mathf.Abs(velocidad.Y) < UMBRAL_APEX)
            gravedadAplicada *= MULTIPLICADOR_GRAVEDAD_APEX;
        else if (cayendo)
            gravedadAplicada *= CaidaRapida == true ? MULTIPLICADOR_CAIDA_RAPIDA : MULTIPLICADOR_CAIDA;

        velocidad.Y += gravedadAplicada * (float)delta;

        return velocidad;
    }

    private Vector2 OnInterrumpirSalto(Vector2 velocidad)
    {
        GD.Print("Salto interrumpido.");
        velocidad.Y = Mathf.Max(velocidad.Y, 0);
        CaidaRapida = false;

        return velocidad;
    }


    private bool PuedeSaltar()
    {
        return _coyoteFrames > 0;
    }

    private bool HayInputSalto()
    {
        return _jumpBufferFrames > 0;
    }

    private Vector2 GestionarSalto(double delta, Vector2 velocidad)
    {
        if (EstadoLocomocion != EstadoLocomocionJugador.Saltando)
        {
            if (HayInputSalto() && PuedeSaltar())
            {
                if (Input.IsActionPressed("ui_down"))
                {
                    if (SistemaPlataformas.HayPlataformasDebajo())
                    {
                        velocidad = SistemaPlataformas.AtravesarPlataformasDebajo(delta, velocidad);
                    }
                }
                else
                {
                    velocidad.Y = Mathf.Min(velocidad.Y, 0);
                    velocidad.Y -= VELOCIDAD_SALTO;
                }

                _coyoteFrames = 0;
                _jumpBufferFrames = 0;
            }
        }
        else
        {
            if (!Input.IsActionPressed("ui_accept") && velocidad.Y < 0)
                velocidad.Y += Gravedad * GRAVEDAD_EXTRA_CORTE_SALTO * (float)delta;
        }

        return velocidad;
    }

    private Vector2 GestionarMovimientoHorizontal(double delta, Vector2 velocidad)
    {
        float direccion = Input.GetAxis("ui_left", "ui_right");

        if (_mantenerAnimacionRodar)
        {
            // Mantener velocidad de rodar aunque cambie el estado
            velocidad.X = _velocidadInicialRodar;
            return velocidad; // ignorar input
        }

        float aceleracion = IsOnFloor() ? ACELERACION_SUELO : ACELERACION_AIRE;

        if (EstadoLocomocion == EstadoLocomocionJugador.Saltando && Mathf.Abs(Velocity.Y) < UMBRAL_APEX)
        {
            aceleracion *= MULTIPLICADOR_CONTROL_APEX;
        }

        float objetivoX = direccion * VELOCIDAD;

        if (!IsOnFloor())
        {
            if (Mathf.Abs(velocidad.X) > Mathf.Abs(objetivoX) && Mathf.Sign(velocidad.X) == Mathf.Sign(objetivoX))
            {
                return velocidad;
            }
        }

        velocidad.X = Mathf.MoveToward(velocidad.X, objetivoX, aceleracion * (float)delta);
        velocidad.X = Mathf.Clamp(velocidad.X, -VELOCIDAD, VELOCIDAD);

        if (direccion != 0)
            _animatedSprite2D.FlipH = direccion < 0;

        return velocidad;
    }

    private void CambiarEstadoLocomocion(EstadoLocomocionJugador nuevoEstado)
    {
        EstadoLocomocionAnterior = EstadoLocomocion;

        if (EstadoLocomocion == nuevoEstado)
            return;

        EstadoLocomocion = nuevoEstado;

        OnEstadoLocomocionChanged(EstadoLocomocionAnterior, nuevoEstado);
    }

    private void EvaluarEstadoLocomocion()
    {
        // Reducimos contador si estamos en un estado temporal
        if (_framesEstadoTemporal > 0)
        {
            _framesEstadoTemporal--;
            if (_framesEstadoTemporal == 0)
            {
                // Al terminar la duración, pasar a estado normal
                if (EstadoLocomocion == EstadoLocomocionJugador.Aterrizando)
                {
                    CambiarEstadoLocomocion(EstadoLocomocionJugador.EnSuelo);
                }
            }

            return; // Mientras dure el estado temporal, no evaluamos otro cambio
        }

        var nuevoEstado = CalcularEstadoLocomocion();

        // Iniciar estados temporales con contador
        if (nuevoEstado == EstadoLocomocionJugador.Aterrizando)
        {
            _framesEstadoTemporal = FRAMES_ATERRIZAR;
        }

        CambiarEstadoLocomocion(nuevoEstado);
    }

    private void OnEstadoLocomocionChanged(EstadoLocomocionJugador anterior, EstadoLocomocionJugador actual)
    {
        if (anterior == EstadoLocomocionJugador.Cayendo &&
            actual == EstadoLocomocionJugador.Aterrizando)
        {
            OnAterrizar();
        }

        if (anterior == EstadoLocomocionJugador.EnSuelo &&
            actual == EstadoLocomocionJugador.Saltando)
        {
            OnDespegar();
        }

        switch (actual)
        {
            case EstadoLocomocionJugador.EnSuelo:
                _mantenerAnimacionRodar = false;
                GD.Print("Jugador en el suelo.");
                break;

            case EstadoLocomocionJugador.Saltando:
                GD.Print("Jugador saltando.");
                break;

            case EstadoLocomocionJugador.Cayendo:
                GD.Print("Jugador cayendo.");
                break;

            case EstadoLocomocionJugador.Aterrizando:
                GD.Print("Jugador Aterrizando.");
                break;

            case EstadoLocomocionJugador.Rodando:
                GD.Print("Jugador rodando.");
                _mantenerAnimacionRodar = true;
                break;
        }
    }

    private void OnAterrizar()
    {
        GD.Print("Aterrizando.");
    }

    private void OnDespegar()
    {
        GD.Print("Despegando.");
    }

    private void ActualizarAnimacion()
    {
        switch (EstadoLocomocion)
        {
            case EstadoLocomocionJugador.EnSuelo:
                ReproducirAnimacion(Mathf.Abs(Velocity.X) > 5f ? NOMBRE_ANIMACION_CORRER : NOMBRE_ANIMACION_IDLE);
                break;

            case EstadoLocomocionJugador.Saltando:
                if (_mantenerAnimacionRodar)
                    ReproducirAnimacion(NOMBRE_ANIMACION_RODANDO, true);
                else
                    ReproducirAnimacion(NOMBRE_ANIMACION_SALTAR);
                break;

            case EstadoLocomocionJugador.Cayendo:
                if (_mantenerAnimacionRodar)
                    ReproducirAnimacion(NOMBRE_ANIMACION_RODANDO, true);
                else
                    ReproducirAnimacion(NOMBRE_ANIMACION_CAER);
                break;

            case EstadoLocomocionJugador.Aterrizando:
                ReproducirAnimacion(NOMBRE_ANIMACION_ATERRIZAR); // nueva animación
                break;

            case EstadoLocomocionJugador.Rodando:
                if (_mantenerAnimacionRodar)
                    ReproducirAnimacion(NOMBRE_ANIMACION_RODANDO, true);
                else
                    ReproducirAnimacion(NOMBRE_ANIMACION_RODAR, true);
                break;
        }
    }

    private void ReproducirAnimacion(string nombreAnimacion, bool forzarReproducir = false)
    {
        if (_animatedSprite2D.Animation == nombreAnimacion && !forzarReproducir)
            return;

        _animatedSprite2D.Play(nombreAnimacion);
    }
}