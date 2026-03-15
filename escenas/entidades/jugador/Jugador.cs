using System;
using System.Threading.Tasks;
using Godot;
using PrimerjuegoPlataformas2D.escenas.entidades.enemigos.slime;

namespace PrimerjuegoPlataformas2D.escenas.entidades.jugador;

public partial class Jugador : CharacterBody2D
{
    #region Nodos
    private AnimatedSprite2D _animatedSprite2D;
    public CollisionShape2D CollisionShape2D;
    public Area2D SensorSuelo;
    private Camera2D _camera2D;
    public SistemaPlataformas SistemaPlataformas;
    public Area2D HitBox;
    #endregion

    #region Spawn
    [Export] public Marker2D PuntoSpawn;
    #endregion

    #region Físicas
    public float Gravedad = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");

    private int _direccion = 1;

    [Export]
    public float VELOCIDAD = 130f;
    [Export]
    public float VELOCIDAD_SALTO = 320.0f;
    [Export]
    public float MAXIMA_VELOCIDAD_CAIDA = 350f;

    private const float ACELERACION_SUELO = 1000f;
    private const float ACELERACION_AIRE = 500f;
    #endregion

    #region Estado de locomoción
    /// <summary>
    /// Describe el estado físico vertical del jugador.
    /// </summary>
    public enum EstadoLocomocionJugador { EnSuelo, Saltando, Cayendo }

    public EstadoLocomocionJugador EstadoLocomocionAnterior = EstadoLocomocionJugador.EnSuelo;
    public EstadoLocomocionJugador EstadoLocomocion = EstadoLocomocionJugador.EnSuelo;

    private int _framesEstadoTemporal = 0;

    bool _enSuelo = false;
    bool _enPared = false;
    #endregion

    #region Salto
    private int _coyoteFrames = 0;
    private const int MAX_COYOTE_FRAMES = 10;

    private int _jumpBufferFrames = 0;
    private const int MAX_JUMP_BUFFER = 6;

    private const float GRAVEDAD_EXTRA_CORTE_SALTO = 2f;

    private const float UMBRAL_APEX = 40f;
    private const float MULTIPLICADOR_CONTROL_APEX = 1.5f;
    private const float MULTIPLICADOR_GRAVEDAD_APEX = 0.5f;
    #endregion

    #region Caída
    private const float MULTIPLICADOR_CAIDA = 1.8f;
    private const float MULTIPLICADOR_CAIDA_RAPIDA = 2.5f;

    private int _framesCaidaRapida = 0;
    private const int FRAMES_CAIDA_RAPIDA_MIN = 2;

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
    #endregion

    #region Rodar
    [Export]
    public float VELOCIDAD_RODAR = 200f;

    private int _framesRodando = 0;
    private const int FRAMES_RODAR = 16;

    private bool _rodandoIniciado = false;
    private float _velocidadInicialRodar = 0f;

