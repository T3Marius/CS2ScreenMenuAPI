using System;
using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2ScreenMenuAPI
{
    public struct EyeAngles
    {
        public Vector3 Position { get; set; }
        public Vector3 Angle { get; set; }
        public Vector3 Forward { get; set; }
        public Vector3 Right { get; set; }
        public Vector3 Up { get; set; }
    }

    public enum ObserverMode
    {
        FirstPerson,
        ThirdPerson,
        Roaming,
    }

    public readonly record struct ObserverInfo(ObserverMode Mode, CCSPlayerPawnBase? Observing);

    internal static class PlayerExtensions
    {
        public static ObserverInfo GetObserverInfo(this CCSPlayerController player)
        {
            if (player.Pawn.Value is not CBasePlayerPawn pawn)
                return new(ObserverMode.Roaming, null);

            if (pawn.ObserverServices is not CPlayer_ObserverServices observerServices)
                return new(ObserverMode.FirstPerson, pawn.As<CCSPlayerPawnBase>());

            var observerMode = (ObserverMode_t)observerServices.ObserverMode;
            var observing = observerServices.ObserverTarget?.Value?.As<CCSPlayerPawnBase>();

            return new()
            {
                Mode = observerMode switch
                {
                    ObserverMode_t.OBS_MODE_IN_EYE => ObserverMode.FirstPerson,
                    ObserverMode_t.OBS_MODE_CHASE => ObserverMode.ThirdPerson,
                    _ => ObserverMode.Roaming,
                },
                Observing = observing,
            };
        }

        private static readonly Vector _Forward = new();
        private static readonly Vector _Right = new();
        private static readonly Vector _Up = new();

        public static EyeAngles? GetEyeAngles(this ObserverInfo observerInfo)
        {
            if (observerInfo.Observing is not CCSPlayerPawnBase pawn) return null;

            var eyeAngles = pawn.EyeAngles;
            NativeAPI.AngleVectors(eyeAngles.Handle, _Forward.Handle, _Right.Handle, _Up.Handle);

            var origin = new Vector3(pawn.AbsOrigin!.X, pawn.AbsOrigin!.Y, pawn.AbsOrigin!.Z);
            var viewOffset = new Vector3(pawn.ViewOffset.X, pawn.ViewOffset.Y, pawn.ViewOffset.Z);

            return new()
            {
                Position = origin + viewOffset,
                Angle = new Vector3(eyeAngles.X, eyeAngles.Y, eyeAngles.Z),
                Forward = new Vector3(_Forward.X, _Forward.Y, _Forward.Z),
                Right = new Vector3(_Right.X, _Right.Y, _Right.Z),
                Up = new Vector3(_Up.X, _Up.Y, _Up.Z),
            };
        }
    }
}