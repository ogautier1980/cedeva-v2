#!/bin/bash

# Get list of missing keys in NL
missing_keys=$(comm -23 <(grep -o 'name="[^"]*"' SharedResources.fr.resx | sed 's/name="//;s/"//' | sort) <(grep -o 'name="[^"]*"' SharedResources.nl.resx | sed 's/name="//;s/"//' | sort))

# Backup files
cp SharedResources.nl.resx SharedResources.nl.resx.bak
cp SharedResources.en.resx SharedResources.en.resx.bak

# For each missing key, extract from FR and add to NL and EN
for key in $missing_keys; do
  # Extract the data block from FR (3 lines: opening tag, value, closing tag)
  block=$(grep -A 2 "name=\"$key\"" SharedResources.fr.resx | head -3)
  
  if [ -n "$block" ]; then
    # Get the French value
    fr_value=$(echo "$block" | grep -oP '(?<=<value>)[^<]+' | head -1)
    
    if [ -n "$fr_value" ]; then
      # Create NL block (TODO: translate to Dutch)
      nl_block="  <data name=\"$key\" xml:space=\"preserve\">\n    <value>[NL] $fr_value</value>\n  </data>"
      
      # Create EN block (TODO: translate to English)  
      en_block="  <data name=\"$key\" xml:space=\"preserve\">\n    <value>[EN] $fr_value</value>\n  </data>"
      
      # Add to NL file before </root>
      sed -i "/<\/root>/i\$nl_block" SharedResources.nl.resx
      
      # Add to EN file before </root>
      sed -i "/<\/root>/i\$en_block" SharedResources.en.resx
    fi
  fi
done

echo "Added $( echo "$missing_keys" | wc -l ) keys to NL and EN files"
