using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class EnemyLifecycleResolver
    {
        private readonly IWorldState _worldState;
        private readonly Dictionary<int, float> _attackCooldowns;
        private readonly Dictionary<int, int> _pathFailStreaks;
        private readonly List<int> _tmpEnemyKeys;
        private float _pruneAccumulator;

        public EnemyLifecycleResolver(
            IWorldState worldState,
            Dictionary<int, float> attackCooldowns,
            Dictionary<int, int> pathFailStreaks,
            List<int> tmpEnemyKeys)
        {
            _worldState = worldState;
            _attackCooldowns = attackCooldowns;
            _pathFailStreaks = pathFailStreaks;
            _tmpEnemyKeys = tmpEnemyKeys;
        }

        public void CleanupEnemy(EnemyId enemyId)
        {
            var enemies = _worldState?.Enemies;
            if (enemies == null)
                return;

            _attackCooldowns.Remove(enemyId.Value);
            _pathFailStreaks.Remove(enemyId.Value);
            enemies.Destroy(enemyId);
        }

        public void PruneEnemyCaches(float dt)
        {
            var enemies = _worldState?.Enemies;
            if (enemies == null)
                return;

            _pruneAccumulator += dt;
            if (_pruneAccumulator < 3f)
                return;
            _pruneAccumulator = 0f;

            int aliveCount = enemies.Count;
            if (aliveCount <= 0)
            {
                _attackCooldowns.Clear();
                _pathFailStreaks.Clear();
                return;
            }

            bool shouldPruneAttack = _attackCooldowns.Count > aliveCount * 2;
            bool shouldPruneFailStreak = _pathFailStreaks.Count > aliveCount * 2;
            if (!shouldPruneAttack && !shouldPruneFailStreak)
                return;

            if (shouldPruneAttack)
                PruneMissingEnemyKeys(_attackCooldowns, enemies);

            if (shouldPruneFailStreak)
                PruneMissingEnemyKeys(_pathFailStreaks, enemies);
        }

        private void PruneMissingEnemyKeys<TValue>(Dictionary<int, TValue> map, IEnemyStore enemies)
        {
            _tmpEnemyKeys.Clear();
            foreach (var kv in map)
            {
                if (!enemies.Exists(new EnemyId(kv.Key)))
                    _tmpEnemyKeys.Add(kv.Key);
            }

            for (int i = 0; i < _tmpEnemyKeys.Count; i++)
                map.Remove(_tmpEnemyKeys[i]);
        }
    }
}
