using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using IAmFuture.Gameplay.InteractiveObjects.Actions;
using IAmFuture.Gameplay.LootSystem;
using IAmFuture.Gameplay.LootSystem.LootSets;
using IAmFuture.Gameplay.Signals;
using UnityEngine;
using Zenject;
using static UnityEngine.GameObject;

namespace Auto_Pickup;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static ManualLogSource _logger;
    readonly float _radius = 2.5f;
    private bool _collecting;
    private float _lastCollectionTime;
    private readonly float _collectionCooldown = 0.5f;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) _collecting = !_collecting;
        if (_collecting && Time.time >= _lastCollectionTime + _collectionCooldown)
        {
            _lastCollectionTime = Time.time;

            FindLootAroundPlayer();
        }
    }

    void collect_loot(GameObject objectGO, GameObject actorGO, PickupLootAction action)
    {
        if (!objectGO.TryGetComponent<LootSet>(out var component1)) return;
        CollectLootAction collectLootAction =
            (CollectLootAction)AccessTools.Field(typeof(PickupLootAction), "collectLootAction").GetValue(action);
        SignalBus signalBus = (SignalBus)AccessTools.Field(typeof(PickupLootAction), "signalBus").GetValue(action);
        if (collectLootAction.TryToExecute(objectGO, actorGO))
            signalBus.Fire<PickupLootSignal>(new PickupLootSignal(objectGO));

        bool autoDestroy = (bool)AccessTools.Field(typeof(PickupLootAction), "autoDestroy").GetValue(action);
        if (!autoDestroy || !component1.IsEmpty) return;
        LootFactory factory = (LootFactory)AccessTools.Field(typeof(PickupLootAction), "factory").GetValue(action);
        factory.Dispose(objectGO);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private void FindLootAroundPlayer()
    {
        GameObject player = FindWithTag("Player");
        if (!player) return;

        var allLootObjects = FindGameObjectsWithTag("Loot");
        if (allLootObjects.Length == 0) return;

        foreach (var loot in allLootObjects)
        {
            if (loot == null) continue;

            if (Vector3.Distance(player.transform.position, loot.transform.position) <= _radius)
            {
                Loot lootComponent = loot.GetComponent<Loot>();
                if (lootComponent)
                {
                    PickupLootAction action = (PickupLootAction)lootComponent.Actions[0];
                    collect_loot(loot, player, action);
                }
            }
        }
    }

    private void Awake()
    {
        // Plugin startup logic
        _logger = Logger;
        _logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
}