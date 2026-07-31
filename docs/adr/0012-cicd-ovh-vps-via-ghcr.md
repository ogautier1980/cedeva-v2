# 0012 — CI/CD GitHub Actions → VPS OVH via GHCR

**Statut :** Accepté (remplace [0007](0007-cicd-azure-app-service-with-health-gate.md))

## Contexte
L'hébergement a migré d'Azure App Service vers un VPS OVH auto-géré (Ubuntu 24.04, Docker Compose
— app + PostgreSQL + Caddy), pour réduire le coût et la dépendance Azure (décision actée avec
l'utilisateur, cf. `docs/notion/BACKLOG.md`). Le pipeline de déploiement basé sur `az webapp
deploy` et l'OIDC Azure n'a plus de cible.

## Décision
Nouveau workflow [deploy-vps.yml](../../.github/workflows/deploy-vps.yml) sur push `main` :
`checkout → setup .NET 10 → dotnet test (gate 85 %) → build & push de l'image Docker sur GHCR
(ghcr.io/ogautier1980/cedeva-v2) → déploiement SSH sur le VPS (docker compose pull && up -d
--remove-orphans) → vérification /health`. Le gate `/health` est conservé à l'identique de l'ADR
0007 (un déploiement n'est vert que si l'app répond réellement en HTTPS).

Pas d'étape de migration EF séparée dans le pipeline : les migrations s'appliquent automatiquement
au démarrage de l'app, en tâche de fond non-bloquante (voir
[ADR 0009](0009-background-nonblocking-startup-seeding.md)) — un `docker compose up -d` suffit.

Secrets GitHub dédiés : `VPS_HOST`, `VPS_USER`, `VPS_SSH_PRIVATE_KEY` (clé SSH `cedeva_ci_deploy`,
distincte de la clé d'administration manuelle du VPS). L'authentification GHCR sur le VPS utilise
le `GITHUB_TOKEN` éphémère de l'exécution du workflow (pas de PAT longue durée stocké sur le VPS).

## Conséquences
- Plus de dépendance à Azure OIDC / `az webapp deploy` / `AZURE_SQL_CONNECTION_STRING` — ces
  secrets GitHub ont été supprimés.
- Déploiement plus rapide et plus simple : l'image est construite une fois en CI, puis seulement
  *pullée* sur le VPS (pas de rebuild côté serveur).
- Nouvelle surface à surveiller : disponibilité du VPS lui-même (pas de plateforme managée
  équivalente à Azure App Service — pas de scaling automatique, pas de slot de staging).
- `e2e-tests.yml` et `integration-sql.yml` restent inchangés (déjà indépendants d'Azure).

## Alternatives écartées
- **Rebuild de l'image directement sur le VPS via `git pull` + `docker compose up --build`** :
  plus simple à mettre en place mais plus lent (rebuild à chaque déploiement) et charge le VPS de
  production avec la compilation ; écarté au profit d'un build unique en CI.
- **Registre Docker Hub au lieu de GHCR** : GHCR est déjà lié au dépôt GitHub (auth via
  `GITHUB_TOKEN`, pas de compte tiers à gérer), retenu par simplicité.
