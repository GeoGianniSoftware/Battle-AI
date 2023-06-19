using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FM.BattleAI
{
    #region Ai Tokens
    public class AiTargetToken
    {
        public UnitInfo refUnit;

        public List<AiDamageToken> damageTokens = new List<AiDamageToken>();

        public List<int> targetTallies = new List<int>();

        public AiTargetToken(UnitInfo target) {
            refUnit = target;
        }

        public bool containsDamagerID(int ID) {
            for (int i = 0; i < damageTokens.Count; i++) {
                if (damageTokens[i].brainID == ID)
                    return true;
            }

            return false;
        }

        public bool containsTallyID(int ID) {
            if (targetTallies == null || targetTallies.Count == 0)
                return false;

            return targetTallies.Contains(ID);
        }

        public void AddDamageToken(int id, int amt) {
            damageTokens.Add(new AiDamageToken(id, amt));
        }

        public void addTargetTally(FM.BattleAI.AiUnitBrain brain) {
            if (!targetTallies.Contains(brain.attachedUnitID))
                targetTallies.Add(brain.attachedUnitID);
        }

        public int getUniqueDamagerCount() {
            List<int> ids = new List<int>();

            foreach (AiDamageToken damageToken in damageTokens) {
                if (!ids.Contains(damageToken.brainID))
                    ids.Add(damageToken.brainID);
            }

            return ids.Count;
        }
    }

    public class AiDamageToken
    {
        public int brainID;
        public int plannedDamage;

        public AiDamageToken(int id, int amt) {
            brainID = id;
            plannedDamage = amt;
        }
    }
    #endregion

    #region Enums
    public enum PlannedOrderType
    {

        MoveOrder,
        FireOrder,
        ChargeOrder,
        FormationOrder,
        FaceOrder,
        FireAtWill
    }
    public enum AiUnitState
    {
        Idle,
        Pathing,
        InCombat,
        Locked,
        Retreat,
        Melee
    }
    public enum AiUnitGoal
    {
        Domination,
        Territory,
        Objective
    }
    public enum AiAgressionType
    {
        VeryDefensive,
        Defensive,
        Balanced,
        Offensive,
        VeryOffensive
    }
    public enum AiDifficultyType
    {
        VeryEasy,
        Easy,
        Average,
        Hard,
        VeryHard
    }
    #endregion

    public class CMD_AI_Controller2 : MonoBehaviour
    {

        public int colorCheckID;

        #region Variables
        public CMD CMND;

        [Header("Basic Settings")]
        public E_AI_General currentGeneral;

        public bool debugTerritory;
        public AiTerritoryData[,] territoryData = null;


        public bool aiPlayedGame = false;
        public bool aiEnabled;
        public int deploymentZone;
        public int AiID;

        public Color aiColor;

        public int AiPlayerID;
        public int AiFactionID;
        public int AiTeamID = 1;
        [Range(0f, 1f)]
        public float AiTactic;
        [Range(0f, 1f)]
        public float AiAggressiveness = 0.5f;

        public bool isCheater = false;

        public bool turnComplete = false;
        public bool DEBUG_AIEndsTurn;
        public bool DEBUG_Errors;

        UnitInfo[] livingUnits;
        UnitInfo[] enemyUnits;
        public AiUnitBrain[] unitBrains;
        int enemyID = 1;

        public List<AiTargetToken> currentTargets = new List<AiTargetToken>();

        List<AiUnitBrain> unitsToDelay = new List<AiUnitBrain>();

        #endregion

        #region Unity
        private void OnDrawGizmos() {
            if (aiEnabled && debugTerritory) {
                for (int x = 0; x < territoryData.GetLength(0); x++) {
                    for (int y = 0; y < territoryData.GetLength(1); y++) {
                        AiTerritoryData tData = territoryData[x, y];

                        if (tData.baseWeight < 1f)
                            Gizmos.color = Color.black;

                        if (tData.baseWeight == 1f)
                            Gizmos.color = Color.red;

                        if (tData.baseWeight > 2f)
                            Gizmos.color = Color.yellow;

                        if (tData.baseWeight > 4f)
                            Gizmos.color = Color.blue;

                        if (tData.baseWeight > 8f)
                            Gizmos.color = Color.green;

                        if (tData.isOwnedByController)
                            Gizmos.color = Color.white;




                        Gizmos.DrawCube(CMND.cmd_TileMap.TilePositionToTileSurface(tData.position), Vector3.one * .1f);
                    }
                }
            }
        }

        void Update() {
            if (CMND.cmd_TurnManager.currentTurnNumber > 0) {
                AiTurnTick();
            }




        }
        #endregion

        #region Thresholds


        public void PrintAIDebug(string debug, int unitID = -1) {
            if (!DEBUG_Errors)
                return;

            if (unitID != -1)
                print("[AI] AI#" + AiID + ", unit #" + unitID + ": " + debug);
            else {
                print("[AI] AI#" + AiID + ": " + debug);
            }
        }

        public bool InAggressionTreshold(AiAgressionType type) {
            switch (type) {
                default:
                case AiAgressionType.VeryDefensive:
                    return AiAggressiveness < .2f;
                case AiAgressionType.Defensive:
                    return AiAggressiveness > .2f && AiAggressiveness < .4f;
                case AiAgressionType.Balanced:
                    return AiAggressiveness > .4f && AiAggressiveness < .6f;
                case AiAgressionType.Offensive:
                    return AiAggressiveness > .6f && AiAggressiveness < .8f;
                case AiAgressionType.VeryOffensive:
                    return AiAggressiveness > .8f;
            }
        }

        public bool UnderAtAggressionTreshold(AiAgressionType type) {
            switch (type) {
                default:
                case AiAgressionType.VeryDefensive:
                    return AiAggressiveness <= .2f;
                case AiAgressionType.Defensive:
                    return AiAggressiveness <= .4f;
                case AiAgressionType.Balanced:
                    return AiAggressiveness <= .6f;
                case AiAgressionType.Offensive:
                    return AiAggressiveness <= .8f;
                case AiAgressionType.VeryOffensive:
                    return AiAggressiveness <= 1f;
            }
        }

        public bool OverAtAggressionTreshold(AiAgressionType type) {
            switch (type) {
                default:
                case AiAgressionType.VeryDefensive:
                    return AiAggressiveness >= .2f;
                case AiAgressionType.Defensive:
                    return AiAggressiveness >= .4f;
                case AiAgressionType.Balanced:
                    return AiAggressiveness >= .6f;
                case AiAgressionType.Offensive:
                    return AiAggressiveness >= .8f;
                case AiAgressionType.VeryOffensive:
                    return AiAggressiveness >= 1f;
            }
        }

        public bool InDifficultyTreshold(AiDifficultyType type) {
            switch (type) {
                default:
                case AiDifficultyType.VeryEasy:
                    return AiTactic < .2f;
                case AiDifficultyType.Easy:
                    return AiTactic > .2f && AiTactic < .4f;
                case AiDifficultyType.Average:
                    return AiTactic > .4f && AiTactic < .6f;
                case AiDifficultyType.Hard:
                    return AiTactic > .6f && AiTactic < .8f;
                case AiDifficultyType.VeryHard:
                    return AiTactic > .8f;
            }
        }
        public bool UnderAtDifficultyTreshold(AiDifficultyType type) {
            switch (type) {
                default:
                case AiDifficultyType.VeryEasy:
                    return AiTactic <= .2f;
                case AiDifficultyType.Easy:
                    return AiTactic <= .4f;
                case AiDifficultyType.Average:
                    return AiTactic <= .6f;
                case AiDifficultyType.Hard:
                    return AiTactic <= .8f;
                case AiDifficultyType.VeryHard:
                    return AiTactic <= 1f;
            }
        }
        public bool OverAtDifficultyTreshold(AiDifficultyType type) {
            switch (type) {
                default:
                case AiDifficultyType.VeryEasy:
                    return AiTactic >= .2f;
                case AiDifficultyType.Easy:
                    return AiTactic >= .4f;
                case AiDifficultyType.Average:
                    return AiTactic >= .6f;
                case AiDifficultyType.Hard:
                    return AiTactic >= .8f;
                case AiDifficultyType.VeryHard:
                    return AiTactic <= 1f;
            }
        }
        #endregion

        #region Ai Turn Logic

        int[] CalculateDefensivePosition(AiUnitBrain brain) {
            int[] tilePos = brain.getPlannedPosition();
            TileTerrainInfo originInfo = CMND.cmd_TileMap.GetTile(tilePos, true);

            float currentMax = 0;
            if (originInfo != null)
                currentMax = originInfo.coverLevel + originInfo.elevation;


            int[] maxIndex = tilePos;

            int unitMoveRange = brain.attachedUnit.GetTotalSpeed() * 2;

            for (int x = -unitMoveRange; x < unitMoveRange + 1; x++) {
                for (int y = -unitMoveRange; y < unitMoveRange + 1; y++) {
                    int[] tempPos = new int[] { tilePos[0] + x, tilePos[1] + y };

                    if (withinMap(tempPos)) {

                        AiTerritoryData tData = territoryData[tempPos[0], tempPos[1]];

                        float defenseIndex = tData.Calculate(this, brain);
                        if (tData.plannedOccupants.Count == 0 && defenseIndex > currentMax) {
                            maxIndex = tempPos;
                            currentMax = defenseIndex;
                            tData.plannedOccupants.Add(brain.attachedUnitID);
                        }
                    }


                }
            }
            if (maxIndex == null) {
                print("Defensive Position Calculation failed for AI#:" + AiID);
            }

            return maxIndex;
        }

        public void CalculateBrainTarget(AiUnitBrain brain) {
            int targetID = -1;
            switch (brain.currentUnitGoal) {
                case AiUnitGoal.Domination:
                    UnitInfo targetInfo = CalculateOptimalTarget(brain, brain.getPlannedPosition());
                    if (targetInfo != null)
                        targetID = targetInfo.unitID;
                    break;
                case AiUnitGoal.Territory:
                case AiUnitGoal.Objective:
                    //Get Unit Target
                    UnitInfo nearestEnemy = getNearestEnemy(brain);
                    if (nearestEnemy != null)
                        targetID = nearestEnemy.unitID;

                    break;
            }

            if (targetID != -1) {

                AiTargetToken aiTargetData = getUnitTargetData(targetID);
                if (aiTargetData == null) {
                    aiTargetData = CreateTargetToken(brain, targetID);
                }

                if (!aiTargetData.containsTallyID(brain.attachedUnitID))
                    aiTargetData.addTargetTally(brain);

                brain.currentTarget = targetID;
            }
        }

        public AiTargetToken CreateTargetToken(AiUnitBrain targeterBrain, int targetID) {
            AiTargetToken newTargetToken = new AiTargetToken(CMND.cmd_Units.GetUnit(targetID));
            newTargetToken.addTargetTally(targeterBrain);
            currentTargets.Add(newTargetToken);

            return newTargetToken;
        }

        public bool canUnitFAW(AiUnitBrain brain) {
            if (brain.currentFormation == 6)
                return false;

            bool first = OverAtDifficultyTreshold(AiDifficultyType.Easy) && brain.currentFormation != 4 && ((brain.attachedUnit.classification == UnitData.UnitClass.Artillery && brain.currentFormation != 9) || brain.attachedUnit.hasEquippedWeapon());
            bool second = brain.lastCycle && brain.attachedUnit.IsFrontline() && brain.attackOrderCount <= 0 && !brain.attachedUnit.fireAtWill;
            return (first && second);
        }
        public AiUnitState CalculateUnitState(AiUnitBrain brain) {
            AiUnitState result = AiUnitState.Idle;

            bool cumberSomeFailed = false;

            bool cumbersomeCheck = brain.attachedUnit.HasPerk(8);
            if (cumbersomeCheck && (brain.moveOrderCount > 0 || brain.attackOrderCount > 0))
                cumberSomeFailed = true;

            int[] plannedPos = getPlannedUnitPosition(brain);

            List<int[]> chargable = new List<int[]>();
            if(brain.attachedUnit.classification != UnitData.UnitClass.Artillery) {
                chargable = CMND.cmd_UnitManager.getChargableTiles(brain.attachedUnit, plannedPos);
            }

            if (brain.attachedUnit.inMeleeCombat)
                return AiUnitState.Melee;


            if (brain.currentHealthState == AiUnitBrain.HealthState.Critical || (brain.currentHealthState == AiUnitBrain.HealthState.Cohesionless && OverAtDifficultyTreshold(AiDifficultyType.Average) && UnderAtAggressionTreshold(AiAgressionType.Balanced))) {
                bool walkingDead = brain.attachedUnit.hitpoints == 1 && brain.attachedUnit.cohesion == 0;


                if (!walkingDead || (walkingDead && UnderAtDifficultyTreshold(AiDifficultyType.VeryEasy)))
                    return AiUnitState.Retreat;
            }



            switch (brain.currentUnitGoal) {
                case AiUnitGoal.Domination:
                    //Get Unit Target
                    if (cumberSomeFailed)
                        return AiUnitState.Pathing;



                    CalculateBrainTarget(brain);

                    if (brain.currentTarget == -1) {
                        if (brain.deepDebug) {
                            print("Ai #" + brain.attachedUnitID + " failed to fetch a target");
                        }

                        //Fire at will
                        bool canFaW = canUnitFAW(brain);
                        if (canFaW) {
                            brain.holdGround = true;
                            return AiUnitState.InCombat;
                        }

                        return AiUnitState.Idle;
                    }
                    else {
                        if (brain.attachedUnit == null || plannedPos == null || brain.getTargetInfo() == null) {
                            return AiUnitState.Idle;
                        }


                        UnitInfo inRange = ReturnEnemyInCone(brain, plannedPos, brain.getTargetInfo());

                        bool combatActivated = false;

                        int targetFlankOnYou = CMD.CMND.cmd_UnitManager.CheckFlanking(brain.getTargetInfo(), brain.attachedUnit);

                        bool isFlankedByEnemy = false;
                        if (targetFlankOnYou == 3 && OverAtDifficultyTreshold(AiDifficultyType.Easy))
                            isFlankedByEnemy = true;

                        if (targetFlankOnYou == 2 && OverAtDifficultyTreshold(AiDifficultyType.Hard))
                            isFlankedByEnemy = true;

                        //Combat!
                        if ((inRange != null && brain.attachedUnit.IsFrontline()) || (chargable != null && chargable.Count > 0)) {


                            combatActivated = true;
                        }
                        //Fire at will

                        if (!combatActivated) {
                            bool canFaW = canUnitFAW(brain);
                            if (canFaW && UnderAtDifficultyTreshold(AiDifficultyType.Average)) {
                                if (Random.Range(0, 2) == 0)
                                    canFaW = false;
                            }

                            if (canFaW && brain.lastCycle && inRange == null && brain.attachedUnit.IsFrontline() && brain.attackOrderCount <= 0 && !brain.attachedUnit.fireAtWill) {
                                brain.holdGround = true;
                                return AiUnitState.InCombat;
                            }
                        }


                        if (combatActivated) {
                            //Repositioning Units during combat

                            if (brain.attachedUnit.classification != UnitData.UnitClass.Artillery) {
                                float repoCalc = Random.Range(AiTactic / 3, 1f);


                                int flankDirection = 0;
                                if (brain.currentTarget != -1) {


                                    flankDirection = CMND.cmd_UnitManager.CheckFlanking(brain.attachedUnit, brain.getTargetInfo());

                                    if (brain.getTargetInfo().perkModifiers.rangeResistant && flankDirection != 3)
                                        repoCalc += .3f;

                                    if (flankDirection == 1)
                                        repoCalc += .4f;

                                    if (flankDirection == 2)
                                        repoCalc += .15f;
                                }

                                if (OverAtAggressionTreshold(AiAgressionType.VeryOffensive))
                                    repoCalc -= .25f;

                                if (brain.attackOrderCount == 0 && Random.Range(0, 4) < 3)
                                    repoCalc = 0f;

                                if ((repoCalc > .9f || isFlankedByEnemy) && brain.hasMovesRemaning()) {


                                    //If Unit is not behind and wont take cohesion damage (or doesnt care)
                                    if (flankDirection != 3 && (brain.attachedUnit.cohesion > 0)) {
                                        return AiUnitState.Pathing;
                                    }


                                }
                            }

                            return AiUnitState.InCombat;
                        }


                        return AiUnitState.Pathing;
                    }


                    break;
                case AiUnitGoal.Territory:

                    //Get Unit Target
                    CalculateBrainTarget(brain);

                    if (brain.currentDestination == null) {

                        brain.currentDestination = CalculateDefensivePosition(brain);

                        if (brain.currentDestination == null)
                            return AiUnitState.Idle;
                        else {

                            return AiUnitState.Pathing;
                        }



                    }
                    else {
                        if (brain.attachedUnit == null || plannedPos == null) {
                            return AiUnitState.Idle;
                        }


                        UnitInfo inRange = ReturnEnemyInCone(brain, plannedPos, brain.getTargetInfo());

                        if (inRange != null && brain.attachedUnit.IsFrontline()) {
                            return AiUnitState.InCombat;
                        }

                        return AiUnitState.Pathing;
                    }


                    break;
                case AiUnitGoal.Objective:
                    //Get Unit Target
                    CalculateBrainTarget(brain);

                    if (brain.currentDestination == null)
                        return AiUnitState.Idle;
                    else
                        return AiUnitState.Pathing;
            }


            return result;
        }

        public void AIUnitDeployment() {
            if (CMD.IsCampaignMode()) {

            }

            List<UnitInfo> leftovers = getAiControlledUnits();

            Debug.Log($"Getting Deployment Zone for AI {deploymentZone}");

            List<int[]> deploymentTiles = CMND.cmd_TileMap.getTeamDeploymentZones(deploymentZone);

            if (enemyID == deploymentZone) {
                enemyID = 0;
            }

            List<UnitInfo> sortedList = new List<UnitInfo>();
            //Sorting Variables;
            List<UnitInfo> inf = new List<UnitInfo>();
            List<UnitInfo> cav = new List<UnitInfo>();
            List<UnitInfo> art = new List<UnitInfo>();

            print("AI #" + AiID + " has deployed their forces in zone: " + deploymentZone);


            for (int i = 0; i < leftovers.Count; i++) {
                if (leftovers[i].hitpoints <= 0)
                    continue;

                switch (leftovers[i].classification) {
                    case UnitData.UnitClass.Infantry:
                        inf.Add(leftovers[i]);
                        break;
                    case UnitData.UnitClass.Cavalry:
                        cav.Add(leftovers[i]);
                        break;
                    case UnitData.UnitClass.Artillery:
                        art.Add(leftovers[i]);
                        break;
                }
            }

            sortedList.AddRange(inf);
            int cavHalf = cav.Count / 2;
            for (int i = 0; i < cavHalf; i++) {
                sortedList.Add(cav[i]);
            }


            for (int i = cavHalf; i < cav.Count; i++) {
                sortedList.Add(cav[i]);
            }

            sortedList.AddRange(art);

            if (deploymentZone != 0 && deploymentZone != 2 && deploymentZone != 4) {
                deploymentTiles.Reverse();
            }

            List<TileTerrainInfo> infDeployment = new List<TileTerrainInfo>();
            List<TileTerrainInfo> cavDeployment = new List<TileTerrainInfo>();
            List<TileTerrainInfo> artDeployment = new List<TileTerrainInfo>();

            for (int x = 0; x < deploymentTiles.Count; x++) {
                List<int[]> neighbors = new List<int[]>();

                TileTerrainInfo tile = CMND.cmd_TileMap.GetTile(deploymentTiles[x]);
                neighbors.AddRange(tile.getNeighbouringTiles());
                for (int i = 0; i < neighbors.Count; i++) {
                    if (!deploymentTiles.Contains(neighbors[i])) {
                        deploymentTiles.Remove(neighbors[i]);
                        neighbors.RemoveAt(i);
                    }

                }

                bool isRoad = tile.DATA.tileModifier == 0;
                bool isMud = tile.DATA.tileModifier == 1;
                float neighborValue = neighbors.Count * .05f;

                tile.ai_deploymentScore -= neighborValue;

                if (isRoad) {
                    tile.ai_deploymentScore--;
                }

                if (isMud) {
                    tile.ai_deploymentScore++;
                }

                bool overHalf = (deploymentTiles[x][1] > CMND.cmd_MapInfo.BoardSizeHalved[1]);
                bool isFrontLineTile = ((overHalf && deploymentTiles[x][1] != CMND.cmd_MapInfo.BoardSize[1] - 1) || (!overHalf && deploymentTiles[x][1] != 0));
                bool isBorderTile = (deploymentTiles[x][0] == 0 || deploymentTiles[x][1] == 0 || deploymentTiles[x][0] == CMND.cmd_MapInfo.BoardSize[0] - 1 || deploymentTiles[x][1] == CMND.cmd_MapInfo.BoardSize[1] - 1);

                if (isFrontLineTile) {
                    infDeployment.Add(tile);
                }
                else if (isBorderTile) {
                    artDeployment.Add(tile);

                    if (!isRoad)
                        cavDeployment.Add(tile);
                }

            }

            infDeployment.Sort((p1, p2) => p1.ai_deploymentScore.CompareTo(p2.ai_deploymentScore));
            cavDeployment.Sort((p1, p2) => p1.ai_deploymentScore.CompareTo(p2.ai_deploymentScore));
            artDeployment.Sort((p1, p2) => p1.ai_deploymentScore.CompareTo(p2.ai_deploymentScore));


            int unitCount = 2;
            if (deploymentTiles.Count > sortedList.Count) {
                unitCount = 1;
            }

            for (int i = 0; i < sortedList.Count; i++) {
                List<TileTerrainInfo> positions = infDeployment;

                if (sortedList[i].classification == UnitData.UnitClass.Artillery)
                    positions = artDeployment;
                else if (sortedList[i].classification == UnitData.UnitClass.Cavalry)
                    positions = cavDeployment;

                foreach (TileTerrainInfo _tile in positions) {
                    int[] _pos = _tile.tilePosition;

                    TileTerrainInfo tile = CMND.cmd_TileMap.GetTile(_pos, true);


                    if (tile != null && tile.count_AllUnits < unitCount) {
                        int l_directionFacing = CMND.cmd_UnitManager.calculateDeploymentFacing(tile.tilePosition);
                        CMND.cmd_TileMap.GetTile(new int[] { tile.tilePosition[0], tile.tilePosition[1] }).directionUnitsFacing = l_directionFacing;


                        CMND.cmd_TileMap.AddUnitToTile(new int[] { tile.tilePosition[0], tile.tilePosition[1] }, sortedList[i]);
                        getBrainFromUnit(sortedList[i]).currentDirection = l_directionFacing;
                        deploymentFormation(sortedList[i]);

                        CMND.cmd_N_OrderManager.DesyncCorrectUnit(sortedList[i].unitID, "teamOnly");

                        goto endLoop;
                    }
                    else {
                    }
                }

                foreach (int[] pos in deploymentTiles) {

                    TileTerrainInfo tile = CMND.cmd_TileMap.GetTile(pos, true);

                    if (tile != null && tile.count_AllUnits < 2) {
                        int l_directionFacing = CMND.cmd_UnitManager.calculateDeploymentFacing(tile.tilePosition);
                        CMND.cmd_TileMap.GetTile(new int[] { tile.tilePosition[0], tile.tilePosition[1] }).directionUnitsFacing = l_directionFacing;

                        CMND.cmd_TileMap.AddUnitToTile(new int[] { tile.tilePosition[0], tile.tilePosition[1] }, sortedList[i]);
                        getBrainFromUnit(sortedList[i]).currentDirection = l_directionFacing;
                        deploymentFormation(sortedList[i]);

                        break;
                    }
                }
                endLoop:
                int b = 0;
            }

            EndAiTurn();
        }

        void deploymentFormation(UnitInfo unit, int[] tilePos = null) {
            AiUnitBrain possibleBrain = getBrainFromUnit(unit);
            if (possibleBrain != null) {
                int enemyZone = -1;
                if (deploymentZone == 0 || deploymentZone == 2) {
                    enemyZone = 1;
                }
                else {
                    enemyZone = 0;
                }

                int random = Random.Range(0, CMND.cmd_TileMap.getTeamDeploymentZones(enemyZone).Count);





                int formation = CalculateUnitFormations(possibleBrain, CMND.cmd_TileMap.getTeamDeploymentZones(enemyZone)[random], tilePos);
                //print("Deployment Formation: " + formation);
                if (formation != -1) {
                    CMND.cmd_UnitManager.UnitsFormation(new UnitInfo[] { unit }, formation, true);
                    possibleBrain.currentFormation = formation;
                }

            }
        }

        public void EndAiTurn() {

            if (territoryData != null) {
                for (int x = 0; x < territoryData.GetLength(0); x++) {
                    for (int y = 0; y < territoryData.GetLength(1); y++) {
                        territoryData[x, y].ClearOccupants();
                    }
                }
            }

            stolenPositions.Clear();

            CMND.cmd_TurnManager.Ai_EndedTurn(AiID);
        }


        public bool PlanMoveToDefensivePosition(AiUnitBrain currentBrain, bool retreat = false) {
            //Target Destination Set
            int[] plannedTilePos = getPlannedUnitPosition(currentBrain);

            int[] destinationTile = CalculateDefensivePosition(currentBrain);
            if (retreat) {

                destinationTile = getNearestDeploymentZone(plannedTilePos);


                if (currentBrain.hasMovesRemaning()) {

                    int direction = CMD.CMND.cmd_UnitManager.getDirectionTowardTile(plannedTilePos, destinationTile);

                    int[] tilePos = destinationTile;

                    TileTerrainInfo _tile = CMND.cmd_TileMap.GetTile(tilePos, true);
                    if (_tile != null && _tile.count_AllUnits == 2)
                        tilePos = getNearestNeighborFromOLD(destinationTile, plannedTilePos, currentBrain.attachedUnit);

                    if (direction != -1) {
                        AIMoveOrder(currentBrain, tilePos, direction);
                        return true;
                    }

                }
            }

            if (currentBrain == null || currentBrain.attachedUnit == null || !currentBrain.hasMovesRemaning() || currentBrain.getTargetInfo() == null)
                return false;

            //In favorable defensive position
            if (samePosition(currentBrain.getPlannedPosition(), destinationTile)) {
                if (currentBrain.attachedUnit.movementPoints > 0 && plannedTilePos != null && currentBrain.getPlannedPosition() != null && currentBrain.hasMovesRemaning()) {
                    AIFaceOrder(currentBrain, CMD.CMND.cmd_UnitManager.getDirectionTowardTile(plannedTilePos, currentBrain.getTargetInfo().TilePosition));
                    return true;
                }

            }
            else if (currentBrain.attachedUnit.IsFrontline() && !targetInRange(currentBrain) && CMND.cmd_TileMap.TileDistance(plannedTilePos, currentBrain.getTargetInfo().TilePosition) - .5f <= currentBrain.attachedUnit.GetRange() && currentBrain.hasMovesRemaning()) {

                if (currentBrain.attachedUnit.movementPoints > 0 && plannedTilePos != null && currentBrain.getTargetInfo().TilePosition != null && currentBrain.hasMovesRemaning()) {

                    AIFaceOrder(currentBrain, CMD.CMND.cmd_UnitManager.getDirectionTowardTile(plannedTilePos, currentBrain.getTargetInfo().TilePosition));
                    return true;
                }
            }
            else {//Target Not within range

                if (currentBrain.hasMovesRemaning()) {
                    int direction = CMD.CMND.cmd_UnitManager.getDirectionTowardTile(plannedTilePos, destinationTile);

                    int[] tilePos = destinationTile;

                    TileTerrainInfo _tile = CMND.cmd_TileMap.GetTile(tilePos, true);
                    if (_tile != null && _tile.count_AllUnits > 2)
                        tilePos = getNearestNeighborFromOLD(destinationTile, plannedTilePos, currentBrain.attachedUnit);

                    if (direction != -1) {
                        AIMoveOrder(currentBrain, tilePos, direction);
                        return true;
                    }

                }


            }

            return false;
        }

        public bool TargetComputer(AiUnitBrain currentBrain) {
            int[] rawCalc = rawTargetInRange(currentBrain);
            if (rawCalc != null) {
                int direction = CMD.CMND.cmd_UnitManager.getDirectionTowardTile(currentBrain.getPlannedPosition(), rawCalc);
                if (direction != currentBrain.currentDirection && currentBrain.hasMovesRemaning()) {
                    AIFaceOrder(currentBrain, direction);
                    return true;
                }

            }

            SearchAndDestroyTarget(currentBrain);
            return false;
        }

        public bool SearchAndDestroyTarget(AiUnitBrain currentBrain) {
            int[] plannedTilePos = getPlannedUnitPosition(currentBrain);

            if (currentBrain == null || currentBrain.attachedUnit == null || currentBrain.getTargetInfo() == null || !currentBrain.hasMovesRemaning()) {
                if (currentBrain.deepDebug) {
                    print("Ai unit #" + currentBrain.attachedUnitID + " failed first pathing check : " + (currentBrain == null) + (currentBrain.attachedUnit == null) + (currentBrain.getTargetInfo() == null) + (currentBrain.moveOrderCount >= currentBrain.getMaxMoveCount()) + (currentBrain.attachedUnit.movementPoints <= 0));
                }

                return false;
            }


            if (inRangeButWrongFacing(currentBrain.getTargetInfo().TilePosition, currentBrain)) {
                print("Ai#" + currentBrain.attachedUnitID + " is in range but facing the wrong.");


                if (currentBrain.deepDebug) {
                    if (plannedTilePos == null) {
                        print("Ai unit #" + currentBrain.attachedUnitID + " plannedPos is null");
                    }

                    if (currentBrain.getTargetInfo().TilePosition == null) {
                        print("Ai unit #" + currentBrain.attachedUnitID + " target tilePositon is null");
                    }

                }

                if (currentBrain.attachedUnit.IsFrontline() && plannedTilePos != null && currentBrain.getTargetInfo().TilePosition != null && currentBrain.hasMovesRemaning())
                    AIFaceOrder(currentBrain, CMD.CMND.cmd_UnitManager.getDirectionTowardTile(plannedTilePos, currentBrain.getTargetInfo().TilePosition));

                if (!currentBrain.attachedUnit.IsFrontline() && (currentBrain.attachedUnit.cohesion > 0 || currentBrain.attachedUnit.hitpoints > 1)) {
                    print("Ai Unit " + currentBrain.attachedUnit.unitID + "is in reserve ready to fire");
                    AttemptAiMove(currentBrain, plannedTilePos);
                    return true;
                }
            }
            else {//Target Not within range
                if (currentBrain.deepDebug) {
                    if (currentBrain.hasChangedTiles) {
                        print("Ai unit #" + currentBrain.attachedUnitID + " has charged this turn");
                    }

                }

                if (currentBrain.attachedUnit.cohesion > 0 || currentBrain.attachedUnit.hitpoints > 1) {

                    AttemptAiMove(currentBrain, plannedTilePos);
                    return true;
                }

                return false;

            }
            return false;
        }



        public bool PlanDominationAi(AiUnitBrain currentBrain) {
            currentBrain.currentUnitState = CalculateUnitState(currentBrain);

            switch (currentBrain.currentUnitState) {
                default:
                case AiUnitState.Idle:
                    return true;
                case AiUnitState.Pathing:
                    if (currentBrain.attachedUnit.classification == UnitData.UnitClass.Artillery) {
                        return TargetComputer(currentBrain);
                    }
                    else {
                        return SearchAndDestroyTarget(currentBrain);
                    }
                case AiUnitState.InCombat:


                    if (currentBrain == null || currentBrain.attachedUnit == null || currentBrain.getTargetInfo() == null || currentBrain.currentTarget == -1)
                        return false;


                    //Firing
                    int shotsAllowed = 0;
                    if (currentBrain.attachedUnit.classification == UnitData.UnitClass.Artillery || currentBrain.attachedUnit.hasEquippedWeapon())
                        shotsAllowed = 1;

                    if (currentBrain.Breech) {
                        shotsAllowed = 2;
                    }



                    if (currentBrain.holdGround) {
                        AIFaWOrder(currentBrain);
                    }
                    else {

                        int maxCap = 10;

                        while (currentBrain.attackOrderCount < shotsAllowed && maxCap > 0) {
                            AIFireOrder(currentBrain);
                            maxCap--;
                        }

                    }



                    if (currentBrain.attachedUnit.classification == UnitData.UnitClass.Artillery)
                        return true;

                    //Charging
                    List<int[]> tilesToCharge = CMND.cmd_UnitManager.getChargableTiles(currentBrain.attachedUnit);

                    bool attacksRemaning = (currentBrain.attackOrderCount == 0 || (currentBrain.attackOrderCount < 2 && currentBrain.Breech));

                    if (tilesToCharge.Count > 0 && currentBrain.isCharger(tilesToCharge[0]) && attacksRemaning) {
                        print("[AI]" + AiID + " is attemtping to charge!");
                        AIChargeOrder(currentBrain);
                    }


                    return true;
                case AiUnitState.Retreat:
                    return PlanMoveToDefensivePosition(currentBrain, true);
                case AiUnitState.Melee:
                    List<UnitInfo> enemiesInTile = currentBrain.getEnemiesInTile(AiTeamID);
                    List<UnitInfo> alliesInTile = currentBrain.getAlliesInTile(AiTeamID);

                    bool retreat = false;

                    if (enemiesInTile.Count > 0) {

                        //Doesnt have Melee Perk and enemy Does
                        bool enemy_eliteMeleeCheck = false;
                        //Enemy has more hp + coh
                        bool lifeDisadvantage = false;
                        //Outnumbered
                        bool countDisadvantage = enemiesInTile.Count > alliesInTile.Count;
                        for (int x = 0; x < enemiesInTile.Count; x++) {

                            if (!currentBrain.attachedUnit.HasPerk(2) && enemiesInTile[x].HasPerk(2)) {
                                enemy_eliteMeleeCheck = true;
                            }

                            if (currentBrain.attachedUnit.totalLifePoints < enemiesInTile[x].totalLifePoints)
                                lifeDisadvantage = true;
                        }

                        if (enemy_eliteMeleeCheck || lifeDisadvantage || countDisadvantage)
                            retreat = true;
                    }

                    if (retreat) {
                        return PlanMoveToDefensivePosition(currentBrain, true);
                    }
                    else {
                        return true;
                    }

            }
        }


        public bool PlanDefensiveAi(AiUnitBrain currentBrain) {
            currentBrain.currentUnitState = CalculateUnitState(currentBrain);

            switch (currentBrain.currentUnitState) {
                default:
                case AiUnitState.Idle:
                    return true;
                case AiUnitState.Pathing:
                    return PlanMoveToDefensivePosition(currentBrain);
                case AiUnitState.InCombat:
                    if (currentBrain == null || currentBrain.attachedUnit == null || currentBrain.getTargetInfo() == null || currentBrain.currentTarget == -1)
                        return false;


                    //Firing
                    int shotsAllowed = 0;
                    if (currentBrain.attachedUnit.hasEquippedWeapon())
                        shotsAllowed = 1;

                    if (currentBrain.Breech)
                        shotsAllowed = 2;

                    if (currentBrain.attackOrderCount < shotsAllowed)
                        AIFireOrder(currentBrain);

                    if (currentBrain.attachedUnit.classification == UnitData.UnitClass.Artillery)
                        return true;

                    //Charging
                    List<int[]> tilesToCharge = CMND.cmd_UnitManager.getChargableTiles(currentBrain.attachedUnit);

                    if (tilesToCharge.Count > 0 && ((currentBrain.attachedUnit.classification == UnitData.UnitClass.Cavalry || currentBrain.attachedUnit.HasPerk("shock")) || (currentBrain.attackOrderCount == 0 || currentBrain.attachedUnit.HasPerk("breech_loading"))))
                        AIChargeOrder(currentBrain);

                    return true;

            }
        }


        AiTargetToken getUnitTargetData(int unitID) {
            for (int i = 0; i < currentTargets.Count; i++) {
                if (currentTargets[i].refUnit.unitID == unitID)
                    return currentTargets[i];
            }

            return null;
        }

        public void PlanAiTurn() {
            if (!aiEnabled)
                return;

            if (CMND.cmd_TurnManager.deploymentPhase) {
                AIUnitDeployment();
                return;
            }


            currentTargets.Clear();
            for (int i = 0; i < unitBrains.Length; i++) {
                AiUnitBrain currentBrain = unitBrains[i];
                currentBrain.RefreshBrain();


                int attemps = 2;

                if ((doubleMover(currentBrain.attachedUnit)) && cumbersomeCheck(currentBrain))
                    attemps++;

                for (int x = 0; x < attemps; x++) {
                    if (currentBrain.hasCharged)
                        break;

                    if (x == attemps - 1)
                        currentBrain.lastCycle = true;

                    if (currentBrain.currentUnitGoal == AiUnitGoal.Domination) {
                        bool planState = PlanDominationAi(currentBrain);
                    }
                    else if (currentBrain.currentUnitGoal == AiUnitGoal.Territory) {

                        bool planState = PlanDefensiveAi(currentBrain);
                    }
                }
            }

        }

        public void PlanDelayedAITurn() {
            if (!aiEnabled)
                return;

            if (CMND.cmd_TurnManager.deploymentPhase) {
                AIUnitDeployment();
                return;
            }

            for (int i = 0; i < unitsToDelay.Count; i++) {
                AiUnitBrain currentBrain = unitBrains[i];


                int attemps = 2;

                if ((doubleMover(currentBrain.attachedUnit)) && cumbersomeCheck(currentBrain))
                    attemps++;

                for (int x = 0; x < attemps; x++) {

                    if (x == attemps - 1)
                        currentBrain.lastCycle = true;

                    if (currentBrain.currentUnitGoal == AiUnitGoal.Domination) {

                        bool planState = PlanDominationAi(currentBrain);
                    }
                    else if (currentBrain.currentUnitGoal == AiUnitGoal.Territory) {

                        bool planState = PlanDefensiveAi(currentBrain);
                    }
                }
            }

            CalculateExecutionOrder(unitsToDelay);

            turnComplete = true;
        }

        void AttemptAiMove(AiUnitBrain brain, int[] plannedTilePos) {
            if (!brain.hasMovesRemaning())
                return;

            if (!brain.hasChangedTiles) {


                int direction = CMD.CMND.cmd_UnitManager.getDirectionTowardTile(plannedTilePos, brain.getTargetInfo().TilePosition);


                int[] posToMove = getNearestNeighborFromOLD(brain.getTargetInfo(), plannedTilePos, brain, brain.attachedUnit.classification != UnitData.UnitClass.Artillery);

                if (direction != -1 && direction != brain.attachedUnit.direction && posToMove == brain.attachedUnit.TilePosition && brain.hasMovesRemaning()) {
                    AIFaceOrder(brain, direction);
                    return;
                }

                AIMoveOrder(brain, posToMove, direction);
                if (brain.deepDebug) {
                    print("Ai unit #" + brain.attachedUnitID + " has a directionCheck of : " + direction);
                }
            }
        }

        public void CalculateExecutionOrder(List<AiUnitBrain> brainList = null) {
            List<AiUnitBrain> brains = new List<AiUnitBrain>();

            if (brainList == null)
                brains.AddRange(unitBrains);
            else
                brains.AddRange(brainList);


            brains.Sort(SortByPriority);

            if (brainList == null)
                unitBrains = brains.ToArray();
            else {
                unitsToDelay = brains;
            }
        }

        public void ExecuteAiTurn(bool delayed = false) {
            if (!aiEnabled)
                return;


            AiUnitBrain[] unitBrainArray = unitBrains;
            if (delayed)
                unitBrainArray = unitsToDelay.ToArray();

            for (int i = 0; i < unitBrainArray.Length; i++) {
                AiUnitBrain currentBrain = unitBrainArray[i];

                for (int x = 0; x < currentBrain.aiPlans.Count; x++) {
                    PlannedOrder currentOrder = currentBrain.aiPlans[x];
                    //print(currentBrain.attachedUnit.PlayerNetworkID + "'s unit " + currentBrain.attachedUnit.unitID + " is creating an order of type " + currentOrder.orderType.ToString());

                    switch (currentOrder.orderType) {
                        case PlannedOrderType.FaceOrder:
                        case PlannedOrderType.MoveOrder:

                            int lookDir = CMD.CMND.cmd_UnitManager.getDirectionTowardTile(currentBrain.getPlannedPosition(), currentOrder.orderTargetPos);
                            if (currentOrder.faceDirection != -1) {
                                lookDir = currentOrder.faceDirection;
                            }

                            try {
                                CMND.cmd_UnitManager.GenerateMovementOrder(currentOrder.attachedUnits.ToArray(), currentOrder.orderTargetPos, lookDir, AiPlayerID, true, currentOrder.swapReserve);
                            }
                            catch (System.Exception exception) {
                                Debug.LogError("Ai #" + AiID + " Failed to create " + currentOrder.orderType.ToString() + "!" + exception);
                            }


                            break;
                        case PlannedOrderType.FireOrder:

                            if (currentBrain.attachedUnit.IsFrontline()) {
                                try {
                                    int[] targetPos = currentOrder.getTargetInfo().TilePosition;
                                    if (targetPos != null) {
                                        CMND.cmd_UnitManager.SelectTileToFire(targetPos, new List<UnitInfo>() { currentBrain.attachedUnit }, AiPlayerID, true);
                                    }
                                    else {
                                        CMND.cmd_UnitManager.ForceFireAtWill(currentOrder.attachedUnits.ToArray(), AiPlayerID, true);
                                    }

                                }
                                catch (System.Exception exception) {
                                    Debug.LogError("Ai #" + AiID + " Failed to create " + currentOrder.orderType.ToString() + "!" + exception);
                                }
                            }

                            break;
                        case PlannedOrderType.FireAtWill:
                            if (currentBrain.hasFaWOrder && currentBrain.attachedUnit.IsFrontline()) {
                                try {
                                    CMND.cmd_UnitManager.ForceFireAtWill(currentOrder.attachedUnits.ToArray(), AiPlayerID, true);
                                }
                                catch (System.Exception exception) {
                                    Debug.LogError("Ai #" + AiID + " Failed to create " + currentOrder.orderType.ToString() + "!" + exception);
                                }
                            }

                            break;
                        case PlannedOrderType.ChargeOrder:
                            try {
                                CMND.cmd_UnitManager.CreateChargeOrder(currentOrder.attachedUnits, new List<UnitInfo>() { CMND.cmd_Units.GetUnit(currentOrder.targetUnitID) }, true);
                            }
                            catch (System.Exception exception) {
                                Debug.LogError("Ai #" + AiID + " Failed to create " + currentOrder.orderType.ToString() + "!" + exception);
                            }
                            break;
                        case PlannedOrderType.FormationOrder:
                            try {
                                CMND.cmd_UnitManager.SetUnitsFormation(currentOrder.attachedUnits, currentOrder.formation, currentBrain.attachedUnit.PlayerNetworkID, true);
                            }
                            catch (System.Exception exception) {
                                Debug.LogError("Ai #" + AiID + " Failed to create " + currentOrder.orderType.ToString() + "!" + exception);
                            }
                            break;
                    }

                }

            }
        }

        void AiTurnTick() {
            if (!aiEnabled)
                return;

            if (isCheater && !allPlayersHaveCompletedTurn())
                return;

            if (aiEnabled && !CMD.ONT && !turnComplete) {
                ResetUnitBrains();

                Debug.Log($"[CMD AI] Planning AI TURN");
                PlanAiTurn();
                Debug.Log($"[CMD AI] Finished Calling Plan AI TURN");
                OptimizeAiTurn();
                Debug.Log($"[CMD AI] Finished Optimizing Ai Turn");
                ExecuteAiTurn();
                Debug.Log($"[CMD AI] Finished Executing Ai Turn");
                if (unitsToDelay.Count > 0) {
                    ResetDelayedUnitBrains();
                    Debug.Log($"[CMD AI] Finished Reseting Delay Unit Brains");
                    PlanDelayedAITurn();
                    Debug.Log($"[CMD AI] Finished Delayed AI Turn");
                    ExecuteAiTurn(true);
                    Debug.Log($"[CMD AI] Finished executing Delayaed Unit Brains");
                }
                Debug.Log($"[CMD AI] Starting End AI Turn");
                EndAiTurn();
                Debug.Log($"[CMD AI] Planning AI TURN Completed");
            }


            if (turnComplete && !CMND.cmd_enviroment.timeIsChanging) {
                if (DEBUG_AIEndsTurn) {
                    Debug.Log($"[CMD AI] Force Ending AI TURN");
                    AiForceEndsTurn();
                    Debug.Log($"[CMD AI] Force Ending AI TURN Completed");
                }
            }
        }

        Dictionary<int, int[]> stolenPositions = new Dictionary<int, int[]>();

        int[] StealEnemyPosition(int targetID) {
            if (stolenPositions == null)
                return null;

            if (stolenPositions.ContainsKey(targetID))
                return stolenPositions[targetID];


            int[] stolenPos = null;
            if (CMND.cmd_OrderManager.players_OrderLists == null)
                return null;

            int listCount = CMND.cmd_OrderManager.players_OrderLists.Length;
            print("Jingle Count: " + listCount);
            for (int listIndex = 0; listIndex < listCount; listIndex++) {
                if (CMND.cmd_OrderManager.players_OrderLists[listIndex] == null)
                    continue;

                print("Jingle Team: " + CMND.cmd_OrderManager.players_OrderLists[listIndex].team + ", " + enemyID);
                if (CMND.cmd_OrderManager.players_OrderLists[listIndex].team != enemyID)
                    continue;

                PlayerOrderList orderList = CMND.cmd_OrderManager.players_OrderLists[listIndex];

                int orderCount = orderList.orderList.Count;
                for (int orderIndex = 0; orderIndex < orderCount; orderIndex++) {
                    bool containsTarget = false;
                    if (orderList.orderList[orderIndex] == null || orderList.orderList[orderIndex].units == null)
                        continue;

                    for (int unitIndex = 0; unitIndex < orderList.orderList[orderIndex].units.Length; unitIndex++) {
                        if (orderList.orderList[orderIndex].units[unitIndex] == targetID)
                            containsTarget = true;
                    }

                    if (containsTarget == false)
                        continue;

                    PlayerOrder peakedOrder = orderList.orderList[orderIndex];
                    if ((peakedOrder.orderType == 0 || peakedOrder.orderType == 1) && peakedOrder.movementOrder.destinationTile != null) {
                        if (peakedOrder.movementOrder == null || peakedOrder.movementOrder.destinationTile == null)
                            continue;

                        stolenPos = peakedOrder.movementOrder.destinationTile;

                    }


                }

            }

            if (stolenPos != null) {
                stolenPositions.Add(targetID, stolenPos);
                print("Jingle");
                return stolenPos;
            }


            return null;
        }

        public void OptimizeAiTurn() {
            for (int i = 0; i < unitBrains.Length; i++) {
                for (int p = 0; p < unitBrains[i].aiPlans.Count; p++) {
                    if (unitBrains[i].aiPlans[p].orderType == PlannedOrderType.FireOrder || unitBrains[i].aiPlans[p].orderType == PlannedOrderType.FireAtWill)
                        continue;


                    for (int iX = 0; iX < unitBrains.Length; iX++) {
                        for (int pX = 0; pX < unitBrains[iX].aiPlans.Count; pX++) {
                            if (unitBrains[iX].aiPlans[pX].orderType == PlannedOrderType.FireOrder || unitBrains[i].aiPlans[p].orderType == PlannedOrderType.FireAtWill)
                                continue;

                            if (unitBrains[i] == unitBrains[iX])
                                continue;

                            PlannedOrder aiPlan = unitBrains[i].aiPlans[p];
                            PlannedOrder aiPlanX = unitBrains[iX].aiPlans[pX];

                            if (aiPlan.orderType != aiPlanX.orderType)
                                continue;


                            if (samePosition(aiPlan.orderOriginPos, aiPlanX.orderOriginPos) && (samePosition(aiPlan.orderTargetPos, aiPlanX.orderTargetPos) || samePosition(aiPlan.orderNextTilePos, aiPlanX.orderNextTilePos))) {
                                print("Matching plans found. Combining");
                                PlannedOrder newPlan = new PlannedOrder(aiPlan);
                                newPlan.attachedUnits.AddRange(aiPlanX.attachedUnits);

                                unitBrains[i].aiPlans[p] = newPlan;
                                unitBrains[iX].aiPlans.RemoveAt(pX);
                                unitBrains[iX].AddPlanLite(newPlan);
                            }

                        }
                    }


                }
            }


            CalculateExecutionOrder();

            turnComplete = true;
        }

        bool allPlayersHaveCompletedTurn() {
            return CMND.cmd_TurnManager.AllHumanPlayersHaveSubmitted();
        }

        public int CalculateInfantryFormations(AiUnitBrain brain, int[] enemyTile, int[] tempPos = null, bool wantsToFire = false) {
            int output = -1;
            UnitInfo unit = brain.attachedUnit;

            if (!brain.hasMovesRemaning() || unit.inMeleeCombat) {
                goto endFormation;
            }

            int[] pos = unit.TilePosition;
            if (tempPos != null) {
                pos = tempPos;
            }

            float distanceToEnemy = CMND.cmd_TileMap.TileDistance(enemyTile, pos);
            float columTileSensitivity = (6f - (6f * (AiAggressiveness / 2)) - (AiAggressiveness));
            if (columTileSensitivity < 1.5f)
                columTileSensitivity = 1.5f;

            float attackDistance = unit.GetRange() * columTileSensitivity - .5f + ((1f - (AiAggressiveness / 2)) * 2.5f);

            //March or Attack Column
            int columnType = 4;
            if (OverAtAggressionTreshold(AiAgressionType.Balanced)) {
                float attackColumnCalc = 0f;

                if (brain.attachedUnit.perkModifiers.chargeDamageModifier)
                    attackColumnCalc += .5f;

                if (attackColumnCalc >= .5f) {
                    columnType = 3;
                }
            }


            //Minimum Attack Distance
            float minDistance = Mathf.RoundToInt(unit.GetRange() + (-2f + (AiTactic * 2)));
            if (minDistance < 1.5f)
                minDistance = 1.5f;



            //Columns
            if (distanceToEnemy > attackDistance && unit.classification == UnitData.UnitClass.Infantry && (unit.formation != columnType)) {
                output = columnType;
                goto endFormation;
            }

            bool wantsToFireFormationsCheck = (wantsToFire && (unit.formation == 4 || unit.formation == 2 || unit.formation == 5 || unit.formation == 6));
            bool aggressionCheck = (distanceToEnemy <= minDistance && OverAtAggressionTreshold(AiAgressionType.Offensive));
            bool infantryDistanceCheck = (distanceToEnemy <= attackDistance && unit.classification == UnitData.UnitClass.Infantry);

            //Combat Formations
            if (wantsToFireFormationsCheck || aggressionCheck || infantryDistanceCheck) {
                output = 0;

                float openOrderCalc = 0f;
                if (brain.Breech)
                    openOrderCalc += .2f;
                if (unit.HasPerk(11))
                    openOrderCalc += .7f;

                if (brain.currentTarget != -1) {
                    UnitInfo tInfo = brain.getTargetInfo();

                    if (tInfo.HasPerk("breech_loading") && brain.getTargetInfo().HasPerk("rifling"))
                        openOrderCalc += .5f;

                    if (tInfo.perkModifiers.rangeDamageModifier)
                        openOrderCalc += .5f;

                    if (tInfo.subclass == UnitData.UnitSubclass.Modern)
                        openOrderCalc += .5f;

                    if (tInfo.formation == 1)
                        openOrderCalc = 0f;
                }

                if (openOrderCalc >= .7f && unit.subclass != UnitData.UnitSubclass.Militia) {
                    output = 1;
                }

                if (unit.HasPerk(11) && unit.formation == 3) {
                    output = 3;
                }

                if (unit.formation == output) {
                    output = -1;
                }
            }


            endFormation:

            if (output != -1 && (brain.currentFormation == output || unit.formation == output)) {
                output = -1;
            }

            return output;
        }
        public int CalculateCavFormations(AiUnitBrain brain, int[] enemyTile, int[] tempPos = null, bool wantsToFire = false) {
            int output = -1;
            UnitInfo unit = brain.attachedUnit;

            if (brain.hasMovesRemaning() || unit.inMeleeCombat) {
                goto endFormation;
            }

            int[] pos = unit.TilePosition;
            if (tempPos != null) {
                pos = tempPos;
            }

            float distanceToEnemy = CMND.cmd_TileMap.TileDistance(enemyTile, pos);
            float columTileSensitivity = (6f - (6f * (AiAggressiveness / 2)) - (AiAggressiveness));
            if (columTileSensitivity < 1.5f)
                columTileSensitivity = 1.5f;

            float attackDistance = unit.GetRange() * columTileSensitivity - .5f + ((1f - (AiAggressiveness / 2)) * 2.5f);
            if (!unit.hasEquippedWeapon())
                attackDistance = 3f;

            //March or Attack Column
            int columnType = 5;


            //Minimum Attack Distance
            float minDistance = Mathf.RoundToInt(unit.GetRange() + (-2f + (AiTactic * 2)));
            if (minDistance < 1.5f)
                minDistance = 1.5f;



            //Columns
            if (distanceToEnemy > attackDistance && unit.classification == UnitData.UnitClass.Cavalry && (unit.formation != columnType)) {
                if (distanceToEnemy < attackDistance * 1.5 && unit.subclass == UnitData.UnitSubclass.Heavy) {
                    output = 6;
                }
                else if (brain.attachedUnit.HasPerk("efficiency")) {
                    output = columnType;
                }
                else {
                    output = 7;
                }

                goto endFormation;
            }

            bool wantsToFireFormationsCheck = (wantsToFire && (unit.formation == 7 || unit.formation == 8));
            bool aggressionCheck = (distanceToEnemy <= minDistance && OverAtAggressionTreshold(AiAgressionType.Offensive));

            bool cavDistanceCheck = (distanceToEnemy <= attackDistance && unit.classification == UnitData.UnitClass.Cavalry);


            //Combat Formations
            if (wantsToFireFormationsCheck || aggressionCheck || cavDistanceCheck) {
                output = 7;

                float openOrderCalc = 0f;
                if (brain.Breech)
                    openOrderCalc += .2f;
                if (unit.HasPerk(11))
                    openOrderCalc += .7f;

                if (brain.currentTarget != -1) {
                    UnitInfo tInfo = brain.getTargetInfo();

                    if (brain.getTargetInfo().HasPerk("rifling"))
                        openOrderCalc += .5f;

                    if (tInfo.perkModifiers.rangeDamageModifier)
                        openOrderCalc += .5f;

                    if (tInfo.subclass == UnitData.UnitSubclass.Modern)
                        openOrderCalc += .5f;

                    if (tInfo.formation == 1)
                        openOrderCalc = 0f;
                }

                if (openOrderCalc >= .7f && brain.attachedUnit.HasPerk("breech_loading")) {
                    output = 8;
                }

                if (unit.formation == output) {
                    output = -1;
                }
            }

            endFormation:

            if (output != -1 && (brain.currentFormation == output || unit.formation == output)) {
                output = -1;
            }

            return output;
        }

        public int CalculateArtilleryFormations(AiUnitBrain brain, int[] enemyTile, int[] tempPos = null, bool wantsToFire = false, bool wantsToMove = false) {

            bool wantsToFireFormationsCheck = (wantsToFire);
            int output = -1;
            UnitInfo unit = brain.attachedUnit;


            if (brain.hasMovesRemaning() || unit.inMeleeCombat) {
                goto endFormation;
            }



            int[] pos = unit.TilePosition;
            if (tempPos != null) {
                pos = tempPos;
            }

            if (!wantsToFire) {
                if (wantsToMove && brain.currentFormation != 9) {
                    output = 9;
                    goto endFormation;
                }

                float distanceToEnemy = CMND.cmd_TileMap.TileDistance(enemyTile, pos);

                float attackDistance = unit.GetRange(false, null, true);
                attackDistance *= .8f;

                //March or Attack Column
                int columnType = 9;


                //Minimum Attack Distance
                float minDistance = Mathf.RoundToInt(unit.GetRange(false, null, true));
                if (minDistance < 1.5f)
                    minDistance = 1.5f;



                //Columns
                if (!wantsToFire && distanceToEnemy > unit.GetRange(false, null, true) && (brain.currentFormation != columnType)) {
                    output = columnType;
                    goto endFormation;
                }



            }
            else {
                //Combat Formations
                output = 11;

                float conBatteryFloat = 0f;
                if (brain.Breech)
                    conBatteryFloat += .7f;

                if (brain.currentTarget != -1) {
                    UnitInfo tInfo = brain.getTargetInfo();

                    if (tInfo.HasPerk("breech_loading") || tInfo.HasPerk("rifling"))
                        conBatteryFloat += .5f;

                    if (tInfo.perkModifiers.rangeDamageModifier)
                        conBatteryFloat += .5f;

                    if (tInfo.subclass == UnitData.UnitSubclass.Modern)
                        conBatteryFloat += .5f;
                }

                if (conBatteryFloat >= .7f) {
                    output = 10;
                }
            }

            endFormation:

            if (output != -1 && brain.currentFormation == output) {
                output = -1;
            }

            return output;
        }

        int CalculateUnitFormations(AiUnitBrain brain, int[] enemyTile, int[] tempPos = null, bool wantsToFire = false, bool wantsToMove = false) {
            switch (brain.attachedUnit.classification) {
                case UnitData.UnitClass.Cavalry:
                    return CalculateCavFormations(brain, enemyTile, tempPos, wantsToFire);
                case UnitData.UnitClass.Artillery:
                    return CalculateArtilleryFormations(brain, enemyTile, tempPos, wantsToFire, wantsToMove);
                default:
                    return CalculateInfantryFormations(brain, enemyTile, tempPos, wantsToFire);
            }
        }

        #endregion

        #region AI Order Creation
        PlannedOrder AIFaceOrder(AiUnitBrain brainCell, int lookDir) {
            UnitInfo unit = brainCell.attachedUnit;


            if (unit.TilePosition != null) {
                int[] plannedPos = getPlannedUnitPosition(brainCell);


                if (plannedPos == null) {
                    Debug.LogError("NextTile Null");
                }

                PlannedOrder order = new PlannedOrder(unit, plannedPos, plannedPos, plannedPos);
                order.orderType = PlannedOrderType.FaceOrder;
                order.faceDirection = lookDir;
                brainCell.AddPlan(order);
            }
            return null;




            /*if (unit.currentOrder != null)
                aiOrderList.Add(selectedUnits[0].currentOrder);*/


        }

        PlannedOrder AIFireOrder(AiUnitBrain brainCell) {


            if (brainCell.attachedUnit.HasPerk("cumbersome") && brainCell.moveOrderCount > 0) {
                PrintAIDebug("Failed to fire [Cumbersome]", brainCell.attachedUnitID);
                return null;
            }

            if (!brainCell.AiHasPerk(5) && brainCell.attackOrderCount > 0) {
                PrintAIDebug("Failed to fire, too many orders", brainCell.attachedUnitID);
                return null;
            }


            UnitInfo inRange = ReturnEnemyInCone(brainCell, getPlannedUnitPosition(brainCell), brainCell.getTargetInfo());
            if (inRange == null) {
                PrintAIDebug("Failed to fire [InRange] is null", brainCell.attachedUnitID);
                return null;
            }


            int[] enemyTile = inRange.TilePosition;
            int formationInt = CalculateUnitFormations(brainCell, enemyTile, null, true);
            bool changeFormation = false;
            if (formationInt != -1 && !brainCell.attachedUnit.GetTile().isBridge) {
                changeFormation = true;
                brainCell.AddPlan(new PlannedOrder(brainCell.attachedUnit, formationInt));
            }

            //Ai Targeting Decision Making
            bool canFireBasics = (inRange != null) && brainCell.hasMovesRemaning(changeFormation);

            bool shouldFireBasics = false;
            if (brainCell.attachedUnit.classification == UnitData.UnitClass.Artillery) {
                shouldFireBasics = true;
            }
            else {
                if (OverAtDifficultyTreshold(AiDifficultyType.Average) && UnderAtAggressionTreshold(AiAgressionType.Balanced))
                    shouldFireBasics = inRange == brainCell.getTargetInfo();
                else {
                    shouldFireBasics = true;
                }
            }

            //Friendly Fire Logic! Ai Allies, Allied, FF
            List<UnitInfo> allies = new List<UnitInfo>();

            if (inRange.GetTile() != null) {
                TileTerrainInfo t = inRange.GetTile();
                for (int u = 0; u < t.count_AllUnits; u++) {
                    if (t.units_All[u] != null && t.units_All[u].team == AiTeamID) {
                        allies.Add(t.units_All[u]);
                    }
                }
            }

            bool badFF = false;

            if (allies.Count > 0) {
                int worstFlank = -1;

                for (int a = 0; a < allies.Count; a++) {
                    if (allies[a] != null) {

                        if (allies[a].cohesion <= 0)
                            badFF = true;

                        int l_flank = CMND.cmd_UnitManager.CheckFlanking(brainCell.attachedUnit, allies[a]);
                        if (l_flank > worstFlank)
                            worstFlank = l_flank;
                    }
                }

                int ogFlank = CMND.cmd_UnitManager.CheckFlanking(brainCell.attachedUnit, inRange);

                if (worstFlank > ogFlank) {
                    badFF = true;
                }
            }

            if (OverAtAggressionTreshold(AiAgressionType.Balanced) && allies.Count > 0) {
                shouldFireBasics = false;
            }

            if (OverAtAggressionTreshold(AiAgressionType.Offensive) && !badFF) {
                shouldFireBasics = true;
            }

            if (canFireBasics && shouldFireBasics) {
                PlannedOrder newOrder = new PlannedOrder(brainCell.attachedUnit, inRange.TilePosition, inRange.unitID, false);
                brainCell.AddPlan(newOrder);

                PrintAIDebug("Planned to fire on enemy unit #" + inRange.unitID, brainCell.attachedUnitID);
                return newOrder;
            }

            PrintAIDebug("Failed to fire [" + canFireBasics + ", " + shouldFireBasics + "]", brainCell.attachedUnitID);
            return null;
        }

        PlannedOrder AIFaWOrder(AiUnitBrain brainCell) {
            if ((brainCell.attachedUnit.HasPerk("cumbersome") && brainCell.moveOrderCount > 0))
                return null;

            if (brainCell.attachedUnit.equippedWeapon == null && brainCell.attachedUnit.classification != UnitData.UnitClass.Artillery)
                return null;

            PlannedOrder newOrder = new PlannedOrder(brainCell.attachedUnit, true);
            brainCell.AddPlan(newOrder);
            return newOrder;
        }

        PlannedOrder AIMoveOrder(AiUnitBrain brainCell, int[] destination, int direction = -1) {
            UnitInfo unit = brainCell.attachedUnit;

            if (brainCell.attachedUnit.classification == UnitData.UnitClass.Artillery && brainCell.currentFormation != 9) {
                brainCell.AddPlan(new PlannedOrder(brainCell.attachedUnit, 9));
                return null;
            }

            if (unit.HasPerk("cumbersome") && brainCell.attackOrderCount > 0) {
                print("fail1");
                return null;
            }


            if (unit.classification == UnitData.UnitClass.Artillery && ReturnEnemyInCone(brainCell, getPlannedUnitPosition(brainCell), brainCell.getTargetInfo()) != null) {
                print("fail2");
                return null;
            }

            int formationInt = -1;
            if (brainCell.getTargetInfo() != null) {
                int[] enemyTile = brainCell.getTargetInfo().TilePosition;
                formationInt = CalculateUnitFormations(brainCell, enemyTile, null, false, true);
            }

            bool changeFormation = false;
            if (formationInt != -1 && brainCell.currentFormation != formationInt && !brainCell.attachedUnit.GetTile().isBridge) {
                changeFormation = true;
                brainCell.AddPlan(new PlannedOrder(unit, formationInt));
            }

            if (!changeFormation || (changeFormation && unit.HasPerk("efficiency"))) {

                if (CMND.cmd_TileMap.GetTile(destination, true) == null) {
                    print("Ai #" + AiID + " move destination is null");
                    print("fail3");
                    return null;
                }


                if (brainCell == null) {
                    Debug.LogError("Braincell Null for unit:" + unit.unitID);
                }
                if (destination == null) {
                    Debug.LogError("Destination Null for unit:" + unit.unitID);
                }
                if (brainCell != null && getPlannedUnitPosition(brainCell) == null) {
                    Debug.LogError("Planned Position Null for unit:" + unit.unitID);
                }
                PF_Properties ghostPath = new PF_Properties(brainCell, getPlannedUnitPosition(brainCell), destination, unit.team, unit.PlayerNetworkID, false, unit.HasPerk("rugged"));
                ghostPath.checkForAllies = false;
                ghostPath.aiPath = true;


                //PF_Properties pathProps = new PF_Properties(brainCell, getPlannedUnitPosition(brainCell), destination, unit.team, unit.PlayerNetworkID, false, unit.HasPerk("rugged"));
                bool bridge = false;

                PF_Return path = CMND.cmd_Pathfinding.GeneratePath(ghostPath);
                if (path != null && path.foundPath && unit.movementPoints > 0) {
                    int[] nextTile = null;
                    if (path.pathway.tilepath.Count > 7) {
                        path.pathway.tilepath.RemoveRange(7, path.pathway.tilepath.Count - 7);
                        destination = path.pathway.tilepath[6];
                    }


                    int speed = unit.GetTotalSpeed(brainCell.currentFormation);

                    if (CMND.cmd_UnitManager.CanUnitsUtilizeRoads(new List<UnitInfo>() { unit })) {
                        int roadCount = 0;
                        for (int i = 0; i < path.pathway.tilepath.Count; i++) {
                            TileTerrainInfo tile = CMND.cmd_TileMap.GetTile(path.pathway.tilepath[i], true);
                            if (tile.isBridge)
                                bridge = true;
                            if (tile != null && tile.DATA.HasModifier(0)) {
                                roadCount++;
                            }
                        }

                        if (roadCount > (float)path.pathway.tilepath.Count / 2f)
                            speed += 1;
                    }

                    int[] finPos = null;

                    if (path.pathway.tilepath.Count <= speed)
                        speed = path.pathway.tilepath.Count - 1;

                    if (speed > 0) {
                        nextTile = path.pathway.tilepath[speed];
                        finPos = path.pathway.tilepath[speed];
                    }



                    //if (speed < path.pathway.tilepath.Count - 1)
                    //finPos = path.pathway.tilepath[speed];


                    //AI_Potential_Order order = new AI_Potential_Order(unit.tilePosition, finPos, destination);

                    if (nextTile == null) {
                        Debug.LogError("NextTile Null");
                    }

                    int[] _tPos = getPlannedUnitPosition(brainCell);

                    PlannedOrder order = new PlannedOrder(unit, _tPos, finPos, nextTile);

                    if (direction != -1)
                        order.faceDirection = direction;

                    //AI Bridge Logic

                    if (bridge && (brainCell.currentFormation != 4 && brainCell.currentFormation != 5 && brainCell.currentFormation != 9)) {
                        int bridgeFormation = 4;
                        switch (brainCell.attachedUnit.classification) {
                            case UnitData.UnitClass.Infantry:
                                bridgeFormation = 4;
                                break;
                            case UnitData.UnitClass.Cavalry:
                                bridgeFormation = 5;
                                break;
                            case UnitData.UnitClass.Artillery:
                                bridgeFormation = 9;
                                break;
                        }

                        PlannedOrder bridgeOrder = new PlannedOrder(unit, bridgeFormation);
                        brainCell.AddPlan(bridgeOrder);
                        return bridgeOrder;
                    }


                    if ((destination != null && destination.Length < 2) && (brainCell.currentDestination != null && samePosition(brainCell.currentDestination, destination))) {
                        print("fail4");
                        return null;
                    }

                    brainCell.AddPlan(order);
                    brainCell.currentDestination = destination;

                    //brainCell.addPlannedOrder(order);
                    //brainCell.lastTurnActivated = CMND.cmd_TurnManager.currentTurnNumber;

                    //print(brainCell.attachedUnit.movementPoints);


                    return order;
                }
                else {
                    if (DEBUG_Errors || brainCell.deepDebug)
                        print("Path Failed. No Route!");
                    print("fail");
                    return null;
                }
            }
            return null;




            /*if (unit.currentOrder != null)
                aiOrderList.Add(selectedUnits[0].currentOrder);*/


        }

        

        PlannedOrder AIChargeOrder(AiUnitBrain brainCell) {


            UnitInfo inRange = getBestChargableUnit(brainCell);

            if (inRange != null && (brainCell.attackOrderCount == 0 || (brainCell.attackOrderCount < 2 && brainCell.Breech))) {
                PlannedOrder newOrder = new PlannedOrder(brainCell.attachedUnit, inRange.TilePosition, inRange.unitID, true);
                brainCell.AddPlan(newOrder);
                return newOrder;
            }

            return null;
        }
        #endregion

        #region Inits
        public void BootUp(int ai_id, int p_id, int f_id, Color p_aiColor, int t_id = 1, int deploymentZoneID = -1) {


            Debug.Log($"Boot up AI {deploymentZoneID}");
            if (deploymentZoneID >= 0) {
                deploymentZone = deploymentZoneID;
            }

            AiID = ai_id;
            AiPlayerID = p_id;
            AiFactionID = f_id;
            AiTeamID = t_id;
            CMND.cmd_TurnManager.BattlePhaseActivated += TurnEnded;
            aiColor = p_aiColor;

            int newAIColor = CMD.getNextAvailableColor(p_id);

            InitalizeAi();
            AIUnitDeployment();
            InitTerritory();
            aiPlayedGame = true;
            Debug.Log($"Booting Up AI");
            if (AiTactic > .90f) {
                isCheater = true;

            }

        }


        public void SetAiTactic(float amt) {
            if (CMD.forcedAIDifficulty != -1) {
                if (AiTactic != CMD.forcedAIDifficulty)
                    AiTactic = CMD.forcedAIDifficulty;
            }
            else {
                AiTactic = amt;
            }

        }

        void InitalizeAi() {
            if (currentGeneral != null) {
                SetAiTactic(currentGeneral.Mods.Difficulty);
                AiAggressiveness = currentGeneral.Mods.Aggressiveness;
            }
            ResetUnitBrains();
        }

        void InitTerritory() {
            territoryData = new AiTerritoryData[CMND.cmd_MapInfo.BoardSize[0], CMND.cmd_MapInfo.BoardSize[1]];
            List<int[]> deploymentZones = CMND.cmd_TileMap.getTeamDeploymentZones(deploymentZone);

            float averageDistance = (CMND.cmd_MapInfo.BoardSizeHalved[0] + CMND.cmd_MapInfo.BoardSizeHalved[1]) / 2;

            for (int x = 0; x < CMND.cmd_MapInfo.BoardSize[0]; x++) {
                for (int y = 0; y < CMND.cmd_MapInfo.BoardSize[1]; y++) {
                    int[] tilePos = new int[] { x, y };
                    TileTerrainInfo tInfo = CMND.cmd_TileMap.GetTile(tilePos);
                    AiTerritoryData tData = new AiTerritoryData(tilePos, this, tInfo);
                    territoryData[x, y] = tData;
                }
            }

            for (int i = 0; i < deploymentZones.Count; i++) {
                AiTerritoryData tData = territoryData[deploymentZones[i][0], deploymentZones[i][1]];
                tData.isOwnedByController = true;
                tData.baseWeight = 1f;
            }
        }
        #endregion

        #region Util
        int[] getNearestNeighborFromOLD(int[] tileTO, int[] tileFROM, UnitInfo unit) {
            List<int[]> neighboringTiles = new List<int[]>();


            int trackingValue = 0;

            int rangeLow = -trackingValue - 1;
            int rangeHigh = trackingValue + 2;


            for (int x = rangeLow; x < rangeHigh; x++) {
                for (int y = rangeLow; y < rangeHigh; y++) {
                    if (x == 0 && y == 0)
                        continue;

                    int[] tilePos = new int[] { tileTO[0] + x, tileTO[1] + y };
                    TileTerrainInfo tile = CMND.cmd_TileMap.GetTile(tilePos, true);


                    bool enemiesInTile = false;
                    if (tile != null) {
                        foreach (UnitInfo _U in tile.units_Defenders) {
                            if (_U != null && _U.team != AiPlayerID)
                                enemiesInTile = true;
                        }
                    }


                    if (tile != null && tile.DATA.IsAccessible() && tile.count_Defenders < 2 && !enemiesInTile) {
                        neighboringTiles.Add(tilePos);
                    }
                }
            }

            int[] minPos = new int[] { };
            float minDist = float.MaxValue;


            foreach (int[] tilePos in neighboringTiles) {
                float minValue = CMND.cmd_TileMap.TileDistance(tileFROM, tilePos);

                TileTerrainInfo tileD = CMD.CMND.cmd_TileMap.GetTile(tilePos, true);

                if (tileD == null || CMD.CMND.cmd_TileMap.GetTile(unit.TilePosition, true) == null || unit == null)
                    continue;

                if (tileD != null) {
                    minValue -= tileD.elevation;
                }

                PF_Properties pathProps = new PF_Properties(unit, tilePos, unit.faction, unit.PlayerNetworkID, false, unit.HasPerk("Rugged"));
                pathProps.aiPath = true;

                if (minValue < minDist && !CMND.cmd_MapInfo.ImpassableTile(tilePos, pathProps)) {
                    minPos = tilePos;
                    minDist = minValue;
                }
            }


            return minPos;
        }

        bool isInEnemies(UnitInfo info) {
            for (int i = 0; i < enemyUnits.Length; i++) {
                if (info.unitID == enemyUnits[i].unitID)
                    return true;
            }

            return false;
        }

        int[] getNearestNeighborFromOLD(UnitInfo target, int[] tileFROM, AiUnitBrain brain, bool checkFlankValue = false) {
            List<int[]> neighboringTiles = new List<int[]>();




            int[] trackingPos = target.TilePosition;

            if (isCheater && Random.Range(0, 4) != 0) {

                if (Random.Range(0, 3) == 0) {
                    target = getNearestEnemy(brain);
                }


                int[] checkPeak = StealEnemyPosition(target.unitID);
                if (checkPeak != null) {
                    trackingPos = checkPeak;
                    PrintAIDebug("Stole a unitPosition from playerOrders", brain.attachedUnitID);
                }

            }


            int trackingValue = 0;

            int rangeLow = -trackingValue - 1;
            int rangeHigh = trackingValue + 2;


            for (int x = rangeLow; x < rangeHigh; x++) {
                for (int y = rangeLow; y < rangeHigh; y++) {
                    if (x == 0 && y == 0)
                        continue;

                    int[] tilePos = new int[] { trackingPos[0] + x, trackingPos[1] + y };
                    TileTerrainInfo tile = CMND.cmd_TileMap.GetTile(tilePos, true);


                    bool enemiesInTile = false;
                    if (tile != null) {
                        foreach (UnitInfo _U in tile.units_Defenders) {
                            if (_U != null && _U.team != AiPlayerID)
                                enemiesInTile = true;
                        }
                    }


                    if (tile != null && tile.DATA.IsAccessible() && tile.count_Defenders < 2 && !enemiesInTile) {
                        neighboringTiles.Add(tilePos);
                    }
                }
            }

            int[] minPos = new int[] { };
            float minDist = float.MaxValue;

            foreach (int[] tilePos in neighboringTiles) {
                float checkValue = CMND.cmd_TileMap.TileDistance(tileFROM, tilePos) * 2;

                if (checkFlankValue) {

                    int flankDir = CMND.cmd_UnitManager.CheckFlanking(tilePos, target);
                    if (flankDir != 1)
                        checkValue = -flankDir;
                }

                TileTerrainInfo tileD = CMD.CMND.cmd_TileMap.GetTile(tilePos, true);

                if (tileD == null || CMD.CMND.cmd_TileMap.GetTile(brain.attachedUnit.TilePosition, true) == null || brain.attachedUnit == null)
                    continue;

                if (tileD != null) {
                    checkValue -= tileD.elevation * 2;
                }

                PF_Properties pathProps = new PF_Properties(brain.attachedUnit, tilePos, brain.attachedUnit.faction, brain.attachedUnit.PlayerNetworkID, false, brain.attachedUnit.HasPerk("Rugged"));
                pathProps.aiPath = true;

                if (checkValue < minDist && !CMND.cmd_MapInfo.ImpassableTile(tilePos, pathProps)) {
                    minPos = tilePos;
                    minDist = checkValue;
                }
            }


            return minPos;
        }
        public List<UnitInfo> getAiControlledUnits() {
            List<UnitInfo> temp = new List<UnitInfo>();
            for (int i = 0; i < CMD_Units.PlayerUnits[AiPlayerID].Count; i++) {
                temp.Add(CMD_Units.PlayerUnits[AiPlayerID][i]);
            }

            //print("AI #" + AiID + "with playerID:" + AiPlayerID + " unit count = " + temp.Count);
            return temp;
        }
        public float getDistanceToNearestDeploymentZone(int[] position) {
            float min = float.MaxValue;
            int[] closestDeploymentZone = null;

            List<int[]> deploymentZones = CMND.cmd_TileMap.getTeamDeploymentZones(deploymentZone);
            for (int i = 0; i < deploymentZones.Count; i++) {
                float dst = CMND.cmd_TileMap.TileDistance(position, deploymentZones[i]);
                if (dst < min) {
                    closestDeploymentZone = deploymentZones[i];
                    min = dst;
                }
            }

            if (closestDeploymentZone != null) {
                return min;
            }

            return -1;
        }
        int[] getNearestDeploymentZone(int[] position) {
            float min = float.MaxValue;
            int[] closestDeploymentZone = null;

            List<int[]> deploymentZones = CMND.cmd_TileMap.getTeamDeploymentZones(deploymentZone);
            for (int i = 0; i < deploymentZones.Count; i++) {
                float dst = CMND.cmd_TileMap.TileDistance(position, deploymentZones[i]);
                if (dst < min) {
                    closestDeploymentZone = deploymentZones[i];
                    min = dst;
                }
            }

            return closestDeploymentZone;
        }
        public AiUnitBrain getBrainFromUnit(UnitInfo unit) {
            for (int i = 0; i < unitBrains.Length; i++) {
                if (unitBrains[i].attachedUnit == unit)
                    return unitBrains[i];
            }
            return null;
        }
        bool withinMap(int[] pos) {
            if (pos[0] < 0 || pos[0] >= CMD.CMND.cmd_MapInfo.BoardSize[0] || pos[1] < 0 || pos[1] >= CMD.CMND.cmd_MapInfo.BoardSize[1])
                return false;

            return true;
        }
        int[] getPlannedUnitPosition(AiUnitBrain brain) {
            return brain.getPlannedPosition();
        }
        bool doubleMover(UnitInfo attached) {
            return (attached.HasPerk("breech_loading") || attached.HasPerk("efficiency"));
        }
        bool cumbersomeCheck(AiUnitBrain brain) {
            return (!brain.attachedUnit.HasPerk("cumbersome") || ((brain.moveOrderCount < 0 && brain.attackOrderCount < 0)));
        }
        int SortByPriority(AiUnitBrain p1, AiUnitBrain p2) {

            int p1Priority = getBrainPriority(p1);
            int p2Priority = getBrainPriority(p2);

            return p1Priority.CompareTo(p2Priority);
        }

        int getBrainPriority(AiUnitBrain brainToCheck) {
            int amt = 0;
            for (int i = 0; i < brainToCheck.aiPlans.Count; i++) {
                if (brainToCheck.aiPlans[i].attachedUnits.Count > 1)
                    amt += 3;

                if (brainToCheck.aiPlans[i].orderType == PlannedOrderType.FireOrder || brainToCheck.aiPlans[i].orderType == PlannedOrderType.ChargeOrder)
                    amt -= 5;

            }

            if (brainToCheck.currentHealthState != AiUnitBrain.HealthState.Normal) {
                if (brainToCheck.currentHealthState == AiUnitBrain.HealthState.Cohesionless)
                    amt -= 2;
                if (brainToCheck.currentHealthState == AiUnitBrain.HealthState.Wounded)
                    amt -= 4;
                if (brainToCheck.currentHealthState == AiUnitBrain.HealthState.Critical)
                    amt -= 6;

            }

            int distanceToTarget = 0;

            if (brainToCheck.currentTarget != -1 && brainToCheck.getPlannedPosition() != null && brainToCheck.getTargetInfo().TilePosition != null) {

                distanceToTarget = (int)CMND.cmd_TileMap.TileDistance(brainToCheck.getPlannedPosition(), brainToCheck.getTargetInfo().TilePosition);

                amt += distanceToTarget;

            }



            return amt;
        }

        int SortByDamageDealt(AiDamageToken p1, AiDamageToken p2) {
            return p1.plannedDamage.CompareTo(p2.plannedDamage);
        }
        bool samePosition(int[] pos1, int[] pos2) {
            if (pos1 == null || pos2 == null || pos1.Length < 2 || pos2.Length < 2)
                return false;

            if (pos1[0] == pos2[0] && pos1[1] == pos2[1])
                return true;

            return false;
        }
        #endregion

        #region Target Calculation

        UnitInfo getNearestEnemy(AiUnitBrain brain) {
            TeamUnits allUnits = CMND.cmd_Units.TeamUnits[enemyID];

            UnitInfo nearestUnit = null;
            float minDst = float.MaxValue;

            for (int i = 0; i < allUnits.units.Count; i++) {
                if (allUnits.units[i].hitpoints <= 0)
                    continue;

                float dst = CMND.cmd_TileMap.TileDistance(allUnits.units[i].TilePosition, brain.getPlannedPosition());
                if (dst < minDst) {
                    nearestUnit = allUnits.units[i];
                    minDst = dst;
                }
            }

            return nearestUnit;
        }

        public int[] rawTargetInRange(AiUnitBrain brain, bool targetOnly = false) {
            int[] tilePos = brain.getPlannedPosition();
            int range = brain.attachedUnit.GetRange();

            for (int x = -range; x < range; x++) {
                for (int y = -range; y < range; y++) {
                    TileTerrainInfo tile = CMND.cmd_TileMap.GetTile(new int[] { tilePos[0] + x, tilePos[1] + y }, true);
                    if (tile == null)
                        continue;


                    if (targetOnly) {

                        UnitInfo target = brain.getTargetInfo();
                        if (target == null)
                            continue;

                        if (samePosition(tile.tilePosition, target.TilePosition)) {
                            return tile.tilePosition;
                        }
                    }
                    else {
                        if (tile.count_AllUnits > 0 && tile.units_All != null) {
                            for (int i = 0; i < tile.units_All.Length; i++) {
                                if (tile.units_All[i] != null && tile.units_All[i].team == enemyID)
                                    return tile.tilePosition;
                            }
                        }
                    }



                }
            }

            return null;
        }

        public bool targetInRange(AiUnitBrain brain) {
            UnitInfo inRange = ReturnEnemyInCone(brain, getPlannedUnitPosition(brain), brain.getTargetInfo());

            if (brain.attachedUnit.classification == UnitData.UnitClass.Artillery) {
                return (inRange != null);
            }
            else {
                return (inRange == brain.getTargetInfo());
            }
        }

        bool inRangeButWrongFacing(int[] targetPos, AiUnitBrain brain) {
            bool inRange = targetInRange(brain);

            TileTerrainInfo tile = CMD.CMND.cmd_TileMap.GetTile(getPlannedUnitPosition(brain));

            if (inRange) {
                List<int[]> fireTiles = CMND.cmd_UnitManager.GetFiringPositionsRaw(brain.attachedUnit, brain.currentFormation, brain.currentDirection, tile);

                for (int i = 0; i < fireTiles.Count; i++) {
                    if (fireTiles[i][0] == targetPos[0] && fireTiles[i][1] == targetPos[1]) {
                        return true;
                    }
                }

            }

            return false;
        }

        UnitInfo CalculateOptimalTarget(AiUnitBrain brain, int[] injectPos = null) {
            UnitInfo unit = brain.attachedUnit;


            float maxValue = float.MinValue;
            UnitInfo currentTarget = null;

            int difficultyIndex;

            if (OverAtDifficultyTreshold(AiDifficultyType.VeryHard))
                difficultyIndex = 4;
            else if (OverAtDifficultyTreshold(AiDifficultyType.Hard))
                difficultyIndex = 3;
            else if (OverAtDifficultyTreshold(AiDifficultyType.Average))
                difficultyIndex = 2;
            else if (OverAtDifficultyTreshold(AiDifficultyType.Easy))
                difficultyIndex = 1;
            else
                difficultyIndex = 0;

            int[] unitPos = unit.TilePosition;
            if (injectPos != null)
                unitPos = injectPos;

            List<int[]> firingPositions = CMND.cmd_UnitManager.GetFiringPositionsRaw(unit, brain.currentFormation, brain.currentDirection, CMND.cmd_TileMap.GetTile(unitPos));
            if (firingPositions != null) {
                for (int x = 0; x < enemyUnits.Length; x++) {
                    bool inRage = false;

                    for (int ff = 0; ff < firingPositions.Count; ff++) {
                        if (firingPositions[ff][0] == enemyUnits[x].TilePosition[0] && firingPositions[ff][1] == enemyUnits[x].TilePosition[1])
                            inRage = true;
                    }



                    float targetValue = CMND.cmd_ai_observer.CalculateEnemyTargetValue(difficultyIndex, unit, enemyUnits[x]);

                    AiTargetToken targetToken = getUnitTargetData(enemyUnits[x].unitID);

                    int tokenTallyCount = 0;

                    if (targetToken != null) {
                        tokenTallyCount = targetToken.targetTallies.Count;
                        if (targetToken.targetTallies.Contains(unit.unitID)) {
                            tokenTallyCount = -5;
                        }

                        targetValue -= tokenTallyCount;
                    }




                    if (!inRage) {
                        float distBetween = CMND.cmd_TileMap.TileDistance(enemyUnits[x].TilePosition, brain.getPlannedPosition());
                        targetValue -= distBetween * .5f;
                    }


                    if (targetValue > maxValue) {
                        currentTarget = enemyUnits[x];
                        maxValue = targetValue;
                    }
                }
            }



            return currentTarget;
        }

        UnitInfo getBestChargableUnit(AiUnitBrain braincell) {
            List<UnitInfo> units = getChargableUnits(braincell);

            if (units == null)
                return null;

            int minHitpointsLeft = int.MaxValue;
            UnitInfo currentMinimum = null;

            for (int i = 0; i < units.Count; i++) {
                if (units[i].unitID == braincell.currentTarget)
                    return units[i];

                int friendlyCount = CMND.cmd_TileMap.ReturnTileUnitsByOwner(units[i].TilePosition, AiPlayerID).Count;
                for (int x = 0; x < unitBrains.Length; x++) {
                    for (int y = 0; y < unitBrains[x].aiPlans.Count; y++) {
                        if (unitBrains[x].aiPlans[y].getTargetInfo() != null && samePosition(unitBrains[x].aiPlans[y].getTargetInfo().TilePosition, units[i].TilePosition))
                            friendlyCount++;
                    }
                }


                int direction = CMND.cmd_UnitManager.ChargeDirection(braincell.attachedUnit, units[i]);


                int remainingLife = units[i].hitpoints + units[i].cohesion;
                if (remainingLife - units[i].GetRangedDamage() < minHitpointsLeft && friendlyCount < 2) {
                    currentMinimum = units[i];
                    minHitpointsLeft = remainingLife - units[i].GetTraitChargedDamage(braincell.attachedUnit) - CMND.cmd_UnitManager.getDirectionalChargeDamage(direction, units[i].formation)[1];
                }
            }

            return currentMinimum;
        }

        List<UnitInfo> getChargableUnits(AiUnitBrain braincell) {
            List<int[]> tiles = new List<int[]>();
            tiles = CMND.cmd_UnitManager.getChargableTiles(braincell.attachedUnit);

            List<UnitInfo> unitsInRange = new List<UnitInfo>();

            for (int i = 0; i < tiles.Count; i++) {
                unitsInRange.Add(CMND.cmd_TileMap.GetEnemyOnTile(tiles[i], braincell.attachedUnit.team));
            }

            return unitsInRange;
        }

        public UnitInfo[] filterTeamUnits() {
            List<UnitInfo> teamUnits = getAiControlledUnits();
            for (int i = 0; i < teamUnits.Count; i++) {
                if (teamUnits[i].hitpoints <= 0)
                    teamUnits.RemoveAt(i);
            }

            return teamUnits.ToArray();
        }

        public UnitInfo[] filterEnemyUnits() {
            List<UnitInfo> _enemyUnits = new List<UnitInfo>();

            int enemyTeam = 0;
            if (enemyTeam == AiTeamID)
                enemyTeam = 1;

            for (int x = 0; x < CMND.cmd_Units.TeamUnits[enemyTeam].units.Count; x++) {
                if (CMND.cmd_Units.TeamUnits[enemyTeam].units[x].hitpoints > 0)
                    _enemyUnits.Add(CMND.cmd_Units.TeamUnits[enemyTeam].units[x]);
            }

            //print(AiPlayerID+" EnemyCount: " + _enemyUnits.Count);
            return _enemyUnits.ToArray();
        }

        bool positionWithinFiringRange(AiUnitBrain brain, int[] tilePos) {
            List<int[]> positions = CMND.cmd_UnitManager.GetFiringPositionsRaw(brain.attachedUnit, brain.currentFormation, brain.currentDirection, CMD.CMND.cmd_TileMap.GetTile(tilePos));

            for (int i = 0; i < positions.Count; i++) {
                if (positions[i][0] == tilePos[0] && positions[i][1] == tilePos[1]) {
                    return true;
                }
            }

            return false;
        }

        UnitInfo ReturnEnemyInCone(AiUnitBrain brain, int[] tilePos, UnitInfo unitToCheck = null) {

            int minHitpointsLeft = int.MaxValue;
            UnitInfo currentMinimum = null;
            TileTerrainInfo l_tile = CMND.cmd_TileMap.GetTile(tilePos, true);

            TileTerrainInfo attachedTPos = CMND.cmd_TileMap.GetTile(brain.getPlannedPosition(), true);

            if (tilePos == null || l_tile == null || attachedTPos == null) {
                PrintAIDebug("Failed to return enemies [" + (tilePos == null) + ", " + (l_tile == null) + ", " + (attachedTPos == null) + "]", brain.attachedUnitID);
                return null;
            }

            bool prefireCheck = false;
            //Passes prefire check
            if (OverAtAggressionTreshold(AiAgressionType.Offensive) && OverAtDifficultyTreshold(AiDifficultyType.Average)) {

                if (Random.Range(0, 11) == 10)
                    prefireCheck = true;

            }


            int formationToCheck = brain.currentFormation;
            if (brain.attachedUnit.classification == UnitData.UnitClass.Artillery && formationToCheck == 9) {
                formationToCheck = 10;
            }


            List<int[]> positions = CMND.cmd_UnitManager.GetFiringPositionsRaw(brain.attachedUnit, formationToCheck, brain.currentDirection, l_tile, false, prefireCheck);

            foreach (int[] pos in positions) {
                TileTerrainInfo tile = CMND.cmd_TileMap.GetTile(pos, true);

                if (tile == null || tile.units_All == null)
                    continue;




                foreach (UnitInfo x in tile.units_All) {
                    if (x == null || x.team == AiTeamID)
                        continue;

                    if (unitToCheck != null && unitToCheck.hitpoints > 0 && x == unitToCheck)
                        return x;

                    if (x != null && x.hitpoints > 0) {
                        int remainingLife = brain.attachedUnit.hitpoints + brain.attachedUnit.cohesion;
                        if (remainingLife - brain.attachedUnit.GetRangedDamage() < minHitpointsLeft) {
                            currentMinimum = x;
                            minHitpointsLeft = remainingLife - brain.attachedUnit.GetRangedDamage();
                        }

                    }
                }
            }

            if (currentMinimum != null) {
                return currentMinimum;
            }
            else {
                return null;
            }

        }

        bool forceDeployment = false;
        #endregion

        #region Unit Brains
        void ResetUnitBrains() {
            livingUnits = filterTeamUnits();
            enemyUnits = filterEnemyUnits();
            if ((unitBrains == null || unitBrains.Length == 0) && (livingUnits != null && livingUnits.Length > 0) || (livingUnits.Length > unitBrains.Length)) {
                //No Brain Data
                InitUnitBrains();
            }
            else if (unitBrains != null && unitBrains.Length > 0 && livingUnits.Length > 0) {
                FilterUnitBrains();

                for (int i = 0; i < unitBrains.Length; i++) {
                    if (CMD.CMND.cmd_TileMap.GetTile(unitBrains[i].attachedUnit.TilePosition, true) == null) {
                        continue;
                    }

                    if (AiAggressiveness < .25f) {
                        if (unitBrains[i].attachedUnit.classification == UnitData.UnitClass.Cavalry && unitBrains[i].currentUnitGoal == AiUnitGoal.Territory) {
                            bool ambush = false;

                            float distance = float.MaxValue;
                            UnitInfo closest = getNearestEnemyUnit(unitBrains[i].getPlannedPosition(), out distance);
                            if (distance < unitBrains[i].attachedUnit.GetTotalSpeed() * 3)
                                ambush = true;

                            if (ambush) {
                                print("[AI] AI #" + AiID + "'s unit #" + unitBrains[i].attachedUnitID + " has entered ambush mode @ distance " + distance);
                                unitBrains[i].currentUnitGoal = AiUnitGoal.Domination;
                            }
                        }
                    }
                    else {
                        if (unitBrains[i].attachedUnit.classification == UnitData.UnitClass.Cavalry && unitBrains[i].currentUnitGoal == AiUnitGoal.Territory)
                            unitBrains[i].currentUnitGoal = AiUnitGoal.Domination;
                    }

                }
            }


        }

        void ResetDelayedUnitBrains() {
            if (unitsToDelay != null && unitsToDelay.Count > 0 && livingUnits.Length > 0) {
                FilterUnitBrains(unitsToDelay);

                for (int i = 0; i < unitsToDelay.Count; i++) {
                    if (AiAggressiveness < .25f) {
                        if (unitsToDelay[i].attachedUnit.classification == UnitData.UnitClass.Cavalry && unitsToDelay[i].currentUnitGoal == AiUnitGoal.Territory) {
                            bool ambush = false;

                            float distance = float.MaxValue;
                            UnitInfo closest = getNearestEnemyUnit(unitsToDelay[i].getPlannedPosition(), out distance);
                            if (distance < unitsToDelay[i].attachedUnit.GetTotalSpeed() * 3)
                                ambush = true;

                            if (ambush) {
                                print("[AI] AI #" + AiID + "'s unit #" + unitsToDelay[i].attachedUnitID + " has entered ambush mode @ distance " + distance);
                                unitsToDelay[i].currentUnitGoal = AiUnitGoal.Domination;
                            }
                        }
                    }
                    else {
                        if (unitsToDelay[i].attachedUnit.classification == UnitData.UnitClass.Cavalry && unitsToDelay[i].currentUnitGoal == AiUnitGoal.Territory)
                            unitsToDelay[i].currentUnitGoal = AiUnitGoal.Domination;
                    }

                }
            }


        }
        public int GetPathDistance(int[] origin, int[] dest) {
            PF_Properties pathSettings = new PF_Properties(origin, dest);
            pathSettings.checkForAllies = false;
            pathSettings.aiPath = true;

            PF_Return path = CMD.CMND.cmd_Pathfinding.GeneratePath(pathSettings, false);
            if (path.foundPath) {
                print("[AI] path found with length: " + (path.pathway.tilepath.Count - 2));
                return path.pathway.tilepath.Count - 2;
            }
            else return 99999;

        }

        UnitInfo getNearestEnemyUnit(int[] tilePos, out float distance) {

            float min = float.MaxValue;
            UnitInfo closest = null;

            for (int i = 0; i < enemyUnits.Length; i++) {
                float dst = CMD.CMND.cmd_TileMap.TileDistance(tilePos, enemyUnits[i].TilePosition);
                if (dst < min) {
                    closest = enemyUnits[i];
                    min = dst;
                }
            }

            if (closest != null) {
                min = GetPathDistance(tilePos, closest.TilePosition);
            }

            distance = min;
            return closest;
        }

        void InitUnitBrains() {
            List<AiUnitBrain> tempBrains = new List<AiUnitBrain>();
            for (int i = 0; i < livingUnits.Length; i++) {
                if (livingUnits[i] == null) {
                    Debug.Log("[AI Failure] unit data at index " + i + " is null");
                    continue;
                }

                tempBrains.Add(new AiUnitBrain(livingUnits[i], OverAtAggressionTreshold(AiAgressionType.Defensive)));
            }
            unitBrains = tempBrains.ToArray();
        }
        void FilterUnitBrains(List<AiUnitBrain> brainList = null) {
            List<AiUnitBrain> tempBrains = new List<AiUnitBrain>();
            if (brainList == null)
                tempBrains.AddRange(unitBrains);
            else
                tempBrains.AddRange(brainList);

            for (int i = 0; i < tempBrains.Count; i++) {
                if (tempBrains[i].attachedUnit.hitpoints <= 0 || tempBrains[i].attachedUnit.PlayerNetworkID != AiPlayerID) {
                    tempBrains.RemoveAt(i);
                    continue;
                }
                else {
                    tempBrains[i].ClearPlans();
                }

                if (tempBrains[i].currentTarget != -1 && CMND.cmd_Units.GetUnit(tempBrains[i].currentTarget).hitpoints <= 0)
                    tempBrains[i].currentTarget = -1;
            }

            if (brainList == null)
                unitBrains = tempBrains.ToArray();
            else
                unitsToDelay = tempBrains;
        }
        #endregion

        #region Turn Management
        public void TurnEnded() {
            AiTurnReset();
        }

        public void AiTurnReset() {
            //ResetUnitBrains();
            turnComplete = false;
            aiEndedTurn = false;
        }

        bool aiEndedTurn = false;
        public void AiForceEndsTurn() {
            if (!aiEndedTurn) {
                CMND.cmd_TurnManager.EndedTurn(false);
                aiEndedTurn = true;
            }
        }
        #endregion
    }
    

    public class AiTerritoryData
    {
        public int[] position;
        public bool isOwnedByController;

        public bool walkable;
        public int elevation;
        public int cover;
        public bool bridge;
        public bool bridgeAdj;
        public bool river;
        public bool riverAdj;
        public bool muck;


        public float baseWeight;

        float averageDistance;
        float deploymentDistance;

        List<int[]> deploymentZones;

        public List<int> plannedOccupants = new List<int>();

        public AiTerritoryData(int[] pos, CMD_AI_Controller2 controller, TileTerrainInfo src) {
            position = pos;
            isOwnedByController = false;

            deploymentZones = CMD.CMND.cmd_TileMap.getTeamDeploymentZones(controller.deploymentZone);

            averageDistance = (CMD.CMND.cmd_MapInfo.BoardSizeHalved[0] + CMD.CMND.cmd_MapInfo.BoardSizeHalved[1]) / 2;
            deploymentDistance = controller.getDistanceToNearestDeploymentZone(pos);
            setWeightValues(src);
            baseWeight = Calculate(controller, null);

        }

        public void ClearOccupants() {
            plannedOccupants.Clear();
        }

        public void setOwnership(bool state) {
            isOwnedByController = state;
        }

        public void setWeightValues(TileTerrainInfo src) {
            elevation = src.elevation;
            cover = src.coverLevel;
            bridge = src.isBridge;

            walkable = src.DATA.IsAccessible();
            muck = (src.DATA.HasModifier(1) || src.DATA.HasModifier(2));

            List<TileTerrainInfo> neighbors = src.getNeighbouringTilesInfo();


            for (int i = 0; i < neighbors.Count; i++) {
                if (neighbors[i].isBridge)
                    bridgeAdj = true;

                if (!neighbors[i].DATA.IsAccessible() && src.DATA.IsAccessible()) {
                    riverAdj = true;
                }
            }
        }

        public float Calculate(CMD_AI_Controller2 controller, FM.BattleAI.AiUnitBrain brain) {
            int[] tilePos = position;


            int bridgeValue = 2;

            float _baseWeight = 0;

            if (deploymentDistance >= averageDistance) {
                _baseWeight = (10f - deploymentDistance);
            }
            else {
                _baseWeight = ((deploymentDistance) * 2f);
            }

            float coverAmt = cover * 3;
            if (muck && (brain == null || !brain.attachedUnit.HasPerk(12)))
                coverAmt = -5f;

            float calc = _baseWeight + coverAmt;

            if (brain == null || brain.attachedUnit.GetRange() > 0) {
                float offset = 1;
                if (brain != null && brain.attachedUnit.classification == UnitData.UnitClass.Artillery)
                    offset = 2.5f;

                calc += elevation * offset;
            }

            if (riverAdj) {
                calc += bridgeValue / 3;
            }
            else if (river)
                calc -= bridgeValue / 3;


            if (bridgeAdj) {
                calc += bridgeValue;
            }
            else if (bridge)
                calc -= bridgeValue;

            int maxX = CMD.CMND.cmd_MapInfo.BoardSize[0] - 2;
            int maxY = CMD.CMND.cmd_MapInfo.BoardSize[1] - 2;

            //Edge check
            if ((tilePos[0] < 2 || tilePos[0] >= maxX) || (tilePos[1] < 2 || tilePos[0] >= maxY))
                calc *= .25f;

            if (calc < 0 || !walkable)
                calc = 0;
            if (calc > 10) {
                calc = 10;
            }

            return calc;
        }
    }

    
}