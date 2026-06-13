# 0006 — Montée à .NET 10

**Statut :** Accepté

## Contexte
Le projet ciblait `net9.0`. L'objectif est de rester sur une version récente et supportée du
runtime (sécurité, performances, support long terme).

## Décision
Cibler **`net10.0`** sur les 4 projets (Website, Core, Infrastructure, Tests) via
`Directory.Build.props` (centralisation), packages Microsoft EF Core / Identity alignés sur `10.0.x`,
Dockerfile en images `sdk:10.0` / `aspnet:10.0`, et `setup-dotnet@v…` en `10.x` dans le CI.

⚠️ Le runtime de l'hôte doit suivre : l'App Service Azure a dû passer en `DOTNETCORE|10.0` (sinon le
conteneur sort en exit 150 « framework not found »). Voir [0007](0007-cicd-azure-app-service-with-health-gate.md).

## Conséquences
- Version récente, build 0 warning, 95 tests verts.
- Toute dépendance d'hôte (App Service stack, image Docker) doit être alignée sur .NET 10.

## Alternatives écartées
- **Rester en .NET 9** : reporte la dette, perd les améliorations runtime.
