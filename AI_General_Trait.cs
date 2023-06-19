using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "newGeneralTrait", menuName = "AI/General/New Trait")]
public class AI_General_Trait : ScriptableObject
{
    public string Name;
    public int index;
    public string slug;
    [TextArea(2, 5)]
    public string description;
}
