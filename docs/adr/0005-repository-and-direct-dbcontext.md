# 0005 — Repository+UoW *et* accès direct au DbContext

**Statut :** Accepté

## Contexte
v1 utilisait un Repository Pattern systématique jugé trop verbeux. v2 voulait limiter la cérémonie
tout en gardant une abstraction là où elle aide (tests, opérations CRUD génériques).

## Décision
Approche **hybride assumée** :
- `IRepository<T>` + `IUnitOfWork` (génériques) pour les opérations CRUD simples et là où une
  abstraction facilite les tests.
- **Accès direct au `CedevaDbContext`** dans les controllers/services pour les requêtes riches
  (includes, projections, agrégations) où passer par un repository générique n'apporterait rien.

## Conséquences
- Moins de boilerplate pour les requêtes complexes (ex. `FinancialController`, `BookingsController`).
- Cohérence à surveiller : deux styles coexistent ; choisir le direct DbContext quand la requête est
  spécifique, le repository quand le CRUD est générique.
- Le `DbContext` implémente `IUnitOfWork` (un seul `SaveChanges`).

## Alternatives écartées
- **Repository strict partout** : verbeux, masque les capacités d'EF (v1).
- **DbContext direct partout** : perd l'abstraction utile pour certains tests/CRUD.
