using System;
using Godot;

namespace PrimerjuegoPlataformas2D.escenas.entidades.enemigos.slime;

public partial class Slime : CharacterBody2D
{
    #region Nodos

    private AnimatedSprite2D _animatedSprite2D;
    public CollisionShape2D CollisionShape2D;
    public RayCast2D DetectorParedDerecha;
    public RayCast2D DetectorSueloDerechaArriba;
    public RayCast2D DetectorSueloDerechaAbajo;
    public RayCast2D DetectorParedIzquierda;
    public RayCast2D DetectorSueloIzquierdaArriba;
    public RayCast2D DetectorSueloIzquierdaAbajo;

    #endregion

    #region Físicas

    public float Gravedad = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");

    [Export]
    public float VELOCIDAD = 40f;
    [Export]
    public float VELOCIDAD_SALTO = 200.0f;
    [Export]
    public float MAXIMA_VELOCIDAD_CAIDA = 350f;

    private const float UMBRAL_APEX = 40f;
    private const float MULTIPLICADOR_GRAVEDAD_APEX = 0.5f;

    private const float ACELERACION_SUELO = 1000f;
    private const float ACELERACION_AIRE = 500f;

    #endregion

    #region Estado de locomoción

    /// <summary>
    /// Describe el estado físico vertical del jugador.
    /// </summary>
    public enum EstadoLocomocionEnemigo
    {
        EnSuelo,
        Saltando,
        Cayendo
    }

    public EstadoLocomocionEnemigo EstadoLocomocionAnterior = EstadoLocomocionEnemigo.EnSuelo;
    public EstadoLocomocionEnemigo EstadoLocomocion = EstadoLocomocionEnemigo.EnSuelo;

    bool _enSuelo = false;
    bool _enPared = false;

    private const float MULTIPLICADOR_CAIDA = 1.8f;

    #endregion

    #region Inputs
    private struct InputEnemigo
    {
        public bool CambiarDireccion;
        public bool Saltar;
    }

    private float _direccion = 1f;
    #endregion

    public override void _Ready()
    {
        this._animatedSprite2D = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        this.CollisionShape2D = GetNode<CollisionShape2D>("CollisionShape2D");
        this.DetectorParedDerecha = GetNode<RayCast2D>("DetectorParedDerecha");
        this.DetectorSueloDerechaArriba = GetNode<RayCast2D>("DetectorSueloDerechaArriba");
        this.DetectorSueloDerechaAbajo = GetNode<RayCast2D>("DetectorSueloDerechaAbajo");
        this.DetectorParedIzquierda = GetNode<RayCast2D>("DetectorParedIzquierda");
        this.DetectorSueloIzquierdaArriba = GetNode<RayCast2D>("DetectorSueloIzquierdaArriba");
        this.DetectorSueloIzquierdaAbajo = GetNode<RayCast2D>("DetectorSueloIzquierdaAbajo");
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocidad = Velocity;

        InputEnemigo inputEnemigo = ActualizarInputs();

        velocidad = GestionarMovimientoHorizontal(delta, velocidad, inputEnemigo);
        velocidad = GestionarMovimientoVertical(delta, velocidad, inputEnemigo);

        Velocity = velocidad;

        MoveAndSlide();

        EvaluarEstadoLocomocion();
        ActualizarAnimacion();
    }

    private InputEnemigo ActualizarInputs()
    {
        bool cambiarDireccion = false;
        bool saltar = false;

        if (IsOnFloor())
        {
            if (_direccion > 0)
            {
                if (DetectorParedDerecha.IsColliding())
                {
                    if (!DetectorSueloDerechaArriba.IsColliding())
                        saltar = true;
                    else
                        cambiarDireccion = true;
                }
                else if (!DetectorSueloDerechaAbajo.IsColliding())
                {
                    cambiarDireccion = true;
                }
            }
            else
            {
                if (DetectorParedIzquierda.IsColliding())
                {
                    if (!DetectorSueloIzquierdaArriba.IsColliding())
                        saltar = true;
                    else
                        cambiarDireccion = true;
                }
                else if (!DetectorSueloIzquierdaAbajo.IsColliding())
                {
                    cambiarDireccion = true;
                }
            }
        }

        return new InputEnemigo
        {
            CambiarDireccion = cambiarDireccion,
            Saltar = saltar
        };
    }

    private Vector2 GestionarMovimientoHorizontal(double delta, Vector2 velocidad, InputEnemigo inputEnemigo)
    {
        if (inputEnemigo.CambiarDireccion)
        {
            _direccion *= -1f;
        }

        velocidad.X = VELOCIDAD * _direccion;
        _animatedSprite2D.FlipH = velocidad.X < 0;

        return velocidad;
    }

