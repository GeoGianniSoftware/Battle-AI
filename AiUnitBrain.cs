using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FM.BattleAI
{
    [System.Serializable]
    public class AiUnitBrain
    {
        public enum HealthState
        {
            Normal,
            Cohesionless,
            Wounded,
            Critical
        }

        [HideInInspector]
        public UnitInfo attachedUnit;
        public int attachedUnitID;
        public bool deepDebug;

        public List<PlannedOrder> aiPlans;
        public int moveOrderCount;
        public int attackOrderCount;

        public bool peakEnabled = false;

        public AiUnitState currentUnitState = AiUnitState.Idle;
        public AiUnitGoal currentUnitGoal = AiUnitGoal.Domination;

        public HealthState currentHealthState { get { return getCurrentHealthState(); } }
        public int[] currentDestination;
        public int currentDirection;
        public int currentFormation;
        public int currentTarget = -1;
        public List<int[]> tilePositions = new List<int[]>();
        public bool hasChangedTiles = false;
        public bool hasChangedFormation = false;
        public bool hasCharged = false;
        public bool holdGround = false;
        public bool lastCycle = false;
        public bool hasFaWOrder = false;
        public bool Breech;
        public bool Rifled;
        public bool Cumbersome;
        public bool Efficient;

        public AiUnitBrain(UnitInfo unit, bool aggressive = true) {
            if (aggressive) {
                currentUnitGoal = AiUnitGoal.Domination;
            }
            else {
                currentUnitGoal = AiUnitGoal.Territory;
            }

            attachedUnit = unit;
            attachedUnitID = unit.unitID;
            aiPlans = new List<PlannedOrder>();
            currentDirection = attachedUnit.direction;
            currentFormation = attachedUnit.formation;

            Breech = AiHasPerk(5);
            Rifled = AiHasPerk(9);
            Cumbersome = AiHasPerk(8);
            Efficient = AiHasPerk(3);
        }

        #region Orders
        public void AddPlanLite(PlannedOrder plan) {

            if (plan.orderType == PlannedOrderType.FormationOrder) {
                hasChangedFormation = true;
                currentFormation = plan.formation;
                if (currentFormation == -1)
                    currentFormation = attachedUnit.formation;
            }

            if (plan.orderType == PlannedOrderType.MoveOrder || plan.orderType == PlannedOrderType.FaceOrder || plan.orderType == PlannedOrderType.ChargeOrder)
                tilePositions.Add(plan.orderNextTilePos);
            else if (plan.orderTargetPos != null && (plan.orderType != PlannedOrderType.FireOrder && plan.orderType != PlannedOrderType.FireAtWill)) {
                tilePositions.Add(plan.orderTargetPos);
            }

            if (plan.orderType == PlannedOrderType.FaceOrder || plan.orderType == PlannedOrderType.MoveOrder) {
                currentDirection = plan.faceDirection;
                if (currentDirection == -1)
                    currentDirection = attachedUnit.direction;
            }

            if (plan.orderType == PlannedOrderType.ChargeOrder)
                hasCharged = true;

            if (plan.orderType == PlannedOrderType.MoveOrder)
                hasChangedTiles = true;

            if (plan.orderType == PlannedOrderType.MoveOrder || plan.orderType == PlannedOrderType.FormationOrder || plan.orderType == PlannedOrderType.FaceOrder)
                moveOrderCount++;

            if (plan.orderType == PlannedOrderType.FireOrder || plan.orderType == PlannedOrderType.ChargeOrder)
                attackOrderCount++;
        }
        bool brainContainsSameOrder(PlannedOrder order) {
            for (int x = 0; x < aiPlans.Count; x++) {
                if (aiPlans[x].isSame(order))
                    return true;
            }

            return false;
        }

        public void AddPlan(PlannedOrder plan) {
            if (brainContainsSameOrder(plan)) {
                Debug.Log("[AI] Duplicate order found. Removing!");

                return;
            }

            if (aiPlans != null)
                aiPlans.Add(plan);

            if (plan.orderType == PlannedOrderType.FireAtWill) {
                hasFaWOrder = true;
            }

            if (plan.orderType == PlannedOrderType.FormationOrder) {
                hasChangedFormation = true;
                currentFormation = plan.formation;
                if (currentFormation == -1)
                    currentFormation = attachedUnit.formation;
            }

            if (plan.orderType == PlannedOrderType.MoveOrder || plan.orderType == PlannedOrderType.FaceOrder || plan.orderType == PlannedOrderType.ChargeOrder)
                tilePositions.Add(plan.orderNextTilePos);
            else if (plan.orderTargetPos != null && (plan.orderType != PlannedOrderType.FireOrder && plan.orderType != PlannedOrderType.FireAtWill)) {
                tilePositions.Add(plan.orderTargetPos);
            }

            if (plan.orderType == PlannedOrderType.FaceOrder || plan.orderType == PlannedOrderType.MoveOrder) {
                currentDirection = plan.faceDirection;
                if (currentDirection == -1)
                    currentDirection = attachedUnit.direction;
            }

            if (plan.orderType == PlannedOrderType.ChargeOrder)
                hasCharged = true;

            if (plan.orderType == PlannedOrderType.MoveOrder)
                hasChangedTiles = true;

            if (plan.orderType == PlannedOrderType.MoveOrder || plan.orderType == PlannedOrderType.FormationOrder || plan.orderType == PlannedOrderType.FaceOrder)
                moveOrderCount++;

            if (plan.orderType == PlannedOrderType.FireOrder || plan.orderType == PlannedOrderType.ChargeOrder)
                attackOrderCount++;
        }

        public void ClearPlans() {
            if (aiPlans != null && aiPlans.Count > 0) {
                aiPlans.Clear();
            }
            moveOrderCount = 0;
            attackOrderCount = 0;
            hasChangedTiles = false;
            holdGround = false;
            hasFaWOrder = false;
            hasChangedFormation = false;
            lastCycle = false;
            currentDestination = null;
            tilePositions.Clear();
            tilePositions.Add(attachedUnit.TilePosition);
            currentFormation = attachedUnit.formation;
        }
        #endregion

        public int[] getPlannedPosition() {
            if (attachedUnit != null) {
                if (tilePositions != null && tilePositions.Count > 0)
                    return tilePositions[tilePositions.Count - 1];
                else
                    return attachedUnit.TilePosition;
            }

            return null;
        }

        public HealthState getCurrentHealthState() {
            HealthState currentState = HealthState.Normal;

            if (attachedUnit != null) {
                if (attachedUnit.hitpoints < attachedUnit.unitType.maxHitpoints)
                    currentState = HealthState.Wounded;

                if (attachedUnit.cohesion > 0 && attachedUnit.hitpoints == attachedUnit.unitType.maxHitpoints)
                    currentState = HealthState.Normal;
                else if (attachedUnit.cohesion <= 0 && attachedUnit.hitpoints == attachedUnit.unitType.maxHitpoints)
                    currentState = HealthState.Cohesionless;
                else if (attachedUnit.cohesion <= 0 && attachedUnit.hitpoints <= attachedUnit.unitType.maxHitpoints / 2) {
                    currentState = HealthState.Critical;
                }
            }

            return currentState;
        }

        public bool isCharger(int[] tileToCharge = null) {

            UnitInfo info = getTargetInfo();
            if (tileToCharge != null) {
                UnitInfo inf = CMD.CMND.cmd_TileMap.GetEnemyOnTile(tileToCharge, attachedUnit.team);

                if (inf != null)
                    info = inf;
            }

            bool worthIt = false;

            if (info != null) {
                int dir = CMD.CMND.cmd_UnitManager.CheckFlanking(attachedUnit, info);
                if (dir == 2 || dir == 3 || info.perkModifiers.chargeVulnerable || attachedUnit.HasPerk(0) || attachedUnit.HasPerk(2))
                    worthIt = true;
            }


            if ((worthIt && attachedUnit.classification == UnitData.UnitClass.Cavalry) || (worthIt && (attachedUnit.HasPerk("shock") || attachedUnit.HasPerk("disciplined_melee")))) {
                return true;
            }

            return false;
        }

        public List<UnitInfo> getEnemiesInTile(int AiTeamID) {
            List<UnitInfo> enemiesInTile = new List<UnitInfo>();
            for (int i = 0; i < attachedUnit.GetTile().count_AllUnits; i++) {
                UnitInfo tempEnemy = attachedUnit.GetTile().units_All[i];
                if (tempEnemy != null && tempEnemy.team != AiTeamID)
                    enemiesInTile.Add(tempEnemy);
            }

            return enemiesInTile;
        }

        public List<UnitInfo> getAlliesInTile(int AiTeamID) {
            List<UnitInfo> alliesInTile = new List<UnitInfo>();
            for (int i = 0; i < attachedUnit.GetTile().count_AllUnits; i++) {

                UnitInfo newFriend = attachedUnit.GetTile().units_All[i];
                if (newFriend.unitID == attachedUnit.unitID)
                    continue;

                if (newFriend != null && newFriend.team == AiTeamID)
                    alliesInTile.Add(newFriend);
            }

            return alliesInTile;
        }

        public int getMaxMoveCount() {
            if (attachedUnit.HasPerk("cumbersome")) {
                if (attackOrderCount > 0)
                    return 0;

            }


            if (attachedUnit.HasPerk("efficiency")) {
                return 2;
            }
            else {
                return 1;
            }
        }

        public void RefreshBrain() {
            tilePositions.Clear();
            tilePositions.Add(attachedUnit.TilePosition);
            currentFormation = attachedUnit.formation;
        }

        public UnitInfo getTargetInfo() {
            if (currentTarget == -1)
                return null;

            return CMD.CMND.cmd_Units.GetUnit(currentTarget);
        }

        public bool AiHasPerk(int id) {
            return attachedUnit.HasPerk(id);
        }

        public bool hasMovesRemaning(bool changedFormations = false) {
            if (hasChangedFormation)
                changedFormations = true;

            if (changedFormations && (!AiHasPerk(3) || moveOrderCount > 1))
                return false;

            if (attackOrderCount > 0 && Cumbersome)
                return false;

            if (moveOrderCount >= getMaxMoveCount())
                return false;

            if (attachedUnit.UnitHasTileMod(1))
                return false;

            return true;
        }

        public bool hasAttacksRemaning() {
            if (moveOrderCount > 0 && Cumbersome)
                return false;

            if ((attackOrderCount > 0 && !Breech) || attackOrderCount > 1)
                return false;

            if (attachedUnit.UnitHasTileMod(1))
                return false;

            return true;
        }
    }

}