# Cedeva — Documentation d'ingénierie

Documentation technique de Cedeva, au-delà du [README](../README.md) (fonctionnalités) et du
[CLAUDE.md](../CLAUDE.md) (guide de développement).

| Document | Contenu |
|----------|---------|
| [architecture.md](architecture.md) | Vue d'ensemble C4 (contexte / conteneurs / composants), stack, flux de requête |
| [non-functional-requirements.md](non-functional-requirements.md) | Exigences non-fonctionnelles sous forme de scénarios qualité + tactiques en place |
| [test-strategy.md](test-strategy.md) | 5 niveaux de tests (~1170 unit/intégration + 65 E2E + 3 SQL, ≈92 % couverture), infra, 3 workflows CI, gate de couverture, pièges appris |
| [adr/](adr/) | Architecture Decision Records — le *pourquoi* des choix structurants |

> Convention : ces documents décrivent l'état **réel** du code. Quand une décision change,
> on ajoute/actualise un ADR plutôt que de réécrire l'historique.