    private Vector2 GestionarMovimientoVertical(double delta, Vector2 velocidad, InputEnemigo inputEnemigo)
    {
        // Procesamos el salto.
        velocidad = ProcesarSalto(delta, velocidad, inputEnemigo);

        // Aplicamos la gravedad.
        velocidad = AplicarGravedad(delta, velocidad, inputEnemigo);

        // Limitamos la velocidad máxima de caída.
        if (velocidad.Y > MAXIMA_VELOCIDAD_CAIDA)
            velocidad.Y = MAXIMA_VELOCIDAD_CAIDA;

        return velocidad;
    }

    private Vector2 ProcesarSalto(double delta, Vector2 velocidad, InputEnemigo inputEnemigo)
    {
        if (EstadoLocomocion != EstadoLocomocionEnemigo.Saltando)
        {
            if (inputEnemigo.Saltar)
            {
                velocidad.Y = Mathf.Min(velocidad.Y, 0);
                velocidad.Y -= VELOCIDAD_SALTO;
            }
        }

        return velocidad;
    }


    private Vector2 AplicarGravedad(double delta, Vector2 velocidad, InputEnemigo inputEnemigo)
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
            gravedadAplicada *= MULTIPLICADOR_CAIDA;
        }

        velocidad.Y += gravedadAplicada * (float)delta;

        return velocidad;
    }

    private void EvaluarEstadoLocomocion()
    {
        var nuevoEstado = CalcularEstadoLocomocion();
        CambiarEstadoLocomocion(nuevoEstado);
    }

    private EstadoLocomocionEnemigo CalcularEstadoLocomocion()
    {
        if (_enSuelo)
        {
            return EstadoLocomocionEnemigo.EnSuelo;
        }

        if (Velocity.Y < 0)
        {
            return EstadoLocomocionEnemigo.Saltando;
        }

        return EstadoLocomocionEnemigo.Cayendo;
    }


    private void CambiarEstadoLocomocion(EstadoLocomocionEnemigo nuevoEstado)
    {
        if (EstadoLocomocion == nuevoEstado)
            return;

        var anterior = EstadoLocomocion;
        EstadoLocomocionAnterior = anterior;
        EstadoLocomocion = nuevoEstado;

        OnEstadoLocomocionChanged(anterior, nuevoEstado);
    }

    private void OnEstadoLocomocionChanged(EstadoLocomocionEnemigo anterior, EstadoLocomocionEnemigo actual)
    {
        // Cambios de estados compuestos.
        if (anterior == EstadoLocomocionEnemigo.Cayendo &&
            actual == EstadoLocomocionEnemigo.EnSuelo)
        {
            OnAterrizar();
        }
        else if (anterior == EstadoLocomocionEnemigo.EnSuelo &&
            actual == EstadoLocomocionEnemigo.Saltando)
        {
            OnDespegar();
        }
        // Cambios de estados simples.
        else
        {
            switch (actual)
            {
                case EstadoLocomocionEnemigo.EnSuelo:
                    OnEnSuelo();
                    break;

                case EstadoLocomocionEnemigo.Saltando:
                    OnSaltando();
                    break;

                case EstadoLocomocionEnemigo.Cayendo:
                    OnCayendo();
                    break;
            }
        }
    }

    private void OnAterrizar()
    {
        OnEnSuelo();
    }

    private void OnDespegar()
    {
        OnSaltando();
    }

    private void OnSaltando()
    {
    }

    private void OnEnSuelo()
    {
    }

    private void OnCayendo()
    {
    }

    private void ActualizarAnimacion()
    {

        switch (EstadoLocomocion)
        {
            case EstadoLocomocionEnemigo.EnSuelo:
                ActualizarAnimacionEnSuelo();
                break;

            case EstadoLocomocionEnemigo.Saltando:
                ActualizarAnimacionSaltando();
                break;

            case EstadoLocomocionEnemigo.Cayendo:
                ActualizarAnimacionCayendo();
                break;
        }
    }

    private void ActualizarAnimacionEnSuelo()
    {
        if (true)//_direccion != 0)
            ReproducirAnimacion(AnimacionEnemigo.Caminar);
        else
            ReproducirAnimacion(AnimacionEnemigo.Idle);
    }

    private void ActualizarAnimacionSaltando()
    {
        ReproducirAnimacion(AnimacionEnemigo.Saltar);
    }

    private void ActualizarAnimacionCayendo()
    {
        ReproducirAnimacion(AnimacionEnemigo.Caer);
    }

    private void ReproducirAnimacion(AnimacionEnemigo animacion, bool forzarReproducir = false)
    {
        if (_animatedSprite2D.Animation == animacion.Nombre && !forzarReproducir)
            return;

        _animatedSprite2D.Play(animacion.Nombre);
    }
}