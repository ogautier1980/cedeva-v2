# 0001 — Documenter les décisions via des ADR

**Statut :** Accepté

## Contexte
Le code et l'outillage de Cedeva sont solides, mais beaucoup de choix structurants (multi-tenancy,
DI, stratégie de déploiement, seeding…) ne sont écrits nulle part. En solo, l'intention se perd ;
en cas d'arrivée d'un collègue (ou de « soi-même dans 6 mois »), le *pourquoi* est introuvable.

## Décision
Tenir des **Architecture Decision Records** courts dans `docs/adr/`, un fichier par décision,
numérotés. Chaque ADR explicite la motivation et les alternatives écartées.

## Conséquences
- Le raisonnement derrière l'architecture est préservé et versionné avec le code.
- Léger surcoût à l'écriture, compensé par le gain de maintenabilité/onboarding.
- Les décisions changées ne sont pas effacées mais remplacées par un nouvel ADR.

## Alternatives écartées
- **Ne rien documenter** : perte de connaissance, dérive d'architecture.
- **Un wiki externe** : se désynchronise du code ; un dossier versionné reste au plus près.
