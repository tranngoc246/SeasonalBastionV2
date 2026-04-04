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
        private readonly GameServices _s;

        // jobId -> remaining craft time
        private readonly Dictionary<int, float> _remain = new();

        private const string DefaultAmmoRecipeId = "ForgeAmmo";
        private string _ammoRecipeId;

        public CraftAmmoExecutor(GameServices s)
        {
            _s = s;
            _ammoRecipeId = ResolveAmmoRecipeIdOrDefault();
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null || _s.DataRegistry == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var forge = job.Workplace;
            if (forge.Value == 0 || !_s.WorldState.Buildings.Exists(forge))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var bs = _s.WorldState.Buildings.Get(forge);
            if (!bs.IsConstructed)
                return false;

            // Move to Forge ENTRY (driveway)
            var entry = EntryCellUtil.GetApproachCellForBuilding(_s, bs, npcState.Cell);

            job.TargetCell = entry;
            job.Status = JobStatus.InProgress;

            bool arrived = _s.AgentMover.StepToward(ref npcState, entry, dt);
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
                int outCap = _s.StorageService.GetCap(forge, recipe.OutputType);
                int outCur = _s.StorageService.GetAmount(forge, recipe.OutputType);
                if (outCap <= 0 || (outCap - outCur) < recipe.OutputAmount)
                {
                    // Not enough output space -> cancel, AmmoService should retry later.
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                // Need local inputs IN FORGE
                int inCur = _s.StorageService.GetAmount(forge, recipe.InputType);
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

                        int cur = _s.StorageService.GetAmount(forge, c.Resource);
                        if (cur < c.Amount)
                        {
                            job.Status = JobStatus.Cancelled;
                            Cleanup(jid);
                            return true;
                        }
                    }
                }

                // Consume main input
                int remIn = _s.StorageService.Remove(forge, recipe.InputType, recipe.InputAmount);
                if (remIn > 0)
                    _s.EventBus?.Publish(new ResourceSpentEvent(recipe.InputType, remIn, forge));

                // Consume extra inputs
                if (extras != null && extras.Length > 0)
                {
                    for (int i = 0; i < extras.Length; i++)
                    {
                        var c = extras[i];
                        if (c == null || c.Amount <= 0) continue;

                        int remX = _s.StorageService.Remove(forge, c.Resource, c.Amount);
                        if (remX > 0)
                            _s.EventBus?.Publish(new ResourceSpentEvent(c.Resource, remX, forge));
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
            _s.StorageService.Add(forge, recipeFinish.OutputType, recipeFinish.OutputAmount);
            InteractionCellExitHelper.TryStepOffBuildingEntry(_s, ref npcState, bs, dt);

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
                recipe = _s.DataRegistry.GetRecipe(rid);
                return recipe != null;
            }
            catch
            {
                // fallback once to default
                if (!string.Equals(rid, DefaultAmmoRecipeId, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        recipe = _s.DataRegistry.GetRecipe(DefaultAmmoRecipeId);
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
            var rid = _s?.Balance?.AmmoRecipeId;
            if (!string.IsNullOrWhiteSpace(rid))
                return rid.Trim();

            return DefaultAmmoRecipeId;
        }
    }
}