using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using IAmFuture.Data.Character;
using IAmFuture.Gameplay.Character;
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
    private const float CollectionCooldown = 0.5f;
    private const float HugePickupDelay = 0.1f;
    private const float Radius = 2.5f;
    private static ManualLogSource _logger;
    private bool _collectEnabled;
    private float _lastCollectionTime;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) _collectEnabled = !_collectEnabled;
        if (!_collectEnabled || Time.time < _lastCollectionTime + CollectionCooldown) return;
        _lastCollectionTime = Time.time;
        StartCoroutine(FindLootAroundPlayer());
    }

    private static void collect_loot(GameObject objectGO, GameObject actorGO, PickupLootAction action)
    {
        if (!objectGO.TryGetComponent<LootSet>(out var component1)) return;
        var collectLootAction =
            (CollectLootAction)AccessTools.Field(typeof(PickupLootAction), "collectLootAction").GetValue(action);
        var signalBus = (SignalBus)AccessTools.Field(typeof(PickupLootAction), "signalBus").GetValue(action);
        if (collectLootAction.TryToExecute(objectGO, actorGO))
            signalBus.Fire(new PickupLootSignal(objectGO));

        var autoDestroy = (bool)AccessTools.Field(typeof(PickupLootAction), "autoDestroy").GetValue(action);
        if (!autoDestroy || !component1.IsEmpty) return;
        var factory = (LootFactory)AccessTools.Field(typeof(PickupLootAction), "factory").GetValue(action);
        factory.Dispose(objectGO);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private static IEnumerator FindLootAroundPlayer()
    {
        var player = FindWithTag("Player");
        if (!player) yield break;
        var characterServiceProvider = player.GetComponent<CharacterServicesProvider>();
        if (characterServiceProvider == null) yield break;
        var state = characterServiceProvider.StateMachine.GetState<CarrierCharacter>();
        if (state == null) yield break;
        var isCarrying = state.IsActive;
        var array = Physics.OverlapSphere(player.transform.position, Radius).Select(x => x.gameObject)
            .Where(x => x != null && x.tag == (isCarrying ? "LootCarry" : "Loot")).ToArray();
        if (array.Length <= 0) yield break;
        foreach (var objectGo in array)
        {
            if (objectGo?.name == null) continue;
            if (isCarrying && objectGo.name.Contains(state.CarriedObject.name))
            {
                yield return new WaitForSeconds(HugePickupDelay);
                state.TryToPickUpAdditionalObject(objectGo);
                break;
            }

            if (isCarrying) continue;
            var loot = objectGo.GetComponent<Loot>();
            if (!loot) continue;
            if (!player.TryGetComponent<CharacterInventory>(out var inventory)) break;
            var item = loot.LootSet.Stack?.Object;
            if (item == null) continue;
            if (inventory.DoesItemRequireSpaceToBeAdded(item) && inventory.Storage.IsFull &&
                !inventory.Storage.HasVacantStack(item)) break;
            var pickupLootAction = (PickupLootAction)loot.Actions[0];
            collect_loot(objectGo, player, pickupLootAction);
        }
    }

    private void Awake()
    {
        _logger = Logger;
        _logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
}