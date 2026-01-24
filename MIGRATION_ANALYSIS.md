# Analyse de Migration - Cedeva v1 vers v2

## Comparaison des Versions

### Ancien Repository (C:\Users\ogaut\source\repos\Cedeva)
- **Structure**: Cedeva.Web + Cedeva.Infrastructure
- **Architecture**: Repository Pattern avec pagination
- **Design**: Couleurs bleues (#007faf), background image, cards avec ombres

### Nouveau Repository (C:\Users\ogaut\cedeva-v2)
- **Structure**: Cedeva.Website + Cedeva.Core + Cedeva.Infrastructure
- **Architecture**: Clean Architecture avec feature folders
- **Design**: Sidebar menu, couleurs à définir

---

## 🎨 DESIGN À RÉCUPÉRER

### Palette de Couleurs (Ancienne Version)
```css
/* Couleur primaire */
Primary: #007faf (bleu clair)
Primary Hover: #005778 (bleu foncé)

/* Couleur des titres */
Headings: #005778

/* Background */
Background Image: url('../images/ok.jpg') - fixed, bottom right

/* Succès */
Success: #28a745
Success Hover: #218838

/* Footer */
Footer Text: #646a70

/* Gris */
Grey Background: #E9ECEF

/* Checkboxes */
Checked: #8C8C8C
```

### Classes CSS Utiles à Récupérer
```css
.fa-white-head - Icons blancs dans navbar (1.5rem)
.fa-white - Icons blancs standard (1rem)
.fa-small - Icons petits bleus (1rem, #007faf)
.fa-big - Icons grands bleus (6rem, #007faf)

.contour - Cards avec bordures arrondies:
  - background: white
  - padding: 10-20px
  - height: 220px
  - border-radius: 4px
  - text-align: center
  - font-weight: 500
  - margin-bottom: 30px
  - shadow-sm

.min-w300, .min-h450, .w-200, .w-230 - Utilitaires de taille
.bg-grey - Fond gris clair (#E9ECEF)
.bg-img - Background image fixe
```

### Layout Differences
**Ancien (Mieux)**:
- Navbar fixed-top avec dropdown Admin
- Background image fixe en bas à droite
- Footer sticky en bas avec copyright
- Cards avec ombres pour les actions
- Icons FontAwesome grands et colorés

**Nouveau (À améliorer)**:
- Sidebar menu (garder mais styliser)
- Pas de background image
- Design plus basique
- Améliorer avec les couleurs et shadows de l'ancien

---

## 🚀 FONCTIONNALITÉS À RÉCUPÉRER

### 1. ⭐ ActivityManagement Controller (MODULE PRINCIPAL)
**Localisation**: `Cedeva.Web/Controllers/ActivityManagementController.cs`

**Description**: Module centralisé de gestion d'activité avec dashboard d'actions

**Features**:
- Index - Dashboard avec cards d'actions (Inscriptions en attente, Présences, Comptes, E-mails, Excursions, Équipe, ONE)
- UnconfirmedBookings - Gestion des inscriptions en attente
- Presences - Gestion des présences par jour
- SendEmail - Envoi d'emails ciblés (tous les parents, par groupe, rappel fiche médicale)
- SentEmails - Historique des emails envoyés
- TeamMembers - Gestion de l'équipe par activité

**Vue**: `Views/ActivityManagement/Index.cshtml`
- Grid de cards cliquables avec icons FontAwesome
- Design cohérent avec classes `.contour` et `.activity-mng`
- Boutons stylisés comme des liens (`.btn-as-link`)

**Priorité**: ⭐⭐⭐⭐⭐ (MODULE ESSENTIEL À INTÉGRER)

---

### 2. 📧 Email Management
**Localisation**: `ActivityManagement/SendEmail` et `ActivityManagement/SentEmails`

**Features**:
- Sélection de destinataires:
  - Tous les parents de l'activité
  - Parents d'un groupe spécifique
  - Rappel fiche médicale (parents sans fiche)
- Composition d'email avec sujet et message
- Pièce jointe optionnelle
- Historique des emails envoyés (table EmailSent)

**Entité Manquante**:
```csharp
EmailSent {
    EmailSentId, ActivityId, RecipientType (enum), RecipientGroupId?,
    RecipientEmails (CSV), Subject, Message, AttachmentFileName?,
    AttachmentFilePath?, SentDate
}

EmailRecipient enum { AllParents, ActivityGroup, MedicalSheetReminder }
```

**Services**:
- EmailRecipientService - Logique de sélection des destinataires
- BrevoEmailSender avec support pièces jointes

**Priorité**: ⭐⭐⭐⭐ (TRÈS UTILE)

---

### 3. 👥 CedevaUsers Controller
**Localisation**: `Cedeva.Web/Controllers/CedevaUsersController.cs`

**Description**: Gestion complète des utilisateurs (CRUD) - MANQUE dans v2

**Features**:
- Index avec liste paginée des utilisateurs
- Create - Créer un utilisateur avec rôle et organisation
- Edit - Modifier email, rôle, organisation
- Delete - Supprimer un utilisateur
- Details - Voir les détails d'un utilisateur

**Views**: Toutes les vues CRUD standard

**Priorité**: ⭐⭐⭐⭐ (MANQUE ACTUELLEMENT)

---

### 4. 📋 Pagination Component
**Localisation**: `Views/Shared/Components/Pager/Default.cshtml`

**Description**: ViewComponent réutilisable pour la pagination

**Features**:
- Pagination avec Previous/Next
- Numéros de pages cliquables
- Support de l'ordre de tri (OrderBy parameter)
- Style Bootstrap avec couleurs personnalisées

**Modèle**:
```csharp
PaginatedAndSortedResult<T> {
    IEnumerable<T> Data,
    int CurrentPage,
    int PageSize,
    int TotalItems,
    int TotalPages,
    string? OrderBy
}
```

**Priorité**: ⭐⭐⭐ (AMÉLIORATION UX)

---

### 5. 🧒 CreateWithParent - Children
**Localisation**: `Views/Children/CreateWithParent.cshtml`

**Description**: Créer un enfant en même temps qu'un parent (workflow simplifié)

**Features**:
- Formulaire combiné Parent + Enfant
- Validation croisée
- Gain de temps pour les coordinateurs

**Priorité**: ⭐⭐⭐ (NICE TO HAVE)

---

### 6. 🔍 Belgian Address Autocomplete
**Localisation**: `Controllers/AddressAPIController.cs`

**Description**: API pour autocomplete des villes belges

**Features**:
- Endpoint `/AddressAPI/GetCities?postalCode={code}`
- Retourne liste des villes pour un code postal
- Utilise BelgianMunicipality table (déjà présent dans v2)

**Priorité**: ⭐⭐⭐ (AMÉLIORATION UX)

---

### 7. 📱 Responsive Cards Layout
**Localisation**: `Views/Home/Index.cshtml` (ancienne version)

**Description**: Dashboard d'accueil avec cards pour actions rapides

**Features**:
- Grid responsive (col-sm-6 col-md-6 col-lg-4 col-xl-3)
- Cards avec icons FontAwesome grands (fa-big)
- Hover effects avec bordures bleues
- Links vers Activities, Parents, Children, Bookings

**Priorité**: ⭐⭐⭐ (AMÉLIORATION VISUELLE)

---

### 8. 🎨 _SelectLanguagePartial Better Design
**Localisation**: `Views/Shared/_SelectLanguagePartial.cshtml`

**Description**: Sélecteur de langue mieux intégré dans navbar

**Comparaison**:
- **Ancien**: Dropdown dans navbar avec drapeaux et noms de langues
- **Nouveau**: Simple dropdown avec emojis

**Priorité**: ⭐⭐ (AMÉLIORATION MINEURE)

---

### 9. 📊 _ChildrenTable Partial View
**Localisation**: `Views/Shared/_ChildrenTable.cshtml`

**Description**: Table réutilisable pour afficher liste d'enfants

**Features**:
- Affichage nom, prénom, date de naissance, parent
- Actions (Details, Edit, Delete)
- Réutilisable dans différentes vues

**Priorité**: ⭐⭐ (AMÉLIORATION CODE)

---

### 10. 🏷️ Activity Labels Système
**Différence**: Ancien utilise ActivityDay.Label pour nommer les jours

**Exemple**:
```
ActivityDay {
    Label: "Lundi 12/07",
    ActivityDayDate: 2026-07-12,
    Week: 1
}
```

**Avantage**: Affichage personnalisé au lieu de juste la date

**Priorité**: ⭐⭐ (AMÉLIORATION DATA)

---

## 🔧 DIFFÉRENCES TECHNIQUES

### Repository Pattern vs Direct DbContext
**Ancien**: Repository Pattern avec interfaces et implémentations
```csharp
IActivityRepository, IBookingRepository, etc.
PaginatedAndSortedResult<T> pour pagination
```

**Nouveau**: Accès direct via DbContext dans controllers

**Recommandation**: Garder approche actuelle (plus simple, EF Core suffit)

---

### ViewModels Organisation
**Ancien**: `Models/{Domain}/{Entity}{Action}ViewModel.cs`
```
Models/Activities/ActivityCreateViewModel.cs
Models/Bookings/BookingEditViewModel.cs
```

**Nouveau**: `Features/{Domain}/ViewModels/{ViewModel}.cs`
```
Features/Activities/ViewModels/CreateViewModel.cs
```

**Recommandation**: Garder approche actuelle (feature folders meilleure organisation)

---

### Claims Transformer vs ClaimsPrincipalFactory
**Ancien**: ClaimsTransformer middleware
**Nouveau**: CedevaUserClaimsPrincipalFactory

**Recommandation**: Garder approche actuelle (plus standard)

---

## 📋 PLAN D'IMPLÉMENTATION RECOMMANDÉ

### Phase 1: Design (PRIORITÉ HAUTE)
1. ✅ Appliquer palette de couleurs (#007faf, #005778)
2. ✅ Ajouter background image fixe
3. ✅ Styliser cards avec shadows et `.contour`
4. ✅ Améliorer navbar avec dropdown style ancien
5. ✅ Ajouter classes CSS utilitaires (fa-big, fa-small, etc.)

### Phase 2: ActivityManagement Module (PRIORITÉ HAUTE)
1. ✅ Créer ActivityManagementController
2. ✅ Créer vue Index avec cards dashboard
3. ✅ Implémenter UnconfirmedBookings
4. ✅ Améliorer Presences existant avec style ancien
5. ✅ Implémenter SendEmail feature
6. ✅ Implémenter SentEmails history
7. ✅ Implémenter TeamMembers par activité

### Phase 3: Email Management (PRIORITÉ HAUTE)
1. ✅ Créer EmailSent entity et migration
2. ✅ Créer EmailRecipient enum
3. ✅ Implémenter EmailRecipientService
4. ✅ Ajouter support pièces jointes dans BrevoEmailSender
5. ✅ Créer vues SendEmail et SentEmails

### Phase 4: Users Management (PRIORITÉ HAUTE)
1. ✅ Créer CedevaUsersController
2. ✅ Créer toutes vues CRUD
3. ✅ Ajouter menu entry

### Phase 5: Améliorations UX (PRIORITÉ MOYENNE)
1. ✅ Implémenter Pagination Component
2. ✅ Ajouter AddressAPIController pour autocomplete
3. ✅ Créer CreateWithParent pour Children
4. ✅ Améliorer Home dashboard avec cards

### Phase 6: Polish (PRIORITÉ BASSE)
1. ✅ Améliorer _SelectLanguagePartial
2. ✅ Créer _ChildrenTable partial
3. ✅ Ajouter Label sur ActivityDay
4. ✅ Refactoring et optimisations

---

## 🎯 ÉLÉMENTS À NE PAS RÉCUPÉRER

1. ❌ Repository Pattern - Trop verbeux, EF Core suffit
2. ❌ ClaimsTransformer - CedevaUserClaimsPrincipalFactory meilleur
3. ❌ Structure de dossiers ancienne - Feature folders meilleure
4. ❌ Certaines validations complexes - Simplifier si possible

---

## 📸 SCREENSHOTS RECOMMANDÉS

Pour faciliter l'implémentation, prendre screenshots de:
1. `Views/ActivityManagement/Index.cshtml` - Dashboard
2. `Views/Home/Index.cshtml` - Home cards
3. `wwwroot/css/cedeva.css` - Toutes les classes
4. `Views/Shared/_Layout.cshtml` - Navbar et footer
5. `Views/ActivityManagement/SendEmail.cshtml` - Email form
6. `Views/ActivityManagement/Presences.cshtml` - Présences list

---

## 🔗 FICHIERS CLÉS À EXAMINER

### Controllers
- `ActivityManagementController.cs` ⭐⭐⭐⭐⭐
- `CedevaUsersController.cs` ⭐⭐⭐⭐
- `AddressAPIController.cs` ⭐⭐⭐

### Views
- `Views/ActivityManagement/Index.cshtml` ⭐⭐⭐⭐⭐
- `Views/ActivityManagement/SendEmail.cshtml` ⭐⭐⭐⭐
- `Views/ActivityManagement/Presences.cshtml` ⭐⭐⭐⭐
- `Views/Shared/Components/Pager/` ⭐⭐⭐

### CSS
- `wwwroot/css/cedeva.css` ⭐⭐⭐⭐⭐

### Services
- `Services/EmailRecipientService.cs` ⭐⭐⭐⭐
- `Services/BelgianMunicipalityService.cs` ⭐⭐⭐

---

**Date d'analyse**: 2026-01-24
**Analysé par**: Claude Sonnet 4.5
