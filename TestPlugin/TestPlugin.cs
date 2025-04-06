using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2ScreenMenuAPI;
namespace TestMenu;
public class TestMenu : BasePlugin
{
    public override string ModuleName => "TestMenu";
    public override string ModuleVersion => "1.0";
    private int VoteCount = 0;

    public Dictionary<string, string> Pistols = new Dictionary<string, string>()
    {
        {"weapon_deagle", "Desert Eagle"},
        {"weapon_elite", "Dual Berettas"},
        {"weapon_fiveseven", "Five-SeveN"},
        {"weapon_glock", "Glock-18"},
        {"weapon_hkp2000", "P2000"},
        {"weapon_p250", "P250"},
        {"weapon_usp_silencer", "USP-S"},
        {"weapon_cz75a", "CZ75-Auto"},
        {"weapon_revolver", "R8 Revolver"},
        {"weapon_tec9", "TEC-9"},
    };

    public Dictionary<string, string> Rifles = new Dictionary<string, string>()
    {
        {"weapon_ak47", "AK-47"},
        {"weapon_aug", "AUG"},
        {"weapon_awp", "AWP"},
        {"weapon_famas", "FAMAS"},
        {"weapon_g3sg1", "G3SG1"},
        {"weapon_galilar", "Galil AR"},
        {"weapon_m4a1", "M4A1"},
        {"weapon_scar20", "SCAR-20"},
        {"weapon_sg556", "SG 553"},
        {"weapon_ssg08", "SSG 08"},
        {"weapon_m4a1_silencer", "M4A1-S"},
    };

    public Dictionary<string, string> SMGs = new Dictionary<string, string>()
    {
        {"weapon_mac10", "MAC-10"},
        {"weapon_p90", "P90"},
        {"weapon_mp5sd", "MP5-SD"},
        {"weapon_ump45", "UMP-45"},
        {"weapon_bizon", "PP-Bizon"},
        {"weapon_mp7", "MP7"},
        {"weapon_mp9", "MP9"},
    };

    public Dictionary<string, string> Heavy = new Dictionary<string, string>()
    {
        {"weapon_m249", "M249"},
        {"weapon_xm1014", "XM1014"},
        {"weapon_mag7", "MAG-7"},
        {"weapon_negev", "Negev"},
        {"weapon_sawedoff", "Sawed-Off"},
        {"weapon_nova", "Nova"},
        {"weapon_taser", "Zeus x27"},
    };

    public override void Load(bool hotReload)
    {
        AddCommand("css_test", "test menu", Command_Test);
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

        mainMenu.AddItem("Select Pistol", (p, option) => {
            CreatePistolMenu(p, mainMenu); // Create the pistol menu with it's parent.
        });

        mainMenu.AddItem("Select Rifle", (p, option) => {
            CreateRifleMenu(p, mainMenu);
        });

        mainMenu.AddItem("Select SMG", (p, option) => {
            CreateSMGMenu(p, mainMenu);
        });

        mainMenu.AddItem("Select Heavy", (p, option) => {
            CreateHeavyMenu(p, mainMenu);
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

    private Menu CreateRifleMenu(CCSPlayerController player, Menu prevMenu)
    {
        Menu rifleMenu = new Menu(player, this)
        {
            Title = "Rifles",
            IsSubMenu = true,
            ShowDisabledOptionNum = true,
            PrevMenu = prevMenu
        };

        foreach (var kvp in Rifles)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            bool shouldBeDisabled = value.Contains("SCAR-20");

            rifleMenu.AddItem(value, (p, option) =>
            {
                player.RemoveWeapons();
                p.PrintToChat($"You got rifle {value}");
                Server.NextFrame(() => p.GiveNamedItem(key));
            }, shouldBeDisabled);
        }

        rifleMenu.Display();
        return rifleMenu;
    }

    private Menu CreateSMGMenu(CCSPlayerController player, Menu prevMenu)
    {
        Menu smgMenu = new Menu(player, this)
        {
            Title = "SMGs",
            IsSubMenu = true,
            ShowDisabledOptionNum = true,
            PrevMenu = prevMenu
        };

        foreach (var kvp in SMGs)
        {
            var key = kvp.Key;
            var value = kvp.Value;

            smgMenu.AddItem(value, (p, option) =>
            {
                player.RemoveWeapons();
                p.PrintToChat($"You got SMG {value}");
                Server.NextFrame(() => p.GiveNamedItem(key));
            });
        }

        smgMenu.Display();
        return smgMenu;
    }

    private Menu CreateHeavyMenu(CCSPlayerController player, Menu prevMenu)
    {
        Menu heavyMenu = new Menu(player, this)
        {
            Title = "Heavy Weapons",
            IsSubMenu = true,
            ShowDisabledOptionNum = true,
            PrevMenu = prevMenu
        };

        foreach (var kvp in Heavy)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            bool shouldBeDisabled = value.Contains("Negev");

            heavyMenu.AddItem(value, (p, option) =>
            {
                player.RemoveWeapons();
                p.PrintToChat($"You got heavy weapon {value}");
                Server.NextFrame(() => p.GiveNamedItem(key));
            }, shouldBeDisabled);
        }

        heavyMenu.Display();
        return heavyMenu;
    }
}