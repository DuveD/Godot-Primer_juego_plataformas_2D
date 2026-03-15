using System;
using Godot;
using PrimerjuegoPlataformas2D.escenas.entidades.jugador;

public partial class ZonaMuerte : Area2D
{
	// Called when the node enters the scene tree for the first time.

	public override void _Ready()
	{
		this.BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Jugador jugador)
		{
			jugador.Muerte();
		}
	}
}
