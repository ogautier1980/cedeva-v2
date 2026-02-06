# Cedeva - Design Guidelines

Guide de cohérence visuelle pour l'application Cedeva. Ce document établit les standards de design à respecter pour maintenir une expérience utilisateur uniforme.

---

## 🎨 Couleurs de Marque

### Palette Principale
- **Primaire:** `#007faf` - Bleu Cedeva (boutons, liens, accents)
- **Hover:** `#005778` - Bleu foncé (états hover)
- **Dégradé:** `linear-gradient(135deg, #007faf 0%, #005778 100%)`

### Couleurs Fonctionnelles
- **Succès:** `#198754` (Bootstrap `success`)
- **Danger:** `#dc3545` (Bootstrap `danger`)
- **Avertissement:** `#ffc107` (Bootstrap `warning`)
- **Information:** `#0dcaf0` (Bootstrap `info`)
- **Secondaire:** `#6c757d` (Bootstrap `secondary`)

---

## 📝 Hiérarchie Typographique

### Pages Index
**Standard:** `<h2><i class="fas fa-icon me-2"></i>@Localizer["Title"]</h2>`

```html
<h2><i class="fas fa-calendar-alt me-2"></i>@Localizer["Activities"]</h2>
```

- **Niveau:** `<h2>` (pas `<h1>`)
- **Icône:** FontAwesome avec classe `me-2` pour l'espacement
- **Cas d'usage:** Toutes les vues Index (Activities, Children, Bookings, etc.)

### Pages Details
**Standard:** `<h1 class="h3 mb-0">@Localizer["Title"]</h1>`

```html
<h1 class="h3 mb-0">@Model.Name</h1>
```

- **Niveau:** `<h1>` avec classe `h3` pour la taille
- **Classe:** `mb-0` pour supprimer la marge inférieure
- **Cas d'usage:** Vues Details avec sous-titre

### Dashboards
**Standard:** `<h1 class="display-5 fw-bold">@Localizer["Title"]</h1>`

```html
<h1 class="display-5 fw-bold mb-2">@Model.Activity.Name</h1>
```

- **Cas d'usage:** Pages principales comme ActivityManagement/Index

### Sections de Formulaires
**Standard:** `<h6 class="mb-3 text-primary"><i class="fas fa-icon me-2"></i>@Localizer["Section"]</h6>`

```html
<h6 class="mb-3 text-primary"><i class="fas fa-user me-2"></i>@Localizer["PersonalInformation"]</h6>
```

