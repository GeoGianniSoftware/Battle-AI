using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FM.BattleAI
{

    [System.Serializable]
    public class PlannedOrder
    {
        [HideInInspector]
        public List<UnitInfo> attachedUnits;
        public PlannedOrderType orderType;

        public int[] orderOriginPos;
        public int[] orderTargetPos;
        public int[] orderNextTilePos;
        public int faceDirection = -1;
        public int formation = -1;
        public int targetUnitID;

        public bool swapReserve = false;

        public PlannedOrder(PlannedOrder clone) {
            this.attachedUnits = clone.attachedUnits;
            this.orderType = clone.orderType;

            if (clone.orderOriginPos != null && clone.orderOriginPos.Length == 2) {
                this.orderOriginPos = new int[2];
                System.Array.Copy(clone.orderOriginPos, this.orderOriginPos, clone.orderOriginPos.Length);
            }


            if (clone.orderTargetPos != null && clone.orderTargetPos.Length == 2) {
                this.orderTargetPos = new int[2];
                System.Array.Copy(clone.orderTargetPos, this.orderTargetPos, clone.orderTargetPos.Length);
            }

            if (clone.orderNextTilePos != null && clone.orderNextTilePos.Length == 2) {
                this.orderNextTilePos = new int[2];
                System.Array.Copy(clone.orderNextTilePos, this.orderNextTilePos, clone.orderNextTilePos.Length);
            }

            this.faceDirection = clone.faceDirection;
            this.formation = clone.formation;
            this.targetUnitID = clone.targetUnitID;
        }
        public PlannedOrder(UnitInfo attachedUnit, int[] orderOriginPos, int[] orderTargetPos, int[] nextTile) {
            this.attachedUnits = new List<UnitInfo> { attachedUnit };
            this.orderType = PlannedOrderType.MoveOrder;
            this.orderOriginPos = orderOriginPos;
            if (nextTile == null) {
                Debug.Log("Next Tile is Null");
            }
            this.orderNextTilePos = nextTile;
            this.orderTargetPos = orderTargetPos;
        }
        public PlannedOrder(UnitInfo attachedUnit, int[] orderTargetPos, int targetUnitID, bool isCharging) {
            this.attachedUnits = new List<UnitInfo> { attachedUnit };
            if (isCharging)
                this.orderType = PlannedOrderType.ChargeOrder;
            else
                this.orderType = PlannedOrderType.FireOrder;

            this.orderOriginPos = attachedUnit.TilePosition;
            this.orderTargetPos = orderTargetPos;
            this.targetUnitID = targetUnitID;
            this.faceDirection = attachedUnit.direction;
        }
        public PlannedOrder(UnitInfo attachedUnit, int formation) {
            this.attachedUnits = new List<UnitInfo> { attachedUnit };
            this.orderType = PlannedOrderType.FormationOrder;
            this.orderOriginPos = attachedUnit.TilePosition;
            this.formation = formation;
        }

        public PlannedOrder(UnitInfo attachedUnit, bool fireAtWill = true) {
            this.attachedUnits = new List<UnitInfo> { attachedUnit };
            this.orderType = PlannedOrderType.FireAtWill;
            this.orderOriginPos = attachedUnit.TilePosition;
        }

        public UnitInfo getTargetInfo() {
            if (targetUnitID == -1)
                return null;

            return CMD.CMND.cmd_Units.GetUnit(targetUnitID);
        }

        public bool isSame(PlannedOrder compare) {
            if (compare.formation != formation)
                return false;

            for (int i = 0; i < compare.attachedUnits.Count; i++) {
                if (!attachedUnits.Contains(compare.attachedUnits[i])) {
                    return false;
                }
                    
            }

            if(orderOriginPos != null && compare.orderOriginPos != null) {
                if (orderOriginPos[0] != compare.orderOriginPos[0] || orderOriginPos[1] != compare.orderOriginPos[1])
                    return false;
            }
            

            if (orderTargetPos != null && compare.orderTargetPos != null) {
                if (orderTargetPos[0] != compare.orderTargetPos[0] || orderTargetPos[1] != compare.orderTargetPos[1])
                    return false;
            }
            

            if (orderNextTilePos != null && compare.orderNextTilePos != null) {
                if (orderNextTilePos[0] != compare.orderNextTilePos[0] || orderNextTilePos[1] != compare.orderNextTilePos[1])
                    return false;
            }
            

            return true;
        }

    }

}
