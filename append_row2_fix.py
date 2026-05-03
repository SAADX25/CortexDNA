import subprocess
import re

out = subprocess.check_output(['git', 'show', 'HEAD:MainWindow.xaml'])
content = out.decode('utf-8')

# Find row 2
row2_start = content.find('<!-- 2. Main Content Cards (CPU/GPU/Storage) -->')
row2_end = content.find('<!-- Status Bar -->', row2_start)

if row2_start != -1 and row2_end != -1:
    row2_content = content[row2_start:row2_end].strip()
    
    # Remove Grid.Row="2"
    row2_content = re.sub(r'Grid\.Row="2"\s*', '', row2_content)

    with open('e:/Code-Setup/CortexDNA/Controls/HardwareDashboardControl.xaml', 'r', encoding='utf-8') as f:
        hw_content = f.read()

    # Append to StackPanel before closing tags
    insert_pos = hw_content.rfind('</StackPanel>')
    if insert_pos != -1:
        new_hw_content = hw_content[:insert_pos] + '\n        ' + row2_content + '\n    ' + hw_content[insert_pos:]
        
        with open('e:/Code-Setup/CortexDNA/Controls/HardwareDashboardControl.xaml', 'w', encoding='utf-8') as f:
            f.write(new_hw_content)
        print("Successfully appended Row2")
    else:
        print("Could not find </StackPanel>")
else:
    print("Could not find bounds")
