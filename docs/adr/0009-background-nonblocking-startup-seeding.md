# 0009 — Seeding de démarrage non-bloquant et non-fatal

**Statut :** Accepté

## Contexte
L'app exécutait `DbSeeder.SeedAsync()` (dont `Database.MigrateAsync()`) + `TestDataSeeder`
**synchronement avant `app.Run()`**, sans gate d'environnement, en relançant l'exception en cas
d'échec. Sur Azure, ce travail bloquant retardait/empêchait la réponse à la sonde de warmup
→ « site failed to start ».

## Décision
Déplacer le seeding dans un `app.Lifetime.ApplicationStarted` exécuté en tâche de fond
(`Task.Run`), **non-fatal** (log au lieu de crash), et gater `TestDataSeeder` sur `Development`.
Un flag de config `RunStartupSeeding` (défaut `true`) permet de désactiver le seeding (utilisé par
les tests d'intégration, où les migrations SQL Server ne tournent pas sur SQLite).

## Conséquences
- Le listener HTTP démarre immédiatement → la sonde de warmup passe vite (déploiements fiables).
- Un hoquet DB au boot est journalisé sans tuer le worker.
- Les migrations restent appliquées par le pipeline CI avant déploiement (le `MigrateAsync` de
  démarrage devient un filet de sécurité, pas le chemin critique).
- Fenêtre courte en dev sur base vide : l'app sert avant la fin du seeding (acceptable).

## Alternatives écartées
- **Seeding synchrone bloquant** (état initial) : cause des timeouts de démarrage en prod.
- **`IHostedService` classique** : son `StartAsync` s'exécute **avant** que Kestrel n'écoute → bloque
  encore le warmup. `ApplicationStarted` s'exécute après.
