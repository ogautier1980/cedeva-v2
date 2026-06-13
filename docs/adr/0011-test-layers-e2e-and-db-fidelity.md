# 0011 — Niveaux de test : E2E navigateur + fidélité base, et cliquet de couverture

**Statut :** Accepté (complète [0007](0007-cicd-azure-app-service-with-health-gate.md))

## Contexte
La suite reposait sur de l'unitaire + intégration sur **SQLite in-memory** et `WebApplicationFactory`.
Deux angles morts ont été démontrés par des régressions réelles :
1. **Côté navigateur** — une CSP a bloqué jQuery/validation/autocomplétion ; invisible aux tests HTTP
   qui n'exécutent pas de JavaScript.
2. **Côté base** — des requêtes EF non traduisibles et des différences de **collation** (SQLite est
   sensible à la casse, SQL Server `CI_AS` ne l'est pas) passaient inaperçues sur SQLite.
Par ailleurs l'« intention » de couverture n'était pas mesurée.

## Décision
Ajouter deux niveaux de **haute fidélité**, dans des projets et workflows CI **dédiés** :
- **`tests/Cedeva.Tests.E2E`** — Playwright + Chromium headless contre l'app sur **Kestrel réel**
  (`PlaywrightAppFactory`, http pour neutraliser `UseHttpsRedirection`, SQLite). Capture erreurs
  console / violations CSP. Workflow `e2e-tests.yml`.
- **`tests/Cedeva.Tests.Sql`** — **SQL Server 2022** réel via `Testcontainers.MsSql`, schéma appliqué
  par les **migrations réelles** (`MigrateAsync`, pas `EnsureCreated`). Workflow `integration-sql.yml`.
- **Gate de couverture chiffré** dans le workflow de déploiement (`coverlet`, seuil ligne en **cliquet**
  qu'on remonte : 12 % → 40 % → 75 % → 85 %), migrations et vues générées exclues.

Ces deux workflows tournent à chaque push mais **ne bloquent pas** le déploiement (signal de qualité) ;
seul `tests/Cedeva.Tests` + le gate de couverture bloquent la prod.

## Conséquences
- Les régressions front (CSP, JS) et les écarts SQLite↔SQL Server sont attrapés en CI.
- Coût : Docker requis pour le niveau SQL, navigateurs Playwright à installer ; ces niveaux sont plus
  lents → isolés hors du chemin critique de déploiement.
- La couverture ne peut plus régresser sous le seuil sans faire échouer la CI ; le cliquet
  institutionnalise la montée (≈ 89 % atteint).
- Le déploiement reste rapide et fiable (suite rapide seule en barrière).

## Alternatives écartées
- **Selenium** pour l'E2E : Playwright est plus moderne (auto-wait, capture console/CSP, multi-navigateur)
  et reste en C# dans la même solution.
- **Tout faire passer SQLite** : masque collation et traduction LINQ propres à SQL Server.
- **E2E/SQL en barrière de déploiement** : trop lents/fragiles pour gater la prod ; gardés en signal.
- **Pas de gate de couverture** : laisse la couverture dériver silencieusement.
