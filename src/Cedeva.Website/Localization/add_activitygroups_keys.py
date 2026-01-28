#!/usr/bin/env python3
import xml.etree.ElementTree as ET

# Keys to add for ActivityGroups
new_keys = {
    "ActivityGroups.Title": "Groupes",
    "ActivityGroups.CreateNew": "Créer un nouveau groupe",
    "ActivityGroups.NoGroups": "Aucun groupe trouvé",
    "ActivityGroups.CreateFirst": "Créer le premier groupe",
    "ActivityGroups.BookingsCount": "Inscriptions",
    "ActivityGroups.Children": "enfants",
    "ActivityGroups.NoLimit": "Illimité",
    "ActivityGroups.Edit": "Modifier le groupe",
    "ActivityGroups.Delete": "Supprimer le groupe",
    "ActivityGroups.DeleteWarning": "Êtes-vous sûr de vouloir supprimer ce groupe ?",
    "ActivityGroups.CannotDeleteHasBookings": "Ce groupe ne peut pas être supprimé car il contient des inscriptions.",
    "ActivityGroups.DeleteErrorHasBookings": "Impossible de supprimer ce groupe car il contient des inscriptions.",
    "ActivityGroups.LabelPlaceholder": "Ex: Groupe A, Les Marmottes, etc.",
    "ActivityGroups.LabelHelp": "Nom du groupe pour organiser les enfants",
    "ActivityGroups.CapacityPlaceholder": "Nombre maximum d'enfants",
    "ActivityGroups.CapacityHelp": "Laissez vide pour aucune limite",
    "ActivityGroups.CreateSuccess": "Groupe créé avec succès",
    "ActivityGroups.UpdateSuccess": "Groupe mis à jour avec succès",
    "ActivityGroups.DeleteSuccess": "Groupe supprimé avec succès",
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

print("Adding ActivityGroups localization keys...")
add_keys_to_resx(f"{base_path}SharedResources.resx", new_keys)
add_keys_to_resx(f"{base_path}SharedResources.fr.resx", new_keys)
add_keys_to_resx(f"{base_path}SharedResources.nl.resx", new_keys, "[NL] ")
add_keys_to_resx(f"{base_path}SharedResources.en.resx", new_keys, "[EN] ")

print("\n[OK] Done!")
