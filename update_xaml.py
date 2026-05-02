import re

with open('e:/Code-Setup/CortexDNA/MainWindow.xaml', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Update CardStyle CornerRadius to 12
content = re.sub(r'<Setter Property="CornerRadius" Value="8"/>', r'<Setter Property="CornerRadius" Value="12"/>', content)

# 2. Add SidebarButtonStyle and ToggleSwitchStyle replacing UtilityButtonStyle
utility_style_pattern = re.compile(r'<!-- Utility Ghost Button Style -->.*?</Style>', re.DOTALL)
new_styles = '''<!-- Sidebar Button Style -->
        <Style x:Key="SidebarButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Margin" Value="0,2,0,2"/>
            <Setter Property="Padding" Value="12,10"/>
            <Setter Property="HorizontalContentAlignment" Value="Left"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="6">
                            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center" Margin="{TemplateBinding Padding}"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="{DynamicResource HoverBackgroundBrush}"/>
                                <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="border" Property="Background" Value="{DynamicResource PressedBackgroundBrush}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Toggle Switch Style (Windows 11 inspired) -->
        <Style x:Key="ModernToggleSwitch" TargetType="ToggleButton">
            <Setter Property="Width" Value="40"/>
            <Setter Property="Height" Value="20"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ToggleButton">
                        <Border x:Name="Border" CornerRadius="10" Background="{DynamicResource CardBorderBrush}" BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="1">
                            <Ellipse x:Name="Thumb" Fill="{DynamicResource SecondaryTextBrush}" Width="12" Height="12" Margin="3" HorizontalAlignment="Left">
                                <Ellipse.RenderTransform>
                                    <TranslateTransform x:Name="ThumbTransform"/>
                                </Ellipse.RenderTransform>
                            </Ellipse>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="Border" Property="Background" Value="{DynamicResource AccentBrush}"/>
                                <Setter TargetName="Border" Property="BorderBrush" Value="{DynamicResource AccentBrush}"/>
                                <Setter TargetName="Thumb" Property="Fill" Value="White"/>
                                <Trigger.EnterActions>
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetName="ThumbTransform" Storyboard.TargetProperty="X" To="20" Duration="0:0:0.15">
                                                <DoubleAnimation.EasingFunction>
                                                    <QuadraticEase EasingMode="EaseOut"/>
                                                </DoubleAnimation.EasingFunction>
                                            </DoubleAnimation>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </Trigger.EnterActions>
                                <Trigger.ExitActions>
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetName="ThumbTransform" Storyboard.TargetProperty="X" To="0" Duration="0:0:0.15">
                                                <DoubleAnimation.EasingFunction>
                                                    <QuadraticEase EasingMode="EaseOut"/>
                                                </DoubleAnimation.EasingFunction>
                                            </DoubleAnimation>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </Trigger.ExitActions>
                            </Trigger>
                            <Trigger Property="IsChecked" Value="False">
                                <Setter TargetName="ThumbTransform" Property="X" Value="0"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>'''
content = utility_style_pattern.sub(new_styles, content)

# 3. Replace ThemeToggle and OpacitySlider with minimalist Theme Button
theme_controls_pattern = re.compile(r'<!-- Theme & Transparency Controls -->.*?</StackPanel>', re.DOTALL)
new_theme_controls = '''<!-- Theme Control -->
                <Button Style="{StaticResource WindowControlButtonStyle}" Click="ThemeButton_Click" ToolTip="Toggle Theme" Width="40" Margin="0,0,10,0" WindowChrome.IsHitTestVisibleInChrome="True">
                    <TextBlock Text="&#xE706;" FontFamily="Segoe MDL2 Assets" FontSize="14" Foreground="{DynamicResource PrimaryTextBrush}"/>
                </Button>'''
content = theme_controls_pattern.sub(new_theme_controls, content, count=1)

# 4. Restructure Layout for Sidebar and move Quick Utilities
content = content.replace('<!-- Main Content Area with Global ScrollViewer -->', '<!-- Main Content Area with Sidebar -->')
content = content.replace('<ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">', '''<Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="220" x:Name="SidebarColumn"/> <!-- Sidebar -->
                <ColumnDefinition Width="*"/> <!-- Main Content -->
            </Grid.ColumnDefinitions>

            <!-- Sidebar -->
            <Border Grid.Column="0" Background="{DynamicResource CardBackgroundBrush}" BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="0,0,1,0">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    
                    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="15,20,15,10">
                        <TextBlock Text="&#xE700;" FontFamily="Segoe MDL2 Assets" FontSize="16" Foreground="{DynamicResource AccentBrush}" VerticalAlignment="Center" Margin="0,0,10,0"/>
                        <TextBlock Text="Utilities" FontSize="14" FontWeight="SemiBold" Foreground="{DynamicResource PrimaryTextBrush}" VerticalAlignment="Center"/>
                    </StackPanel>

                    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" Margin="0,10,0,0">
                        <StackPanel x:Name="UtilitiesWrapPanel" Margin="10,0">
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_MsInfo_Click" ToolTip="System Info">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE946;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="System Info" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_TaskManager_Click" ToolTip="Task Manager">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE90C;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Task Manager" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_DeviceManager_Click" ToolTip="Device Manager">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE7F4;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Device Manager" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_RegEdit_Click" ToolTip="Registry Editor">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE8A5;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Registry Editor" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_Services_Click" ToolTip="Windows Services">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE713;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Windows Services" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_Network_Click" ToolTip="Network Connections">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE704;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Network" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_Cmd_Click" ToolTip="CMD (Admin)">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE120;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Command Prompt" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_PowerShell_Click" ToolTip="PowerShell (Admin)">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE756;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="PowerShell" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_EventViewer_Click" ToolTip="Event Viewer">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE7BA;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Event Viewer" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_ControlPanel_Click" ToolTip="Control Panel">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE72D;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Control Panel" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Button Style="{StaticResource SidebarButtonStyle}" Click="Button_ResMon_Click" ToolTip="Resource Monitor">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE9D2;" FontFamily="Segoe MDL2 Assets" Width="30" FontSize="16"/>
                                    <TextBlock Text="Resource Monitor" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                        </StackPanel>
                    </ScrollViewer>
                </Grid>
            </Border>

            <!-- Main Content Area -->
            <ScrollViewer Grid.Column="1" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">''')

# Remove old Quick Utilities section
utilities_pattern = re.compile(r'<!-- 2\. Quick Utilities -->.*?<!-- 3\. System Overview Cards -->', re.DOTALL)
content = utilities_pattern.sub('<!-- 1. System Overview Cards -->\n        <!-- 3. System Overview Cards -->', content)

# Update row indexing: System Overview is now Row 0
content = content.replace('<Grid Grid.Row="1" Margin="0,0,0,15">', '<Grid Grid.Row="0" Margin="0,0,0,15">')

# Insert Windows Privacy Cards after System Overview
privacy_xaml = '''
            <!-- Windows Privacy Cards -->
            <Border Grid.Row="1" Style="{StaticResource CardStyle}" Margin="0,0,0,15">
                <StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,0,0,15">
                        <TextBlock Text="&#xE1F6;" FontFamily="Segoe MDL2 Assets" FontSize="20" Foreground="{DynamicResource AccentBrush}" VerticalAlignment="Center" Margin="0,0,15,0"/>
                        <TextBlock Text="Windows Privacy Settings" Style="{StaticResource TitleText}" VerticalAlignment="Center"/>
                    </StackPanel>
                    
                    <Grid Margin="0,5">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>

                        <!-- Toggle 1 -->
                        <Grid Grid.Row="0" Margin="0,10">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel>
                                <TextBlock Text="Diagnostic Data" Foreground="{DynamicResource PrimaryTextBrush}" FontSize="14" FontWeight="SemiBold"/>
                                <TextBlock Text="Allow Microsoft to use Windows diagnostic data" Foreground="{DynamicResource SecondaryTextBrush}" FontSize="12" Margin="0,4,0,0" TextWrapping="Wrap"/>
                            </StackPanel>
                            <ToggleButton Grid.Column="1" Style="{StaticResource ModernToggleSwitch}" VerticalAlignment="Center" Margin="15,0,0,0" IsChecked="{Binding PrivacyDiagnosticDataEnabled}"/>
                        </Grid>

                        <!-- Toggle 2 -->
                        <Grid Grid.Row="1" Margin="0,10">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel>
                                <TextBlock Text="Settings Suggestions" Foreground="{DynamicResource PrimaryTextBrush}" FontSize="14" FontWeight="SemiBold"/>
                                <TextBlock Text="Allow Windows to show you suggestions in the Settings app" Foreground="{DynamicResource SecondaryTextBrush}" FontSize="12" Margin="0,4,0,0" TextWrapping="Wrap"/>
                            </StackPanel>
                            <ToggleButton Grid.Column="1" Style="{StaticResource ModernToggleSwitch}" VerticalAlignment="Center" Margin="15,0,0,0" IsChecked="{Binding PrivacySettingsSuggestionsEnabled}"/>
                        </Grid>

                        <!-- Toggle 3 -->
                        <Grid Grid.Row="2" Margin="0,10">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel>
                                <TextBlock Text="Start Menu Web Search" Foreground="{DynamicResource PrimaryTextBrush}" FontSize="14" FontWeight="SemiBold"/>
                                <TextBlock Text="Allow web search apps to show results in Start Menu or Taskbar Search" Foreground="{DynamicResource SecondaryTextBrush}" FontSize="12" Margin="0,4,0,0" TextWrapping="Wrap"/>
                            </StackPanel>
                            <ToggleButton Grid.Column="1" Style="{StaticResource ModernToggleSwitch}" VerticalAlignment="Center" Margin="15,0,0,0" IsChecked="{Binding PrivacyWebSearchEnabled}"/>
                        </Grid>
                    </Grid>
                </StackPanel>
            </Border>

            <!-- 2. Main Content Cards (CPU/GPU/Storage) -->'''

content = content.replace('<!-- 4. Main Content Cards (CPU/GPU/Storage) -->', privacy_xaml)

# The RowDefinitions in <Grid Margin="15"> need to be updated.
old_row_defs = '''<Grid Margin="15">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/> <!-- Quick Utilities -->
                    <RowDefinition Height="Auto"/> <!-- System Overview -->
                    <RowDefinition Height="Auto"/> <!-- Main Content (Auto height for expansion) -->
                    <RowDefinition Height="Auto"/> <!-- Status Bar -->
                </Grid.RowDefinitions>'''

new_row_defs = '''<Grid Margin="15">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/> <!-- System Overview -->
                    <RowDefinition Height="Auto"/> <!-- Windows Privacy -->
                    <RowDefinition Height="Auto"/> <!-- Main Content (CPU/GPU/Storage) -->
                    <RowDefinition Height="Auto"/> <!-- Status Bar -->
                </Grid.RowDefinitions>'''
content = content.replace(old_row_defs, new_row_defs)

# Add closing tags for Grid Grid.Row=1
content = content.replace('</Grid>\n    </ScrollViewer>\n</Grid>\n</Window>', '</Grid>\n    </ScrollViewer>\n</Grid>\n</Grid>\n</Window>')

with open('e:/Code-Setup/CortexDNA/MainWindow.xaml', 'w', encoding='utf-8') as f:
    f.write(content)
print("XAML updated successfully!")
