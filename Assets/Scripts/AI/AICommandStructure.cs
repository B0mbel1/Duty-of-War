using System;
using System.Collections.Generic;

[Serializable]
public class AICommandResponse
{
    public List<AICommand> commands;
    public string ai_thought; // AI wyjaśni, dlaczego podjęło taką decyzję (dla immersji)
}

[Serializable]
public class AICommand
{
    // Kogo dotyczy rozkaz? (np. "Infantry", "Archer", "All", albo konkretne ID grupy "A_Infantry_1")
    public string[] unit_tags; 
    
    // Co mają zrobić? ("MOVE", "ATTACK", "STOP")
    public string action;
    
    // Gdzie? ("Center", "Enemy_Army", "Left_Flank", "My_Base")
    public string target_location;
}