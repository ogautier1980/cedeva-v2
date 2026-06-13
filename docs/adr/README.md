# Architecture Decision Records (ADR)

Décisions structurantes de Cedeva et leur *motivation*. Format léger (MADR) :
**Statut · Contexte · Décision · Conséquences · Alternatives écartées**.

Une décision n'est jamais réécrite : si elle change, on ajoute un nouvel ADR qui remplace
(« superseded by ») l'ancien.

| # | Décision | Statut |
|---|----------|--------|
| [0001](0001-record-architecture-decisions.md) | Documenter les décisions via des ADR | Accepté |
| [0002](0002-clean-architecture-and-feature-folders.md) | 3 projets (Website/Core/Infrastructure) + feature folders | Accepté |
| [0003](0003-multi-tenancy-via-ef-global-query-filters.md) | Multi-tenancy par filtres de requête EF Core | Accepté |
| [0004](0004-autofac-dependency-injection.md) | Autofac comme conteneur DI | Accepté |
| [0005](0005-repository-and-direct-dbcontext.md) | Repository+UoW *et* accès direct au DbContext | Accepté |
| [0006](0006-upgrade-to-net-10.md) | Montée à .NET 10 | Accepté |
| [0007](0007-cicd-azure-app-service-with-health-gate.md) | CI/CD GitHub Actions → Azure App Service avec gate `/health` | Accepté |
| [0008](0008-cookie-identity-and-security-hardening.md) | Auth Identity par cookie + durcissement sécurité | Accepté |
| [0009](0009-background-nonblocking-startup-seeding.md) | Seeding de démarrage non-bloquant et non-fatal | Accepté |
