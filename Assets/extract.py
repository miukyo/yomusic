import re

def extract_glyphs_to_xaml(css_file_path, output_xaml_path):
    # Regex to capture the icon name and the hex code
    # Matches: .name:before { content: "\XXXX"; }
    pattern = re.compile(r'\.(mgc_[^:]+):before\s*{\s*content:\s*"\\([0-9a-fA-F]+)";')
    
    entries = []
    
    with open(css_file_path, 'r') as file:
        content = file.read()
        matches = pattern.findall(content)
        
        for class_name, hex_code in matches:
            # Convert class name to PascalCase for cleaner XAML keys
            # e.g., mgc_add_circle_fill -> MgcAddCircleFill
            key = "".join(part.capitalize() for part in class_name.split('_'))
            # Format hex for XAML (&#xXXXX;)
            xaml_line = f'    <x:String x:Key="{key}">&#x{hex_code.upper()};</x:String>'
            entries.append(xaml_line)
            
    # Write to file
    with open(output_xaml_path, 'w') as f:
        f.write('<ResourceDictionary\n')
        f.write('    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"\n')
        f.write('    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">\n\n')
        f.write('\n'.join(entries))
        f.write('\n\n</ResourceDictionary>')

    print(f"Successfully generated {len(entries)} icons in {output_xaml_path}")

# Run the script
extract_glyphs_to_xaml('MingCute.css', 'FontIcons.xaml')