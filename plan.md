# Plan: Système de Gestion Financière pour Cedeva

## ✅ Statut d'implémentation

### Phase 1: Fondations (COMPLETED - 2026-01-31)
**Commit**: 5b1b255 - "feat: Phase 1 - Gestion Financière (Fondations)"

✅ **Enums créés** (5 fichiers):
- PaymentMethod.cs
- PaymentStatus.cs
- TransactionType.cs
- TransactionCategory.cs (extensible pour futures excursions)
- ExpenseType.cs

✅ **Entités créées** (4 fichiers):
- Payment.cs - Paiements (virement/cash)
- BankTransaction.cs - Transactions importées depuis CODA
- CodaFile.cs - Fichiers CODA importés
- ActivityFinancialTransaction.cs - Transactions financières liées activité

✅ **Entités modifiées** (3 fichiers):
- Booking.cs: + StructuredCommunication, TotalAmount, PaidAmount, PaymentStatus, Payments collection
- Expense.cs: + ExpenseType (Reimbursement vs PersonalConsumption)
- Organisation.cs: + BankAccountNumber, BankAccountName, CodaFiles, BankTransactions collections

✅ **Configurations EF Core** (5 fichiers):
- PaymentConfiguration.cs
- BankTransactionConfiguration.cs (index sur StructuredCommunication)
- CodaFileConfiguration.cs
- ActivityFinancialTransactionConfiguration.cs
- BookingConfiguration.cs (modifié: index unique sur StructuredCommunication, precision pour montants)

✅ **Services**:
- IStructuredCommunicationService + StructuredCommunicationService (génération format belge +++XXX/XXXX/XXXXX+++)
- BrevoEmailService: nouvelle surcharge avec montant, IBAN, communication structurée
- Enregistrement dans Program.cs

✅ **Migration database**:
- 20260131000524_AddFinancialManagement.cs (2852 lignes ajoutées)
- Toutes les tables créées: Payments, BankTransactions, CodaFiles, ActivityFinancialTransactions
- Colonnes ajoutées à Bookings, Expenses, Organisations

### Prochaines phases

⏳ **Phase 2: Parsing CODA** - En attente
⏳ **Phase 3: Rapprochement bancaire** - En attente
⏳ **Phase 4: Paiements manuels** - En attente
⏳ **Phase 5: Salaires équipe** - En attente
⏳ **Phase 6: Rapports financiers** - En attente
⏳ **Phase 7: Dashboard & Navigation** - En attente
⏳ **Phase 8: Localisation** - En attente
⏳ **Phase 9: Tests & Documentation** - En attente

---

## Clarifications importantes (réponses utilisateur)

1. **Salaires équipe**: NE PAS créer de transactions de paiement. Le système calcule uniquement le total (prestations + notes de frais - consommations personnelles) et l'affiche pour information. Le paiement réel est géré en dehors de l'application.

2. **Consommations personnelles animateurs**: Les animateurs peuvent enregistrer des petites dépenses personnelles (ex: coca du frigo) qui sont DÉDUITES du montant à leur verser (pas des remboursements mais des débits).

3. **Permissions**: Tous les coordinateurs ont accès à la gestion financière (pas besoin de nouveau rôle).

4. **Graphiques**: Utiliser Chart.js pour les visualisations des rapports financiers.

5. **Évolutivité**: Le système doit être conçu pour supporter facilement l'ajout futur de:
   - Excursions avec paiements supplémentaires aux parents
   - Réservation/paiement de transport (car)
   - Paiement d'entrées (parc d'attraction, musée, etc.)
   - Nouvelles catégories de transactions

## Vue d'ensemble

Implémentation d'un système complet de gestion financière pour les activités, incluant:
- Génération de communications structurées et envoi dans les emails de confirmation
- Import et parsing des fichiers CODA (relevés bancaires belges)
- Rapprochement automatique des paiements bancaires avec les réservations
- Gestion des notes de frais des animateurs
- Calcul automatique de la paie des membres d'équipe (basé sur présences)
- Saisie manuelle d'entrées/sorties d'argent
- Bilan financier complet par activité
- Export Excel/PDF des rapports financiers

