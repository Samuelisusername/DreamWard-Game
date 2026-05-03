using System;
using Gamelab;
using Microsoft.Xna.Framework;


public static class TutorialManager
{
    private static MapManager mapManager => GamelabGame.MapManager;
    public static bool IsTutorialActive => GamelabGame.GamePhaseManager.DayCount == 1;

    private static int tutorialStep = -1;

    public static Structure CurrentTutorialStructure;

    private static TimeSpan tutorialTimer = TimeSpan.FromSeconds(1);

    private static (TutorialStructureType,string)[] tutorialTexts = [
        (TutorialStructureType.Sheep, "Hey Blank, over here!"),
        (TutorialStructureType.Tent ,"What, who was that?"),
        (TutorialStructureType.Sheep ,"Over here, its me your sheep. You need to protect us!"),
        (TutorialStructureType.Tent ,"WOW, you can talk!?"),
        (TutorialStructureType.Sheep ,"Yes of course I can, but thats not important right now. You need to build up our defence."),
        (TutorialStructureType.Tent ,"Defence, but from what?"),
        (TutorialStructureType.Sheep ,"We got attacked by your nightmares last night, they almost stole all your sanity(TODO: instert sanity Logo)"),
        (TutorialStructureType.Tent ,"Oh that really doesnt sound good, but how should I defend us?"),
        (TutorialStructureType.Sheep ,"Finally, youre starting to think pratical. Start by dragging a card from your hand next to the tent."),

    ];

    public static void Update(GameTime gameTime)
    {
        if(IsTutorialActive && tutorialTimer > TimeSpan.Zero)
        {
            tutorialTimer -= gameTime.ElapsedGameTime;
            if(tutorialTimer <= TimeSpan.Zero)
            {
                //next step
                tutorialTimer = TimeSpan.FromSeconds(5);
                tutorialStep++;
                tutorialStep %= tutorialTexts.Length;
                if(CurrentTutorialStructure != null)
                {
                    CurrentTutorialStructure.ShouldDrawCard = false;
                    CurrentTutorialStructure.tutorialText = null;
                }
                CurrentTutorialStructure = GetTutrialStructure(tutorialTexts[tutorialStep].Item1);
                CurrentTutorialStructure.tutorialText = tutorialTexts[tutorialStep].Item2; 
                CurrentTutorialStructure.ShouldDrawCard = true;               
            }
        }
        else
        {
            if(CurrentTutorialStructure != null)
            {
                CurrentTutorialStructure.ShouldDrawCard = false;
                CurrentTutorialStructure.tutorialText = null;
                CurrentTutorialStructure = null;
            }
        }
    }

    private static Structure GetTutrialStructure(TutorialStructureType type)
    {
        switch (type)
        {
            case TutorialStructureType.Tent:
                var tentPos = mapManager.GetTent();
                var tent = mapManager.GetStructure(tentPos);
                if(tent is not null)
                {
                    return tent;
                }
                else
                {
                    throw new Exception("Tent not found for tutorial help");
                }
            case TutorialStructureType.Sheep:
                var sheep = mapManager.GetSheep();
                if(sheep is not null)
                {
                    return sheep;
                }
                else
                {
                    throw new Exception("Sheep not found for tutorial help");
                }
            default:
                throw new Exception("Tutorial type doesnt exists");
        }
    }

    private enum TutorialStructureType
    {
        Tent,
        Sheep
    }
}