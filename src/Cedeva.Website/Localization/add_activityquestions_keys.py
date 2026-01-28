#!/usr/bin/env python3
import xml.etree.ElementTree as ET

# Keys to add for ActivityQuestions
new_keys = {
    "ActivityQuestions.Title": "Questions personnalisées",
    "ActivityQuestions.CreateNew": "Créer une nouvelle question",
    "ActivityQuestions.NoQuestions": "Aucune question trouvée",
    "ActivityQuestions.CreateFirst": "Créer la première question",
    "ActivityQuestions.QuestionText": "Texte de la question",
    "ActivityQuestions.QuestionTextPlaceholder": "Ex: Votre enfant a-t-il des allergies alimentaires ?",
    "ActivityQuestions.QuestionTextHelp": "La question qui sera posée aux parents lors de l'inscription",
    "ActivityQuestions.QuestionType": "Type de question",
    "ActivityQuestions.IsRequired": "Obligatoire",
    "ActivityQuestions.IsRequiredHelp": "Les parents devront répondre à cette question",
    "ActivityQuestions.Options": "Options",
    "ActivityQuestions.OptionsPlaceholder": "Ex: Option1,Option2,Option3",
    "ActivityQuestions.OptionsHelp": "Séparez les options par des virgules",
    "ActivityQuestions.OptionsRequired": "Les options sont requises pour les questions de type Radio et Liste déroulante",
    "ActivityQuestions.Required": "Obligatoire",
    "ActivityQuestions.AnswersCount": "Réponses",
    "ActivityQuestions.Yes": "Oui",
    "ActivityQuestions.No": "Non",
    "ActivityQuestions.Edit": "Modifier la question",
    "ActivityQuestions.Delete": "Supprimer la question",
    "ActivityQuestions.DeleteWarning": "Êtes-vous sûr de vouloir supprimer cette question ?",
    "ActivityQuestions.CannotDeleteHasAnswers": "Cette question ne peut pas être supprimée car elle contient des réponses.",
    "ActivityQuestions.DeleteErrorHasAnswers": "Impossible de supprimer cette question car elle contient des réponses.",
    "ActivityQuestions.CreateSuccess": "Question créée avec succès",
    "ActivityQuestions.UpdateSuccess": "Question mise à jour avec succès",
    "ActivityQuestions.DeleteSuccess": "Question supprimée avec succès",
    # Enum values
    "Enum.QuestionType.Text": "Texte libre",
    "Enum.QuestionType.Checkbox": "Case à cocher",
    "Enum.QuestionType.Radio": "Choix unique (boutons radio)",
    "Enum.QuestionType.Dropdown": "Liste déroulante",
    "SelectType": "Sélectionnez un type",
}

def add_keys_to_resx(file_path, keys, lang_prefix=""):
    try:
        tree = ET.parse(file_path)
        root = tree.getroot()

        # Get existing keys
        existing_keys = set()
        for data in root.findall('data'):
            name = data.get('name')
            if name:
                existing_keys.add(name)

        # Add new keys
        added = 0
        for key, value in keys.items():
            if key not in existing_keys:
                data_elem = ET.SubElement(root, 'data', name=key)
                data_elem.set('{http://www.w3.org/XML/1998/namespace}space', 'preserve')
                value_elem = ET.SubElement(data_elem, 'value')
                value_elem.text = f"{lang_prefix}{value}" if lang_prefix else value
                added += 1

        if added > 0:
            # Format the XML nicely
            ET.indent(tree, space="  ")
            tree.write(file_path, encoding='utf-8', xml_declaration=True)
            print(f"[OK] Added {added} keys to {file_path}")
        else:
            print(f"[INFO] No new keys to add to {file_path}")

    except Exception as e:
        print(f"[ERROR] Error processing {file_path}: {e}")

# Process all resource files
base_path = "c:/Users/ogaut/cedeva-v2/src/Cedeva.Website/Localization/"

print("Adding ActivityQuestions localization keys...")
add_keys_to_resx(f"{base_path}SharedResources.resx", new_keys)
add_keys_to_resx(f"{base_path}SharedResources.fr.resx", new_keys)
add_keys_to_resx(f"{base_path}SharedResources.nl.resx", new_keys, "[NL] ")
add_keys_to_resx(f"{base_path}SharedResources.en.resx", new_keys, "[EN] ")

print("\n[OK] Done!")