## Analyse des besoins

### 1. Communications structurées belges
- Format: `+++XXX/XXXX/XXXXX+++` (12 chiffres avec modulo 97)
- Unique par réservation pour identification automatique
- Envoyée dans l'email de confirmation avec montant et IBAN

### 2. Fichiers CODA
- Format fixe belge standard (lignes de 128 caractères)
- Types d'enregistrements: 0 (header), 1 (ancien solde), 2 (mouvement), 3 (info), 8 (nouveau solde), 9 (footer)
- Parsing nécessaire pour extraire: date, montant, communication structurée, contrepartie

### 3. Types de transactions
**Entrées:**
- Paiements parents (via virement ou cash)
- Autres revenus
- (Futur: paiements supplémentaires pour excursions)

**Sorties:**
- Salaires membres d'équipe (calculés automatiquement: jours × défraiement)
- Notes de frais animateurs (remboursements: transport, achats pour activité, etc.)
- Consommations personnelles animateurs (débits: coca, snacks du frigo, etc.)
- Autres dépenses
- (Futur: réservation car, entrées parc, matériel excursion, etc.)

### 4. Rapprochement bancaire
- Matching automatique: communication structurée → réservation
- Matching manuel pour transactions sans communication structurée
- Statuts: non payé, partiellement payé, payé, trop-perçu

## Architecture proposée

### Nouvelles entités (Core/Entities)

#### 1. Payment.cs
```csharp
public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; }
    public decimal Amount { get; set; }  // Precision(10,2)
    public DateTime PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }  // BankTransfer, Cash, Other
    public PaymentStatus Status { get; set; }  // Pending, Completed, Cancelled
    public string? StructuredCommunication { get; set; }  // +++XXX/XXXX/XXXXX+++
    public string? Reference { get; set; }  // Référence libre
    public int? BankTransactionId { get; set; }  // FK to BankTransaction (si importé CODA)
    public BankTransaction? BankTransaction { get; set; }
    public int? CreatedByUserId { get; set; }  // User who recorded payment
}
```

#### 2. BankTransaction.cs (transactions CODA importées)
```csharp
public class BankTransaction
{
    public int Id { get; set; }
    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime ValueDate { get; set; }
    public decimal Amount { get; set; }  // Precision(10,2), peut être négatif
    public string? StructuredCommunication { get; set; }
    public string? FreeCommunication { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyAccount { get; set; }
    public string TransactionCode { get; set; }  // Code CODA (ex: "05" = virement)
    public int CodaFileId { get; set; }
    public CodaFile CodaFile { get; set; }
    public bool IsReconciled { get; set; }  // Rapproché avec une réservation?
    public int? PaymentId { get; set; }  // FK si rapproché
    public Payment? Payment { get; set; }
}
```

#### 3. CodaFile.cs (fichiers CODA importés)
```csharp
public class CodaFile
{
    public int Id { get; set; }
    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; }
    public string FileName { get; set; }
    public DateTime ImportDate { get; set; }
    public DateTime StatementDate { get; set; }
    public string AccountNumber { get; set; }
    public decimal OldBalance { get; set; }
    public decimal NewBalance { get; set; }
    public int TransactionCount { get; set; }
    public int ImportedByUserId { get; set; }
    public ICollection<BankTransaction> Transactions { get; set; }
}
```

#### 4. ActivityFinancialTransaction.cs (autres entrées/sorties)
```csharp
public class ActivityFinancialTransaction
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public Activity Activity { get; set; }
    public DateTime TransactionDate { get; set; }
    public TransactionType Type { get; set; }  // Income, Expense
    public TransactionCategory Category { get; set; }  // Extensible pour futures excursions
    public decimal Amount { get; set; }  // Precision(10,2)
    public string Description { get; set; }
    public int? PaymentId { get; set; }  // FK si c'est un paiement de réservation
    public Payment? Payment { get; set; }
    public int? ExpenseId { get; set; }  // FK si c'est une note de frais ou consommation
    public Expense? Expense { get; set; }
    public int CreatedByUserId { get; set; }
}
```

