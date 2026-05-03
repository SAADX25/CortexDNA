import re

with open('e:/Code-Setup/CortexDNA/MainWindow.xaml', 'r', encoding='utf-8') as f:
    content = f.read()

# Extract the System Overview Cards (Row 0)
row0_start = content.find('<!-- 3. System Overview Cards -->')
row0_end = content.find('<!-- Windows Privacy Cards -->')

if row0_start == -1 or row0_end == -1:
    print("Could not find row 0 bounds")

row0_content = content[row0_start:row0_end].strip()

# Extract the CPU/GPU/Storage Cards (Row 2)
row2_start = content.find('<!-- 2. Main Content Cards (CPU/GPU/Storage) -->')
row2_end = content.find('<!-- Status Bar -->')

if row2_start == -1 or row2_end == -1:
    print("Could not find row 2 bounds")

row2_content = content[row2_start:row2_end].strip()

# Replace DiskCleanup Click with Command
row0_content = row0_content.replace('Click="Button_DiskCleanup_Click"', 'Command="{Binding CleanDiskCommand}"')
row0_content = row0_content.replace('<TextBlock x:Name="TxtDiskCleanup" Foreground="White" FontSize="12" Text="CLEAN DISK" VerticalAlignment="Center"/>', '<TextBlock Foreground="White" FontSize="12" Text="{Binding CleanDiskButtonText}" VerticalAlignment="Center"/>')

# Remove Grid.Row attributes since we will put them in a StackPanel
row0_content = re.sub(r'Grid\.Row="0"\s*', '', row0_content)
row2_content = re.sub(r'Grid\.Row="2"\s*', '', row2_content)

xaml = f"""<UserControl x:Class="CortexDNA.Controls.HardwareDashboardControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" 
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008" 
             xmlns:local="clr-namespace:CortexDNA.Controls"
             mc:Ignorable="d" 
             d:DesignHeight="800" d:DesignWidth="1000">
    <StackPanel Margin="0">
        {row0_content}
        
        {row2_content}
    </StackPanel>
</UserControl>
"""

with open('e:/Code-Setup/CortexDNA/Controls/HardwareDashboardControl.xaml', 'w', encoding='utf-8') as f:
    f.write(xaml)

print("Generated HardwareDashboardControl.xaml successfully.")
