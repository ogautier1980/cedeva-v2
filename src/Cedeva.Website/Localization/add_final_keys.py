import re

def add_keys_to_resx(filename, keys_to_add):
    """Add missing keys to a resx file before the </root> tag"""

    with open(filename, 'r', encoding='utf-8') as f:
        content = f.read()

    # Find the </root> position
    root_pos = content.rfind('</root>')
    if root_pos == -1:
        print(f"Error: Could not find </root> in {filename}")
        return

    # Create new data blocks
    new_blocks = []
    for key, value in keys_to_add.items():
        # Check if key already exists
        if f'name="{key}"' in content:
            print(f"Key '{key}' already exists, skipping")
            continue

        block = f'  <data name="{key}" xml:space="preserve">\n    <value>{value}</value>\n  </data>\n'
        new_blocks.append(block)
        print(f"Adding key: {key}")

    if not new_blocks:
        print("No new keys to add")
        return

    # Insert new blocks before </root>
    new_content = content[:root_pos] + ''.join(new_blocks) + content[root_pos:]

    # Write back
    with open(filename, 'w', encoding='utf-8') as f:
        f.write(new_content)

    print(f"Added {len(new_blocks)} keys to {filename}")

# Keys to add to FR file
keys_fr = {
    'Account.CreateAccount': 'Créer mon compte',
    'Edit': 'Modifier',
    'Delete': 'Supprimer',
    'Organisations.ViewActivities': 'Voir les activités',
    'PublicRegistration.FinalizeBooking': "Finaliser l'inscription",
    'Users.CreateUser': "Créer l'utilisateur",
    'AllOrganisations': '-- Toutes les organisations --'
}

# Add to FR file
print("=== Adding keys to SharedResources.fr.resx ===")
add_keys_to_resx('SharedResources.fr.resx', keys_fr)

# Add to NL file with [NL] prefix
keys_nl = {k: f'[NL] {v}' for k, v in keys_fr.items()}
print("\n=== Adding keys to SharedResources.nl.resx ===")
add_keys_to_resx('SharedResources.nl.resx', keys_nl)

# Add to EN file with [EN] prefix
keys_en = {k: f'[EN] {v}' for k, v in keys_fr.items()}
print("\n=== Adding keys to SharedResources.en.resx ===")
add_keys_to_resx('SharedResources.en.resx', keys_en)

print("\nDone!")
