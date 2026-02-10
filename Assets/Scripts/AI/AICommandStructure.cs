using System;
using System.Collections.Generic;

[Serializable]
public class AICommandResponse
{
    public List<AICommand> commands;
    public string ai_thought; 
}

[Serializable]
public class AICommand
{
    public string[] unit_tags; 
    public string action;
    public string target_location;
}