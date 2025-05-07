// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
    static class Utility
    {
        static int s_LastCheckedForWater = -1;
        static WaterRenderer s_Water;
        public static WaterRenderer Water
        {
            get
            {
                if (s_LastCheckedForWater == Time.frameCount)
                {
                    return s_Water;
                }

                s_LastCheckedForWater = Time.frameCount;

                // Gets the water from the current stage.
                return s_Water = UnityEditor.SceneManagement.StageUtility
                    .GetCurrentStageHandle()
                    .FindComponentsOfType<WaterRenderer>()
                    .FirstOrDefault();
            }
        }

        [InitializeOnEnterPlayMode]
        static void OnEnterPlayMode()
        {
            s_LastCheckedForWater = -1;
        }
    }
}
