# Hito 3 - PepeBot 8.2

- Commit de validacion: `7aaac2251d4238b1b3ec8c3ddd70c75bc82dda7d`
- Rama: `Hito-3`
- Fecha de validacion: 2026-06-29
- Video de validacion: `C:\Users\Phillips\Videos\Captures\Final DEf.mp4`
- Bundle: `Hito3-PepeBot-8.2.bundle`
- Tag: `hito-3-pepebot-8.2`
- Estado: version funcional validada visualmente, estimacion de humanizacion 8.2/10

Este hito conserva el estado tras el ajuste de torretas y mirada/navegacion:
la partida validada dura aproximadamente 4:09, termina visualmente en 3-0 y
reduce claramente los ciclos largos de torreta/dano/respawn vistos antes.

Puntos principales del hito:

- Corner smoothing de NavMesh activo.
- Edge avoidance de NavMesh activo.
- Mirada anti-pared durante navegacion activa.
- Reaccion y giro contra torretas ajustados.
- Queda pendiente mejorar retirada tactica bajo fuego de torreta.

Para inspeccionarlo sin alterar el proyecto actual:

```powershell
git clone "C:\Concurso AI\Milestones\Hito3-PepeBot-8.2.bundle" "C:\Ruta\PepeBot-Hito3"
```

No mover ni actualizar la referencia del hito cuando se creen versiones nuevas.
