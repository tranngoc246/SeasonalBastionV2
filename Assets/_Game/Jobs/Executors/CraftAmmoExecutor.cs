using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Craft ammo at Forge using recipe from Recipes.json (DataRegistry).
    /// - Moves to Forge entry.
    /// - On start: checks output space, checks local inputs (incl. extra inputs), consumes inputs once.
    /// - Counts down craft time.
    /// - On finish: adds output to Forge, completes job.
    /// </summary>
    public sealed class CraftAmmoExecutor : IJobExecutor
    {
        private readonly IWorldState _worldState;
        private readonly IStorageService _storageService;
        private readonly IAgentMoverRuntime _agentMover;
        private readonly IDataRegistry _dataRegistry;
        private readonly IEventBus _eventBus;
        private readonly BalanceService _balance;
        private readonly IGridMap _gridMap;

        // jobId -> remaining craft time
        private readonly Dictionary<int, float> _remain = new();

        private const string DefaultAmmoRecipeId = "ForgeAmmo";
        private string _ammoRecipeId;

        public CraftAmmoExecutor(
            IWorldState worldState,
            IStorageService storageService,
            IAgentMoverRuntime agentMover,
            IDataRegistry dataRegistry,
            IEventBus eventBus,
            BalanceService balance,
            IGridMap gridMap)
        {
            _worldState = worldState;
            _storageService = storageService;
            _agentMover = agentMover;
            _dataRegistry = dataRegistry;
            _eventBus = eventBus;
            _balance = balance;
            _gridMap = gridMap;
            _ammoRecipeId = ResolveAmmoRecipeIdOrDefault();
        }

        public CraftAmmoExecutor(GameServices s)
            : this(s?.WorldState, s?.StorageService, s?.AgentMover, s?.DataRegistry, s?.EventBus, s?.Balance, s?.GridMap)
        {
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_worldState == null || _storageService == null || _agentMover == null || _dataRegistry == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var forge = job.Workplace;
            if (forge.Value == 0 || !_worldState.Buildings.Exists(forge))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var bs = _worldState.Buildings.Get(forge);
            if (!bs.IsConstructed)
                return false;

            // Move to Forge ENTRY (driveway)
            var entry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, bs, npcState.Cell);

            job.TargetCell = entry;
            job.Status = JobStatus.InProgress;

            bool arrived = _agentMover.StepToward(ref npcState, entry, dt);
            if (!arrived)
                return true;

            int jid = job.Id.Value;

            // Start craft: consume inputs once
            if (!_remain.TryGetValue(jid, out var rem))
            {
                if (!TryGetRecipe(out var recipe))
                {
                    job.Status = JobStatus.Failed;
                    Cleanup(jid);
                    return true;
                }

                // Sanity: this executor is intended for Ammo recipe
                if (recipe.OutputType != ResourceType.Ammo)
                {
                    // Fail fast so you notice JSON mismatch (common mistake: wrong enum indices)
                    // Fix by setting outputType in Recipes.json to Ammo (enum value 5).
                    job.Status = JobStatus.Failed;
                    Cleanup(jid);
                    return true;
                }

                // Need local output space IN FORGE
                int outCap = _storageService.GetCap(forge, recipe.OutputType);
                int outCur = _storageService.GetAmount(forge, recipe.OutputType);
                if (outCap <= 0 || (outCap - outCur) < recipe.OutputAmount)
                {
                    // Not enough output space -> cancel, AmmoService should retry later.
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                // Need local inputs IN FORGE
                int inCur = _storageService.GetAmount(forge, recipe.InputType);
                if (inCur < recipe.InputAmount)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                var extras = recipe.ExtraInputs;
                if (extras != null && extras.Length > 0)
                {
                    for (int i = 0; i < extras.Length; i++)
                    {
                        var c = extras[i];
                        if (c == null || c.Amount <= 0) continue;

                        int cur = _storageService.GetAmount(forge, c.Resource);
                        if (cur < c.Amount)
                        {
                            job.Status = JobStatus.Cancelled;
                            Cleanup(jid);
                            return true;
                        }
                    }
                }

                // Consume main input
                int remIn = _storageService.Remove(forge, recipe.InputType, recipe.InputAmount);
                if (remIn > 0)
                    _eventBus?.Publish(new ResourceSpentEvent(recipe.InputType, remIn, forge));

                // Consume extra inputs
                if (extras != null && extras.Length > 0)
                {
                    for (int i = 0; i < extras.Length; i++)
                    {
                        var c = extras[i];
                        if (c == null || c.Amount <= 0) continue;

                        int remX = _storageService.Remove(forge, c.Resource, c.Amount);
                        if (remX > 0)
                            _eventBus?.Publish(new ResourceSpentEvent(c.Resource, remX, forge));
                    }
                }

                rem = recipe.CraftTimeSec > 0f ? recipe.CraftTimeSec : 0.1f;
                _remain[jid] = rem;
            }

            // Work time
            rem -= dt;
            if (rem > 0f)
            {
                _remain[jid] = rem;
                return true;
            }

            // Finish: deposit output to forge
            if (!TryGetRecipe(out var recipeFinish))
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            // Deposit
            _storageService.Add(forge, recipeFinish.OutputType, recipeFinish.OutputAmount);
            InteractionCellExitHelper.TryStepOffBuildingEntry(_dataRegistry, _gridMap, _agentMover, ref npcState, bs, dt);

            job.ResourceType = recipeFinish.OutputType;
            job.Amount = recipeFinish.OutputAmount;
            job.Status = JobStatus.Completed;

            Cleanup(jid);
            return true;
        }

        private void Cleanup(int jobId)
        {
            _remain.Remove(jobId);
        }

        private bool TryGetRecipe(out RecipeDef recipe)
        {
            recipe = null;

            string rid = _ammoRecipeId;
            if (string.IsNullOrEmpty(rid)) rid = DefaultAmmoRecipeId;

            try
            {
                recipe = _dataRegistry.GetRecipe(rid);
                return recipe != null;
            }
            catch
            {
                // fallback once to default
                if (!string.Equals(rid, DefaultAmmoRecipeId, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        recipe = _dataRegistry.GetRecipe(DefaultAmmoRecipeId);
                        return recipe != null;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[CraftAmmoExecutor] Failed to load fallback ammo recipe '{DefaultAmmoRecipeId}' after recipe '{rid}' lookup failed: {ex}");
                    }
                }

                return false;
            }
        }

        private string ResolveAmmoRecipeIdOrDefault()
        {
            var rid = _balance?.AmmoRecipeId;
            if (!string.IsNullOrWhiteSpace(rid))
                return rid.Trim();

            return DefaultAmmoRecipeId;
        }
    }
}
