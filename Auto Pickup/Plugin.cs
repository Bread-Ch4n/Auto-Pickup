using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using IAmFuture.Data.Character;
using IAmFuture.Data.Items;
using IAmFuture.Gameplay.Character;
using IAmFuture.Gameplay.InteractiveObjects.Actions;
using IAmFuture.Gameplay.LootSystem;
using IAmFuture.Gameplay.LootSystem.LootSets;
using IAmFuture.Gameplay.Signals;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zenject;
using static UnityEngine.GameObject;

namespace Auto_Pickup;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static ManualLogSource _logger;
    readonly float _radius = 2.5f;
    private bool _collectEnabled;
    private bool _collecting;
    private float _lastCollectionTime;
    private readonly float _collectionCooldown = 0.5f;
    private readonly float _hugePickupDelay = 0.1f;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) _collectEnabled = !_collectEnabled;
        if (!_collectEnabled || Time.time < _lastCollectionTime + _collectionCooldown) return;
        _lastCollectionTime = Time.time;
        StartCoroutine(FindLootAroundPlayer());
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
    private IEnumerator FindLootAroundPlayer()
    {
        GameObject player = FindWithTag("Player");
        if (player)
        {
            CharacterServicesProvider characterServiceProvider = player.GetComponent<CharacterServicesProvider>();
            if (characterServiceProvider != null)
            {
                CarrierCharacter state = characterServiceProvider.StateMachine.GetState<CarrierCharacter>();
                if (state != null)
                {
                    bool isCarrying = state.IsActive;
                    GameObject[] array = Physics.OverlapSphere(player.transform.position, _radius).Select(x => x.gameObject).Where(x => x != null && x.tag == (isCarrying ? "LootCarry" : "Loot")).ToArray();
                    if (array.Length > 0)
                    {
                        foreach (GameObject gameObject in array)
                        {
                            if (gameObject?.name == null ) continue;
                            if (isCarrying && gameObject.name.Contains(state.CarriedObject.name))
                            {
                                yield return new WaitForSeconds(_hugePickupDelay);
                                state.TryToPickUpAdditionalObject(gameObject);
                                break;
                            }
                            else if (!isCarrying)
                            {
                                Loot loot = gameObject.GetComponent<Loot>();
                                if (loot)
                                {
                                    if (!player.TryGetComponent<CharacterInventory>(out var inventory)) break;
                                    ItemObject item = loot.LootSet.Stack?.Object;
                                    if (item == null) continue;
                                    if (inventory.DoesItemRequireSpaceToBeAdded(item) && inventory.Storage.IsFull && !inventory.Storage.HasVacantStack(item)) break;
                                    PickupLootAction pickupLootAction = (PickupLootAction)loot.Actions[0];
                                    collect_loot(gameObject, player, pickupLootAction);
                                }

                            }
                        }
                    }
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