# CS2ScreenMenuAPI

CS2ScreenMenuAPI is a comprehensive API for creating in-game menus and HUD elements for Counter-Strike 2 servers.

## Features

- **Menu System**: Create interactive menus that appear in the game world
- **Resolution Support**: Automatically adjust menu positioning based on player resolution
- **Scrollable or Key Press Navigation**: Use both key presses or scrolling to navigate menus
- **Database Integration**: Store player preferences for menu positioning

## Config
```toml

# Screen Menu Configuration

[Settings]
FontName = "Tahoma Bold"
MenuType = "KeyPress"
Size = 25
PositionX = 0
PositionY = 0
HasExitOption = true
ShowResolutionOption = true
ShowDisabledOptionNum = false
ShowPageCount = true
FreezePlayer = true
ShowControlsInfo = false
ScrollPrefix = "\u2023"

[Settings.Resolutions."1920x1080"]
PositionX = -9.0
PositionY = 0.0

[Settings.Resolutions."1440x1080"]
PositionX = -7.0
PositionY = 0.0

[Controls]
ScrollUp = "W"
ScrollDown = "S"
Select = "E"
Exit = "Tab"

[Sounds]
Select = "menu.Select"
Next = "menu.Select"
Prev = "menu.Close"
Close = "menu.Close"
ScrollUp = "menu.ScrollUp"
ScrollDown = "menu.ScrollDown"
Volume = 1.0

[Lang.en]
Prev = "Back"
Next = "Next"
Close = "Close"
ScrollKeys ="[{0}/{1}] Scroll"
SelectKey = "[{0}] Select"
ExitKey = "[{0}] Exit"
SelectRes = "Select Your Game Resolution"
ChangeRes = "Change Resolution"

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
    public void Command_NoPlayerArg()
    {
        // you can also create the menu without needing to call the player arg.

        Menu menu = new Menu(this)
        {
            Title = "No Player Arg Menu"
        };

        foreach (var p in Utilities.GetPlayers())
        {
            menu.Display(p); // but you must call player at display, no matter what.
        }
    }
    public void Command_Test(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        var mainMenu = new Menu(player, this) // this the main menu
        {
            Title = "Weapons Menu",
            ShowDisabledOptionNum = true,
        };

        mainMenu.AddItem("Select Pistol", (p, option) =>
        {
            CreatePistolMenu(p, mainMenu); // Create the pistol menu with it's parent.
        });

        mainMenu.AddItem("Refresh Test", (p, o) =>
        {
            CreateVoteMenu(p, mainMenu);
        });

        mainMenu.Display();
    }
    private Menu CreateVoteMenu(CCSPlayerController player, Menu prevMenu)
    {
        Menu voteMenu = new Menu(player, this)
        {
            Title = $"Vote Test | {VoteCount}",
            IsSubMenu = true,
            PrevMenu = prevMenu
        };
        voteMenu.AddItem("Vote", (p, option) =>
        {
            VoteCount++;
            voteMenu.Title = $"Vote Test | {VoteCount}";
            voteMenu.Refresh(); // this will refresh the menu and update the title.
        });
        voteMenu.Display();
        return voteMenu;
    }
    private Menu CreatePistolMenu(CCSPlayerController player, Menu prevMenu)
    {
        Menu pistolMenu = new Menu(player, this)
        {
            Title = "Pistols",
            IsSubMenu = true,
            ShowDisabledOptionNum = true,
            PrevMenu = prevMenu // if prev menu is set when using 7. Back will send you to it.
        };

        foreach (var kvp in Pistols)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            bool shouldBeDisabled = value.Contains("TEC");

            pistolMenu.AddItem(value, (p, option) =>
            {
                player.RemoveWeapons();
                p.PrintToChat($"You got pistol {value}");
                Server.NextFrame(() => p.GiveNamedItem(key));
            }, shouldBeDisabled);
        }

        pistolMenu.Display();
        return pistolMenu;
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

        [ConsoleCommand("css_menu_types")]
        public void OnMenuTypes(CCSPlayerController player, CommandInfo info)
        {
            if (player == null)
                return;

            // KeyPress menu example
            var keyPressMenu = new CS2ScreenMenuAPI.Menu(player, this)
            {
                Title = "Only key press menu",
                HasExitButon = true
            };
            keyPressMenu.SetMenuType(MenuType.KeyPress),
            keyPressMenu.AddItem("Key Press Option", (p, option) => 
            {
                p.PrintToChat("Selected from key press menu!");
            });
            
            // Uncomment to display this menu
            // keyPressMenu.Display();

            // Scroll menu example
            var scrollMenu = new CS2ScreenMenuAPI.Menu(player, this)
            {
                Title = "Only Scroll menu",
                HasExitButon = true
            };
            scrollMenu.SetMenuType(MenuType.Scrollable),
            
            scrollMenu.AddItem("Scroll Option", (p, option) => 
            {
                p.PrintToChat("Selected from scroll menu!");
            });
            
            // Uncomment to display this menu
            // scrollMenu.Display();

            // Both types menu example (default)
            var bothTypesMenu = new CS2ScreenMenuAPI.Menu(player, this)
            {
                Title = "Menu with both key press and scrollable",
                HasExitButon = true
            };

            bothTypesMenu.SetMenuType(MenuType.Both),
            
            bothTypesMenu.AddItem("Option works with both", (p, option) => 
            {
                p.PrintToChat("Selected from menu supporting both input types!");
            });
            
            bothTypesMenu.Display();
        }
}
```

You can use this API in your project by installing it from Manage NuGet Packages or add it with this command
```cmd
dotnet add package CS2ScreenMenuAPI
```