**Note**: Les salaires équipe ne sont PAS enregistrés comme transactions. Ils sont calculés dynamiquement pour affichage uniquement.

#### 5. Modifications à Booking.cs
Ajouter:
```csharp
public string? StructuredCommunication { get; set; }  // Généré automatiquement
public decimal TotalAmount { get; set; }  // Calculé: PricePerDay * NumberOfDays
public decimal PaidAmount { get; set; }  // Somme des paiements
public PaymentStatus PaymentStatus { get; set; }  // NotPaid, PartiallyPaid, Paid, Overpaid
public ICollection<Payment> Payments { get; set; }
```

#### 6. Modifications à Expense.cs (entité existante)
Ajouter:
```csharp
public ExpenseType ExpenseType { get; set; }  // Reimbursement (note de frais) ou PersonalConsumption (débit)
```

**Comportement**:
- `Reimbursement`: Montant AJOUTÉ au solde de l'animateur (remboursement)
- `PersonalConsumption`: Montant DÉDUIT du solde de l'animateur (consommation perso)

#### 7. Modifications à Organisation.cs
Ajouter:
```csharp
public string? BankAccountNumber { get; set; }  // IBAN
public string? BankAccountName { get; set; }
```

### Nouveaux Enums

```csharp
public enum PaymentMethod { BankTransfer, Cash, Other }
public enum PaymentStatus { NotPaid, PartiallyPaid, Paid, Overpaid, Cancelled }
public enum TransactionType { Income, Expense }

// Catégories extensibles pour futures excursions
public enum TransactionCategory
{
    Payment,              // Paiement de réservation
    TeamExpense,          // Note de frais animateur (remboursement)
    PersonalConsumption,  // Consommation personnelle animateur (débit)
    Other,                // Autres dépenses/revenus
    // Futur: ExcursionPayment, TransportCost, TicketCost, etc.
}

// Type de dépense pour l'entité Expense existante
public enum ExpenseType
{
    Reimbursement,        // Note de frais → AJOUTÉ au solde
    PersonalConsumption   // Consommation perso → DÉDUIT du solde
}
```

### Nouveaux Services (Infrastructure/Services)

#### 1. StructuredCommunicationService.cs
```csharp
public interface IStructuredCommunicationService
{
    string GenerateStructuredCommunication(int bookingId);
    bool ValidateStructuredCommunication(string communication);
    int? ExtractBookingIdFromCommunication(string communication);
}
```
Logique:
- Format: `+++{bookingId avec padding}/XXXX/XXXXX+++`
- Checksum modulo 97 pour validation

#### 2. CodaParserService.cs
```csharp
public interface ICodaParserService
{
    Task<CodaFileDto> ParseCodaFileAsync(Stream fileStream, string fileName);
    Task<CodaFile> ImportCodaFileAsync(CodaFileDto dto, int organisationId, int userId);
}
```
Logique:
- Parsing ligne par ligne selon spec CODA belge
- Extraction des transactions avec montants, dates, communications
- Validation du fichier (checksums, formats)

#### 3. BankReconciliationService.cs
```csharp
public interface IBankReconciliationService
{
    Task<int> AutoReconcileTransactionsAsync(int codaFileId);
    Task<bool> ManualReconcileAsync(int transactionId, int bookingId);
    Task<List<UnreconciledTransactionDto>> GetUnreconciledTransactionsAsync(int organisationId);
}
```
Logique:
- Auto-matching par communication structurée
- Création automatique de Payment quand match trouvé
- Mise à jour de Booking.PaidAmount et PaymentStatus

#### 4. FinancialReportService.cs
```csharp
public interface IFinancialReportService
{
    Task<ActivityFinancialSummaryDto> GetActivitySummaryAsync(int activityId);
    Task<List<TeamMemberSalaryDto>> CalculateTeamSalariesAsync(int activityId);  // Calcul SANS enregistrement
    Task<byte[]> GenerateFinancialReportPdfAsync(int activityId);
}
```
Logique:
- Calcul revenus: somme des paiements confirmés
- Calcul dépenses théoriques: salaires (TeamMember.DailyCompensation × jours présents) + notes de frais enregistrées
- Bilan: revenus - dépenses
- **Important**: Les salaires ne sont JAMAIS enregistrés comme transactions, seulement calculés pour affichage
- Le paiement réel des salaires se fait en dehors de l'application

