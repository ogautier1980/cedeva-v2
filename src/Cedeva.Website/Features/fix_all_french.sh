#!/bin/bash

# Activities/Create.cshtml
sed -i 's/>Créer l'\''activité</>@Localizer["Activities.CreateButton"]</g' Activities/Create.cshtml

# Activities/Details.cshtml  
sed -i 's/>Retour</>@Localizer["Back"]</g' Activities/Details.cshtml

# Account/Profile.cshtml
sed -i 's/>Retour</>@Localizer["Back"]</g' Account/Profile.cshtml

# PublicRegistration files
sed -i 's/>Continuer</>@Localizer["Continue"]</g' PublicRegistration/ChildInformation.cshtml
sed -i 's/>Continuer</>@Localizer["Continue"]</g' PublicRegistration/ParentInformation.cshtml
sed -i 's/>Continuer</>@Localizer["Continue"]</g' PublicRegistration/SelectActivity.cshtml
sed -i 's/>Finaliser l'\''inscription</>@Localizer["PublicRegistration.FinalizeRegistration"]</g' PublicRegistration/ActivityQuestions.cshtml

# Organisations/Delete.cshtml
sed -i 's/Cette organisation a @totalRelated élément(s) associé(s)\./@string.Format(Localizer["Organisations.DeleteRelatedWarning"].Value, totalRelated)/g' Organisations/Delete.cshtml

# Organisations/Details.cshtml
sed -i 's/>Voir les activités</>@Localizer["Organisations.ViewActivities"]</g' Organisations/Details.cshtml

# Users/Index.cshtml
sed -i 's/-- Toutes les organisations --/@Localizer["AllOrganisations"]/g' Users/Index.cshtml
sed -i 's/>Précédent</>@Localizer["Previous"]</g' Users/Index.cshtml
sed -i 's/>Suivant</>@Localizer["Next"]</g' Users/Index.cshtml

echo "Done fixing French texts"
