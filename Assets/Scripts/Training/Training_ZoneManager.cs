using UnityEngine;
using ArcaneVR.Input;
using System.Collections.Generic;

namespace ArcaneVR.Training
{
    public class Training_ZoneManager : MonoBehaviour
    {
        [SerializeField] private Training_UIController uiController;
        
        [Header("Zone 1: Movement Course")]
        [SerializeField] private Training_Waypoint[] waypoints;
        [SerializeField] private GameObject zone1Passage;
        private int currentWaypointIndex = 0;

        [Header("Zone 2: Shooting Range")]
        [SerializeField] private Training_MovingTarget[] targets;
        [SerializeField] private int targetsRequired = 10;
        [SerializeField] private GameObject zone2Passage;
        private int targetHitCount = 0;

        [Header("Zone 3: Defense Arena")]
        [SerializeField] private Training_DefenseLogic defenseLogic;
        [SerializeField] private Training_ProjectileSpawner[] defenseSpawners;
        [SerializeField] private int defenseRequired = 5;
        private int defenseSuccessCount = 0;

        void Start()
        {
            SetupZone1();
            SetupZone2();
            SetupZone3();
        }

        private void SetupZone1()
        {
            if (zone1Passage != null) zone1Passage.SetActive(true);
            for (int i = 0; i < waypoints.Length; i++)
            {
                int index = i;
                waypoints[i].SetState(i == 0);
                waypoints[i].OnReached += (wp) => HandleWaypointReached(index);
            }
        }

        private void HandleWaypointReached(int index)
        {
            if (index != currentWaypointIndex) return;

            currentWaypointIndex++;
            uiController.UpdateScore(20);

            if (currentWaypointIndex < waypoints.Length)
            {
                waypoints[currentWaypointIndex].SetState(true);
            }
            else
            {
                // Zone 1 Cleared
                if (zone1Passage != null) zone1Passage.SetActive(false);
                uiController.SetDefenseStatus("ZONE 1 CLEAR!", Color.green);
            }
        }

        private void SetupZone2()
        {
            if (zone2Passage != null) zone2Passage.SetActive(true);
            foreach (var target in targets)
            {
                if (target != null)
                    target.OnHit += HandleTargetHit;
            }
        }

        private void HandleTargetHit(int score)
        {
            targetHitCount++;
            uiController.UpdateScore(score);
            uiController.SetDefenseStatus($"TARGETS: {targetHitCount}/{targetsRequired}", Color.white);

            if (targetHitCount >= targetsRequired)
            {
                if (zone2Passage != null) zone2Passage.SetActive(false);
                uiController.SetDefenseStatus("ZONE 2 CLEAR!", Color.green);
            }
        }

        private void SetupZone3()
        {
            if (defenseLogic != null)
            {
                defenseLogic.OnDefenseSuccess += HandleDefenseSuccess;
                defenseLogic.OnDefenseFailure += () => uiController.SetDefenseStatus("HIT!", Color.red);
            }
        }

        private void HandleDefenseSuccess()
        {
            defenseSuccessCount++;
            uiController.UpdateScore(50);
            uiController.SetDefenseStatus($"BLOCKED: {defenseSuccessCount}/{defenseRequired}", Color.cyan);

            if (defenseSuccessCount >= defenseRequired)
            {
                uiController.SetDefenseStatus("TRAINING COMPLETE!", Color.yellow);
                // Stop spawners or something
                foreach (var spawner in defenseSpawners) spawner.enabled = false;
            }
        }

        void Update()
        {
            UpdateCooldowns();
        }

        private void UpdateCooldowns()
        {
            float dashCD = Mathf.PingPong(Time.time * 0.5f, 1f);
            float pullCD = Mathf.PingPong(Time.time * 0.3f, 1f);
            uiController.UpdateDashCooldown(dashCD);
            uiController.UpdatePullCooldown(pullCD);
        }
    }
}