#### 5. Modification BrevoEmailService.cs
Ajouter méthode:
```csharp
Task SendBookingConfirmationEmailAsync(
    string parentEmail,
    string parentName,
    string childName,
    string activityName,
    DateTime startDate,
    DateTime endDate,
    decimal totalAmount,
    string structuredCommunication,
    string bankAccount)
```
Template email incluant:
- Montant à payer
- IBAN du compte
- Communication structurée à utiliser

### Nouveaux Controllers & Views

#### 1. FinancialController.cs
**Actions:**
- `Index(int activityId)` - Dashboard financier de l'activité
- `Transactions(int activityId)` - Liste toutes les transactions
- `AddTransaction(int activityId)` - Ajouter entrée/sortie manuelle
- `ImportCoda()` - Upload fichier CODA
- `ProcessCoda(int codaFileId)` - Traiter et réconcilier
- `Reconciliation(int organisationId)` - Vue rapprochement manuel
- `ReconcileTransaction(int transactionId, int bookingId)` - POST rapprochement
- `TeamSalaries(int activityId)` - Calcul salaires équipe
- `Report(int activityId)` - Bilan financier complet
- `ExportReport(int activityId)` - Export Excel/PDF

**Views:**
- `Index.cshtml` - Cards: revenus totaux, dépenses totales, bilan, paiements en attente
- `Transactions.cshtml` - Tableau filtrable/triable de toutes les transactions
- `ImportCoda.cshtml` - Form upload fichier + liste fichiers importés
- `Reconciliation.cshtml` - Split view: transactions non rapprochées / réservations impayées
- `TeamSalaries.cshtml` - Tableau: membre, jours présents, défraiement journalier, notes de frais, total à payer (AFFICHAGE UNIQUEMENT)
  - Bouton "Exporter pour comptabilité" → Excel/PDF
  - **Pas de bouton "Payer" ou "Valider paiement"**
- `Report.cshtml` - Bilan complet avec graphiques Chart.js et détails
  - Graphique camembert: répartition revenus/dépenses
  - Graphique barres: comparaison par catégorie
  - Tableaux détaillés des transactions

#### 2. PaymentsController.cs
**Actions:**
- `Index(int? activityId, int? bookingId)` - Liste paiements
- `Create(int bookingId)` - Enregistrer paiement manuel (cash)
- `Details(int id)` - Détail paiement
- `Cancel(int id)` - Annuler paiement

#### 3. Navigation
Ajouter dans ActivityManagement/Index.cshtml:
- Card "Gestion financière" → `/Financial/Index?activityId={id}`

### Migration Database

```csharp
public partial class AddFinancialManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Ajouter colonnes à Booking
        migrationBuilder.AddColumn<string>("StructuredCommunication", "Bookings");
        migrationBuilder.AddColumn<decimal>("TotalAmount", "Bookings", precision: 10, scale: 2);
        migrationBuilder.AddColumn<decimal>("PaidAmount", "Bookings", precision: 10, scale: 2);
        migrationBuilder.AddColumn<int>("PaymentStatus", "Bookings");

        // Ajouter colonnes à Organisation
        migrationBuilder.AddColumn<string>("BankAccountNumber", "Organisations");
        migrationBuilder.AddColumn<string>("BankAccountName", "Organisations");

        // Créer tables
        migrationBuilder.CreateTable("CodaFiles", ...);
        migrationBuilder.CreateTable("BankTransactions", ...);
        migrationBuilder.CreateTable("Payments", ...);
        migrationBuilder.CreateTable("ActivityFinancialTransactions", ...);
    }
}
```

### Workflow utilisateur

#### A. Configuration initiale
1. Admin configure IBAN dans Organisation settings
2. System génère communications structurées pour réservations existantes

#### B. Réservation et paiement
1. Parent fait réservation → Booking créé
2. System génère communication structurée unique
3. Email envoyé avec: montant, IBAN, communication structurée
4. Parent paie par virement ou cash pendant activité

