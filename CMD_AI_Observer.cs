using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CMD_AI_Observer : MonoBehaviour
{
    public CMD CMND;

    public bool AI_DEBUG = false;
    public bool AI_DEBUG_MOVEMENT = false;
    public bool AI_DEBUG_TERRITORY = false;
    [Header("Warning: May Cause FPS Drop")]
    public bool AI_DEBUG_TERRITORY_THREAT = false;
    public Gradient threatColor;

    public TerritoryNode[,] territories;
    public int lastTurnObserved;

    // Start is called before the first frame update
    void Start() {
        //territories = new int[CMND.cmd_MapInfo.BoardSize[0], CMND.cmd_MapInfo.BoardSize[1]];
    }

    // Update is called once per frame
    void Update() {
        if(lastTurnObserved != CMND.cmd_TurnManager.currentTurnNumber) {
            ObserverTick();
            System.GC.Collect();
        }
    }

    public void ObserverTick() {

        CalculateTotalTerritory();

        lastTurnObserved = CMND.cmd_TurnManager.currentTurnNumber;
    }


    private void OnDrawGizmos() {
        if (!AI_DEBUG || CMND == null || CMND.cmd_Units == null)
            return;

        /*
        if (CMND.cmd_Units.TeamUnits.Length > 0)
        {
            foreach (UnitInfo unit in CMND.cmd_Units.TeamUnits[0].units)
            {
                CalculateUnitObserver(unit);
            }
        }

        if (CMND.cmd_Units.TeamUnits.Length > 1)
        {
            foreach (UnitInfo unit in CMND.cmd_Units.TeamUnits[1].units)
            {
                CalculateUnitObserver(unit);
            }
        }
        */
        
        if(AI_DEBUG_TERRITORY)
            DrawTerritory();
        
        if (AI_DEBUG_TERRITORY_THREAT && territories != null) {
            for (int x = 0; x < CMND.cmd_MapInfo.BoardSize[0]; x++) {
                for (int y = 0; y < CMND.cmd_MapInfo.BoardSize[1]; y++) {
#if UNITY_EDITOR
                    int[] tilePos = new int[] { x, y };
                    Vector3 tilePosition = CMND.cmd_TileMap.TilePositionToTileSurface(tilePos);

                    GUIStyle guiStyle = new GUIStyle();
                    guiStyle.fontSize = 18;
                    guiStyle.normal.textColor = Color.white;

                    guiStyle.normal.background = testTexture(20,20, Color.black);

                    UnityEditor.Handles.Label(tilePosition + Vector3.up, "Threat: " + territories[x,y].currentThreatLevel[0]+", "+ territories[x, y].currentThreatLevel[1]);
#endif
                }
            }
        }
    }

    Texture2D testTexture(int width, int height, Color c) {
        Texture2D test = new Texture2D(width, height);

        for (int x = 0; x < test.width; x++) {
            for (int y = 0; y < test.height; y++) {
                test.SetPixel(x, y, c);
            }
        }
        test.Apply();
        return test;
    }


    public float getTileThreatLevel(int[] tilePos, int teamID) {
        if (!CMND.aiEnabled) return 0;
        if (CMND.cmd_TurnManager.deploymentPhase)
            return -1;

        List<UnitInfo> enemyUnits = CMND.cmd_Units.TeamUnits[0].units;
        if (teamID == 0 && CMND.cmd_Units.TeamUnits.GetLength(0) > 1)
            enemyUnits = CMND.cmd_Units.TeamUnits[1].units;

        List<UnitInfo> unitsThreateningTile = new List<UnitInfo>();
        float threatLevel = 0;

        List<UnitInfo> neighbourData = CheckNeighborsForEnemys(tilePos, teamID);

        if (neighbourData != null) {
            foreach(UnitInfo enemy in neighbourData) {
                unitsThreateningTile.Add(enemy);

                threatLevel += getDirectionalThreat(tilePos, enemy);
            }


        }

        foreach (UnitInfo enemy in enemyUnits) {
            if (enemy.hitpoints <= 0 || enemy.GetRangedDamage() <= 0 || enemy.GetRange() <= 0 || CMND.cmd_TileMap.TileDistance(tilePos, enemy.TilePosition) > enemy.GetRange())
                continue;


            List<int[]> positions = CMND.cmd_UnitManager.GetFiringPositions(enemy);
            if(positions != null && positions.Count > 0) {
                foreach (int[] firingPos in positions)
                    if (firingPos[0] == tilePos[0] && firingPos[1] == tilePos[1] && !unitsThreateningTile.Contains(enemy)) {
                        unitsThreateningTile.Add(enemy);

                        threatLevel += getDirectionalThreat(tilePos, enemy);



                    }
                        
            }

            
        }

        return threatLevel;
    }

    float getDirectionalThreat(int[] tilePos, UnitInfo enemy) {
        if (enemy == null)
            return 0f;

        if (CMND.cmd_TileMap.GetTile(tilePos, true) == null)
            return -1f;


        float threatLevel = 0f;

        int enemyTileDirectionToward = getDirectionTowardTile(enemy.TilePosition, tilePos);

        

        int direction = CMND.cmd_UnitManager.CheckFlanking(tilePos, enemy);

        


        if (direction == 1) {
            threatLevel += 1f;
        }
        else if (direction == 2) {
            threatLevel += .4f;
        }
        else {
            threatLevel += .25f;
        }

        return threatLevel;
    }

    public int getDirectionTowardTile(int[] tilePosA, int[] tilePosB) {


        bool up = (tilePosB[1] > tilePosA[1]);
        bool right = (tilePosB[0] > tilePosA[0]);
        bool down = (tilePosB[1] < tilePosA[1]);
        bool left = (tilePosB[0] < tilePosA[0]);

        if (up && right)
            return 1;
        if (up && left)
            return 7;
        if (down && right)
            return 3;
        if (down && left)
            return 5;



        if (up)
            return 0;
        if (right)
            return 2;
        if (down)
            return 4;
        if (left)
            return 6;

        return -1;
    }

    public UnitInfo CheckNeighborsForEnemy(int[] check, int teamID) {
        List<int[]> neighboringTiles = new List<int[]>();

        for (int x = -1; x < 2; x++) {
            for (int y = -1; y < 2; y++) {
                if (CMND.cmd_TileMap.GetTile(new int[] { check[0] + x, check[1] + y }, true) != null && !(x == 0 && y == 0)) {
                    neighboringTiles.Add(new int[] { check[0] + x, check[1] + y });
                }
            }
        }


        foreach (int[] tilePos in neighboringTiles) {
            foreach (UnitInfo unit in CMND.cmd_TileMap.GetTile(tilePos).units_Defenders) {
                if (CMND.cmd_TileMap.EnemyOnTile(tilePos, teamID)) {
                    return unit;
                }

            }
        }

        return null;
    }

    public List<UnitInfo> CheckNeighborsForEnemys(int[] check, int teamID) {
        List<int[]> neighboringTiles = new List<int[]>();

        for (int x = -1; x < 2; x++) {
            for (int y = -1; y < 2; y++) {
                if (CMND.cmd_TileMap.GetTile(new int[] { check[0]+x, check[1]+y }, true) != null && !(x == 0 && y == 0)) {
                    neighboringTiles.Add(new int[] { check[0] + x, check[1] + y });
                }
            }
        }

        List<UnitInfo> enemies = new List<UnitInfo>();

        foreach (int[] tilePos in neighboringTiles) {
            for (int i = 0; i < CMND.cmd_TileMap.GetTile(tilePos).count_Defenders; i++) {
                if (CMND.cmd_TileMap.EnemyOnTile(tilePos, teamID)) {
                    
                    enemies.Add(CMND.cmd_TileMap.GetTile(tilePos).units_Defenders[i]);
                    //print(enemies[enemies.Count - 1].unitID + " " + enemies[enemies.Count - 1].PlayerNetworkID + " " + teamID);
                }
            }
        }
        if (enemies.Count > 0)
            return enemies;
        return null;
    }

    public void CalculateTotalTerritory() {
        if (!territoriesInit) {
            //print("Init!");
            InitTerritories();
        }

        CalculateUnitTerritory();
    }
    /*
    public void CalculateUnitObserver(UnitInfo unit) {


        if (unit.PlayerNetworkID == 0)
            Gizmos.color = Color.green;
        else
            Gizmos.color = Color.red;


        Gizmos.DrawCube(CMND.cmd_TileMap.TilePositionToTileSurface(unit.tilePosition) + Vector3.up / 2, Vector3.one * .25f);



        //Threat Cube
        float threatLevel = GetUnitThreatLevel(unit);

        Gizmos.color = threatColor.Evaluate(threatLevel);
        Gizmos.DrawCube(CMND.cmd_TileMap.TilePositionToTileSurface(unit.tilePosition) + Vector3.up, Vector3.one * .1f);

#if UNITY_EDITOR
        if (AI_DEBUG)
            UnityEditor.Handles.Label(CMND.cmd_TileMap.TilePositionToTileSurface(unit.tilePosition) + Vector3.up * .9f, "" + threatLevel);

        if (CMND.cmd_UnitManager.SelectedUnits.Count > 0) {
                UnityEditor.Handles.Label(CMND.cmd_TileMap.TilePositionToTileSurface(unit.tilePosition) + Vector3.up * 1.5f, "" + CalculateRelativeUnitThreat(CMND.cmd_UnitManager.SelectedUnits[0],unit));
        }

#endif
        E_AI_ProcessCell braincell =CMND.cmd_ai_controller.getCellFromUnitID(unit.unitID);
        if(braincell != null) {
            foreach (AI_Potential_Order _orders in braincell.plannedOrders) {
                Debug.DrawLine(unit.GetUnit().transform.position, CMND.cmd_TileMap.TilePositionToTileSurface(_orders.destTile));
            }
        }

        //Movement
        if (!AI_DEBUG_MOVEMENT)
            return;

        if (unit.PlayerNetworkID == 0)
            Gizmos.color = Color.green;
        else
            Gizmos.color = Color.red;

        int[][] moveTiles = getMoveTiles(unit);
        for (int i = 0; i < moveTiles.GetLength(0); i++) {
            Gizmos.DrawCube(CMND.cmd_TileMap.TilePositionToTileSurface(new int[] { moveTiles[i][0], moveTiles[i][1] }), Vector3.one * .25f);

        }
    }
    */
    

    public int[][] getMoveTiles(UnitInfo unit) {
        List<int[]> finTiles = new List<int[]>();

        int[][] navTiles = CMND.cmd_Pathfinding.RetrieveNavigableTiles(new PF_Properties(unit, new int[] { -500, -500 }, unit.team, unit.PlayerNetworkID, false, unit.HasPerk(12)), unit.movementPoints);



        

        return navTiles;
    }
    public int[][] getMoveTiles(UnitInfo unit, int[] tilePos) {
        List<int[]> finTiles = new List<int[]>();

        int[][] navTiles = CMND.cmd_Pathfinding.RetrieveNavigableTiles(new PF_Properties(unit, tilePos, new int[] { -500, -500 }, unit.team, unit.PlayerNetworkID, false, unit.HasPerk(12)), unit.movementPoints);





        return navTiles;
    }

    void DrawTerritory() {
        for (int x = 0; x < CMND.cmd_MapInfo.BoardSize[0]; x++) {
            for (int y = 0; y < CMND.cmd_MapInfo.BoardSize[1]; y++) {

                if (territories[x, y].currentOwner == 0) {
                    Gizmos.color = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, .25f);


                    Gizmos.DrawCube(CMND.cmd_TileMap.TilePositionToTileSurface(new int[] { x, y }), Vector3.one);
                }
                else if (territories[x, y].currentOwner == 1) {
                    Gizmos.color = new Color(Color.blue.r, Color.blue.g, Color.blue.b, .25f);

                    Gizmos.DrawCube(CMND.cmd_TileMap.TilePositionToTileSurface(new int[] { x, y }), Vector3.one);
                }



            }
        }
    }

    bool territoriesInit = false;
    void InitTerritories() {
        territories = new TerritoryNode[CMND.cmd_MapInfo.BoardSize[0], CMND.cmd_MapInfo.BoardSize[1]];

        for (int x = 0; x < CMND.cmd_MapInfo.BoardSize[0]; x++) {
            for (int y = 0; y < CMND.cmd_MapInfo.BoardSize[1]; y++) {
                territories[x, y] = new TerritoryNode();
                
            }
        }
        territoriesInit = true;
    }

    public void CalculateUnitTerritory() {


        foreach (UnitInfo unit in CMD_Units.BoardUnitsInfo) {
            if (unit.hitpoints <= 0 || unit.TilePosition[0] < 0 || unit.TilePosition[1] < 0 || unit.TilePosition[0] >= CMND.cmd_MapInfo.BoardSize[0] || unit.TilePosition[1] >= CMND.cmd_MapInfo.BoardSize[1])
                continue;



            if (unit.GetTile().count_AllUnits > 0 && unit.GetTile().units_All[0].PlayerNetworkID == unit.PlayerNetworkID) {
                territories[unit.GetTile().tilePosition[0], unit.GetTile().tilePosition[1]].currentOwner = unit.PlayerNetworkID;
                territories[unit.GetTile().tilePosition[0], unit.GetTile().tilePosition[1]].currentThreatLevel = new float[] { getTileThreatLevel(new int[] { unit.GetTile().tilePosition[0], unit.GetTile().tilePosition[1] }, 0), getTileThreatLevel(new int[] { unit.GetTile().tilePosition[0], unit.GetTile().tilePosition[1] }, 1) };
            }

            


            foreach (TileTerrainInfo tileInfo in CMND.cmd_TileMap.GetTile(unit.TilePosition).getNeighbouringTilesInfo()) {
                if (tileInfo.count_AllUnits > 0 && tileInfo.units_All[0].PlayerNetworkID != unit.PlayerNetworkID)
                    continue;

                territories[tileInfo.tilePosition[0], tileInfo.tilePosition[1]].currentOwner = unit.PlayerNetworkID;
                territories[tileInfo.tilePosition[0], tileInfo.tilePosition[1]].currentThreatLevel = new float[] { getTileThreatLevel(new int[] { tileInfo.tilePosition[0], tileInfo.tilePosition[1] }, 0), getTileThreatLevel(new int[] { tileInfo.tilePosition[0], tileInfo.tilePosition[1] }, 1) };
            }
        }

    }

    public float getPerkOffset(UnitPerk p) {
        float threat = 0f;

        threat += (p.price / 1000);

        if (p.rangeResistant)
            threat += 0.025f;

        if (p.rangeVulnerable)
            threat -= 0.025f;

        if (p.chargeResistant)
            threat += 0.025f;

        if (p.chargeVulnerable)
            threat -= 0.025f;

        if (p.chargeInvulnerable)
            threat += 0.05f;

        if (p.easilyBroken)
            threat -= 0.05f;

        if (p.slug == "disciplined_melee" || p.slug == "shock")
            threat += .07f;

        if (p.slug == "efficiency" || p.slug == "skirmishing")
            threat += .1f;

        return threat;
    }

    public float GetUnitThreatLevel(UnitInfo unitToJudge) {
        float threat = 0f;
        
        threat += (unitToJudge.GetTotalSpeed()/5f) * .50f;

        float[] ranges = new float[] { .0f, .05f, .15f, .25f, 0.4f, .5f, .5f, .5f};

        threat += (ranges[unitToJudge.GetRange()]);

        float[] rangeDamages = new float[] { 0f, .1f, .225f, .35f, .4f };

        threat += rangeDamages[unitToJudge.GetRangedDamage()];
        
        //Factor in perk price

        for (int i = 0; i < unitToJudge.unitType.perkTypes.Length; i++) {
            int index = unitToJudge.unitType.perkTypes[i];

            UnitPerk p = CMND.cmd_AssetManager.UnitPerks[index];

            threat += getPerkOffset(p);
        }

        //Vulns
        for (int i = 0; i < unitToJudge.temporaryModifiers.Count; i++) {
            UnitPerk p = unitToJudge.temporaryModifiers[i];

            threat += getPerkOffset(p);
        }

        //Direction Facing
        float[] cohesions = new float[] { -.4f, .0f, .05f, .1f, .225f, 0.38f, .5f };

        threat += cohesions[unitToJudge.cohesion];

        if(unitToJudge.cohesion > 0)
        threat += (unitToJudge.hitpoints/8f) * .15f;
        else {
            threat *= ((float)unitToJudge.hitpoints / (float)unitToJudge.unitType.maxHitpoints);
        }


        if (threat > 1)
            threat = 1f;

        return threat;
    }

    public float CalculateEnemyTargetValue(float aiDifficulty, UnitInfo viewer, UnitInfo target) {
        //float viewerThreat = GetUnitThreatLevel(viewer);
        //float targetThreat = GetUnitThreatLevel(target);


        //1 = front, 2 = side, 3 = back
        int flankDir = CMND.cmd_UnitManager.CheckFlanking(viewer.TilePosition, target);

        float targetValue = 0;
        int difficultyIndex = 0;

        TileTerrainInfo enemyTile = target.GetTile();
        if(enemyTile != null && enemyTile.coverLevel > 0) {

            if (viewer.HasPerk(0) || viewer.HasPerk(1) || viewer.HasPerk(2) || viewer.HasPerk(6))
                targetValue += 2;
            else
                targetValue -= 2;

        }


        switch (viewer.classification) {
            case UnitData.UnitClass.Infantry:
                if (target.classification == UnitData.UnitClass.Cavalry)
                    targetValue++;

                if (target.classification == UnitData.UnitClass.Artillery)
                    targetValue--;

                if(difficultyIndex >= 1) {
                    //if (target.HasPerk(14))
                        //targetValue++;
                    if (target.HasPerk(6))
                        targetValue--;
                }

                if(difficultyIndex >= 2) {

                    if(target.classification != UnitData.UnitClass.Cavalry) {
                        if (target.GetRange() < viewer.GetRange())
                            targetValue++;

                        if (target.GetRange() > viewer.GetRange())
                            targetValue--;
                    }
                    
                }

                if(difficultyIndex >= 3) {
                    if (target.HasPerk(2))
                        targetValue++;

                    if (target.HasPerk(0))
                        targetValue++;
                }

                if(viewer.equippedWeapon != null) {
                    if (viewer.equippedWeapon.isRifledBreech()) {

                    }else if (viewer.equippedWeapon.isRifle()) {
                        if (difficultyIndex >= 3 && target.perkModifiers.rangeResistant)
                            targetValue--;
                        if (difficultyIndex >= 1 && target.currentCoverLevel > 0) {
                            if (viewer.HasPerk(1))
                                targetValue++;
                            targetValue++;
                        }
                            

                    }
                    else if (viewer.equippedWeapon.isBreech()) {
                        if (difficultyIndex >= 3 && target.perkModifiers.rangeResistant)
                            targetValue++;
                    }
                    else {

                    }

                }
                
                //Melee Focus Infantry
                if(viewer.HasPerk(0) || viewer.HasPerk(2)) {
                    if (difficultyIndex >= 4 && target.HasPerk(11))
                        targetValue++;

                    if (target.perkModifiers.chargeVulnerable)
                        targetValue++;
                }

                break;
            case UnitData.UnitClass.Cavalry:
                if (target.classification == UnitData.UnitClass.Artillery)
                    targetValue+= 2;

                if (target.classification == UnitData.UnitClass.Infantry)
                    targetValue--;

                if (difficultyIndex >= 1) {
                    if (target.HasPerk(0))
                        targetValue--;
                }

                if (difficultyIndex >= 4) {
                    if (target.HasPerk(11))
                        targetValue++;
                }

                if(difficultyIndex >= 3) {
                    if (flankDir == 2) {
                        targetValue++;
                    }
                    if (flankDir == 3) {
                        targetValue += 2;
                    }

                    if(difficultyIndex >= 4 && (flankDir == 3 || flankDir == 2) && target.HasPerk(0)) {
                        targetValue += 2;
                    }
                }
                

                //Carbine
                if (viewer.equippedWeapon != null) {
                    if (difficultyIndex >= 2 && target.currentCoverLevel > 0)
                        targetValue--;

                    if (difficultyIndex >= 3 && target.GetRange() > 1)
                        targetValue--;
                }
                //Melee
                else {
                    if (difficultyIndex >= 1 && target.HasPerk(11))
                        targetValue++;
                    if (difficultyIndex >= 1 && target.perkModifiers.chargeVulnerable)
                        targetValue++;

                    if (flankDir == 0)
                        targetValue-= 2;
                    if (difficultyIndex >= 2 && target.HasPerk(2))
                        targetValue++;

                    if (difficultyIndex >= 3 && target.currentCoverLevel > 0)
                        targetValue++;

                    if (target.formation == 2)
                        targetValue = -1;
                }

                break;
            case UnitData.UnitClass.Artillery:
                if (target.classification == UnitData.UnitClass.Infantry)
                    targetValue++;

                if (target.classification == UnitData.UnitClass.Cavalry)
                    targetValue--;

                if(difficultyIndex >= 1) {
                    if (target.perkModifiers.rangeVulnerable)
                        targetValue++;

                    if (target.HasPerk(14))
                        targetValue++;
                }
                if (difficultyIndex >= 2) {
                    if (target.currentCoverLevel > 0)
                        targetValue++;
                    if (target.HasPerk(13))
                        targetValue++;
                }
                if (difficultyIndex >= 3) {
                    if (target.HasPerk(2))
                        targetValue++;
                }
                break;
        }

        /*
        if (viewer.classification == UnitData.UnitClass.Cavalry && (target.HasPerk(2)))
            targetThreat *= 1.3f;

        if (viewer.classification == UnitData.UnitClass.Cavalry && (target.perkModifiers.chargeVulnerable))
            targetThreat *= .75f;

        if (viewer.classification != UnitData.UnitClass.Cavalry && (target.perkModifiers.rangeVulnerable))
            targetThreat *= .75f;


        

        float divider = 1;

        if (distanceBetween > 0)
            divider = distanceBetween;

        float baseline = targetThreat/ divider;

        */
        



        return targetValue;
    }


}
public class TerritoryNode
{
    public int currentOwner;
    public float[] currentThreatLevel = new float[2];

    public TerritoryNode() {
        currentOwner = 0;
    }
}

