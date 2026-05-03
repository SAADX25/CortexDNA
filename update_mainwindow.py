import re

with open('e:/Code-Setup/CortexDNA/MainWindow.xaml', 'r', encoding='utf-8') as f:
    content = f.read()

# Replace row 0 and row 2 with HardwareDashboardControl
row0_start = content.find('<!-- 3. System Overview Cards -->')
row0_end = content.find('<!-- Windows Privacy Cards -->')

if row0_start != -1 and row0_end != -1:
    content = content[:row0_start] + '\n<!-- Hardware Dashboard -->\n<controls:HardwareDashboardControl DataContext="{Binding HardwareVM}" Grid.Row="0" Grid.RowSpan="3" />\n\n' + content[row0_end:]

row2_start = content.find('<!-- 2. Main Content Cards (CPU/GPU/Storage) -->')
row2_end = content.find('<!-- Status Bar -->')

if row2_start != -1 and row2_end != -1:
    content = content[:row2_start] + content[row2_end:]

# Update StatusBar binding
content = content.replace('Text="{Binding StatusMessage}"', 'Text="{Binding HardwareVM.StatusMessage}"')

with open('e:/Code-Setup/CortexDNA/MainWindow.xaml', 'w', encoding='utf-8') as f:
    f.write(content)

print("Updated MainWindow.xaml successfully.")