#### C. Import CODA
1. Coordinateur télécharge CODA de la banque
2. Upload fichier dans `/Financial/ImportCoda`
3. System parse et crée BankTransactions
4. Auto-réconciliation par communication structurée
5. Paiements créés automatiquement, Booking.PaidAmount mis à jour

#### D. Rapprochement manuel
1. Transactions sans communication structurée → `/Financial/Reconciliation`
2. Coordinateur match manuellement transaction ↔ réservation
3. System crée Payment et met à jour statuts

#### E. Notes de frais et consommations personnelles
1. **Remboursements** (ExpenseType.Reimbursement):
   - Animateur soumet note de frais (transport, achats matériel, etc.)
   - System crée Expense avec ExpenseType = Reimbursement
   - Montant sera AJOUTÉ au solde de l'animateur

2. **Consommations personnelles** (ExpenseType.PersonalConsumption):
   - Animateur enregistre consommation (coca, snack du frigo, etc.)
   - System crée Expense avec ExpenseType = PersonalConsumption
   - Montant sera DÉDUIT du solde de l'animateur

3. System crée ActivityFinancialTransaction automatiquement pour les deux types

#### F. Salaires équipe (CALCUL UNIQUEMENT - pas de paiement)
1. `/Financial/TeamSalaries?activityId={id}`
2. System calcule et affiche pour chaque TeamMember:
   - **Prestations**: nombre de jours présents × DailyCompensation
   - **+ Remboursements**: somme des Expense.Reimbursement
   - **- Consommations**: somme des Expense.PersonalConsumption
   - **= Solde à verser**
3. **Pas de création de transaction** - le paiement réel se fait en dehors de l'app
4. Option d'export Excel/PDF pour remise au service comptable

#### G. Bilan
1. `/Financial/Report?activityId={id}`
2. Affiche:
   - Revenus: paiements confirmés
   - Dépenses: salaires + notes de frais + autres
   - Bilan net
   - Détail par catégorie
3. Export Excel/PDF

## Fichiers critiques à créer/modifier

### Nouveaux fichiers (Core)
- `src/Cedeva.Core/Entities/Payment.cs`
- `src/Cedeva.Core/Entities/BankTransaction.cs`
- `src/Cedeva.Core/Entities/CodaFile.cs`
- `src/Cedeva.Core/Entities/ActivityFinancialTransaction.cs`
- `src/Cedeva.Core/Enums/PaymentMethod.cs`
- `src/Cedeva.Core/Enums/PaymentStatus.cs`
- `src/Cedeva.Core/Enums/TransactionType.cs`
- `src/Cedeva.Core/Enums/TransactionCategory.cs`
- `src/Cedeva.Core/Enums/ExpenseType.cs` (pour Expense: Reimbursement vs PersonalConsumption)
- `src/Cedeva.Core/Interfaces/IStructuredCommunicationService.cs`
- `src/Cedeva.Core/Interfaces/ICodaParserService.cs`
- `src/Cedeva.Core/Interfaces/IBankReconciliationService.cs`
- `src/Cedeva.Core/Interfaces/IFinancialReportService.cs`

### Nouveaux fichiers (Infrastructure)
- `src/Cedeva.Infrastructure/Data/Configurations/PaymentConfiguration.cs`
- `src/Cedeva.Infrastructure/Data/Configurations/BankTransactionConfiguration.cs`
- `src/Cedeva.Infrastructure/Data/Configurations/CodaFileConfiguration.cs`
- `src/Cedeva.Infrastructure/Data/Configurations/ActivityFinancialTransactionConfiguration.cs`
- `src/Cedeva.Infrastructure/Services/StructuredCommunicationService.cs`
- `src/Cedeva.Infrastructure/Services/CodaParserService.cs`
- `src/Cedeva.Infrastructure/Services/BankReconciliationService.cs`
- `src/Cedeva.Infrastructure/Services/FinancialReportService.cs`
- `src/Cedeva.Infrastructure/Migrations/YYYYMMDDHHMMSS_AddFinancialManagement.cs`