    public bool Rodando
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            if (value)
                OnRodando();
            else
                OnDejarRodar();
        }
    }
    #endregion

    #region Inputs
    private struct InputJugador
    {
        public int Direccion;
        public bool SaltoPresionado;
        public bool SaltoMantenido;
        public bool Rodar;
        public bool Abajo;
    }
    #endregion

    #region Muerte
    public bool DesactivarFisicas = false;

    private const float DISTANCIA_SUPERIOR_ANIMACION_MUERTE = 80f;
    private const float DISTANCIA_FINAL_ANIMACION_MUERTE = 600f;
    private const float VELOCIDAD_SUBIDA_MUERTE = 200f;
    private const float VELOCIDAD_BAJADA_MUERTE = 600f;
    #endregion

    public override void _Ready()
    {
        this._animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        this.CollisionShape2D = GetNode<CollisionShape2D>("CollisionShape2D");
        this.SensorSuelo = GetNode<Area2D>("SensorSuelo");
        this._camera2D = GetNode<Camera2D>("Camera2D");
        this.HitBox = GetNode<Area2D>("HitBox");

        HitBox.BodyEntered += OnBodyEntered;

        this.SistemaPlataformas = new SistemaPlataformas(this);
        AddChild(SistemaPlataformas);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Si están desactivadas las físicas del jugador, no procesamos nada.
        if (DesactivarFisicas)
            return;

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

    private InputJugador LeerInput()
    {
        return new InputJugador
        {
            Direccion = (int)Input.GetAxis("ui_left", "ui_right"),
            SaltoPresionado = Input.IsActionJustPressed("ui_accept"),
            SaltoMantenido = Input.IsActionPressed("ui_accept"),
            Rodar = Input.IsActionJustPressed("rodar"),
            Abajo = Input.IsActionPressed("ui_down")
        };
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

    private Vector2 GestionarMovimientoVertical(double delta, Vector2 velocidad, InputJugador inputJugador)
    {
        // Procesamos el salto.
        velocidad = ProcesarSalto(delta, velocidad, inputJugador);

        // Aplicamos la gravedad.
        velocidad = AplicarGravedad(delta, velocidad, inputJugador);

        // Limitamos la velocidad máxima de caída.
        if (velocidad.Y > MAXIMA_VELOCIDAD_CAIDA)
            velocidad.Y = MAXIMA_VELOCIDAD_CAIDA;

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
                    velocidad.Y -= VELOCIDAD_SALTO;
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
        ProcesarRodar(velocidad, inputJugador);
        if (Rodando)
            return MantenerVelocidadRodar(velocidad);

        velocidad = AplicarAceleracionHorizontal(delta, velocidad, inputJugador);

        if (velocidad.X != 0)
        {
            _animatedSprite2D.FlipH = velocidad.X < 0;
            _direccion = Mathf.Sign(velocidad.X);
        }

        return velocidad;
    }

    private void ProcesarRodar(Vector2 velocidad, InputJugador inputJugador)
    {
        ActualizarRodar();

        if (_enSuelo && inputJugador.Rodar)
        {
            if (!Rodando)
                IniciarRodar(inputJugador);
            else
                CambioDireccionRodar(velocidad, inputJugador);
        }
    }

    private Vector2 MantenerVelocidadRodar(Vector2 velocidad)
    {
        velocidad.X = _velocidadInicialRodar;
        return velocidad;
    }

    private void ActualizarRodar()
    {
        if (!Rodando)
            return;

        _framesRodando--;

        // Después del primer frame cambiamos a la animación loop
        if (_framesRodando < FRAMES_RODAR - 1)
            _rodandoIniciado = true;

        if (_framesRodando <= 0 && _enSuelo)
            Rodando = false;
    }

    private void IniciarRodar(InputJugador inputJugador)
    {
        Rodando = true;
        _rodandoIniciado = false;
        int direccion = inputJugador.Direccion != 0 ? inputJugador.Direccion : _direccion;
        _velocidadInicialRodar = direccion * VELOCIDAD_RODAR;
        _framesRodando = FRAMES_RODAR;
    }

    private void OnRodando()
    {
        GD.Print("Jugador rodando.");
    }

    private void OnDejarRodar()
    {
        GD.Print("Jugador dejar de rodar.");
        _rodandoIniciado = false;
    }

    private void CambioDireccionRodar(Vector2 velocidad, InputJugador inputJugador)
    {
        _rodandoIniciado = true;

        int direccion = (inputJugador.Direccion == 0 || !_enSuelo)
            ? Mathf.Sign(_velocidadInicialRodar)
            : Mathf.Sign(inputJugador.Direccion);

        _velocidadInicialRodar = direccion * VELOCIDAD_RODAR;
        _animatedSprite2D.FlipH = direccion < 0;
        _direccion = direccion;
        _framesRodando = FRAMES_RODAR;
    }

    private Vector2 AplicarAceleracionHorizontal(double delta, Vector2 velocidad, InputJugador inputJugador)
    {
        float aceleracion = _enSuelo ? ACELERACION_SUELO : ACELERACION_AIRE;

        if (EstadoLocomocion == EstadoLocomocionJugador.Saltando && Mathf.Abs(Velocity.Y) < UMBRAL_APEX)
            aceleracion *= MULTIPLICADOR_CONTROL_APEX;

        float objetivoX = inputJugador.Direccion * VELOCIDAD;

        if (!_enSuelo && Mathf.Abs(velocidad.X) > Mathf.Abs(objetivoX) &&
            Mathf.Sign(velocidad.X) == Mathf.Sign(objetivoX))
            return velocidad;

        velocidad.X = Mathf.MoveToward(velocidad.X, objetivoX, aceleracion * (float)delta);
        velocidad.X = Mathf.Clamp(velocidad.X, -VELOCIDAD, VELOCIDAD);

        return velocidad;
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

    private void CambiarEstadoLocomocion(EstadoLocomocionJugador nuevoEstado)
    {
        if (EstadoLocomocion == nuevoEstado)
            return;

        var anterior = EstadoLocomocion;
        EstadoLocomocionAnterior = anterior;
        EstadoLocomocion = nuevoEstado;

        OnEstadoLocomocionChanged(anterior, nuevoEstado);
    }

    private void OnEstadoLocomocionChanged(EstadoLocomocionJugador anterior, EstadoLocomocionJugador actual)
    {
        // Cambios de estados compuestos.
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
        // Cambios de estados simples.
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

    private void ActualizarAnimacion(InputJugador inputJugador)
    {
        if (Rodando)
        {
            ActualizarAnimacionRodando();
            return;
        }

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
        ReproducirAnimacion(AnimacionJugador.Saltar);
    }

    private void ActualizarAnimacionCayendo()
    {
        ReproducirAnimacion(AnimacionJugador.Caer);
    }

    private void ActualizarAnimacionRodando()
    {
        if (_rodandoIniciado)
            ReproducirAnimacion(AnimacionJugador.Rodando, true);
        else
            ReproducirAnimacion(AnimacionJugador.Rodar, true);
    }

    private void ReproducirAnimacion(AnimacionJugador animacion, bool forzarReproducir = false)
    {
        if (_animatedSprite2D.Animation == animacion.Nombre && !forzarReproducir)
            return;

        _animatedSprite2D.Play(animacion.Nombre);
    }

    public void InformarPuntoSpawn(Marker2D marker2D)
    {
        this.PuntoSpawn = marker2D;
    }

    public async void Muerte()
    {
        if (DesactivarFisicas)
            return;

        MarcarComoMuerto();
        await EjecutarAnimacionMuerte();
        Revivir();
    }

    private void MarcarComoMuerto()
    {
        DesactivarFisicas = true;
        Velocity = Vector2.Zero;
        Rodando = false;
    }

    private async Task EjecutarAnimacionMuerte()
    {
        // Reparentamos la cámara al escenario para mantener su posición durante la animación.
        Node padreJugador = GetParent();
        Vector2 posicionGlobalCamara = _camera2D.GlobalPosition;
        _camera2D.Reparent(padreJugador);
        _camera2D.GlobalPosition = posicionGlobalCamara;

        ReproducirAnimacion(AnimacionJugador.MuerteEnCaida);

        await ToSignal(GetTree().CreateTimer(0.5f), Timer.SignalName.Timeout);

        Tween tween = AnimacionMuerte();
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private Tween AnimacionMuerte()
    {
        Vector2 posicionInicial = Position;
        Vector2 posicionApex = posicionInicial - new Vector2(0, DISTANCIA_SUPERIOR_ANIMACION_MUERTE);
        Vector2 posicionFinal = posicionInicial + new Vector2(0, DISTANCIA_FINAL_ANIMACION_MUERTE);

        float duracionSubida = DISTANCIA_SUPERIOR_ANIMACION_MUERTE / VELOCIDAD_SUBIDA_MUERTE;
        float duracionBajada = DISTANCIA_FINAL_ANIMACION_MUERTE / VELOCIDAD_BAJADA_MUERTE;

        Tween tween = CreateTween();
        tween.SetProcessMode(Tween.TweenProcessMode.Idle);

        tween.TweenProperty(this, "position", posicionApex, duracionSubida)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);

        tween.TweenProperty(this, "position", posicionFinal, duracionBajada)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad);

        return tween;
    }

    private void Revivir()
    {
        // Movemos el jugador al últimpo punto de Spawn.
        Position = PuntoSpawn.Position;

        // Devolvemos la cámara al jugador, ahora ya en el punto de spawn.
        _camera2D.Reparent(this);
        _camera2D.Position = Vector2.Zero;

        // Devolvemos el estado de muerto a false.
        DesactivarFisicas = false;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Slime)
        {
            this.CallDeferred(nameof(OnBodyEnteredSlime));
        }
    }

    private void OnBodyEnteredSlime()
    {
        this.Muerte();
    }

}