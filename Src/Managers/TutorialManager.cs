using System;
using System.Collections.Generic;
using System.Linq;
using Gamelab;
using Microsoft.Xna.Framework;

public enum TutorialAction
{
    Click,
    PlacedTower,
    StartedNight,
    MovedSheep,
    None
}

public static class TutorialManager
{
    private static MapManager mapManager => GamelabGame.MapManager;
    private static GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    public static bool IsTutorialActive => gamePhaseManager.IsDay &&  GamelabGame.GamePhaseManager.DayCount <= 4 && tutorialStep < tutorialTexts[gamePhaseManager.DayCount-1].Length;

    private static int tutorialSubStep = -1;

    private static int tutorialStep = 0;

    public static Structure CurrentTutorialStructure;

    private static TimeSpan tutorialTimer = TimeSpan.FromSeconds(1);

    private static int charIndex = 1;

    private static TimeSpan timePerChar = TimeSpan.FromSeconds(0.03);


    private static List<(List<(TutorialAction, int)>, List<(TutorialStructureType,string)>)[]> tutorialTexts = [[
        ([(TutorialAction.PlacedTower, 1),(TutorialAction.StartedNight,3)],[
            (TutorialStructureType.Sheep, "Hey Blank, over here!"),
            (TutorialStructureType.Tent ,"What, who was that?"),
            (TutorialStructureType.Sheep ,"Over here, its us your sheep. You need to protect us!"),
            (TutorialStructureType.Tent ,"WOW, you can talk!?"),
            (TutorialStructureType.Sheep ,"Yes of course I can, but thats not important right now. You need to build up our defence."),
            (TutorialStructureType.Tent ,"Defence, but from what?"),
            (TutorialStructureType.Sheep ,"We got attacked by your nightmares last night, they almost stole all your sanity([SANITY])"),
            (TutorialStructureType.Tent ,"Oh that really doesnt sound good, but how should I defend us?"),
            (TutorialStructureType.Sheep ,"Finally, youre starting to think pratical. Start by dragging a card from your hand next to the tent.")]),
        ([(TutorialAction.Click,1),(TutorialAction.StartedNight,2)], [
            (TutorialStructureType.Sheep, "Oh wow, that cost you quite a bit of sanity([SANITY])"),
            (TutorialStructureType.Tent, "What?! Bulding structures costs me sanity([SANITY])?"),
            (TutorialStructureType.Sheep, "Seems to be the case, but at least you have a way to protect yourself."),
            (TutorialStructureType.Sheep, "And if we survive the night we will replentish some of your sanity([SANITY]), so make sure to also protect us and not just your tent"),
            (TutorialStructureType.Tent, "So you're telling me I should spend my sanity([SANITY]) to protect us? But what if i spend too much and the monsters get to me and steal the rest of my sanity([SANITY])?"),
            (TutorialStructureType.Sheep, "Best not to think about that."),
            (TutorialStructureType.Tent, "Alright, i think i get it. Place towers to defend my tent and sheep and try not to go insane."),
            (TutorialStructureType.Sheep, "And now finish your preperation for the first night and start the night once you're ready by clicking the button in the bottom left or holding space"),]),
        ([(TutorialAction.StartedNight,1)], []),
        ([(TutorialAction.None,0)], [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),])
    ],
    [
        ([(TutorialAction.MovedSheep, 1),(TutorialAction.StartedNight,3)],[
            (TutorialStructureType.Sheep, "Phew that was scary, and to think that monsters are only gonna get stronger from now on"),
            (TutorialStructureType.Tent ,"What!? they get even stronger?"),
            (TutorialStructureType.Sheep, "Oh yes, this was only the beginning, you need to stay here for 10 nights and then defeat the shadow queens army before you can leave"),
            (TutorialStructureType.Tent ,"That sound impossible how should I build up a defence to defeat an army and stay sane with the puny amount of sanity([SANITY]) i get from you"),
            (TutorialStructureType.Sheep, "WOW, rude! i can also leave if you dont want my help"),
            (TutorialStructureType.Tent, "NOO, don't. Im sorry, but is there really no way to get more sanity([SANITY])?"),
            (TutorialStructureType.Sheep, "Hmmm, do you see the juicy looking flowers?"),
            (TutorialStructureType.Tent, "Uhm, yes. Don't tell me you want me to eat them now!"),
            (TutorialStructureType.Sheep, "No, of course not! But if you move us there we can produce more sanity([SANITY]) in the morning."),
            (TutorialStructureType.Tent, "Now we're talking that sounds like its gonna help!"),
            (TutorialStructureType.Sheep, "Keep in mind though that you still have to defend us when you move us, if we get corrupted we cant produce sanity([SANITY]) in the morning."),
            (TutorialStructureType.Sheep, "Try moving us to a meadow close by now, by clicking and holding it. Its gonna cost you sanity([SANITY]) the further you move us, but its gonna be worth it in the morning!"),]),
        ([(TutorialAction.Click,1),(TutorialAction.StartedNight,2)], [
            (TutorialStructureType.Tent, "huh whats that thing laying on the grazing spot?"),
            (TutorialStructureType.Sheep, "Looks like something you can use to defend us, we will graze the flowers arround it so you can use it, but its gonna take until tomorrow morning"),
            (TutorialStructureType.Tent, "Im starting to like these grazing sports more and more"),
            (TutorialStructureType.Sheep, "You can hover over all the grazing spots to see how much sanity([SANITY]) we generate each morning if we are there, how much its costs to move us there and what kind of useful item is laying there!"),
            (TutorialStructureType.Sheep, "Now get ready for the next night, theres gonna be more monsters tonight, and im gonna do the very important job of eating these juicy flowers!"),]),
        ([(TutorialAction.StartedNight,1)], []),
        ([(TutorialAction.None,0)], [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),])
    ],
    [
        ([(TutorialAction.Click, 1),(TutorialAction.StartedNight,2)],[
            (TutorialStructureType.Sheep, "Another night, another victo... Oh, thats not good"),
            (TutorialStructureType.Tent ,"What!? What is not good?"),
            (TutorialStructureType.Sheep, "I sense a new type of threat, a winged nightmare. They can fly over most of the structures you buid."),
            (TutorialStructureType.Tent ,"They fly now?!"),
            (TutorialStructureType.Sheep, "Hey don't blame me for whats happening its your dreams attacking us, not mine"),]),
        ([(TutorialAction.StartedNight,1)], []),
        ([(TutorialAction.None,0)], [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),])
    ],
    [
        ([(TutorialAction.StartedNight,1)], []),
        ([(TutorialAction.None,0)], [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),])
    ],
    ];

    private static TimeSpan textTimer = TimeSpan.Zero;
    public static void Update(GameTime gameTime)
    {
        textTimer += gameTime.ElapsedGameTime;
        while(tutorialSubStep >= 0 && tutorialSubStep <  tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2.Count && charIndex < tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.Length && textTimer > timePerChar)
        {
            if(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.AsSpan(charIndex).StartsWith("[SANITY]"))
            {
                charIndex += 8;
            }
            charIndex++;
            textTimer -= timePerChar;
            CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.Substring(0,charIndex);
        }
        if(IsTutorialActive && tutorialTimer > TimeSpan.Zero)
        {
            if(tutorialSubStep == -1 || (tutorialSubStep >= 0 && tutorialSubStep <  tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2.Count && charIndex >= tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.Length))
            {
                tutorialTimer -= gameTime.ElapsedGameTime;
                if(tutorialTimer <= TimeSpan.Zero)
                {
                    //next step
                    tutorialTimer = TimeSpan.FromSeconds(10);
                    if(tutorialSubStep < tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2.Count - 1)
                    {
                        tutorialSubStep++;
                        charIndex = 1;
                        textTimer = TimeSpan.Zero;
                        if(CurrentTutorialStructure != null)
                        {
                            CurrentTutorialStructure.ShouldDrawCard = false;
                            CurrentTutorialStructure.tutorialText = null;
                        }
                        CurrentTutorialStructure = GetTutrialStructure(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item1);
                        // CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2; 
                        CurrentTutorialStructure.ShouldDrawCard = true; 
                    }              
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
        if(!IsTutorialActive) return;
        if(action == TutorialAction.Click && tutorialSubStep < tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2.Count - 1)
        {
            if(charIndex < tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.Length)
            {
                charIndex = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.Length;
                CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2;
                return;
            }
            charIndex = 1;
            textTimer = TimeSpan.Zero;
            tutorialTimer = TimeSpan.FromSeconds(5);
            tutorialSubStep++;
            if(CurrentTutorialStructure != null)
            {
                CurrentTutorialStructure.ShouldDrawCard = false;
                CurrentTutorialStructure.tutorialText = null;
            }
            CurrentTutorialStructure = GetTutrialStructure(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item1);
            // CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2;
            CurrentTutorialStructure.ShouldDrawCard = true;
        }
        else if(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item1.Select(i => i.Item1).Contains(action))
        {
            tutorialStep += tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item1.SingleOrDefault(i => i.Item1 == action).Item2;
            tutorialSubStep = 0;
            charIndex = 1;
            textTimer = TimeSpan.Zero;
            tutorialTimer = TimeSpan.FromSeconds(5);
            if(CurrentTutorialStructure != null)
            {
                CurrentTutorialStructure.ShouldDrawCard = false;
                CurrentTutorialStructure.tutorialText = null;
                CurrentTutorialStructure = null;
            }            
            if(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2.Count > 0)
            {
                CurrentTutorialStructure = GetTutrialStructure(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item1);
                // CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2; 
                CurrentTutorialStructure.ShouldDrawCard = true;
            }
        }
    }

    public static void AdvanceDay()
    {
        tutorialSubStep = -1;
        tutorialStep = 0;
        tutorialTimer = TimeSpan.FromSeconds(1);
        if(CurrentTutorialStructure != null)
        {
            CurrentTutorialStructure.ShouldDrawCard = false;
            CurrentTutorialStructure.tutorialText = null;
            CurrentTutorialStructure = null;
        }            
    }

    public static void Reset()
    {
        tutorialSubStep = -1;
        tutorialStep = 0;
        CurrentTutorialStructure = null;
        tutorialTimer = TimeSpan.FromSeconds(1);
        charIndex = 1;
        textTimer = TimeSpan.Zero;
    }
    private enum TutorialStructureType
    {
        Tent,
        Sheep
    }
}