### Nouveaux fichiers (Website)
- `src/Cedeva.Website/Features/Financial/FinancialController.cs`
- `src/Cedeva.Website/Features/Financial/ViewModels/ActivityFinancialSummaryViewModel.cs`
- `src/Cedeva.Website/Features/Financial/ViewModels/BankTransactionViewModel.cs`
- `src/Cedeva.Website/Features/Financial/ViewModels/ReconciliationViewModel.cs`
- `src/Cedeva.Website/Features/Financial/ViewModels/TeamSalaryViewModel.cs`
- `src/Cedeva.Website/Features/Financial/ViewModels/ImportCodaViewModel.cs`
- `src/Cedeva.Website/Features/Financial/Index.cshtml`
- `src/Cedeva.Website/Features/Financial/Transactions.cshtml`
- `src/Cedeva.Website/Features/Financial/ImportCoda.cshtml`
- `src/Cedeva.Website/Features/Financial/Reconciliation.cshtml`
- `src/Cedeva.Website/Features/Financial/TeamSalaries.cshtml`
- `src/Cedeva.Website/Features/Financial/Report.cshtml`
- `src/Cedeva.Website/Features/Payments/PaymentsController.cs`
- `src/Cedeva.Website/Features/Payments/ViewModels/PaymentViewModel.cs`
- `src/Cedeva.Website/Features/Payments/Index.cshtml`
- `src/Cedeva.Website/Features/Payments/Create.cshtml`

### Fichiers à modifier
- `src/Cedeva.Core/Entities/Booking.cs` - Ajouter champs financiers
- `src/Cedeva.Core/Entities/Expense.cs` - Ajouter champ ExpenseType (Reimbursement/PersonalConsumption)
- `src/Cedeva.Core/Entities/Organisation.cs` - Ajouter IBAN
- `src/Cedeva.Infrastructure/Data/CedevaDbContext.cs` - Ajouter DbSets et configurations
- `src/Cedeva.Infrastructure/Services/Email/BrevoEmailService.cs` - Mettre à jour template email
- `src/Cedeva.Website/Features/ActivityManagement/Index.cshtml` - Ajouter card "Finances"
- `src/Cedeva.Website/Features/Shared/_Layout.cshtml` - Ajouter Chart.js CDN
- `src/Cedeva.Website/Program.cs` - Enregistrer nouveaux services
- `src/Cedeva.Website/Localization/SharedResources.*.resx` - Ajouter clés financières

## Localisation requise

Nouvelles clés à ajouter (français):
- `Financial.*` - Navigation, titres, labels
- `Payment.*` - Statuts, méthodes, actions
- `BankTransaction.*` - Import CODA, rapprochement
- `TransactionCategory.*` - Catégories transactions
- `StructuredCommunication.*` - Messages validation
- `Report.*` - Titres rapports, labels graphiques

Exemples:
```
Financial.Dashboard = Tableau de bord financier
Financial.ImportCoda = Importer fichier CODA
Payment.Status.NotPaid = Non payé
Payment.Status.Paid = Payé
Report.TotalRevenue = Revenus totaux
Report.TotalExpenses = Dépenses totales
Report.NetBalance = Bilan net
```

## Architecture extensible pour futures excursions

**Principe**: Le système est conçu pour être facilement extensible sans modifications majeures de l'architecture.

### Futures fonctionnalités supportées

1. **Paiements supplémentaires aux parents (excursions)**:
   - Ajouter nouvelle valeur à `TransactionCategory` (ex: `ExcursionPayment`)
   - Utiliser l'entité `Payment` existante avec BookingId
   - Générer nouvelle communication structurée pour le paiement supplémentaire
   - Envoyer email automatique avec montant + communication

2. **Réservation de car**:
   - Ajouter `TransportCost` à `TransactionCategory`
   - Créer `ActivityFinancialTransaction` avec Category = TransportCost
   - Lier éventuellement à un TeamMember si payé par un animateur (remboursement)

3. **Entrées parc/musée/activités**:
   - Ajouter `TicketCost` à `TransactionCategory`
   - Créer transactions avec description détaillée (nombre de personnes, lieu, etc.)
   - Possibilité de lier à un TeamMember pour remboursement

