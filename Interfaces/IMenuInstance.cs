using CounterStrikeSharp.API.Core;

namespace CS2ScreenMenuAPI.Interfaces
{
    public interface IMenuInstance
    {
        void NextPage(int nextSelectionIndex);
        void PrevPage(int backSelectionIndex);
        void Reset();
        void Close();
        void Display();
        void OnKeyPress(CCSPlayerController player, int key);
    }
}