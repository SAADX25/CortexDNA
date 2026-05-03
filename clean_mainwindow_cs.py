import re

with open('e:/Code-Setup/CortexDNA/MainWindow.xaml.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Remove CopyCpuInfo_Click and CopyGpuInfo_Click
content = re.sub(r'\s*private async void CopyCpuInfo_Click\(.*?\)\s*\{[\s\S]*?(?=\n\s*private async void CopyGpuInfo_Click)', '', content)
content = re.sub(r'\s*private async void CopyGpuInfo_Click\(.*?\)\s*\{[\s\S]*?(?=\n\s*private void TitleBar_MouseDown)', '', content)

# Remove Button_DiskCleanup_Click and all cleanup methods down to Button_RegEdit_Click
content = re.sub(r'\s*private async void Button_DiskCleanup_Click\(.*?\)\s*\{[\s\S]*?(?=\n\s*private void Button_RegEdit_Click)', '', content)

with open('e:/Code-Setup/CortexDNA/MainWindow.xaml.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Updated MainWindow.xaml.cs successfully.")