4. **Matériel pour excursions**:
   - Utiliser `Category = Other` ou créer `MaterialCost`
   - Même mécanisme de transaction

### Modifications nécessaires pour excursions (futur)

**Changements minimaux requis**:
- Ajouter nouvelles valeurs d'enum `TransactionCategory` (pas de migration de données)
- Possiblement créer entité `Excursion`:
  ```csharp
  public class Excursion
  {
      public int Id { get; set; }
      public int ActivityId { get; set; }
      public Activity Activity { get; set; }
      public string Name { get; set; }
      public DateTime Date { get; set; }
      public string? Location { get; set; }
      public decimal EstimatedCostPerChild { get; set; }
      public ICollection<ActivityFinancialTransaction> Transactions { get; set; }
  }
  ```
- Créer vues UI pour gestion excursions
- Ajouter logique d'envoi email pour paiements supplémentaires

**Pas de modifications requises**:
- Entités financières principales (Payment, BankTransaction, etc.)
- Service CODA (fonctionne avec toutes les communications structurées)
- Service de rapprochement bancaire
- Rapports financiers (agrègent automatiquement par catégorie)

### Avantages de cette architecture

- **Tables financières génériques**: `ActivityFinancialTransaction` supporte toutes les catégories
- **Enum extensible**: Ajouter des valeurs sans migration de données
- **Séparation logique**: Calcul vs paiement, revenus vs dépenses
- **Support multi-catégories natif**: Les rapports agrègent déjà par catégorie
- **Communications structurées réutilisables**: Même mécanisme pour tous les paiements
- **CODA-agnostic**: Le parsing ne dépend pas du type de paiement

## Considérations techniques

### Parsing CODA
- Format fixe 128 caractères par ligne
- Encodage: ASCII ou EBCDIC (à détecter)
- Validation checksums sur chaque ligne
- Gestion des lignes multilignes (type 2 + plusieurs type 3)

### Communications structurées
- Format: 12 chiffres divisés en 3 groupes (XXX/XXXX/XXXXX)
- Modulo 97 des 10 premiers chiffres = 2 derniers chiffres
- Si modulo = 0, utiliser 97
- Padding bookingId sur 10 chiffres

### Sécurité
- Upload CODA: valider extension (.cod, .txt)
- Validation anti-CSRF sur tous les POST
- **Authorization: Tous les coordinateurs** ont accès à la gestion financière (attribut `[Authorize(Roles = "Coordinator,Admin")]`)
- Multi-tenancy: filtrer par OrganisationId partout
- Les parents n'ont AUCUN accès aux vues financières

### Graphiques et Visualisation
- **Chart.js** pour les graphiques interactifs
- CDN: `https://cdn.jsdelivr.net/npm/chart.js`
- Types de graphiques à implémenter:
  - Camembert: répartition revenus/dépenses
  - Barres: comparaison par catégorie
  - Ligne: évolution des paiements dans le temps (optionnel)
- Couleurs Cedeva: bleu #007faf pour revenus, rouge #dc3545 pour dépenses

### Performance
- Index sur BankTransaction.StructuredCommunication
- Index sur Payment.BookingId
- Pagination sur listes transactions (peut devenir volumineuse)

## Ordre d'implémentation

### Phase 1: Fondations (Entities + Services de base)
1. Créer enums
2. Créer/modifier entities
3. Créer configurations EF
4. Créer migration
5. Créer StructuredCommunicationService
6. Mettre à jour BrevoEmailService

### Phase 2: Parsing CODA
1. Créer CodaParserService
2. Créer ImportCodaViewModel
3. Créer FinancialController.ImportCoda (GET/POST)
4. Créer vue ImportCoda.cshtml
5. Tester parsing avec vrais fichiers CODA

### Phase 3: Rapprochement bancaire
1. Créer BankReconciliationService
2. Créer ReconciliationViewModel
3. Créer FinancialController.Reconciliation + ReconcileTransaction
4. Créer vue Reconciliation.cshtml
5. Implémenter auto-matching

