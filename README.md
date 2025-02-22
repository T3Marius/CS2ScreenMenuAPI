# Config
```jsonc
{
    /* 
        Menu configuration file.
        Adjust your button settings and display texts as needed.
    */
    "Buttons": {
        "ScrollUpButton": "W",
        "ScrollDownButton": "S",
        "SelectButton": "E"
    },
    "DefaultSettings": {
        "MenuType": "Both",
        "TextColor": "Orange",
        "PositionX": -5.5,
        "PositionY": 0,
        "Background": true,
        "BackgroundHeight": 0,
        "BackgroundWidth": 0.2,
        "Font": "Arial Bold",
        "Size": 32,
        "Spacing": true,
        "EnableOptionsCount": true
    },
    "Translations": {
        "NextButton": "Next",
        "BackButton": "Back",
        "ExitButton": "Exit",
        "DisabledOption": "(Disabled)",
        "ScrollInfo": "[W/S] Scroll",
        "SelectInfo": "[E] Select",
        "SelectPrefix": "‣ "
    }
    /* 
        Buttons mapping:
        
        Alt1       - Alt1
        Alt2       - Alt2
        Attack     - Attack
        Attack2    - Attack2
        Attack3    - Attack3
        Bullrush   - Bullrush
        Cancel     - Cancel
        Duck       - Duck
        Grenade1   - Grenade1
        Grenade2   - Grenade2
        Space      - Jump
        Left       - Left
        W          - Forward
        A          - Moveleft
        S          - Back
        D          - Moveright
        E          - Use
        R          - Reload
        F          - (Custom) 0x800000000
        Shift      - Speed
        Right      - Right
        Run        - Run
        Walk       - Walk
        Weapon1    - Weapon1
        Weapon2    - Weapon2
        Zoom       - Zoom
        Tab        - (Custom) 8589934592
    */
}
```

NOTE: The config file creates automaticly when using a menu for the first time ex: !testmenu. It directly updates too so if you change the buttons and info and use !testmenu again it will be changed.

ANOTHER NOTE: When using the API in a plugin you don't need to do anything other than just adding the dll in the project and in the .csproj like this:
```csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="CounterStrikeSharp.API" Version="1.0.305" />
        <Reference Include="CS2ScreenMenuAPI.dll" />
    </ItemGroup>
</Project>
```
# MenuExample
```c#
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CS2ScreenMenuAPI;
using CS2ScreenMenuAPI.Enums;

namespace Example
{
    public class ExampleMenu : BasePlugin
    {
        public override string ModuleAuthor => "T3Marius";
        public override string ModuleName => "TestScrenMenu";
        public override string ModuleVersion => "1.0";

        private int voteCount = 0;

        public override void Load(bool hotReload)
        {

        }

        [ConsoleCommand("css_testmenu")]
        public void OnTestMenu(CCSPlayerController player, CommandInfo info)
        {
            if (player == null)
                return;

            ScreenMenu menu = new ScreenMenu("Test menu", this) // Creating the menu
            {
                PostSelectAction = PostSelectAction.Nothing,
                IsSubMenu = false, // this is not a sub menu
                TextColor = Color.DarkOrange, // if this not set it will be the API default color
                FontName = "Verdana Bold",
            };

            menu.AddOption($"Vote Option ({voteCount})", (p, option) =>
            {
                voteCount++;
                option.Text = $"Vote Option ({voteCount})";
                p.PrintToChat($"Vote registered! Total votes: {voteCount}");

                MenuAPI.GetActiveMenu(p)?.Display(); // with this you can directly change the menu and update vote counts
            });

            menu.AddOption("Enabled Option", (p, option) =>
            {
                p.PrintToChat("This is an enabled option!");
            });
            menu.AddOption("Disabled Option", (p, option) =>
            {
            }, disabled: true);
            menu.AddOption("Another Enabled Option", (p, option) =>
            {
                p.PrintToChat("This is another enabled option!");
            });

            menu.AddOption("SubMenu", (p, option) =>
            {
                ScreenMenu subMenu = new ScreenMenu("SubMenu Title", this) // creating SubMenu
                {
                    IsSubMenu = true, // this is a sub menu
                    PostSelectAction = PostSelectAction.Nothing,
                    TextColor = Color.Blue, // You can use different colors for SubMenus if you want.
                    ParentMenu = menu, // always parent the sub menu to its main menu
                    FontName = "Verdana Bold"
                };
                subMenu.AddOption("SubOption 1", (p, option) =>
                {
                    p.PrintToChat("SubOption 1!");
                });

                MenuAPI.OpenSubMenu(this, p, subMenu); // open the SubMenu
            });

            MenuAPI.OpenMenu(this, player, menu); // open the MainMenu
        }
    }
}
```
# MenuTypes
```C#
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CS2ScreenMenuAPI;
using CS2ScreenMenuAPI.Enums;

namespace Example
{
    public class ExampleMenu : BasePlugin
    {
        public override string ModuleAuthor => "T3Marius";
        public override string ModuleName => "TestScrenMenu";
        public override string ModuleVersion => "1.0";
        public override void Load(bool hotReload)
        {

        }

        [ConsoleCommand("css_testmenu")]
        public void OnTestMenu(CCSPlayerController player, CommandInfo info)
        {
            if (player == null)
                return;

            ScreenMenu menu = new ScreenMenu("Only key press menu", this) // Creating the menu
            {
                PostSelectAction = PostSelectAction.Nothing,
                IsSubMenu = false, // this is not a sub menu
                TextColor = Color.DarkOrange, // if this not set it will be the API default color
                FontName = "Verdana Bold",
                MenuType = MenuType.KeyPress,
            };
            ScreenMenu menu = new ScreenMenu("Only Scroll menu", this) // Creating the menu
            {
                PostSelectAction = PostSelectAction.Nothing,
                IsSubMenu = false, // this is not a sub menu
                TextColor = Color.DarkOrange, // if this not set it will be the API default color
                FontName = "Verdana Bold",
                MenuType = MenuType.Scrollable,
            };
            ScreenMenu menu = new ScreenMenu("Menu with both key press and scrollable", this) // Creating the menu
            {
                PostSelectAction = PostSelectAction.Nothing,
                IsSubMenu = false, // this is not a sub menu
                TextColor = Color.DarkOrange, // if this not set it will be the API default color
                FontName = "Verdana Bold",
                MenuType = MenuType.Both, // IF you wanna use both types you don't need to add this since default value is using Both Types.
            };
    }
}
```

You can use this API in your project by installing it from Manage NuGet Packages or add it with this command
```cmd
dotnet add package CS2ScreenMenuAPI
```
## TODO List

- [X] Allowing spectators to use the menu
- [x] Allowing dead players to use the menu


