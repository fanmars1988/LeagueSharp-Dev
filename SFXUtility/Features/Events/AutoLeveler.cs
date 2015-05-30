﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 AutoLeveler.cs is part of SFXUtility.

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

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXLibrary.Logger;
using SFXUtility.Classes;

#endregion

namespace SFXUtility.Features.Events
{
    internal class AutoLeveler : Base
    {
        private const float CheckInterval = 300f;
        private float _lastCheck = Environment.TickCount;
        private Events _parent;

        public override bool Enabled
        {
            get
            {
                return !Unloaded && _parent != null && _parent.Enabled && Menu != null &&
                       Menu.Item(Name + "Enabled").GetValue<bool>();
            }
        }

        public override string Name
        {
            get { return Global.Lang.Get("F_AutoLeveler"); }
        }

        protected override void OnEnable()
        {
            LeagueSharp.Game.OnUpdate += OnGameUpdate;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            LeagueSharp.Game.OnUpdate -= OnGameUpdate;
            base.OnDisable();
        }

        private List<SpellInfoStruct> GetOrderedPriorityList()
        {
            return GetPriorityList().OrderByDescending(x => x.Value).ToList();
        }

        private List<SpellInfoStruct> GetPriorityList()
        {
            return
                new List<SpellInfoStruct>
                {
                    new SpellInfoStruct(
                        SpellSlot.Q, Menu.Item(Name + ObjectManager.Player.ChampionName + "Q").GetValue<Slider>().Value),
                    new SpellInfoStruct(
                        SpellSlot.W, Menu.Item(Name + ObjectManager.Player.ChampionName + "W").GetValue<Slider>().Value),
                    new SpellInfoStruct(
                        SpellSlot.E, Menu.Item(Name + ObjectManager.Player.ChampionName + "E").GetValue<Slider>().Value),
                    new SpellInfoStruct(SpellSlot.R, 4)
                }.ToList();
        }

        protected override void OnGameLoad(EventArgs args)
        {
            try
            {
                if (Global.IoC.IsRegistered<Events>())
                {
                    _parent = Global.IoC.Resolve<Events>();
                    if (_parent.Initialized)
                    {
                        OnParentInitialized(null, null);
                    }
                    else
                    {
                        _parent.OnInitialized += OnParentInitialized;
                    }
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
                {
                    return;
                }

                Menu = new Menu(Name, Name);

                var championMenu = new Menu(ObjectManager.Player.ChampionName, Name + ObjectManager.Player.ChampionName);

                var earlyMenu = new Menu(Global.Lang.Get("AutoLeveler_Early"), championMenu.Name + "Early");
                earlyMenu.AddItem(
                    new MenuItem(earlyMenu.Name + "1", "1: ").SetValue(
                        new StringList(
                            new[]
                            {
                                Global.Lang.Get("G_None"), Global.Lang.Get("AutoLeveler_Priority"),
                                Global.Lang.Get("G_None"), "Q", "W", "E"
                            })));
                earlyMenu.AddItem(
                    new MenuItem(earlyMenu.Name + "2", "2: ").SetValue(
                        new StringList(
                            new[] { Global.Lang.Get("G_None"), Global.Lang.Get("AutoLeveler_Priority"), "Q", "W", "E" },
                            2)));
                earlyMenu.AddItem(
                    new MenuItem(earlyMenu.Name + "3", "3: ").SetValue(
                        new StringList(
                            new[] { Global.Lang.Get("G_None"), Global.Lang.Get("AutoLeveler_Priority"), "Q", "W", "E" },
                            4)));
                earlyMenu.AddItem(
                    new MenuItem(earlyMenu.Name + "4", "4: ").SetValue(
                        new StringList(
                            new[] { Global.Lang.Get("G_None"), Global.Lang.Get("AutoLeveler_Priority"), "Q", "W", "E" },
                            2)));
                earlyMenu.AddItem(
                    new MenuItem(earlyMenu.Name + "5", "5: ").SetValue(
                        new StringList(
                            new[] { Global.Lang.Get("G_None"), Global.Lang.Get("AutoLeveler_Priority"), "Q", "W", "E" },
                            3)));

                championMenu.AddSubMenu(earlyMenu);

                championMenu.AddItem(new MenuItem(championMenu.Name + "Q", "Q").SetValue(new Slider(3, 3, 1)));
                championMenu.AddItem(new MenuItem(championMenu.Name + "W", "W").SetValue(new Slider(1, 3, 1)));
                championMenu.AddItem(new MenuItem(championMenu.Name + "E", "E").SetValue(new Slider(2, 3, 1)));

                championMenu.AddItem(
                    new MenuItem(championMenu.Name + "OnlyR", Global.Lang.Get("AutoLeveler_OnlyR")).SetValue(false));
                championMenu.AddItem(
                    new MenuItem(championMenu.Name + "Enabled", Global.Lang.Get("G_Enabled")).SetValue(false));

                Menu.AddSubMenu(championMenu);

                Menu.AddItem(new MenuItem(Name + "Enabled", Global.Lang.Get("G_Enabled")).SetValue(false));

                _parent.Menu.AddSubMenu(Menu);

                HandleEvents(_parent);
                RaiseOnInitialized();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (_lastCheck + CheckInterval > Environment.TickCount)
            {
                return;
            }
            _lastCheck = Environment.TickCount;

            var availablePoints = ObjectManager.Player.Level -
                                  (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.Q).Level +
                                   ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).Level +
                                   ObjectManager.Player.Spellbook.GetSpell(SpellSlot.E).Level +
                                   ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Level);

            if (availablePoints > 0)
            {
                OnUnitLevelUp(
                    ObjectManager.Player,
                    new CustomEvents.Unit.OnLevelUpEventArgs
                    {
                        NewLevel = ObjectManager.Player.Level,
                        RemainingPoints = availablePoints
                    });
            }
        }