### Phase 4: Paiements manuels
1. Créer PaymentsController
2. Créer PaymentViewModel
3. Créer vues Payments (Index, Create)
4. Tester création paiement cash

### Phase 5: Salaires équipe (CALCUL UNIQUEMENT)
1. Créer TeamSalaryViewModel (avec calculs: jours × défraiement + notes de frais)
2. Créer FinancialController.TeamSalaries (GET - affichage, pas de POST pour paiement)
3. Créer FinancialController.ExportTeamSalaries (Excel/PDF pour comptabilité)
4. Créer vue TeamSalaries.cshtml (affichage tableau + bouton export)
5. Implémenter calcul automatique (lecture seule, PAS de création de transactions)
6. **Important**: Pas de workflow de validation/paiement - juste affichage et export

### Phase 6: Rapports financiers
1. Créer FinancialReportService
2. Créer ActivityFinancialSummaryViewModel
3. Créer FinancialController.Report + Export
4. Ajouter Chart.js CDN dans _Layout.cshtml
5. Créer vue Report.cshtml avec graphiques Chart.js:
   - Camembert: revenus vs dépenses
   - Barres: détail par catégorie
   - Tableaux détaillés
6. Implémenter export Excel/PDF des rapports

### Phase 7: Dashboard & Navigation
1. Créer FinancialController.Index (dashboard)
2. Créer vue Index.cshtml avec cards
3. Ajouter card dans ActivityManagement
4. Créer Transactions.cshtml (liste complète)

### Phase 8: Localisation
1. Ajouter toutes les clés FR
2. Dupliquer en NL/EN avec préfixes

### Phase 9: Tests & Documentation
1. Tester workflow complet
2. Mettre à jour CLAUDE.md
3. Mettre à jour cedeva.md

## Vérification

### Tests manuels
1. Créer réservation → vérifier communication structurée générée
2. Vérifier email contient montant + IBAN + communication
3. Upload fichier CODA → vérifier parsing correct
4. Vérifier auto-réconciliation fonctionne
5. Créer paiement cash manuel
6. Calculer salaires équipe
7. Générer rapport financier
8. Exporter Excel/PDF

### Validations
- Communications structurées uniques par booking
- Checksum modulo 97 correct
- CODA parsing exact (comparer avec import bancaire)
- Multi-tenancy: utilisateur ne voit que son org
- Montants toujours précision (10,2)

## Documentation à mettre à jour

### CLAUDE.md
Section "Implementation Status":
```markdown
### ✅ Phase 6: Gestion Financière (COMPLETED)
- [x] Communications structurées belges
- [x] Import fichiers CODA (relevés bancaires)
- [x] Rapprochement automatique des paiements
- [x] Paiements manuels (cash)
- [x] Calcul automatique salaires équipe
- [x] Notes de frais animateurs
- [x] Bilan financier par activité
- [x] Export rapports Excel/PDF
```

### cedeva.md
Ajouter section "Gestion Financière":
```markdown
## Gestion Financière

### Communications Structurées
Chaque réservation génère automatiquement une communication structurée belge (format +++XXX/XXXX/XXXXX+++)
basée sur l'ID de la réservation avec validation modulo 97.

### Import CODA
Support complet du format CODA belge pour l'import des relevés bancaires. Le système parse
automatiquement les transactions et réconcilie les paiements avec les réservations via la
communication structurée.

### Rapprochement Bancaire
- Auto-matching par communication structurée
- Rapprochement manuel pour transactions sans communication
- Statuts de paiement: Non payé, Partiellement payé, Payé, Trop-perçu

### Revenus et Dépenses
**Revenus:**
- Paiements par virement bancaire (via CODA)
- Paiements en cash (saisie manuelle)

**Dépenses:**
- Salaires équipe (calcul automatique: jours présents × défraiement journalier)
- Notes de frais animateurs
- Autres dépenses

### Rapports Financiers
Bilan complet par activité avec:
- Revenus totaux (paiements confirmés)
- Dépenses totales (salaires + notes de frais)
- Bilan net (revenus - dépenses)
- Export Excel/PDF
```

---

**Estimation:** ~40-50 fichiers à créer/modifier, ~3000-4000 lignes de code
