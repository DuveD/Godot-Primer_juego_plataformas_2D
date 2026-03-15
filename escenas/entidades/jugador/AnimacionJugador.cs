namespace PrimerjuegoPlataformas2D.escenas.entidades.jugador;

public readonly struct AnimacionJugador(string nombre)
{
    public string Nombre { get; } = nombre;

    public static readonly AnimacionJugador Idle = new("idle");
    public static readonly AnimacionJugador Correr = new("correr");
    public static readonly AnimacionJugador Saltar = new("saltar");
    public static readonly AnimacionJugador Caer = new("caer");
    public static readonly AnimacionJugador Rodar = new("rodar");
    public static readonly AnimacionJugador Rodando = new("rodando");
    public static readonly AnimacionJugador Golpeado = new("golpeado");
    public static readonly AnimacionJugador Muerte = new("muerte");
    public static readonly AnimacionJugador MuerteEnCaida = new("muerteEnCaida");
}