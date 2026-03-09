using UnityEditor;
using UnityEngine;

namespace Ilumisoft.VisualStateMachine
{
    class StateMachineSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateStartupProfileConfigurationProvider() => CreateProvider("Project/Visual State Machine", Configuration.Find());

        static SettingsProvider CreateProvider(string settingsWindowPath, Object asset)
        {
            var provider = AssetSettingsProvider.CreateProviderFromObject(settingsWindowPath, asset);

            provider.keywords = SettingsProvider.GetSearchKeywordsFromSerializedObject(new SerializedObject(asset));
            return provider;
        }
    }
}