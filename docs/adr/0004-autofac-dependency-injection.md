# 0004 — Autofac comme conteneur DI

**Statut :** Accepté

## Contexte
L'application a besoin d'injection de dépendances pour les services (email, financier, excursions,
stockage, export…) et les génériques (`IRepository<T>`, `ICedevaControllerContext<T>`).

## Décision
Utiliser **Autofac** via `AutofacServiceProviderFactory` et `builder.Host.ConfigureContainer`.
Les enregistrements sont regroupés dans `Program.cs` (`InstancePerLifetimeScope`), y compris les
ouverts génériques (`RegisterGeneric(typeof(Repository<>))`). Les `IOptions<T>` et le `DbContext`
restent enregistrés via le conteneur Microsoft (peuplé dans Autofac).

## Conséquences
- Support natif des enregistrements génériques ouverts et des scénarios avancés.
- `IOptions<T>` (Microsoft DI) reste résolvable par Autofac.
- Tant que la liste tient dans `Program.cs`, pas de modules séparés (à introduire si elle grossit —
  voir backlog).

## Alternatives écartées
- **Microsoft.Extensions.DependencyInjection seul** : suffisant aujourd'hui, mais Autofac était déjà
  retenu en v2 pour ses génériques/modularité ; conservé.
