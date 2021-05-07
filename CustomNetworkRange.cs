using Facepunch;
using Facepunch.Extend;
using Network.Visibility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Custom Network Range", "WhiteThunder", "1.0.0")]
    [Description("Customizes the network range (draw distance) of entities to players.")]
    internal class CustomNetworkRange : CovalencePlugin
    {
        #region Fields

        private NetworkVisibilityGrid _grid;

        private Configuration _pluginConfig;
        private Configuration _vanillaConfig;

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _grid = Network.Net.sv?.visibility?.provider as NetworkVisibilityGrid;
            if (_grid == null)
            {
                LogError("This server doesn't appear to be using a NetworkVisibilityGrid. This plugin is disabled.");
                return;
            }

            _vanillaConfig = new Configuration()
            {
                VisibilityRadiusFar = _grid.visibilityRadiusFar,
                VisibilityRadiusNear = _grid.visibilityRadiusNear,
            };

            ApplyConfig(_grid, _pluginConfig);
        }

        private void Unload()
        {
            if (_vanillaConfig == null)
                return;

            ApplyConfig(_grid, _vanillaConfig);
        }

        #endregion

        #region Helper Methods

        private void ApplyConfig(NetworkVisibilityGrid grid, Configuration config)
        {
            var changed = false;

            if (grid.visibilityRadiusFar != config.VisibilityRadiusFar)
            {
                Puts($"Updating visibilityRadiusFar from {grid.visibilityRadiusFar} to {config.VisibilityRadiusFar}");
                grid.visibilityRadiusFar = config.VisibilityRadiusFar;
                changed = true;
            }

            if (grid.visibilityRadiusNear != config.VisibilityRadiusNear)
            {
                Puts($"Updating visibilityRadiusNear from {grid.visibilityRadiusNear} to {config.VisibilityRadiusNear}");
                grid.visibilityRadiusNear = config.VisibilityRadiusNear;
                changed = true;
            }

            if (!changed)
                return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.net != null
                    && !player.IsReceivingSnapshot
                    && !player.isCallingUpdateNetworkGroup)
                {
                    player.isCallingUpdateNetworkGroup = true;
                    player.Invoke(() =>
                    {
                        player.UpdateNetworkGroup();
                        UpdateSubscriptions(player.net);
                    }, UnityEngine.Random.Range(0f, 5f));
                }
            }
        }

        // TODO: Replace with vanilla call.
        private void UpdateSubscriptions(Network.Networkable net)
        {
            if (net.subscriber == null)
                return;

            List<Group> obj = Pool.GetList<Group>();
            List<Group> obj2 = Pool.GetList<Group>();
            List<Group> obj3 = Pool.GetList<Group>();
            net.sv.visibility.GetVisibleFromFar(net.group, obj3);
            AddVisibleFromNear(net, net.secondaryGroup, obj3);
            net.subscriber.subscribed.Compare(obj3, obj, obj2, null);
            for (int i = 0; i < obj2.Count; i++)
            {
                Group group = obj2[i];
                net.subscriber.Unsubscribe(group);
                if (net.handler != null)
                {
                    net.handler.OnNetworkGroupLeave(group);
                }
            }
            for (int j = 0; j < obj.Count; j++)
            {
                Group group2 = obj[j];
                net.subscriber.Subscribe(group2);
                if (net.handler != null)
                {
                    net.handler.OnNetworkGroupEnter(group2);
                }
            }
            Pool.FreeList(ref obj);
            Pool.FreeList(ref obj2);
            Pool.FreeList(ref obj3);
        }

        // TODO: Replace with vanilla call.
        private void AddVisibleFromNear(Network.Networkable net, Group additionalGroup, List<Group> groupsVisible)
        {
            if (additionalGroup == null)
            {
                return;
            }
            List<Group> obj = Pool.GetList<Group>();
            net.sv.visibility.GetVisibleFromNear(additionalGroup, obj);
            for (int i = 0; i < obj.Count; i++)
            {
                Group item = obj[i];
                if (!groupsVisible.Contains(item))
                {
                    groupsVisible.Add(item);
                }
            }
            Pool.FreeList(ref obj);
        }

        #endregion

        #region Configuration

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("VisibilityRadiusFar")]
            public int VisibilityRadiusFar = 8;

            [JsonProperty("VisibilityRadiusNear")]
            public int VisibilityRadiusNear = 4;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Boilerplate

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion
    }
}
