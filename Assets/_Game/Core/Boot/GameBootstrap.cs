// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using UnityEngine;
using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    using UnityEngine;

    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private DefsCatalog _defsCatalog; // ScriptableObject listing all defs roots (optional)
        [SerializeField] private bool _autoStartRun = true;

        private GameServices _services;
        private GameLoop _loop;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _services = GameServicesFactory.Create(_defsCatalog);
            _loop = new GameLoop(_services);

            if (_autoStartRun)
                _loop.StartNewRun(seed: 12345); // TODO: seed source UI
        }

        private void Update()
        {
            _loop.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _loop.Dispose();
        }
    }
}