        private void OnUnitLevelUp(Obj_AI_Base sender, CustomEvents.Unit.OnLevelUpEventArgs args)
        {
            try
            {
                if (!sender.IsValid || !sender.IsMe ||
                    !Menu.Item(Name + ObjectManager.Player.ChampionName + "Enabled").GetValue<bool>())
                {
                    return;
                }

                var availablePoints = args.RemainingPoints;

                if (ObjectManager.Player.Level <= 5)
                {
                    var index =
                        Menu.Item(Name + ObjectManager.Player.ChampionName + "Early" + ObjectManager.Player.Level)
                            .GetValue<StringList>()
                            .SelectedIndex;
                    switch (index)
                    {
                        case 0:
                            return;
                        case 1:
                            return;
                        case 2:
                            ObjectManager.Player.Spellbook.LevelUpSpell(SpellSlot.Q);
                            return;
                        case 3:
                            ObjectManager.Player.Spellbook.LevelUpSpell(SpellSlot.W);
                            return;
                        case 4:
                            ObjectManager.Player.Spellbook.LevelUpSpell(SpellSlot.E);
                            return;
                    }
                }

                foreach (var pItem in GetOrderedPriorityList())
                {
                    if (availablePoints <= 0)
                    {
                        return;
                    }
                    var pointsToLevelSlot = MaxSpellLevel(pItem.Slot, args.NewLevel) -
                                            ObjectManager.Player.Spellbook.GetSpell(pItem.Slot).Level;
                    pointsToLevelSlot = pointsToLevelSlot > availablePoints ? availablePoints : pointsToLevelSlot;

                    for (var i = 0; pointsToLevelSlot > i; i++)
                    {
                        ObjectManager.Player.Spellbook.LevelUpSpell(pItem.Slot);
                        availablePoints--;
                    }

                    if (pItem.Slot == SpellSlot.R &&
                        Menu.Item(Name + ObjectManager.Player.ChampionName + "OnlyR").GetValue<bool>())
                    {
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private int MaxSpellLevel(SpellSlot slot, int level)
        {
            return slot == SpellSlot.R
                ? (level >= 16 ? 3 : (level >= 11 ? 2 : (level >= 6 ? 1 : 0)))
                : (level >= 9 ? 5 : (level >= 7 ? 4 : (level >= 5 ? 3 : (level >= 3 ? 2 : 1))));
        }

        private struct SpellInfoStruct
        {
            public readonly SpellSlot Slot;
            public readonly int Value;

            public SpellInfoStruct(SpellSlot slot, int value)
            {
                Slot = slot;
                Value = value;
            }
        }
    }
}