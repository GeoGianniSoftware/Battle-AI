using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "newGeneral", menuName = "AI/General/New General")]
public class E_AI_General: ScriptableObject
{
    public string Name;
    public AI_General_Modifiers Mods;
    public List<AI_General_Trait> Traits = new List<AI_General_Trait>();
    
}
[System.Serializable]
public class AI_General_Modifiers
{
    [Range(0f, 1f)]
    public float Aggressiveness = 0.5f;

    [Range(0f, 1f)]
    public float Difficulty = 0.5f;
    
    public AI_General_Modifiers(float a, float d) {
        Aggressiveness = a;
        Difficulty = d;
    }

}
