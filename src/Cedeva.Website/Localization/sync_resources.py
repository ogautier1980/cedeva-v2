import re

def extract_keys_and_values(filename):
    """Extract all data blocks from a resx file"""
    with open(filename, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Find all data blocks
    pattern = r'<data name="([^"]+)"[^>]*>\s*<value>([^<]*)</value>\s*</data>'
    matches = re.findall(pattern, content, re.MULTILINE | re.DOTALL)
    
    return {key: value for key, value in matches}

def add_missing_keys(source_file, target_file, prefix):
    """Add missing keys from source to target with a prefix"""
    source_keys = extract_keys_and_values(source_file)
    target_keys = extract_keys_and_values(target_file)
    
    missing_keys = set(source_keys.keys()) - set(target_keys.keys())
    
    if not missing_keys:
        print(f"No missing keys in {target_file}")
        return
    
    # Read target file
    with open(target_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Find the </root> position
    root_pos = content.rfind('</root>')
    if root_pos == -1:
        print(f"Error: Could not find </root> in {target_file}")
        return
    
    # Create new data blocks for missing keys
    new_blocks = []
    for key in sorted(missing_keys):
        value = source_keys[key]
        block = f'  <data name="{key}" xml:space="preserve">\n    <value>{prefix}{value}</value>\n  </data>\n'
        new_blocks.append(block)
    
    # Insert new blocks before </root>
    new_content = content[:root_pos] + ''.join(new_blocks) + content[root_pos:]
    
    # Write back
    with open(target_file, 'w', encoding='utf-8') as f:
        f.write(new_content)
    
    print(f"Added {len(missing_keys)} keys to {target_file}")

# Add missing keys to NL and EN
add_missing_keys('SharedResources.fr.resx', 'SharedResources.nl.resx', '[NL] ')
add_missing_keys('SharedResources.fr.resx', 'SharedResources.en.resx', '[EN] ')

print("Done!")
