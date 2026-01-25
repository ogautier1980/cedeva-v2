import xml.etree.ElementTree as ET

# Les nouvelles clés à ajouter
new_keys = {
    "NoGroupOptional": "-- Aucun groupe (optionnel) --",
    "SelectOrganisation": "-- Sélectionnez une organisation --",
    "Bookings.DeleteWarning": "Attention ! Êtes-vous sûr de vouloir supprimer cette inscription ?",
    "Bookings.ActionRequired": "Action requise",
    "Bookings.NotYetConfirmed": "Cette inscription n'est pas encore confirmée.",
    "Bookings.MedicalSheetNotReceived": "La fiche médicale n'a pas été reçue.",
    "Bookings.ContactParentForMedicalSheet": "Contactez le parent pour demander la fiche médicale.",
    "Home.NoActivities": "Aucune activité enregistrée.",
    "Home.NoBookings": "Aucune inscription enregistrée.",
    "Organisations.LogoUrlOptional": "URL du logo de l'organisation (optionnel)",
    "PublicRegistration.ActivityQuestionsLead": "Veuillez répondre aux questions suivantes concernant l'activité.",
    "PublicRegistration.NoQuestionsRequired": "Aucune question n'est requise pour cette activité.",
    "PublicRegistration.ChildInformationLead": "Veuillez renseigner les informations de votre enfant.",
    "PublicRegistration.ParentInformationLead": "Veuillez renseigner vos informations personnelles.",
    "PublicRegistration.SelectActivityLead": "Veuillez sélectionner l'activité pour laquelle vous souhaitez inscrire votre enfant.",
    "PublicRegistration.NoActivitiesAvailable": "Aucune activité disponible pour le moment.",
    "SelectOrganisationOptional": "-- Sélectionnez une organisation (optionnel) --",
    "NoOrganisation": "Aucune organisation"
}

# Lire le fichier FR
with open('SharedResources.fr.resx', 'r', encoding='utf-8') as f:
    content = f.read()

# Trouver la position de </root>
root_pos = content.rfind('</root>')

# Créer les nouveaux blocs
new_blocks = []
for key, value in new_keys.items():
    block = f'  <data name="{key}" xml:space="preserve">\n    <value>{value}</value>\n  </data>\n'
    new_blocks.append(block)

# Insérer les nouveaux blocs
new_content = content[:root_pos] + ''.join(new_blocks) + content[root_pos:]

# Écrire le fichier
with open('SharedResources.fr.resx', 'w', encoding='utf-8') as f:
    f.write(new_content)

print(f"Added {len(new_keys)} keys to SharedResources.fr.resx")
