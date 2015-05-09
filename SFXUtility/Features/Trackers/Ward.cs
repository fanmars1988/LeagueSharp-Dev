﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Ward.cs is part of SFXUtility.

 SFXUtility is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXUtility is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXUtility. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

// Credits: TC-Crew

namespace SFXUtility.Features.Trackers
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Classes;
    using LeagueSharp;
    using LeagueSharp.Common;
    using Properties;
    using SFXLibrary;
    using SFXLibrary.Extensions.NET;
    using SFXLibrary.Extensions.SharpDX;
    using SFXLibrary.Logger;
    using SharpDX;
    using SharpDX.Direct3D9;
    using Color = System.Drawing.Color;

    #endregion

    internal class Ward : Base
    {
        private const float CheckInterval = 300f;
        private readonly List<WardObject> _wardObjects = new List<WardObject>();

        private readonly List<WardStruct> _wardStructs = new List<WardStruct>
        {
            new WardStruct(60, 1100, "YellowTrinket", "TrinketTotemLvl1", WardType.Green),
            new WardStruct(60*3, 1100, "YellowTrinketUpgrade", "TrinketTotemLvl2", WardType.Green),
            new WardStruct(60*3, 1100, "SightWard", "TrinketTotemLvl3", WardType.Green),
            new WardStruct(60*3, 1100, "SightWard", "SightWard", WardType.Green),
            new WardStruct(60*3, 1100, "SightWard", "ItemGhostWard", WardType.Green),
            new WardStruct(60*3, 1100, "SightWard", "wrigglelantern", WardType.Green),
            new WardStruct(60*3, 1100, "SightWard", "ItemFeralFlare", WardType.Green),
            new WardStruct(int.MaxValue, 1100, "VisionWard", "TrinketTotemLvl3B", WardType.Pink),
            new WardStruct(int.MaxValue, 1100, "VisionWard", "VisionWard", WardType.Pink),
            new WardStruct(60*4, 212, "CaitlynTrap", "CaitlynYordleTrap", WardType.Trap),
            new WardStruct(60*10, 212, "TeemoMushroom", "BantamTrap", WardType.Trap),
            new WardStruct(60*1, 212, "ShacoBox", "JackInTheBox", WardType.Trap),
            new WardStruct(60*2, 212, "Nidalee_Spear", "Bushwhack", WardType.Trap),
            new WardStruct(60*10, 212, "Noxious_Trap", "BantamTrap", WardType.Trap)
        };

        private Texture _greenWardTexture;
        private float _lastCheck = Environment.TickCount;
        private Line _line;
        private Trackers _parent;
        private Texture _pinkWardTexture;
        private Sprite _sprite;
        private Font _text;

        public override bool Enabled
        {
            get { return !Unloaded && _parent != null && _parent.Enabled && Menu != null && Menu.Item(Name + "Enabled").GetValue<bool>(); }
        }

        public override string Name
        {
            get { return Language.Get("F_Ward"); }
        }

        protected override void OnEnable()
        {
            Game.OnUpdate += OnGameUpdate;
            Obj_AI_Base.OnProcessSpellCast += OnObjAiBaseProcessSpellCast;
            GameObject.OnCreate += OnGameObjectCreate;

            Drawing.OnPreReset += OnDrawingPreReset;
            Drawing.OnPostReset += OnDrawingPostReset;
            Drawing.OnEndScene += OnDrawingEndScene;

            Game.OnWndProc += OnGameWndProc;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Game.OnUpdate -= OnGameUpdate;
            Obj_AI_Base.OnProcessSpellCast -= OnObjAiBaseProcessSpellCast;
            GameObject.OnCreate -= OnGameObjectCreate;

            Drawing.OnPreReset -= OnDrawingPreReset;
            Drawing.OnPostReset -= OnDrawingPostReset;
            Drawing.OnEndScene -= OnDrawingEndScene;

            Game.OnWndProc += OnGameWndProc;

            OnUnload(null, new UnloadEventArgs());

            base.OnDisable();
        }

        protected override void OnUnload(object sender, UnloadEventArgs args)
        {
            if (args != null && args.Final)
                base.OnUnload(sender, args);

            if (Initialized)
            {
                OnDrawingPreReset(null);
                OnDrawingPostReset(null);
            }
        }

        protected override void OnGameLoad(EventArgs args)
        {
            try
            {
                if (Global.IoC.IsRegistered<Trackers>())
                {
                    _parent = Global.IoC.Resolve<Trackers>();
                    if (_parent.Initialized)
                        OnParentInitialized(null, null);
                    else
                        _parent.OnInitialized += OnParentInitialized;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnParentInitialized(object sender, EventArgs eventArgs)
        {
            try
            {
                if (_parent.Menu == null)
                    return;

                Menu = new Menu(Name, Name);

                var drawingMenu = new Menu(Language.Get("G_Drawing"), Name + "Drawing");
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "TimeFormat", Language.Get("G_TimeFormat")).SetValue(new StringList(new[] {"mm:ss", "ss"})));
                drawingMenu.AddItem(new MenuItem(drawingMenu.Name + "FontSize", Language.Get("G_FontSize")).SetValue(new Slider(13, 3, 30)));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "CircleRadius", Language.Get("G_Circle") + " " + Language.Get("G_Radius")).SetValue(new Slider(
                        150, 25, 300)));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "CircleThickness", Language.Get("G_Circle") + " " + Language.Get("G_Thickness")).SetValue(
                        new Slider(2, 1, 10)));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "GreenColor", Language.Get("Ward_Green") + " " + Language.Get("G_Color")).SetValue(Color.Lime));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "PinkColor", Language.Get("Ward_Pink") + " " + Language.Get("G_Color")).SetValue(Color.Magenta));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "TrapColor", Language.Get("Ward_Trap") + " " + Language.Get("G_Color")).SetValue(Color.Red));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "VisionRange", Language.Get("Ward_Vision") + " " + Language.Get("G_Range")).SetValue(true));

                Menu.AddSubMenu(drawingMenu);
                Menu.AddItem(new MenuItem(Name + "Hotkey", Language.Get("G_Hotkey")).SetValue(new KeyBind(16, KeyBindType.Press)));
                Menu.AddItem(new MenuItem(Name + "Enabled", Language.Get("G_Enabled")).SetValue(false));

                _parent.Menu.AddSubMenu(Menu);

                _sprite = new Sprite(Drawing.Direct3DDevice);
                _greenWardTexture = Resources.WT_Green.ToTexture();
                _pinkWardTexture = Resources.WT_Pink.ToTexture();
                _text = new Font(Drawing.Direct3DDevice,
                    new FontDescription
                    {
                        FaceName = Global.DefaultFont,
                        Height = Menu.Item(Name + "DrawingFontSize").GetValue<Slider>().Value,
                        OutputPrecision = FontPrecision.Default,
                        Quality = FontQuality.Default
                    });
                _line = new Line(Drawing.Direct3DDevice) {Width = Menu.Item(Name + "DrawingCircleThickness").GetValue<Slider>().Value};

                HandleEvents(_parent);
                RaiseOnInitialized();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnGameWndProc(WndEventArgs args)
        {
            if (args.Msg == (ulong) WindowsMessages.WM_LBUTTONDBLCLCK && Menu.Item(Name + "Hotkey").GetValue<KeyBind>().Active)
            {
                var ward = _wardObjects.OrderBy(w => Game.CursorPos.Distance(w.Position)).FirstOrDefault();
                if (ward != null && Game.CursorPos.Distance(ward.Position) <= 500)
                {
                    _wardObjects.Remove(ward);
                }
            }
        }

        private void OnDrawingEndScene(EventArgs args)
        {
            try
            {
                if (Drawing.Direct3DDevice == null || Drawing.Direct3DDevice.IsDisposed)
                    return;

                var totalSeconds = Menu.Item(Name + "DrawingTimeFormat").GetValue<StringList>().SelectedIndex == 1;
                var circleRadius = Menu.Item(Name + "DrawingCircleRadius").GetValue<Slider>().Value;
                var circleThickness = Menu.Item(Name + "DrawingCircleThickness").GetValue<Slider>().Value;
                var visionRange = Menu.Item(Name + "DrawingVisionRange").GetValue<bool>();
                var hotkey = Menu.Item(Name + "Hotkey").GetValue<KeyBind>().Active;

                _sprite.Begin(SpriteFlags.AlphaBlend);
                foreach (var ward in _wardObjects)
                {
                    var color =
                        Menu.Item(Name + "Drawing" +
                                  (ward.Data.Type == WardType.Green ? "Green" : (ward.Data.Type == WardType.Pink ? "Pink" : "Trap")) + "Color")
                            .GetValue<Color>();
                    if (ward.Position.IsOnScreen())
                    {
                        if (ward.Data.Type != WardType.Green)
                        {
                            Render.Circle.DrawCircle(ward.Position, circleRadius, color, circleThickness);
                        }
                        if (ward.Data.Type == WardType.Green)
                        {
                            _text.DrawTextCentered(
                                string.Format("{0} {1} {0}", ward.IsFromMissile ? (ward.Corrected ? "?" : "??") : string.Empty,
                                    (ward.EndTime - Game.Time).FormatTime(totalSeconds)), Drawing.WorldToScreen(ward.Position),
                                (new SharpDX.Color(color.R, color.G, color.B, color.A)));
                        }
                    }
                    if (ward.Data.Type != WardType.Trap)
                    {
                        _sprite.DrawCentered(ward.Data.Type == WardType.Green ? _greenWardTexture : _pinkWardTexture, ward.MinimapPosition.To2D());
                    }
                    if (hotkey)
                    {
                        if (visionRange)
                        {
                            Render.Circle.DrawCircle(ward.Position, ward.Data.Range, Color.FromArgb(30, color), circleThickness);
                        }
                        if (ward.IsFromMissile)
                        {
                            _line.Begin();
                            _line.Draw(new[] {Drawing.WorldToScreen(ward.StartPosition), Drawing.WorldToScreen(ward.EndPosition)}, SharpDX.Color.White);
                            _line.End();
                        }
                    }
                }
                _sprite.End();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnDrawingPostReset(EventArgs args)
        {
            try
            {
                _text.OnResetDevice();
                _sprite.OnResetDevice();
                _line.OnResetDevice();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnDrawingPreReset(EventArgs args)
        {
            try
            {
                _text.OnLostDevice();
                _sprite.OnLostDevice();
                _line.OnLostDevice();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnGameObjectCreate(GameObject sender, EventArgs args)
        {
            try
            {
                var missile = sender as Obj_SpellMissile;
                if (missile != null)
                {
                    if (missile.SpellCaster != null && !missile.SpellCaster.IsAlly)
                    {
                        if (missile.SData != null && missile.SData.Name.Equals("itemplacementmissile", StringComparison.OrdinalIgnoreCase) &&
                            !missile.SpellCaster.IsVisible)
                        {
                            var sPos = missile.StartPosition;
                            var ePos = missile.EndPosition;

                            var offset =
                                _wardObjects.Count(
                                    w => w.Position.To2D().Distance(sPos.To2D(), ePos.To2D(), false) < 300 && w.Data.Type == WardType.Green)*
                                (Menu.Item(Name + "DrawingFontSize").GetValue<Slider>().Value + 3);
                            _wardObjects.Add(new WardObject(_wardStructs[3],
                                new Vector3(ePos.X, ePos.Y - offset, NavMesh.GetHeightForPosition(ePos.X, ePos.Y)), (int) Game.Time, null, true,
                                new Vector3(sPos.X, sPos.Y, NavMesh.GetHeightForPosition(sPos.X, sPos.Y))));
                        }
                    }
                }
                else
                {
                    var wardObject = sender as Obj_AI_Base;
                    if (wardObject != null)
                    {
                        if (!wardObject.IsAlly)
                        {
                            foreach (var ward in _wardStructs)
                            {
                                if (wardObject.BaseSkinName.Equals(ward.ObjectBaseSkinName, StringComparison.OrdinalIgnoreCase))
                                {
                                    var offset =
                                        _wardObjects.Count(w => w.Position.Distance(wardObject.Position) < 200 && w.Data.Type == WardType.Green)*
                                        (Menu.Item(Name + "DrawingFontSize").GetValue<Slider>().Value + 3);
                                    _wardObjects.Add(new WardObject(ward,
                                        new Vector3(wardObject.Position.X, wardObject.Position.Y - offset, wardObject.Position.Z),
                                        (int) (Game.Time - (int) ((wardObject.MaxMana - wardObject.Mana))), wardObject));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnObjAiBaseProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            try
            {
                if (sender.IsAlly)
                    return;

                foreach (var ward in _wardStructs)
                {
                    if (args.SData.Name.Equals(ward.SpellName, StringComparison.OrdinalIgnoreCase))
                    {
                        _wardObjects.Add(new WardObject(ward, ObjectManager.Player.GetPath(args.End).LastOrDefault(), (int) Game.Time));
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            try
            {
                if (_lastCheck + CheckInterval > Environment.TickCount)
                    return;
                _lastCheck = Environment.TickCount;

                _wardObjects.RemoveAll(w => w.EndTime <= Game.Time && w.Data.Duration != int.MaxValue);
                _wardObjects.RemoveAll(w => w.Object != null && !w.Object.IsValid);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private class WardObject
        {
            private readonly int _startT;
            public readonly bool Corrected;
            public readonly Vector3 EndPosition;
            public readonly Vector3 MinimapPosition;
            public readonly Obj_AI_Base Object;
            public readonly Vector3 StartPosition;
            private Vector3 _position;

            public WardObject(WardStruct data, Vector3 position, int startT, Obj_AI_Base wardObject = null, bool isFromMissile = false,
                Vector3 startPosition = default(Vector3))
            {
                var pos = position;
                if (isFromMissile)
                {
                    var newPos = GuessPosition(startPosition, position);
                    if (position.X != newPos.X || position.Y != newPos.Y)
                    {
                        pos = newPos;
                        Corrected = true;
                    }
                }
                IsFromMissile = isFromMissile;
                Data = data;
                Position = RealPosition(pos);
                EndPosition = Position.Equals(position) ? position : RealPosition(position);
                MinimapPosition = Drawing.WorldToMinimap(Position).To3D();
                _startT = startT;
                StartPosition = startPosition.Equals(default(Vector3)) ? startPosition : RealPosition(startPosition);
                Object = wardObject;
            }

            public Vector3 Position
            {
                get
                {
                    if (Object != null && Object.IsValid && Object.IsVisible)
                    {
                        _position = Object.Position;
                    }
                    return _position;
                }
                private set { _position = value; }
            }

            public bool IsFromMissile { get; private set; }

            public int EndTime
            {
                get { return _startT + Data.Duration; }
            }

            public WardStruct Data { get; private set; }

            private Vector3 GuessPosition(Vector3 start, Vector3 end)
            {
                var grass = new List<Vector3>();
                var distance = start.Distance(end);
                for (var i = 0; i < distance; i++)
                {
                    var pos = start.Extend(end, i);
                    if (NavMesh.IsWallOfGrass(pos, 1))
                    {
                        grass.Add(pos);
                    }
                }
                return grass.Count > 0 ? grass[(int)((grass.Count / 2d) + 0.5d * Math.Sign(grass.Count / 2d))] : end;
            }

            private Vector3 RealPosition(Vector3 end)
            {
                if (end.IsWall())
                {
                    for (var i = 0; i < 1000; i = i + 2)
                    {
                        var c = new Geometry.Polygon.Circle(end, i, 15).ToClipperPath();
                        foreach (var item in c)
                        {
                            if (!(new Vector2(item.X, item.Y).To3D().IsWall()))
                            {
                                return new Vector3(item.X, item.Y, NavMesh.GetHeightForPosition(item.X, item.Y));
                            }
                        }
                    }
                }
                return end;
            }
        }

        private enum WardType
        {
            Green,
            Pink,
            Trap
        }

        private struct WardStruct
        {
            public readonly int Duration;
            public readonly string ObjectBaseSkinName;
            public readonly int Range;
            public readonly string SpellName;
            public readonly WardType Type;

            public WardStruct(int duration, int range, string objectBaseSkinName, string spellName, WardType type)
            {
                Duration = duration;
                Range = range;
                ObjectBaseSkinName = objectBaseSkinName;
                SpellName = spellName;
                Type = type;
            }
        }
    }
}