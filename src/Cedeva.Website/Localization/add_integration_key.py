#!/usr/bin/env python3
import xml.etree.ElementTree as ET

# Key to add
new_keys = {
    "Activities.IntegrationCode": "Code d'intégration iframe",
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

print("Adding integration code localization key...")
add_keys_to_resx(f"{base_path}SharedResources.resx", new_keys)
add_keys_to_resx(f"{base_path}SharedResources.fr.resx", new_keys)
add_keys_to_resx(f"{base_path}SharedResources.nl.resx", new_keys, "[NL] ")
add_keys_to_resx(f"{base_path}SharedResources.en.resx", new_keys, "[EN] ")

print("\n[OK] Done!")
