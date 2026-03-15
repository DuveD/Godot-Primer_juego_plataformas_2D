namespace PrimerjuegoPlataformas2D.escenas.entidades.enemigos;

public readonly struct AnimacionEnemigo(string nombre)
{
    public string Nombre { get; } = nombre;

    public static readonly AnimacionEnemigo Idle = new("idle");
    public static readonly AnimacionEnemigo Caminar = new("caminar");
    public static readonly AnimacionEnemigo Saltar = new("saltar");
    public static readonly AnimacionEnemigo Caer = new("caer");
}