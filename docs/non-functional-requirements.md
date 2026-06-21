# Exigences non-fonctionnelles (NFR)

Cedeva n'avait pas de NFR écrites : beaucoup de tactiques sont **implémentées** sans que l'objectif
qualité soit énoncé. Ce document rend les NFR explicites sous forme de **scénarios qualité**
(Source · Stimulus · Artefact · Réponse · Mesure) et liste les tactiques en place + le reste à faire.

Priorisation pour un SaaS multi-tenant solo : **Sécurité ≈ Maintenabilité > Disponibilité >
Performance**.

---

## QA-1 — Isolation multi-tenant (Sécurité) — *priorité haute*
- **Source :** un coordinateur authentifié de l'organisation A.
- **Stimulus :** requête (volontaire ou par bug) visant des données de l'organisation B.
- **Artefact :** couche d'accès aux données (EF Core).
- **Réponse :** les données d'autres organisations ne sont jamais renvoyées ; un accès direct par id
  hors-tenant renvoie *not found*.
- **Mesure :** 0 fuite ; couvert par tests (coordinateur ne voit que son org, 404 cross-org).
- **En place :** filtres de requête globaux EF ([ADR 0003](adr/0003-multi-tenancy-via-ef-global-query-filters.md)) + tests d'intégration et controller (`Financial` 404 cross-org).

## QA-2 — Sécurité du formulaire iframe public — *priorité haute*
- **Source :** un parent anonyme (ou un site tiers malveillant).
- **Stimulus :** charger/embarquer le formulaire d'inscription ; tenter du clickjacking sur l'app authentifiée.
- **Artefact :** middleware d'en-têtes ; `PublicRegistrationController`.
- **Réponse :** l'app authentifiée refuse l'embarquement ; le formulaire public reste embarquable chez les partenaires.
- **Mesure :** `X-Frame-Options: SAMEORIGIN` sur l'app, **absent** sur `/PublicRegistration` ; vérifié par tests + en prod.
- **En place :** `SecurityHeadersMiddleware`, HSTS via ForwardedHeaders ([ADR 0008](adr/0008-cookie-identity-and-security-hardening.md)).
- **CSP vérifiée en navigateur (E2E)** : aucune violation sur l'iframe public ni sur les pages admin riches (éditeur Summernote, Choices.js) — `RegistrationIframeTests` + `CspE2ETests`.

## QA-3 — Résistance au brute-force / spam (Sécurité) — *priorité haute*
- **Source :** un bot / attaquant depuis une IP.
- **Stimulus :** rafale de tentatives de login ou d'inscriptions publiques.
- **Artefact :** middleware de rate limiting ; endpoints `Account` et `PublicRegistration`.
- **Réponse :** les requêtes au-delà du quota sont rejetées (HTTP 429).
- **Mesure :** login/register ≤ 10/min/IP ; inscription publique ≤ 30/min/IP ; couvert par un test (429).
- **En place :** `AddRateLimiter` + verrouillage Identity + antiforgery.
- **Reste :** réglage fin du lockout + audit des échecs de login — backlog.

## QA-4 — Disponibilité au déploiement — *priorité moyenne-haute*
- **Source :** un push sur `main`.
- **Stimulus :** build + déploiement automatique.
- **Artefact :** pipeline CI/CD + App Service.
- **Réponse :** un déploiement n'est validé que si l'app répond réellement ; sinon le run est rouge.
- **Mesure :** gate `/health` (HTTP 200) ; runs ~3-4 min ; Always On actif.
- **En place :** gate `/health`, Always On, seeding non-bloquant ([ADR 0007](adr/0007-cicd-azure-app-service-with-health-gate.md), [0009](adr/0009-background-nonblocking-startup-seeding.md)).

## QA-5 — Maintenabilité — *priorité haute*
- **Source :** le développeur (présent ou futur).
- **Stimulus :** ajouter/modifier une fonctionnalité.
- **Artefact :** l'ensemble du code.
- **Réponse :** changement localisé, non régressif, compréhensible.
- **Mesure :** build **0 warning** (analyzers actifs), **~1193 tests** unit/intégration (+ 65 E2E, 3 SQL) verts, couverture lignes ≈ 92 % (gate CI 85 %), décisions documentées (ADR).
- **En place :** feature folders, DI, services extraits, analyzers, Directory.Build.props, .editorconfig, cette documentation.

## QA-6 — Performance — *priorité moyenne*
- **Source :** un coordinateur en charge normale.
- **Stimulus :** consulter un tableau de bord/listing (ex. dashboard financier d'une activité).
- **Artefact :** controllers + EF Core.
- **Réponse :** page rendue rapidement.
- **Mesure (objectif) :** P95 < 500 ms hors cold-start. *Non mesuré formellement aujourd'hui.*
- **Instrumentation :** Serilog (+ sink Seq config-driven) et **Application Insights** (ressource Azure créée + connection string en place → actif en prod). **Reste :** surveiller les requêtes N+1.

## QA-7 — Confidentialité des données / RGPD — *priorité moyenne*
- **Source :** exploitation des logs / fuite.
- **Stimulus :** données personnelles (emails, n° registre national, enfants) présentes dans les logs.
- **Artefact :** pipeline de logs (Serilog).
- **Réponse :** pas d'exposition de PII non nécessaire.
- **Mesure :** *à formaliser.*
- **Reste :** politique de rédaction/masquage PII dans les logs — backlog.

---

### Conflits / arbitrages assumés
- **Sécurité vs ergonomie de l'iframe** : pas de `X-Frame-Options` global (sinon l'iframe casse) → exception ciblée sur `/PublicRegistration`.
- **Sécurité vs friction de connexion** : cookie `SameSite=Lax` (pas `Strict`) + antiforgery, pour ne pas casser les liens entrants.
- **Disponibilité vs fraîcheur des données** : seeding en tâche de fond → l'app sert avant la fin du seed sur base vide (acceptable en dev).
