# Hito 3 - PepeBot 8.2

- Validacion visual original: `C:\Users\Phillips\Videos\Captures\Final DEf.mp4`
- Validacion actual: `C:\Users\Phillips\Videos\Captures\Finaldefv1.1.mp4`
- Fecha de validacion: 2026-06-29
- Rama de recuperacion: `Hito-3`
- Tag de recuperacion: `hito-3-pepebot-8.2`
- Estado: version funcional validada visualmente, estimacion de humanizacion 8.7/10

Este hito conserva el estado tras el ajuste de torretas y mirada/navegacion:
la validacion actual dura aproximadamente 2:58, termina visualmente en 0-2 con
victoria azul y reduce claramente los ciclos largos de torreta/dano/respawn
vistos antes.

Puntos principales del hito:

- Corner smoothing de NavMesh activo.
- Edge avoidance de NavMesh activo.
- Mirada anti-pared durante navegacion activa.
- Reaccion y giro contra torretas ajustados.
- Retirada bajo presion de torreta conservada para seguir ajustandola desde aqui.

Nota de reconstruccion:

- La primera referencia local de Hito 3 quedo desalineada con el gameplay real.
- Este punto fue reconstruido desde la rama `codex/humanizacion-9` y se dejaron fuera `VotingRecords` y assets ajenos al PepeBot.
- `Finaldefv1.1.mp4` confirma una mejora de duracion respecto a `Final DEf.mp4`.
- Para volver a este hito, usar la rama `Hito-3` o el tag `hito-3-pepebot-8.2`.
- El bundle antiguo `Hito3-PepeBot-8.2.bundle` no debe usarse como fuente principal si no se regenera.

Para inspeccionarlo sin alterar el proyecto actual:

```powershell
git switch Hito-3
```
