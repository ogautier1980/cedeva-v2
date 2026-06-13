# 0003 — Multi-tenancy par filtres de requête EF Core

**Statut :** Accepté

## Contexte
Cedeva est multi-tenant : chaque organisation ne doit voir que ses données. Il fallait une
isolation **par défaut** (impossible à oublier dans une requête), tout en autorisant l'admin à
tout voir.

## Décision
Filtres de requête globaux EF Core (`HasQueryFilter`) sur les entités scopées :
`Activity`, `Parent`, `Child`, `TeamMember`, `CodaFile`, `BankTransaction`, `EmailTemplate`.
Le filtre lit l'utilisateur courant via `ICurrentUserService` (claims `OrganisationId` / `Role`) :

```
_currentUserService == null || _currentUserService.IsAdmin
  || e.OrganisationId == _currentUserService.OrganisationId
```

L'admin contourne automatiquement ; `IgnoreQueryFilters()` est utilisé pour les cas explicites
(seeder, opérations admin).

## Conséquences
- Isolation **par défaut** : une requête EF est scopée sans code supplémentaire (vérifié par des
  tests dédiés : coordinateur ne voit que son org, admin voit tout, `IgnoreQueryFilters` bypass).
- ⚠️ Le filtre déréférence `ICurrentUserService` lors de l'extraction de paramètres EF : le service
  **ne doit jamais être null** au moment d'une requête filtrée (sinon `NullReferenceException`).
  En prod il est toujours injecté ; les tests fournissent toujours un faux service (admin par défaut).
- `IgnoreQueryFilters()` retourne `IQueryable<T>` → utiliser `FirstOrDefaultAsync(pred)`, pas `FindAsync`.

## Alternatives écartées
- **Filtrage manuel `Where(OrganisationId == …)`** partout : oubli garanti = fuite de données.
- **Base par tenant** : surcoût opérationnel disproportionné à cette échelle.