- **Couleur:** Toujours `text-primary` (#007faf)
- **Icône:** FontAwesome avec `me-2`
- **Espacement:** `mb-3` pour la marge inférieure
- **Sections suivantes:** Ajouter `mt-4` → `<h6 class="mb-3 mt-4 text-primary">`

### En-têtes de Cartes
**Standard:** `<h6 class="mb-0"><i class="fas fa-icon me-2"></i>@Localizer["Title"]</h6>`

```html
<div class="card-header">
    <h6 class="mb-0"><i class="fas fa-info-circle me-2"></i>@Localizer["Information"]</h6>
</div>
```

- **Espacement:** `mb-0` (pas de marge car dans card-header)
- **Couleur:** Pas de `text-primary` (couleur héritée)

---

## 🔘 Boutons

### Boutons Primaires (Actions Principales)
**Classes:** `btn btn-primary`

**Cas d'usage:**
- Boutons "Créer" / "Create"
- Boutons "Enregistrer" / "Save"
- Boutons "Confirmer" / "Confirm"
- Actions principales dans les formulaires

```html
<button type="submit" class="btn btn-primary">
    <i class="fas fa-save me-2"></i>@Localizer["Save"]
</button>
```

### Boutons Modifier
**Classes:** `btn btn-outline-secondary` (tables/listes) ou `btn btn-outline-secondary` (pages Details)

```html
<!-- Dans une table -->
<a asp-action="Edit" asp-route-id="@item.Id" class="btn btn-sm btn-outline-secondary">
    <i class="fas fa-edit"></i>
</a>

<!-- Sur une page Details -->
<a asp-action="Edit" asp-route-id="@Model.Id" class="btn btn-outline-secondary">
    <i class="fas fa-edit me-2"></i>@Localizer["Edit"]
</a>
```

⚠️ **Ne jamais utiliser:** `btn-warning` (jaune) pour les boutons Modifier

### Boutons Supprimer
**Classes:** `btn btn-sm btn-outline-danger` (tables) ou `btn btn-danger` (confirmations)

```html
<!-- Dans une table -->
<a asp-action="Delete" asp-route-id="@item.Id" class="btn btn-sm btn-outline-danger">
    <i class="fas fa-trash"></i>
</a>

<!-- Page de confirmation de suppression -->
<button type="submit" class="btn btn-danger">
    <i class="fas fa-trash me-2"></i>@Localizer["Delete"]
</button>
```

### Boutons Retour / Annuler
**Classes:** `btn btn-outline-secondary`

```html
<a asp-action="Index" class="btn btn-outline-secondary">
    <i class="fas fa-arrow-left me-2"></i>@Localizer["Back"]
</a>

<a asp-action="Index" class="btn btn-outline-secondary">
    <i class="fas fa-times me-2"></i>@Localizer["Cancel"]
</a>
```

⚠️ **Ne jamais utiliser:** `btn btn-secondary` (rempli) - toujours utiliser `btn-outline-secondary`

### Boutons de Succès
**Classes:** `btn btn-success`

**Cas d'usage:**
- Confirmer une inscription
- Valider un paiement
- Actions positives spécifiques

```html
<button type="button" class="btn btn-success">
    <i class="fas fa-check me-2"></i>@Localizer["Confirm"]
</button>
```

### Tailles de Boutons
- **Tables:** Toujours `btn-sm` → `btn btn-sm btn-outline-secondary`
- **Formulaires:** Taille normale → `btn btn-primary`
- **Actions principales:** Optionnel `btn-lg` → `btn btn-lg btn-primary`

---

## 📊 Tables

### En-tête de Table
**Standard:** Toujours ajouter `class="table-light"` sur `<thead>`

```html
<table class="table table-hover">
    <thead class="table-light">
        <tr>
            <th>@Localizer["Field.Name"]</th>
            <th>@Localizer["Field.Email"]</th>
            <th class="text-end">@Localizer["Actions"]</th>
        </tr>
    </thead>
    <tbody>
        <!-- rows -->
    </tbody>
</table>
```

- **Fond:** Gris clair (#f8f9fa)
- **Cohérence:** Toutes les tables de l'application utilisent ce style

### Classes de Table Courantes
- `table` - Style de base
- `table-hover` - Effet hover sur les lignes
- `table-sm` - Tableau compact (optionnel)
- `table-responsive` - Wrapper pour défilement horizontal sur mobile

---

## 🎴 Cartes (Cards)

### En-têtes de Cartes - Schéma de Couleurs

#### Tables de Données
**Classes:** `card-header bg-primary text-white`

```html
<div class="card">
    <div class="card-header bg-primary text-white">
        <h6 class="mb-0 text-white">@Localizer["Title"]</h6>
    </div>
    <div class="card-body">
        <!-- table -->
    </div>
</div>
```

**Cas d'usage:** Tables de données, listes dans des cartes

#### Confirmations de Suppression
**Classes:** `card-header bg-danger text-white`

```html
<div class="card">
    <div class="card-header bg-danger text-white">
        <h5 class="mb-0 text-white">@Localizer["ConfirmDelete"]</h5>
    </div>
    <!-- ... -->
</div>
```

**Cas d'usage:** Pages Delete, confirmations destructives

#### Formulaires
**Classes:** `card-header` (défaut, fond gris clair)

```html
<div class="card">
    <div class="card-header">
        <h5 class="mb-0">@Localizer["Title"]</h5>
    </div>
    <div class="card-body">
        <!-- form -->
    </div>
</div>
```

**Cas d'usage:** Formulaires Create/Edit

#### Informations Spéciales
**Classes:** `card-header bg-success text-white` (vert) ou `card-header bg-info text-white` (bleu clair)

**Cas d'usage:** Sections d'aide, merge fields, informations complémentaires

---

## 🏷️ Badges

### Mapping Couleur ↔ Signification

#### `bg-success` (Vert)
- Statut "Confirmé" / "Actif" / "Payé"
- États positifs
- Compteurs de succès

```html
<span class="badge bg-success">@Localizer["Confirmed"]</span>
<span class="badge bg-success">@Localizer["Active"]</span>
```

#### `bg-warning` (Jaune/Orange)
- Statut "En attente" / "Non confirmé"
- Avertissements
- Actions requises

```html
<span class="badge bg-warning">@Localizer["Pending"]</span>
<span class="badge bg-warning text-dark">@Localizer["Unconfirmed"]</span>
```

💡 **Note:** Ajouter `text-dark` sur fond jaune pour meilleure lisibilité

#### `bg-danger` (Rouge)
- Statut "Annulé" / "Refusé"
- Erreurs
- Comptes verrouillés
- Nombre d'inscriptions en attente (badge de notification)

```html
<span class="badge bg-danger">@Localizer["Cancelled"]</span>
<span class="badge bg-danger position-absolute top-0 end-0">@pendingCount</span>
```

#### `bg-primary` (Bleu Cedeva)
- Compteurs généraux (inscriptions, groupes, etc.)
- IDs
- Informations neutres importantes

```html
<span class="badge bg-primary">@totalBookings</span>
```

#### `bg-secondary` (Gris)
- Statuts neutres (Inactif)
- Catégories
- Informations secondaires

```html
<span class="badge bg-secondary">@Localizer["Inactive"]</span>
```

#### `bg-info` (Bleu Clair)
- Types d'excursions
- Méthodes de paiement
- Classifications informatives

```html
<span class="badge bg-info">@Localizer[$"Enum.ExcursionType.{item.Type}"]</span>
<span class="badge bg-info">@Localizer[$"Enum.PaymentMethod.{payment.Method}"]</span>
```

---

## 📭 États Vides (Empty States)

### Pattern Standard
**Classes:** `text-center py-5`

```html
@if (!Model.Items.Any())
{
    <div class="text-center py-5">
        <i class="fas fa-icon fa-3x text-muted mb-3"></i>
        <p class="text-muted">@Localizer["NoItemsFound"]</p>
        <a asp-action="Create" class="btn btn-primary">
            <i class="fas fa-plus me-2"></i>@Localizer["CreateNew"]
        </a>
    </div>
}
```

**Éléments:**
1. **Icône:** `fa-3x text-muted mb-3` - Grande icône grise
2. **Message:** `<p class="text-muted">` - Texte gris
3. **Action (optionnel):** Bouton primaire pour créer

**Icônes recommandées par contexte:**
- Children: `fa-child`
- Bookings: `fa-calendar-check`
- Team Members: `fa-users`
- Excursions: `fa-train`
- Activities: `fa-calendar-times`
- Parents: `fa-user-friends`
- No pending items: `fa-check-circle`

⚠️ **Ne plus utiliser:**
- `<div class="alert alert-info">` pour les états vides
- Patterns avec fond coloré pour "aucune donnée"

---

## 🔍 Recherche et Filtres

### Formulaire de Recherche Standard
```html
<form method="get" class="mb-4">
    <div class="input-group">
        <input type="text" name="searchString" value="@searchString"
               class="form-control" placeholder="@Localizer["Search"]" />
        <button type="submit" class="btn btn-primary">
            <i class="fas fa-search"></i>
        </button>
        @if (!string.IsNullOrEmpty(searchString))
        {
            <a asp-action="Index" class="btn btn-outline-secondary">
                <i class="fas fa-times"></i>
            </a>
        }
    </div>
</form>
```

---

## ⚠️ Messages d'Alerte

### Pattern Standardisé
Utiliser le partial `_AlertMessages.cshtml`:

```cshtml
@await Html.PartialAsync("_AlertMessages")
```

**TempData keys:**
- `SuccessMessage` - Messages de succès (vert)
- `ErrorMessage` - Messages d'erreur (rouge)
- `WarningMessage` - Messages d'avertissement (jaune)

**Dans le controller:**
```csharp
TempData["SuccessMessage"] = Localizer["OperationSuccessful"].ToString();
TempData["ErrorMessage"] = Localizer["OperationFailed"].ToString();
```

---

## 📱 Responsive Design

### Breakpoints Bootstrap 5
- `xs`: < 576px
- `sm`: ≥ 576px
- `md`: ≥ 768px
- `lg`: ≥ 992px
- `xl`: ≥ 1200px
- `xxl`: ≥ 1400px

### Classes Utilitaires Courantes
- `d-none d-md-block` - Caché sur mobile, visible sur tablette+
- `d-block d-md-none` - Visible sur mobile, caché sur tablette+
- `col-12 col-md-6 col-lg-4` - Responsive grid
- `mb-3 mb-md-0` - Marge responsive

---

## ✅ Checklist - Nouvelle Vue

Lors de la création d'une nouvelle vue, vérifier:

- [ ] **Titre de page** suit le standard (h2 avec icône pour Index, h1.h3 pour Details)
- [ ] **Tables** ont `<thead class="table-light">`
- [ ] **Boutons Modifier** utilisent `btn-outline-secondary` (jamais `btn-warning`)
- [ ] **Boutons Retour/Annuler** utilisent `btn-outline-secondary` (jamais `btn-secondary`)
- [ ] **État vide** utilise le pattern centré avec icône `fa-3x`
- [ ] **Sections de formulaire** utilisent `<h6 class="mb-3 text-primary">` avec icône
- [ ] **Badges** utilisent les bonnes couleurs selon la signification
- [ ] **Alerts** utilisent le partial `_AlertMessages`
- [ ] **Card headers** suivent le schéma de couleurs (primary/danger/default)

---

## 🚀 Évolutions Futures

### Améliorations Potentielles
1. **Affichage noms complets** dans les audit trails au lieu de UserIds
2. **Soft Delete** avec IsDeleted/DeletedAt/DeletedBy
3. **Tag Helper Razor** pour audit info: `<audit-info entity="@Model" />`
4. **Composants réutilisables** pour états vides avec variations d'icônes

---

**Dernière mise à jour:** 2026-02-06
**Version:** 1.0
**Mainteneur:** Équipe Cedeva
