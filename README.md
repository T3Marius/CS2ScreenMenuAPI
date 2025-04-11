# Config
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
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CS2ScreenMenuAPI;
using static CS2ScreenMenuAPI.MenuType;
using static CS2ScreenMenuAPI.PostSelect;

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
        public Menu TestMenu(CCSPlayerController player)
        {
            if (player == null)
                return;

            var menu = new CS2ScreenMenuAPI.Menu(player, this) // Creating the menu
            {
                Title = "TestMenu",
                ShowDisabledOptionNum = false,
                HasExitButon = true
                PostSelect = PostSelect.Nothing
            };

            menu.AddItem($"Vote Option ({voteCount})", (p, option) =>
            {
                voteCount++;
                p.PrintToChat($"Vote registered! Total votes: {voteCount}");
                
                option.Text = $"Vote Option ({voteCount})";
                menu.Refresh();
            });

            menu.AddItem("Enabled Option", (p, option) =>
            {
                p.PrintToChat("This is an enabled option!");
            });
            
            menu.AddItem("Disabled Option", (p, option) => { }, true);
            
            menu.AddItem("Another Enabled Option", (p, option) =>
            {
                p.PrintToChat("This is another enabled option!");
            });

            menu.Display(); // Display the main menu
            return menu
        }
        private void CreateSubMenu(CCSPlayerController player, Menu prevMenu)
        {
            Menu subMenu = new (player, this)
            {
                IsSubMenu = true,
                PrevMenu = TestMenu(player) // when you hit 7. Back it will send you to the TestMenu
            };

            subMenu.AddItem("SubOption 1", (p, o) =>
            {
                p.PrintToChat("SubOption 1!");
            });
               
            subMenu.Display();
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

        [ConsoleCommand("css_menu_types")]
        public void OnMenuTypes(CCSPlayerController player, CommandInfo info)
        {
            if (player == null)
                return;

            // KeyPress menu example
            var keyPressMenu = new CS2ScreenMenuAPI.Menu(player, this)
            {
                Title = "Only key press menu",
                MenuType = MenuType.KeyPress,
                HasExitButon = true
            };
            
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
                MenuType = MenuType.Scrollable,
                HasExitButon = true
            };
            
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
                MenuType = MenuType.Both, // This is the default, so it's optional
                HasExitButon = true
            };
            
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


