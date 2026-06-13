# 0007 — CI/CD GitHub Actions → Azure App Service avec gate `/health`

**Statut :** Accepté

## Contexte
Historiquement, ~27 % des déploiements échouaient à l'étape « Deploy to Azure Web App » avec
« site failed to start within 10 mins ». La vérification post-déploiement ne testait que l'état
App Service (`Running`), pas la santé réelle de l'application.

## Décision
Workflow unique [main_cedeva-demo.yml](../../.github/workflows/main_cedeva-demo.yml) sur push `main` :
`checkout → setup .NET 10 → publish → dotnet test (gate) → login Azure (OIDC) → ef database update →
az webapp deploy (zip) → vérification /health`. L'étape finale sonde l'endpoint **`/health`**
(health check EF/DB) : un déploiement n'est vert que si l'app répond réellement.

Mesures de fiabilité associées : **Always On** activé (cold-starts), `WEBSITES_CONTAINER_START_TIME_LIMIT`
relevé, et seeding non-bloquant (voir [0009](0009-background-nonblocking-startup-seeding.md)).

## Conséquences
- Le gate de tests empêche un déploiement si la suite est rouge.
- Le gate `/health` détecte les apps qui démarrent mal (DB injoignable, crash) au lieu d'un faux
  « Running ».
- Always On + image runtime en cache ont supprimé la classe d'échecs « failed to start ».

## Alternatives écartées
- **Vérifier seulement l'état App Service** : faux positifs (Running mais app KO).
- **Slot de staging + swap (blue-green)** : souhaitable mais **bloqué** — le plan est Basic B1, les
  slots exigent Standard+ (décision de coût). Reste au backlog.
