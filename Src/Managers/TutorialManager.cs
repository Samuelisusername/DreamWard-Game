using System;
using System.Collections.Generic;
using Gamelab;
using Microsoft.Xna.Framework;

public enum TutorialAction
{
    Click,
    PlacedTower,
    StartedNight,
    None
}

public static class TutorialManager
{
    private static MapManager mapManager => GamelabGame.MapManager;
    public static bool IsTutorialActive => GamelabGame.GamePhaseManager.DayCount == 1 && tutorialStep < tutorialTexts.Length;

    private static int tutorialSubStep = -1;

    private static int tutorialStep = 0;

    public static Structure CurrentTutorialStructure;

    private static TimeSpan tutorialTimer = TimeSpan.FromSeconds(1);

    private static (TutorialAction, List<(TutorialStructureType,string)>)[] tutorialTexts = [
        (TutorialAction.PlacedTower,[
            (TutorialStructureType.Sheep, "Hey Blank, over here!"),
            (TutorialStructureType.Tent ,"What, who was that?"),
            (TutorialStructureType.Sheep ,"Over here, its us your sheep. You need to protect us!"),
            (TutorialStructureType.Tent ,"WOW, you can talk!?"),
            (TutorialStructureType.Sheep ,"Yes of course I can, but thats not important right now. You need to build up our defence."),
            (TutorialStructureType.Tent ,"Defence, but from what?"),
            (TutorialStructureType.Sheep ,"We got attacked by your nightmares last night, they almost stole all your sanity(TODO: instert sanity Logo)"),
            (TutorialStructureType.Tent ,"Oh that really doesnt sound good, but how should I defend us?"),
            (TutorialStructureType.Sheep ,"Finally, youre starting to think pratical. Start by dragging a card from your hand next to the tent.")]),
        (TutorialAction.StartedNight, [
            (TutorialStructureType.Sheep, "Oh wow, that cost you quite a bit of sanity(TODO: instert sanity Logo)"),
            (TutorialStructureType.Tent, "What?! Bulding structures costs me sanity(TODO: instert sanity Logo)?"),
            (TutorialStructureType.Sheep, "Seems to be the case, but at least you have a way to protect yourself."),
            (TutorialStructureType.Sheep, "And if we survive the night we will replentish some of your sanity, so make sure to also protect us and not just your tent"),
            (TutorialStructureType.Tent, "So you're telling me I should spend my sanity to protect us? But what if i spend too much and the monsters get to me and steal the rest of my sanity?"),
            (TutorialStructureType.Sheep, "Best not to think about that. But if you need more sanity you can move us to grazing spots that are further away, we will produce more sanity there!"),
            (TutorialStructureType.Sheep, "Only move us if youre ready to defend our new grazing spot though, if the monsters get to us and corrupt us we cant produce any sanity"),
            (TutorialStructureType.Tent, "Alright, i think i get it. Place towers to defend my tent and sheep, move you guys to different grazing spots to get more sanity in the morning, and try not to go insane."),
            (TutorialStructureType.Tent, "Anything else i should know?"),
            (TutorialStructureType.Sheep, "The monsters are going to get stronger every night, so keep working on your defences!"),
            (TutorialStructureType.Sheep, "And now finish your preperation for the first night and start the night once you're ready by clicking the button in the bottom left or holding space"),
        ]),(TutorialAction.None, [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),
        ])
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
                if(tutorialSubStep < tutorialTexts[tutorialStep].Item2.Count - 1)
                {
                    tutorialSubStep++;
                    if(CurrentTutorialStructure != null)
                    {
                        CurrentTutorialStructure.ShouldDrawCard = false;
                        CurrentTutorialStructure.tutorialText = null;
                    }
                    CurrentTutorialStructure = GetTutrialStructure(tutorialTexts[tutorialStep].Item2[tutorialSubStep].Item1);
                    CurrentTutorialStructure.tutorialText = tutorialTexts[tutorialStep].Item2[tutorialSubStep].Item2; 
                    CurrentTutorialStructure.ShouldDrawCard = true; 
                }              
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

    public static void HandleTutorialAction(TutorialAction action)
    {
        if(action == TutorialAction.Click && tutorialSubStep < tutorialTexts[tutorialStep].Item2.Count - 1)
        {
            tutorialTimer = TimeSpan.FromSeconds(5);
            tutorialSubStep++;
            if(CurrentTutorialStructure != null)
            {
                CurrentTutorialStructure.ShouldDrawCard = false;
                CurrentTutorialStructure.tutorialText = null;
            }
            CurrentTutorialStructure = GetTutrialStructure(tutorialTexts[tutorialStep].Item2[tutorialSubStep].Item1);
            CurrentTutorialStructure.tutorialText = tutorialTexts[tutorialStep].Item2[tutorialSubStep].Item2; 
            CurrentTutorialStructure.ShouldDrawCard = true;
        }
        else if(tutorialTexts[tutorialStep].Item1 == action)
        {
            tutorialStep++;
            tutorialSubStep = 0;
            tutorialTimer = TimeSpan.FromSeconds(5);
            if(CurrentTutorialStructure != null)
            {
                CurrentTutorialStructure.ShouldDrawCard = false;
                CurrentTutorialStructure.tutorialText = null;
            }
            CurrentTutorialStructure = GetTutrialStructure(tutorialTexts[tutorialStep].Item2[tutorialSubStep].Item1);
            CurrentTutorialStructure.tutorialText = tutorialTexts[tutorialStep].Item2[tutorialSubStep].Item2; 
            CurrentTutorialStructure.ShouldDrawCard = true;
        }
    }

    private enum TutorialStructureType
    {
        Tent,
        Sheep
    }
}