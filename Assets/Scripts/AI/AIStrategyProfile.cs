using UnityEngine;
using System.Collections.Generic;

public enum AITriggerType
{
    TimeElapsed,        // Po X sekundach
    EnemyDistance,      // Gdy wróg podejdzie na X metrów
    CasualtiesPercent,  // Gdy stracimy X% wojska
    Immediate           // Od razu po poprzedniej fazie
}

public enum AIActionType
{
    MoveTo,             // Idź w miejsce
    AttackMove,         // Atakuj ogólnie
    DefendFeature,      // Zajmij teren (Wzgórze/Las)
    Flank,              // Atak z flanki
    HoldPosition,       // Stój i czekaj
    ChargeTarget        // Atak na konkretny typ wroga
}

[System.Serializable]
public class AICommandData
{
    public string note = "Opis rozkazu";
    public AIActionType action;
    public string unitTag;       // np. "Cavalry", "Infantry", "All", "B_Archers_1"
    public string targetName;    // np. "Wzgórze Północ", "Flank_West", "Center"
    public string formation;     // "Line", "Wedge", "Loose"
}

[System.Serializable]
public class AIPhase
{
    public string phaseName;
    public AITriggerType trigger;
    public float triggerValue; // Czas w sek, Dystans w metrach, lub % strat (0-1)
    
    [Space]
    public List<AICommandData> commands = new List<AICommandData>();
}

[CreateAssetMenu(fileName = "NewBattleStrategy", menuName = "AI/Strategy Profile")]
public class AIStrategyProfile : ScriptableObject
{
    [TextArea] public string description;
    public List<AIPhase> battlePhases = new List<AIPhase>();
}