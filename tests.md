# Données de test — Cedeva

Données factices pour tester l'application manuellement (formulaires, iframe d'inscription, paiement Stripe).

---

## Numéros de registre national belges (valides)

Format : `AA.MM.JJ-SSS.CC` — date de naissance + séquence du jour (`SSS`, impair = homme / pair = femme) + clé de contrôle (`CC`).
Clé `CC = 97 − (9 premiers chiffres mod 97)` ; **pour une naissance à partir de 2000**, on préfixe un `2` aux 9 chiffres avant le modulo.

| Profil | Format | Brut (11 chiffres) |
|--------|--------|--------------------|
| Parent ♂ né le 15/06/1985 | `85.06.15-133.80` | `85061513380` |
| Parent ♀ née le 22/03/1990 | `90.03.22-236.45` | `90032223645` |
| Parent ♂ né le 02/11/1978 | `78.11.02-501.87` | `78110250187` |
| Enfant ♂ né le 10/09/2015 | `15.09.10-121.83` | `15091012183` |
| Enfant ♀ née le 25/04/2018 | `18.04.25-202.65` | `18042520265` |
| Enfant ♂ né le 01/12/2020 | `20.12.01-047.68` | `20120104768` |
| Enfant ♀ née le 08/07/2016 | `16.07.08-164.10` | `16070816410` |

Les formulaires acceptent les deux écritures (avec ou sans points/tirets) : `StripFormatting` enlève la ponctuation.

> ℹ️ Depuis l'ajout de l'attribut `[ValidNationalRegisterNumber]`, les formulaires vérifient la **clé de contrôle** (modulo 97) et la plausibilité de la date. Il faut donc des numéros réellement valides comme ceux ci-dessus — un nombre arbitraire à 11 chiffres est désormais rejeté.

---

## Cartes bancaires de test Stripe

Mode test uniquement (clés `sk_test_…`). Pour toutes : **date d'expiration future quelconque**, **CVC à 3 chiffres quelconque**, **code postal quelconque**.

### Paiement réussi
| Carte | Marque | Comportement |
|-------|--------|--------------|
| `4242 4242 4242 4242` | Visa | Succès immédiat (sans 3D Secure) |
| `5555 5555 5555 4444` | Mastercard | Succès immédiat |
| `4000 0056 0000 0008` | Visa (debit) | Succès immédiat |

### Authentification 3D Secure
| Carte | Comportement |
|-------|--------------|
| `4000 0025 0000 3155` | Authentification 3DS requise (popup à valider) |
| `4000 0027 6000 3184` | 3DS requise à chaque paiement |

### Paiements refusés
| Carte | Motif du refus |
|-------|----------------|
| `4000 0000 0000 0002` | Refus générique (`card_declined`) |
| `4000 0000 0000 9995` | Fonds insuffisants (`insufficient_funds`) |
| `4000 0000 0000 0069` | Carte expirée (`expired_card`) |
| `4000 0000 0000 0127` | CVC incorrect (`incorrect_cvc`) |
| `4000 0000 0000 0119` | Erreur de traitement (`processing_error`) |

Pour notre flux (Stripe Checkout hébergé), utiliser `4242 4242 4242 4242` pour un test nominal,
et `4000 0025 0000 3155` pour vérifier le parcours 3D Secure.

Référence complète : https://docs.stripe.com/testing

---

## Comptes applicatifs (seed)

| Rôle | Email | Mot de passe |
|------|-------|--------------|
| Admin | `admin@cedeva.be` | `Admin@123456` |
| Coordinateur (Org 1) | `coordinator@cedeva.be` | `Coord@123456` |
| Coordinateur (Org 2) | `coordinator.liege@cedeva.be` | `Coord@123456` |

---

## Codes postaux / villes (autocomplétion)

Exemples présents dans le référentiel `BelgianMunicipalities` : `5030 Gembloux`, `1000 Bruxelles`.
Taper au moins 2 caractères (code postal ou nom de ville) dans le champ adresse combiné.
