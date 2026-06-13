# 0002 — 3 projets (Website/Core/Infrastructure) + feature folders

**Statut :** Accepté

## Contexte
Cedeva v2 est une réécriture de v1. Il fallait une structure qui sépare le domaine de
l'infrastructure tout en restant lisible pour une petite équipe, et qui garde la cohésion par
fonctionnalité côté présentation.

## Décision
- **3 projets** : `Cedeva.Core` (domaine pur : entités, interfaces, enums, DTOs, helpers — sans
  dépendance d'infra), `Cedeva.Infrastructure` (EF Core, services, config), `Cedeva.Website` (MVC).
  Les dépendances pointent vers `Core` (Website→Core, Website→Infrastructure, Infrastructure→Core).
- **Feature folders** côté Website : `Features/{Fonctionnalité}/` contient controller + vues +
  ViewModels au même endroit, plutôt que des dossiers `Controllers/`, `Views/`, `Models/` séparés.

## Conséquences
- Le domaine est testable sans infrastructure (cf. tests unitaires purs).
- Forte cohésion : tout ce qui concerne une fonctionnalité est local.
- Les vues utilisent des `ViewLocationFormats` personnalisés (`/Features/{1}/{0}.cshtml`).

## Alternatives écartées
- **Repository Pattern lourd partout / découpage MVC classique** (v1) : jugé trop verbeux.
- **Projet unique** : mélange domaine/infra, nuit à la testabilité.
- **Microservices** : disproportionné pour l'échelle (SaaS solo/petit).
