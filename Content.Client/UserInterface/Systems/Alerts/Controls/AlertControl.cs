using System.Numerics;
using Content.Client.Actions.UI;
using Content.Client.Cooldown;
using Content.Shared._Sirius.Dirtiness;
using Content.Shared.Alert;
using Content.Shared.Humanoid;
using Content.Shared.Nutrition.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Alerts.Controls
{
    public sealed class AlertControl : BaseButton
    {
        private const float NutritionShakeThreshold = 0.25f;

        public AlertPrototype Alert { get; }

        /// <summary>
        /// Current cooldown displayed in this slot. Set to null to show no cooldown.
        /// </summary>
        public (TimeSpan Start, TimeSpan End)? Cooldown
        {
            get => _cooldown;
            set
            {
                _cooldown = value;
                if (SuppliedTooltip is ActionAlertTooltip actionAlertTooltip)
                {
                    actionAlertTooltip.Cooldown = value;
                }
            }
        }

        private (TimeSpan Start, TimeSpan End)? _cooldown;

        private short? _severity;
        private readonly IGameTiming _gameTiming;
        private readonly IEntityManager _entityManager;
        private readonly IPlayerManager _playerManager;
        private readonly SpriteView _icon;
        private readonly CooldownGraphic _cooldownGraphic;

        private EntityUid _spriteViewEntity;

        /// <summary>
        /// Creates an alert control reflecting the indicated alert + state
        /// </summary>
        /// <param name="alert">alert to display</param>
        /// <param name="severity">severity of alert, null if alert doesn't have severity levels</param>
        public AlertControl(AlertPrototype alert, short? severity)
        {
            _gameTiming = IoCManager.Resolve<IGameTiming>();
            _entityManager = IoCManager.Resolve<IEntityManager>();
            _playerManager = IoCManager.Resolve<IPlayerManager>();
            TooltipSupplier = SupplyTooltip;
            Alert = alert;
            _severity = severity;

            _spriteViewEntity = _entityManager.Spawn(Alert.AlertViewEntity);
            if (_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
            {
                var icon = Alert.GetIcon(_severity);
                if (sprite.LayerMapTryGet(AlertVisualLayers.Base, out var layer))
                    sprite.LayerSetSprite(layer, icon);
            }

            _icon = new SpriteView
            {
                Scale = new Vector2(2, 2)
            };
            _icon.SetEntity(_spriteViewEntity);

            Children.Add(_icon);
            _cooldownGraphic = new CooldownGraphic
            {
                MaxSize = new Vector2(64, 64)
            };
            Children.Add(_cooldownGraphic);
        }

        private Control SupplyTooltip(Control? sender)
        {
            var msg = FormattedMessage.FromMarkup(Loc.GetString(Alert.Name));
            var desc = FormattedMessage.FromMarkup(Loc.GetString(Alert.Description));

            TryBuildNutritionTooltip(desc);
            TryBuildSiriusDirtyTooltip(desc); // #Sirius Add: show the current cleanliness on the HUD icon tooltip.

            return new ActionAlertTooltip(msg, desc) {Cooldown = Cooldown};
        }

        private bool TryBuildNutritionTooltip(FormattedMessage desc)
        {
            var entity = _playerManager.LocalEntity;
            if (entity is null)
                return false;

            switch (Alert.ID)
            {
                case "Peckish":
                case "Starving":
                    if (!_entityManager.TryGetComponent(entity.Value, out HungerComponent? hunger))
                        return false;

                    var hungerWarningMax = hunger.Thresholds[HungerThreshold.Peckish];
                    var hungerCurrent = Math.Clamp(hunger.CurrentHunger, hunger.Thresholds[HungerThreshold.Dead], hungerWarningMax);
                    desc.PushNewline();
                    desc.PushNewline();
                    desc.AddText(Loc.GetString("alerts-hunger-current-value",
                        ("current", (int) hungerCurrent),
                        ("max", (int) hungerWarningMax)));
                    return true;

                case "Thirsty":
                case "Parched":
                    if (!_entityManager.TryGetComponent(entity.Value, out ThirstComponent? thirst))
                        return false;

                    var thirstWarningMax = thirst.ThirstThresholds[ThirstThreshold.Thirsty];
                    var thirstCurrent = Math.Clamp(thirst.CurrentThirst, thirst.ThirstThresholds[ThirstThreshold.Dead], thirstWarningMax);
                    desc.PushNewline();
                    desc.PushNewline();
                    desc.AddText(Loc.GetString("alerts-thirst-current-value",
                        ("current", (int) thirstCurrent),
                        ("max", (int) thirstWarningMax)));
                    return true;

                default:
                    return false;
            }
        }

        // #Sirius Add: HUD tooltip shows the current cleanliness state instead of a raw number.
        private bool TryBuildSiriusDirtyTooltip(FormattedMessage desc)
        {
            if (Alert.ID != "SiriusDirty")
                return false;

            var entity = _playerManager.LocalEntity;
            if (entity is null || !_entityManager.TryGetComponent(entity.Value, out SiriusDirtyComponent? dirty))
                return false;

            desc.PushNewline();
            desc.PushNewline();
            desc.AddText(Loc.GetString("alerts-sirius-dirty-current-state",
                ("state", Loc.GetString(SiriusDirtyStateLoc.NameKey(dirty.State)))));

            // #Sirius Add: the doubled decay is only relevant to ghouls, so only they see it.
            if (_entityManager.TryGetComponent(entity.Value, out HumanoidAppearanceComponent? appearance) &&
                SiriusDirtySpecies.IsGhoul(appearance.Species))
            {
                desc.PushNewline();
                desc.AddText(Loc.GetString("sirius-dirty-tooltip-ghoul"));
            }

            return true;
        }

        /// <summary>
        /// Change the alert severity, changing the displayed icon
        /// </summary>
        public void SetSeverity(short? severity)
        {
            if (_severity == severity)
                return;
            _severity = severity;

            if (!_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
                return;
            var icon = Alert.GetIcon(_severity);
            if (sprite.LayerMapTryGet(AlertVisualLayers.Base, out var layer))
                sprite.LayerSetSprite(layer, icon);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            UserInterfaceManager.GetUIController<AlertsUIController>().UpdateAlertSpriteEntity(_spriteViewEntity, Alert);
            UpdateNutritionShake();

            if (!Cooldown.HasValue)
            {
                _cooldownGraphic.Visible = false;
                _cooldownGraphic.Progress = 0;
                return;
            }

            _cooldownGraphic.FromTime(Cooldown.Value.Start, Cooldown.Value.End);
        }

        private void UpdateNutritionShake()
        {
            var offset = GetNutritionShakeOffset();
            var margin = new Thickness(offset.X, offset.Y, -offset.X, -offset.Y);

            _icon.Margin = margin;
            _cooldownGraphic.Margin = margin;
        }

        private Vector2i GetNutritionShakeOffset()
        {
            var entity = _playerManager.LocalEntity;
            if (entity is null)
                return Vector2i.Zero;

            var shakeData = Alert.ID switch
            {
                "Peckish" or "Starving" => GetHungerShakeData(entity.Value),
                "Thirsty" or "Parched" => GetThirstShakeData(entity.Value),
                _ => null,
            };

            if (shakeData is null || shakeData.Value.Fraction > NutritionShakeThreshold)
                return Vector2i.Zero;

            var intensity = (1f - (shakeData.Value.Fraction / NutritionShakeThreshold)) * shakeData.Value.Multiplier;
            var time = (float) _gameTiming.CurTime.TotalSeconds;
            var x = (int) MathF.Round(MathF.Sin(time * 20f) * (1f + intensity));
            var y = (int) MathF.Round(MathF.Cos(time * 13f) * MathF.Max(1f, intensity * 1.5f));
            return new Vector2i(x, y);
        }

        private (float Fraction, float Multiplier)? GetHungerShakeData(EntityUid entity)
        {
            if (!_entityManager.TryGetComponent(entity, out HungerComponent? hunger))
                return null;

            var max = hunger.Thresholds[HungerThreshold.Peckish];
            if (max <= 0f)
                return null;

            var current = Math.Clamp(hunger.CurrentHunger, hunger.Thresholds[HungerThreshold.Dead], max);
            var multiplier = hunger.CurrentThreshold switch
            {
                HungerThreshold.Dead => 1.85f,
                HungerThreshold.Starving => 1.35f,
                HungerThreshold.Peckish => 0.8f,
                _ => 0f,
            };

            return multiplier <= 0f ? null : (current / max, multiplier);
        }

        private (float Fraction, float Multiplier)? GetThirstShakeData(EntityUid entity)
        {
            if (!_entityManager.TryGetComponent(entity, out ThirstComponent? thirst))
                return null;

            var max = thirst.ThirstThresholds[ThirstThreshold.Thirsty];
            if (max <= 0f)
                return null;

            var current = Math.Clamp(thirst.CurrentThirst, thirst.ThirstThresholds[ThirstThreshold.Dead], max);
            var multiplier = thirst.CurrentThirstThreshold switch
            {
                ThirstThreshold.Dead => 1.95f,
                ThirstThreshold.Parched => 1.45f,
                ThirstThreshold.Thirsty => 0.9f,
                _ => 0f,
            };

            return multiplier <= 0f ? null : (current / max, multiplier);
        }

        [Obsolete]
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);
        }
    }

    public enum AlertVisualLayers : byte
    {
        Base
    }
}
