using System.Drawing;
using System.Numerics;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CS2ScreenMenuAPI
{
    internal class MenuRenderer
    {
        private readonly Menu _menu;
        private readonly CCSPlayerController _player;

        private CPointWorldText? _highlightText;
        private CPointWorldText? _foregroundText;
        private CPointWorldText? _backgroundText;
        private CPointWorldText? _background;
        private nint? _createdForPawn = null;
        public bool ForceRefresh = true;
        private bool _presentingHtml = false;

        private string? _htmlContent = null;
        private nint _menuCurrentObserver = nint.Zero;
        private ObserverMode _menuCurrentObserverMode;
        private static readonly Color HighlightTextColor = Color.FromArgb(127, 255, 255, 64);
        private static readonly Color ForegroundTextColor = Color.FromArgb(230, 153, 39);
        private static readonly Color BackgroundTextColor = Color.FromArgb(234, 209, 175);
        private Vector2 _menuPosition = new(-6.55f, 1.2f);
        public Vector2 MenuPosition { get => _menuPosition; set { _menuPosition = value; ForceRefresh = true; } }

        private readonly StringBuilder _highlightTextSb = new();
        private readonly StringBuilder _foregroundTextSb = new();
        private readonly StringBuilder _backgroundTextSb = new();
        private readonly StringBuilder _backgroundSb = new();
        private readonly StringBuilder _htmlTextSb = new();
        private readonly Dictionary<ulong, List<CPointWorldText>> _playerRoamingWorldTexts = new();
        private readonly record struct VectorData(Vector Position, QAngle Angle);

        public MenuRenderer(Menu menu, CCSPlayerController player)
        {
            _menu = menu;
            _player = player;
        }

        public void Tick()
        {
            if (_presentingHtml && _htmlContent is not null)
            {
                _player.PrintToCenterHtml(_htmlContent);
            }

            var observerInfo = _player.GetObserverInfo();
            bool needsRefresh = ForceRefresh || observerInfo.Mode != _menuCurrentObserverMode || observerInfo.Observing?.Handle != _menuCurrentObserver;

            if (observerInfo.Mode == ObserverMode.Roaming && !needsRefresh)
            {
                UpdateRoamingWorldTexts();
            }

            if (needsRefresh)
            {
                ForceRefresh = false;
                Draw();
            }
        }

        public void Draw()
        {
            if (CreateInitialInvisibleWorldTextEntity())
            {
                ForceRefresh = true;
                return;
            }

            if (DrawWorldText())
            {
                _presentingHtml = false;
            }
            else
            {
                if (!_presentingHtml) DestroyEntities();
                DrawHtml();
                _presentingHtml = true;
            }
        }

        private bool DrawWorldText()
        {
            var observerInfo = _player.GetObserverInfo();

            if (observerInfo.Mode == ObserverMode.FirstPerson)
            {
                return DrawWorldTextFirstPerson(observerInfo);
            }
            else if (observerInfo.Mode == ObserverMode.Roaming)
            {
                return DrawWorldTextRoaming(observerInfo);
            }

            return false;
        }

        private bool DrawWorldTextFirstPerson(ObserverInfo observerInfo)
        {
            var maybeEyeAngles = observerInfo.GetEyeAngles();
            if (!maybeEyeAngles.HasValue) return false;
            var eyeAngles = maybeEyeAngles.Value;

            var predictedViewmodel = DisplayManager.EnsureCustomView(_player);
            if (predictedViewmodel is null) return false;

            _highlightTextSb.Clear(); _foregroundTextSb.Clear(); _backgroundTextSb.Clear(); _backgroundSb.Clear();

            BuildMenuStrings((text, style, selectIndex) =>
            {
                var line = $"{selectIndex}. {text}";
                _highlightTextSb.AppendLine(style.Highlight ? line : string.Empty);
                _foregroundTextSb.AppendLine(style.Foreground ? line : string.Empty);
                _backgroundTextSb.AppendLine(!style.Foreground ? line : string.Empty);
                _backgroundSb.AppendLine(line);
            },
            (text, style) =>
            {
                _highlightTextSb.AppendLine(style.Highlight ? text : string.Empty);
                _foregroundTextSb.AppendLine(style.Foreground ? text : string.Empty);
                _backgroundTextSb.AppendLine(!style.Foreground ? text : string.Empty);
                _backgroundSb.AppendLine(text);
            });

            var finalPosition =
                new Vector(eyeAngles.Position.X, eyeAngles.Position.Y, eyeAngles.Position.Z) +
                new Vector(eyeAngles.Forward.X, eyeAngles.Forward.Y, eyeAngles.Forward.Z) * 7.0f +
                new Vector(eyeAngles.Right.X, eyeAngles.Right.Y, eyeAngles.Right.Z) * _menuPosition.X +
                new Vector(eyeAngles.Up.X, eyeAngles.Up.Y, eyeAngles.Up.Z) * _menuPosition.Y;

            var finalAngle = new QAngle
            {
                Y = eyeAngles.Angle.Y + 270.0f,
                Z = 90.0f - eyeAngles.Angle.X,
                X = 0.0f
            };

            _menuCurrentObserver = observerInfo.Observing?.Handle ?? nint.Zero;
            _menuCurrentObserverMode = observerInfo.Mode;

            bool allValid = _highlightText?.IsValid == true && _foregroundText?.IsValid == true && _backgroundText?.IsValid == true && _background?.IsValid == true;
            if (!allValid)
            {
                DestroyEntities();
                CreateEntities();
            }

            UpdateEntity(_highlightText!, predictedViewmodel, _highlightTextSb.ToString(), finalPosition, finalAngle);
            UpdateEntity(_foregroundText!, predictedViewmodel, _foregroundTextSb.ToString(), finalPosition, finalAngle);
            UpdateEntity(_backgroundText!, predictedViewmodel, _backgroundTextSb.ToString(), finalPosition, finalAngle);
            UpdateEntity(_background!, predictedViewmodel, _backgroundSb.ToString(), finalPosition, finalAngle);

            return true;
        }

        private bool DrawWorldTextRoaming(ObserverInfo observerInfo)
        {
            var vectorData = FindVectorDataForFreeCamera(_menuPosition.X, _menuPosition.Y);
            if (vectorData == null) return false;

            // Try to get the player's pawn for parenting in roaming mode
            var playerPawn = _player.Pawn.Value;

            _highlightTextSb.Clear(); _foregroundTextSb.Clear(); _backgroundTextSb.Clear(); _backgroundSb.Clear();

            BuildMenuStrings((text, style, selectIndex) =>
            {
                var line = $"{selectIndex}. {text}";
                _highlightTextSb.AppendLine(style.Highlight ? line : string.Empty);
                _foregroundTextSb.AppendLine(style.Foreground ? line : string.Empty);
                _backgroundTextSb.AppendLine(!style.Foreground ? line : string.Empty);
                _backgroundSb.AppendLine(line);
            },
            (text, style) =>
            {
                _highlightTextSb.AppendLine(style.Highlight ? text : string.Empty);
                _foregroundTextSb.AppendLine(style.Foreground ? text : string.Empty);
                _backgroundTextSb.AppendLine(!style.Foreground ? text : string.Empty);
                _backgroundSb.AppendLine(text);
            });

            _menuCurrentObserver = observerInfo.Observing?.Handle ?? nint.Zero;
            _menuCurrentObserverMode = observerInfo.Mode;

            bool allValid = _highlightText?.IsValid == true && _foregroundText?.IsValid == true && _backgroundText?.IsValid == true && _background?.IsValid == true;
            if (!allValid)
            {
                DestroyEntities();
                CreateEntities();
            }
            if (playerPawn != null && playerPawn.IsValid)
            {
                UpdateEntityWithParentPawn(_highlightText!, playerPawn, _highlightTextSb.ToString(), vectorData.Value.Position, vectorData.Value.Angle);
                UpdateEntityWithParentPawn(_foregroundText!, playerPawn, _foregroundTextSb.ToString(), vectorData.Value.Position, vectorData.Value.Angle);
                UpdateEntityWithParentPawn(_backgroundText!, playerPawn, _backgroundTextSb.ToString(), vectorData.Value.Position, vectorData.Value.Angle);
                UpdateEntityWithParentPawn(_background!, playerPawn, _backgroundSb.ToString(), vectorData.Value.Position, vectorData.Value.Angle);
            }
            else
            {
                UpdateEntityWithoutParent(_highlightText!, _highlightTextSb.ToString(), vectorData.Value.Position, vectorData.Value.Angle);
                UpdateEntityWithoutParent(_foregroundText!, _foregroundTextSb.ToString(), vectorData.Value.Position, vectorData.Value.Angle);
                UpdateEntityWithoutParent(_backgroundText!, _backgroundTextSb.ToString(), vectorData.Value.Position, vectorData.Value.Angle);
                UpdateEntityWithoutParent(_background!, _backgroundSb.ToString(), vectorData.Value.Position, vectorData.Value.Angle);

                RegisterRoamingWorldTexts();
            }

            return true;
        }

        private VectorData? FindVectorDataForFreeCamera(float positionX, float positionY)
        {
            if (_player.Pawn.Value == null || _player.Pawn.Value.ObserverServices == null)
                return null;

            CCSPlayerPawn? pawn = _player.Pawn.Value.As<CCSPlayerPawn>();
            if (pawn == null) return null;

            Vector observerPos = pawn.AbsOrigin ?? new Vector();
            observerPos += new Vector(0, 0, pawn.ViewOffset.Z);

            QAngle eyeAngles = pawn.EyeAngles;

            Vector forward = new(), right = new(), up = new();
            NativeAPI.AngleVectors(eyeAngles.Handle, forward.Handle, right.Handle, up.Handle);

            float fov = _player.DesiredFOV == 0 ? 90 : _player.DesiredFOV;
            if (fov != 90)
            {
                float scaleFactor = (float)Math.Tan((fov / 2) * Math.PI / 180) / (float)Math.Tan(45 * Math.PI / 180);
                positionX *= scaleFactor;
                positionY *= scaleFactor;
            }

            Vector offset = forward * 7 + right * positionX + up * positionY;

            QAngle angle = new()
            {
                Y = eyeAngles.Y + 270,
                Z = 90 - eyeAngles.X,
                X = 0
            };

            return new VectorData()
            {
                Position = observerPos + offset,
                Angle = angle,
            };
        }

        private void UpdateEntityWithoutParent(CPointWorldText ent, string newText, Vector position, QAngle angles, bool updateText = true)
        {
            if (updateText) ent.MessageText = newText;
            ent.Teleport(position, angles, null);
            if (updateText) Utilities.SetStateChanged(ent, "CPointWorldText", "m_messageText");
        }

        private void UpdateEntityWithParentPawn(CPointWorldText ent, CBasePlayerPawn playerPawn, string newText, Vector position, QAngle angles, bool updateText = true)
        {
            if (updateText) ent.MessageText = newText;
            ent.Teleport(position, angles, null);

            // Try to parent to the player's pawn entity
            try
            {
                ent.AcceptInput("SetParent", playerPawn, null, "!activator");
            }
            catch
            {
                // If parenting fails, continue without parent
            }

            if (updateText) Utilities.SetStateChanged(ent, "CPointWorldText", "m_messageText");
        }

        private void RegisterRoamingWorldTexts()
        {
            ulong steamId = _player.SteamID;
            if (!_playerRoamingWorldTexts.ContainsKey(steamId))
            {
                _playerRoamingWorldTexts[steamId] = new List<CPointWorldText>();
            }

            var entities = _playerRoamingWorldTexts[steamId];
            entities.Clear();

            if (_highlightText?.IsValid == true) entities.Add(_highlightText);
            if (_foregroundText?.IsValid == true) entities.Add(_foregroundText);
            if (_backgroundText?.IsValid == true) entities.Add(_backgroundText);
            if (_background?.IsValid == true) entities.Add(_background);
        }

        private void UpdateRoamingWorldTexts()
        {
            ulong steamId = _player.SteamID;
            if (!_playerRoamingWorldTexts.TryGetValue(steamId, out var entities) || entities.Count == 0)
                return;

            var vectorData = FindVectorDataForFreeCamera(_menuPosition.X, _menuPosition.Y);
            if (vectorData == null) return;

            foreach (var entity in entities.ToList())
            {
                if (!entity.IsValid)
                {
                    entities.Remove(entity);
                    continue;
                }

                entity.Teleport(vectorData.Value.Position, vectorData.Value.Angle, null);
            }
        }

        private void DrawHtml()
        {
            _htmlTextSb.Clear();
            _htmlTextSb.Append("<font class='fontSize-s'>");

            BuildMenuStrings((text, style, selectIndex) =>
            {
                var color = style switch
                {
                    { Foreground: true, Highlight: true } => "#EFCE21",
                    { Foreground: true, Highlight: false } => "#E28B12",
                    { Foreground: false, Highlight: true } => "#EFE472",
                    { Foreground: false, Highlight: false } => "#E7CCA5",
                };
                _htmlTextSb.Append($"<font color='{color}'>{selectIndex}. {text}</font><br>");
            }, (text, style) =>
            {
                var color = style switch
                {
                    { Foreground: true, Highlight: true } => "#EFCE21",
                    { Foreground: true, Highlight: false } => "#E28B12",
                    { Foreground: false, Highlight: true } => "#EFE472",
                    { Foreground: false, Highlight: false } => "#E7CCA5",
                };
                _htmlTextSb.Append($"<font color='{color}'>{text}</font><br>");
            });
            _htmlTextSb.Append("</font>");
            _htmlContent = _htmlTextSb.ToString();
        }

        private struct TextStyling { public bool Foreground; public bool Highlight; }

        private void BuildMenuStrings(Action<string, TextStyling, int> writeLine, Action<string, TextStyling> writeSimpleLine)
        {
            writeSimpleLine(_menu.Title, new TextStyling { Foreground = false });

            var nonSpacerOptions = _menu.Options.Where(o => !(o is SpacerOption)).ToList();
            int startIndex = _menu.CurrentPage * _menu.ItemsPerPage;
            int endIndex = Math.Min(nonSpacerOptions.Count, startIndex + _menu.ItemsPerPage);

            int actualStartIndex = 0;
            int nonSpacersSeen = 0;

            for (int i = 0; i < _menu.Options.Count && nonSpacersSeen < startIndex; i++)
            {
                if (!(_menu.Options[i] is SpacerOption))
                {
                    nonSpacersSeen++;
                }
                if (nonSpacersSeen == startIndex)
                {
                    actualStartIndex = i;
                    break;
                }
            }

            int visibleOptionIndex = 1;
            int enabledOptionIndex = 0;
            int nonSpacersShown = 0;

            for (int i = actualStartIndex; i < _menu.Options.Count && nonSpacersShown < _menu.ItemsPerPage; i++)
            {
                var option = _menu.Options[i];

                if (option is SpacerOption)
                {
                    writeSimpleLine("", default);
                    continue;
                }

                nonSpacersShown++;

                bool isFlashingThisItem = _menu._isFlashing && _menu._flashKey == visibleOptionIndex;
                bool isScrollingThisItem = !_menu._isFlashing &&
                                           (_menu.MenuType != MenuType.KeyPress) &&
                                           !option.IsDisabled &&
                                           enabledOptionIndex == _menu._currentSelectionIndex;

                var textStyle = new TextStyling
                {
                    Foreground = !option.IsDisabled,
                    Highlight = isFlashingThisItem || isScrollingThisItem
                };

                if (!option.IsDisabled || _menu.ShowDisabledOptionNum)
                {
                    writeLine(option.Text, textStyle, visibleOptionIndex++);
                }
                else
                {
                    writeSimpleLine(option.Text, textStyle);
                }

                if (!option.IsDisabled)
                {
                    enabledOptionIndex++;
                }
            }

            writeSimpleLine(" ", default);

            bool showBackButton = _menu.CurrentPage > 0 || (_menu.IsSubMenu && _menu.PrevMenu != null);
            bool showNextButton = _menu.CurrentPage < _menu.GetMaxPage();

            if (showBackButton)
            {
                bool isFlashing = _menu._isFlashing && _menu._flashKey == 7;
                int navIndexForScroll = _menu.GetEnabledOptionsCountOnCurrentPage();
                bool isScrolling = !_menu._isFlashing && (_menu.MenuType != MenuType.KeyPress) && _menu._currentSelectionIndex == navIndexForScroll;
                string text = _menu.IsSubMenu && _menu.CurrentPage == 0 ? _player.Localizer("Back") : _player.Localizer("Prev");
                writeLine(text, new TextStyling { Foreground = true, Highlight = isFlashing || isScrolling }, 7);
            }

            if (showNextButton)
            {
                bool isFlashing = _menu._isFlashing && _menu._flashKey == 8;
                int navIndexForScroll = _menu.GetEnabledOptionsCountOnCurrentPage() + (showBackButton ? 1 : 0);
                bool isScrolling = !_menu._isFlashing && (_menu.MenuType != MenuType.KeyPress) && _menu._currentSelectionIndex == navIndexForScroll;
                writeLine(_player.Localizer("Next"), new TextStyling { Foreground = true, Highlight = isFlashing || isScrolling }, 8);
            }

            if (_menu.HasExitButon)
            {
                bool isFlashing = _menu._isFlashing && _menu._flashKey == 9;
                int navIndexForScroll = _menu.GetEnabledOptionsCountOnCurrentPage() + (showBackButton ? 1 : 0) + (showNextButton ? 1 : 0);
                bool isScrolling = !_menu._isFlashing && (_menu.MenuType != MenuType.KeyPress) && _menu._currentSelectionIndex == navIndexForScroll;
                writeLine(_player.Localizer("Close"), new TextStyling { Foreground = true, Highlight = isFlashing || isScrolling }, 9);
            }

            if (_menu.ShowResolutionOption)
            {
                bool isFlashing = _menu._isFlashing && _menu._flashKey == 0;
                int navIndexForScroll = _menu.GetEnabledOptionsCountOnCurrentPage() + (showBackButton ? 1 : 0) + (showNextButton ? 1 : 0) + (_menu.HasExitButon ? 1 : 0);
                bool isScrolling = !_menu._isFlashing && (_menu.MenuType != MenuType.KeyPress) && _menu._currentSelectionIndex == navIndexForScroll;
                writeLine($"{_player.Localizer("ChangeRes")}", new TextStyling { Foreground = true, Highlight = isFlashing || isScrolling }, 0);
            }

            if (_menu.MenuType != MenuType.KeyPress && _menu.ShowControlsInfo)
            {

                writeSimpleLine(_player.Localizer("ScrollKeys", _menu.ScrollUpKey, _menu.ScrollDownKey), default);
                writeSimpleLine(_player.Localizer("SelectKey", _menu.SelectKey), default);
                writeSimpleLine(_player.Localizer("ExitKey", _menu.ExitKey), default);
            }
        }

        public void DestroyEntities()
        {
            if (_highlightText?.IsValid == true) _highlightText.Remove();
            if (_foregroundText?.IsValid == true) _foregroundText.Remove();
            if (_backgroundText?.IsValid == true) _backgroundText.Remove();
            if (_background?.IsValid == true) _background.Remove();
            _highlightText = _foregroundText = _backgroundText = _background = null;

            ulong steamId = _player.SteamID;
            if (_playerRoamingWorldTexts.ContainsKey(steamId))
            {
                _playerRoamingWorldTexts[steamId].Clear();
            }
        }

        private void CreateEntities()
        {
            _highlightText = CreateWorldText(HighlightTextColor, false, 0.001f);
            _foregroundText = CreateWorldText(ForegroundTextColor, false, 0.000f);
            _backgroundText = CreateWorldText(BackgroundTextColor, false, -0.001f);
            _background = CreateWorldText(Color.FromArgb(125, 127, 127, 127), true, -0.002f);
        }

        private bool CreateInitialInvisibleWorldTextEntity()
        {
            var observerInfo = _player.GetObserverInfo();
            if (_createdForPawn.HasValue && _createdForPawn.Value == observerInfo.Observing?.Handle)
                return false;

            var viewmodel = DisplayManager.EnsureCustomView(_player);
            if (viewmodel is null) return false;

            var vectorData = _player.FindVectorData(MenuPosition.X);
            if (!vectorData.HasValue) return false;

            var entity = CreateWorldText(Color.Orange, false, 0.0f);
            UpdateEntity(entity, viewmodel, "Sup", vectorData.Value.Position, vectorData.Value.Angle, true, true);
            entity.Remove();

            _createdForPawn = observerInfo.Observing?.Handle ?? nint.Zero;
            return true;
        }

        private CPointWorldText CreateWorldText(Color textColor, bool drawBackground, float depthOffset, string text = "", int fontSize = 32, string fontName = "Tahoma Bold")
        {
            var ent = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext")!;
            if (ent is not { IsValid: true }) throw new Exception("Failed to create point_worldtext");

            ent.MessageText = text;
            ent.Enabled = true;
            ent.FontName = _menu._config.Settings.FontName;
            ent.FontSize = _menu._config.Settings.Size;
            ent.Fullbright = true;
            ent.Color = textColor;
            ent.WorldUnitsPerPx = 0.0085f;
            ent.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT;
            ent.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_TOP;
            ent.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;
            ent.RenderMode = RenderMode_t.kRenderNormal;
            ent.DrawBackground = drawBackground;
            ent.BackgroundBorderHeight = 0.1f;
            ent.BackgroundBorderWidth = 0.1f;
            ent.BackgroundWorldToUV = 0.05f;
            ent.DepthOffset = depthOffset;
            ent.DispatchSpawn();
            return ent;
        }

        private void UpdateEntity(CPointWorldText ent, CCSGOViewModel? viewmodel, string newText, Vector position, QAngle angles, bool updateText = true, bool updateParent = true)
        {
            if (updateText) ent.MessageText = newText;
            ent.Teleport(position, angles, null);
            if (updateParent) ent.AcceptInput("SetParent", viewmodel, null, "!activator");
            if (updateText) Utilities.SetStateChanged(ent, "CPointWorldText", "m_messageText");
        }

        public void CheckTransmit(CCheckTransmitInfoList infoList)
        {
            for (int n = 0; n < infoList.Count; n++)
            {
                var info = infoList[n];
                if (info.player == _player) continue;

                if (_highlightText != null) info.info.TransmitEntities.Remove(_highlightText);
                if (_foregroundText != null) info.info.TransmitEntities.Remove(_foregroundText);
                if (_backgroundText != null) info.info.TransmitEntities.Remove(_backgroundText);
                if (_background != null) info.info.TransmitEntities.Remove(_background);
            }
        }
    }
}