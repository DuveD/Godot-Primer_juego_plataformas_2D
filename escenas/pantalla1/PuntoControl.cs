using System;
using Godot;
using PrimerjuegoPlataformas2D.escenas.entidades.jugador;

namespace PrimerjuegoPlataformas2D.escenas.pantalla1;

public partial class PuntoControl : Marker2D
{
	private bool _activado = false;

	private Area2D _area2D;
	private Sprite2D _sprite2D;

	// Called when the node enters the scene tree for the first time.

	public override void _Ready()
	{
		_area2D = GetNode<Area2D>("Area2D");
		_area2D.BodyEntered += OnBodyEntered;
		_sprite2D = GetNode<Sprite2D>("Sprite2D");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.

	public override void _Process(double delta)
	{
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Jugador jugador)
			OnJugadorEntered(jugador);
	}

	private void OnJugadorEntered(Jugador jugador)
	{
		if (_activado)
			return;

		jugador.InformarPuntoSpawn(this);
		_activado = true;

		ActivarAnimacion();
	}

	private void ActivarAnimacion()
	{
		var tween = CreateTween();

		float duracion = 0.08f;
		float escala = 1f;

		for (int i = 0; i < 6; i++)
		{
			escala *= -1;

			tween.TweenProperty(
				_sprite2D,
				"scale:x",
				escala,
				duracion
			);

			duracion *= 1.5f; // cada vuelta más lenta

		}
	}
}
