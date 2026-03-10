using System;
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
        Cayendo,
        Rodando
    }

    private int _framesEstadoTemporal = 0;


    [Export]
    public float Velocidad = 130f;

    [Export]
    public float VelocidadSalto = 320.0f;

    [Export]
    public float MaximaVelocidadCaida = 350f;

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

    private const int FRAMES_RODAR = 16;          // duración del rodar

    private bool _mantenerRodar = false;

    [Export]
    public float VelocidadRodar = 200; // velocidad horizontal durante el rodar

    private float _velocidadInicialRodar = 0f;

    private AnimatedSprite2D _animatedSprite2D;
    public CollisionShape2D CollisionShape2D;
    public Area2D SensorSuelo;
    public float Gravedad = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");

    bool _enSuelo = false;
    bool _enPared = false;

    #region Inputs
    private struct InputJugador
    {
        public float Direccion;
        public bool SaltoPresionado;
        public bool SaltoMantenido;
        public bool Rodar;
        public bool Abajo;
    }

    private InputJugador LeerInput()
    {
        return new InputJugador
        {
            Direccion = Input.GetAxis("ui_left", "ui_right"),
            SaltoPresionado = Input.IsActionJustPressed("ui_accept"),
            SaltoMantenido = Input.IsActionPressed("ui_accept"),
            Rodar = Input.IsActionJustPressed("rodar"),
            Abajo = Input.IsActionPressed("ui_down")
        };
    }
    #endregion

    public SistemaPlataformas SistemaPlataformas;
    public EstadoLocomocionJugador EstadoLocomocionAnterior = EstadoLocomocionJugador.EnSuelo;
    public EstadoLocomocionJugador EstadoLocomocion = EstadoLocomocionJugador.EnSuelo;

    public bool? CaidaRapida
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;

            if (value != null)
            {
                if (value.Value)
                    GD.Print("Caída rápida.");
                else
                    GD.Print("Caida normal.");
            }
        }
    }

    public override void _Ready()
    {
        this._animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        this.CollisionShape2D = GetNode<CollisionShape2D>("CollisionShape2D");
        this.SensorSuelo = GetNode<Area2D>("SensorSuelo");

        this.SistemaPlataformas = new SistemaPlataformas(this);
        AddChild(SistemaPlataformas);

        InputJugador inputJugador = ActualizarInputs();

        EvaluarEstadoLocomocion();
        this.ActualizarAnimacion(inputJugador);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocidad = Velocity;

        InputJugador inputJugador = ActualizarInputs();

        velocidad = GestionarMovimientoHorizontal(delta, velocidad, inputJugador);
        velocidad = GestionarMovimientoVertical(delta, velocidad, inputJugador);

        Velocity = velocidad;

        MoveAndSlide();

        EvaluarEstadoLocomocion();
        ActualizarAnimacion(inputJugador);
    }

    private InputJugador ActualizarInputs()
    {
        InputJugador inputJugador = LeerInput();

        ActualizarCoyoteTime();
        ActualizarBufferDeSalto(inputJugador);

        return inputJugador;
    }

    private void ActualizarCoyoteTime()
    {
        if (_enSuelo)
            _coyoteFrames = MAX_COYOTE_FRAMES;
        else if (_coyoteFrames > 0)
            _coyoteFrames--;
    }

    private void ActualizarBufferDeSalto(InputJugador inputJugador)
    {
        if (inputJugador.SaltoPresionado)
            _jumpBufferFrames = MAX_JUMP_BUFFER;
        else if (_jumpBufferFrames > 0)
            _jumpBufferFrames--;
    }

    public EstadoLocomocionJugador CalcularEstadoLocomocion()
    {
        if (_enSuelo)
        {
            return EstadoLocomocionJugador.EnSuelo;
        }

        if (Velocity.Y < 0)
        {
            return EstadoLocomocionJugador.Saltando;
        }

        return EstadoLocomocionJugador.Cayendo;
    }

    private Vector2 GestionarMovimientoVertical(double delta, Vector2 velocidad, InputJugador inputJugador)
    {
        // Procesamos el salto.
        velocidad = ProcesarSalto(delta, velocidad, inputJugador);

        // Al procesar el salto, la velocidad puede quedar en positivo por la frenada.
        bool cayendo = velocidad.Y > 0;
        if (cayendo && EstadoLocomocion != EstadoLocomocionJugador.Cayendo)
            CambiarEstadoLocomocion(EstadoLocomocionJugador.Cayendo);

        // Aplicamos la gravedad.
        velocidad = AplicarGravedad(delta, velocidad, inputJugador);

        // Limitamos la velocidad máxima de caída.
        if (velocidad.Y > MaximaVelocidadCaida)
            velocidad.Y = MaximaVelocidadCaida;

        return velocidad;
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

    private Vector2 AplicarGravedad(double delta, Vector2 velocidad, InputJugador inputJugador)
    {
        bool subiendo = velocidad.Y < 0;
        bool cayendo = velocidad.Y > 0;

        float gravedadAplicada = Gravedad;

        if (subiendo && Mathf.Abs(velocidad.Y) < UMBRAL_APEX)
        {
            gravedadAplicada *= MULTIPLICADOR_GRAVEDAD_APEX;
        }
        else if (cayendo)
        {
            ActualizarCaidaRapida(inputJugador);
            gravedadAplicada *= CaidaRapida == true ? MULTIPLICADOR_CAIDA_RAPIDA : MULTIPLICADOR_CAIDA;
        }

        velocidad.Y += gravedadAplicada * (float)delta;

        return velocidad;
    }

    private void ActualizarCaidaRapida(InputJugador inputJugador)
    {
        // Si estamos presionando ↓ y no estamos atravesando plataformas, activamos la caída rápida mínima
        if (inputJugador.Abajo && !SistemaPlataformas.AtravesandoPlataformas())
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

    private bool PuedeSaltar()
    {
        return _coyoteFrames > 0;
    }

    private bool HayInputSalto()
    {
        return _jumpBufferFrames > 0;
    }

    private Vector2 ProcesarSalto(double delta, Vector2 velocidad, InputJugador inputJugador)
    {
        if (EstadoLocomocion != EstadoLocomocionJugador.Saltando)
        {
            if (HayInputSalto() && PuedeSaltar())
            {
                if (inputJugador.Abajo)
                {
                    if (SistemaPlataformas.HayPlataformasDebajo())
                    {
                        velocidad = SistemaPlataformas.AtravesarPlataformasDebajo(delta, velocidad);
                    }
                }
                else
                {
                    velocidad.Y = Mathf.Min(velocidad.Y, 0);
                    velocidad.Y -= VelocidadSalto;
                }

                _coyoteFrames = 0;
                _jumpBufferFrames = 0;

                return velocidad;
            }
        }

        bool subiendo = velocidad.Y < 0;
        if (subiendo)
        {
            // Cancelar salto con ↓
            if (inputJugador.Abajo)
            {
                velocidad = CancelarSalto(velocidad);
            }
            // Jump cut: soltar salto antes de tiempo
            else if (!inputJugador.SaltoMantenido)
            {
                velocidad = FrenarSalto(delta, velocidad);
            }
        }

        return velocidad;
    }

    private Vector2 GestionarMovimientoHorizontal(double delta, Vector2 velocidad, InputJugador inputJugador)
    {
        ProcesarRodar(inputJugador);

        if (_mantenerRodar)
            return MantenerVelocidadRodar(velocidad);

        velocidad = AplicarAceleracionHorizontal(delta, velocidad, inputJugador);

        if (inputJugador.Direccion != 0)
            _animatedSprite2D.FlipH = inputJugador.Direccion < 0;

        return velocidad;
    }

    private void ProcesarRodar(InputJugador inputJugador)
    {
        if (!_enSuelo || !inputJugador.Rodar)
            return;

        if (EstadoLocomocion != EstadoLocomocionJugador.Rodando)
            IniciarRodar();
        else
            CambiarDireccionRodar(inputJugador);
    }

    private void IniciarRodar()
    {
        CambiarEstadoLocomocion(EstadoLocomocionJugador.Rodando);
        _velocidadInicialRodar = ObtenerDireccionActual() * VelocidadRodar;
        _framesEstadoTemporal = FRAMES_RODAR;
    }

    private void CambiarDireccionRodar(InputJugador inputJugador)
    {
        float direccion = Mathf.Sign(_velocidadInicialRodar);
        if (Mathf.Sign(inputJugador.Direccion) != 0 && Mathf.Sign(inputJugador.Direccion) != direccion)
        {
            direccion = Mathf.Sign(inputJugador.Direccion);
        }

        _velocidadInicialRodar = Mathf.Sign(direccion) * VelocidadRodar;
        _animatedSprite2D.FlipH = inputJugador.Direccion < 0;
        _framesEstadoTemporal = FRAMES_RODAR;
    }

    private float ObtenerDireccionActual()
    {
        return _animatedSprite2D.FlipH ? -1f : 1f;
    }

    private Vector2 MantenerVelocidadRodar(Vector2 velocidad)
    {
        velocidad.X = _velocidadInicialRodar;
        return velocidad;
    }

    private Vector2 AplicarAceleracionHorizontal(double delta, Vector2 velocidad, InputJugador inputJugador)
    {
        float aceleracion = _enSuelo ? ACELERACION_SUELO : ACELERACION_AIRE;

        if (EstadoLocomocion == EstadoLocomocionJugador.Saltando && Mathf.Abs(Velocity.Y) < UMBRAL_APEX)
            aceleracion *= MULTIPLICADOR_CONTROL_APEX;

        float objetivoX = inputJugador.Direccion * Velocidad;

        if (!_enSuelo && Mathf.Abs(velocidad.X) > Mathf.Abs(objetivoX) &&
            Mathf.Sign(velocidad.X) == Mathf.Sign(objetivoX))
            return velocidad;

        velocidad.X = Mathf.MoveToward(velocidad.X, objetivoX, aceleracion * (float)delta);
        velocidad.X = Mathf.Clamp(velocidad.X, -Velocidad, Velocidad);

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
        _enSuelo = IsOnFloor();
        _enPared = IsOnWall();

        // Reducimos contador si estamos en un estado temporal
        if (_framesEstadoTemporal > 0)
        {
            _framesEstadoTemporal--;

            return; // Mientras dure el estado temporal, no evaluamos otro cambio
        }

        var nuevoEstado = CalcularEstadoLocomocion();
        CambiarEstadoLocomocion(nuevoEstado);
    }

    private void OnEstadoLocomocionChanged(EstadoLocomocionJugador anterior, EstadoLocomocionJugador actual)
    {
        if (anterior == EstadoLocomocionJugador.Cayendo &&
            actual == EstadoLocomocionJugador.EnSuelo)
        {
            OnAterrizar();
        }
        else if (anterior == EstadoLocomocionJugador.EnSuelo &&
            actual == EstadoLocomocionJugador.Saltando)
        {
            OnDespegar();
        }
        else
        {
            switch (actual)
            {
                case EstadoLocomocionJugador.EnSuelo:
                    OnEnSuelo();
                    break;

                case EstadoLocomocionJugador.Saltando:
                    OnSaltando();
                    break;

                case EstadoLocomocionJugador.Cayendo:
                    OnCayendo();
                    break;

                case EstadoLocomocionJugador.Rodando:
                    OnRodando();
                    break;
            }
        }
    }

    private void OnAterrizar()
    {
        GD.Print("Aterrizando.");
        OnEnSuelo();
    }

    private void OnDespegar()
    {
        GD.Print("Despegando.");
        OnSaltando();
    }

    private void OnEnSuelo()
    {
        _mantenerRodar = false;

        CaidaRapida = null;
        _framesCaidaRapida = 0;

        GD.Print("Jugador en el suelo.");
    }

    private void OnSaltando()
    {
        GD.Print("Jugador saltando.");
    }

    private void OnCayendo()
    {
        GD.Print("Jugador cayendo.");
    }

    private void OnRodando()
    {
        GD.Print("Jugador rodando.");
        _mantenerRodar = true;
    }

    private void ActualizarAnimacion(InputJugador inputJugador)
    {
        switch (EstadoLocomocion)
        {
            case EstadoLocomocionJugador.EnSuelo:
                ActualizarAnimacionEnSuelo(inputJugador);
                break;

            case EstadoLocomocionJugador.Saltando:
                ActualizarAnimacionSaltando();
                break;

            case EstadoLocomocionJugador.Cayendo:
                ActualizarAnimacionCayendo();
                break;

            case EstadoLocomocionJugador.Rodando:
                ActualizarAnimacionRodando();
                break;
        }
    }

    private void ActualizarAnimacionEnSuelo(InputJugador inputJugador)
    {
        if (inputJugador.Direccion != 0 && !IsOnWall())
            ReproducirAnimacion(AnimacionJugador.Correr);
        else
            ReproducirAnimacion(AnimacionJugador.Idle);
    }

    private void ActualizarAnimacionSaltando()
    {
        if (_mantenerRodar)
            ReproducirAnimacion(AnimacionJugador.Rodando, true);
        else
            ReproducirAnimacion(AnimacionJugador.Saltar);
    }

    private void ActualizarAnimacionCayendo()
    {
        if (_mantenerRodar)
            ReproducirAnimacion(AnimacionJugador.Rodando, true);
        else
            ReproducirAnimacion(AnimacionJugador.Caer);
    }

    private void ActualizarAnimacionRodando()
    {
        if (_mantenerRodar)
            ReproducirAnimacion(AnimacionJugador.Rodando, true);
        else
            ReproducirAnimacion(AnimacionJugador.Rodar, true);
    }

    private void ReproducirAnimacion(AnimacionJugador animacionJugador, bool forzarReproducir = false)
    {
        if (_animatedSprite2D.Animation == animacionJugador.Nombre && !forzarReproducir)
            return;

        _animatedSprite2D.Play(animacionJugador.Nombre);
    }
}