using UnityEngine;
using System.Collections.Generic;

public enum AITriggerType
{
    TimeElapsed,       
    EnemyDistance,
    CasualtiesPercent, 
    Immediate 
}

public enum AIActionType
{
    MoveTo,
    AttackMove,
    DefendFeature,
    Flank,
    HoldPosition,
    ChargeTarget
}

[System.Serializable]
public class AICommandData
{
    public string note = "Opis rozkazu";
    public AIActionType action;
    public string unitTag;
    public string targetName;
    public string formation;
}

[System.Serializable]
public class AIPhase
{
    public string phaseName;
    public AITriggerType trigger;
    public float triggerValue;
    
    [Space]
    public List<AICommandData> commands = new List<AICommandData>();
}

[CreateAssetMenu(fileName = "NewBattleStrategy", menuName = "AI/Strategy Profile")]
public class AIStrategyProfile : ScriptableObject
{
    [TextArea] public string description;
    public List<AIPhase> battlePhases = new List<AIPhase>();
}