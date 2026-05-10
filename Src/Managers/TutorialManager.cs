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
            (TutorialStructureType.Sheep, "Hey! Over here!"),
            (TutorialStructureType.Tent ,"What... who's there?"),
            (TutorialStructureType.Sheep ,"It's us, down here! Your nightmares nearly got us!"),
            (TutorialStructureType.Tent ,"Wait, you can talk?!"),
            (TutorialStructureType.Sheep ,"Focus! Your nightmares almost drained all your sanity [SANITY] last night."),
            (TutorialStructureType.Tent ,"That would explain things. How do I stop them?"),
            (TutorialStructureType.Sheep ,"Build defenses! Drag a card from your hand onto a tile next to the tent.")]),
        ([(TutorialAction.Click,1),(TutorialAction.StartedNight,2)], [
            (TutorialStructureType.Sheep, "That used up some sanity [SANITY]."),
            (TutorialStructureType.Tent, "Building costs sanity [SANITY]?!"),
            (TutorialStructureType.Sheep, "Doesn't everything? But we can restore some if we survive the night."),
            (TutorialStructureType.Tent, "Spend sanity [SANITY] to gain sanity [SANITY]. Got it."),
            (TutorialStructureType.Sheep, "Start the night when you're ready."),]),
        ([(TutorialAction.StartedNight,1)], []),
        ([(TutorialAction.None,0)], [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),])
    ],
    [
        ([(TutorialAction.MovedSheep, 1),(TutorialAction.StartedNight,3)],[
            (TutorialStructureType.Sheep, "Phew! And they're only going to get stronger..."),
            (TutorialStructureType.Tent ,"Stronger?! How long does this go on?"),
            (TutorialStructureType.Sheep, "10 days and nights [DAYICON]. Then you can wake up."),
            (TutorialStructureType.Tent ,"10?! I'll run out of sanity [SANITY] way before that."),
            (TutorialStructureType.Sheep, "Only without grazing. See those flowers?"),
            (TutorialStructureType.Tent, "...You want me to eat flowers?"),
            (TutorialStructureType.Sheep, "We want to eat flowers. Move us to a meadow and we'll generate extra sanity [SANITY] for three days."),
            (TutorialStructureType.Tent, "That makes complete sense."),
            (TutorialStructureType.Sheep, "Click and hold the nearest meadow. Not as much yield as others, but easier to defend."),]),
        ([(TutorialAction.Click,1),(TutorialAction.StartedNight,2)], [
            (TutorialStructureType.Sheep, "I think we found something useful. We'll uncover it by morning."),
            (TutorialStructureType.Tent, "These meadows keep getting better."),
            (TutorialStructureType.Sheep, "Hover over any meadow to see its sanity [SANITY] yield, move cost, and hidden item."),
            (TutorialStructureType.Sheep, "Now prep for night two. We'll be busy with these juicy flowers."),]),
        ([(TutorialAction.StartedNight,1)], []),
        ([(TutorialAction.None,0)], [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),])
    ],
    [
        ([(TutorialAction.Click, 1),(TutorialAction.StartedNight,2)],[
            (TutorialStructureType.Sheep, "Another night, another victo... Oh, that's not good."),
            (TutorialStructureType.Tent ,"What?!"),
            (TutorialStructureType.Sheep, "Winged nightmares. They fly over most of your defenses."),
            (TutorialStructureType.Tent ,"Of course they do."),
            (TutorialStructureType.Sheep, "Hey, they're your dreams. We're just stuck in them."),]),
        ([(TutorialAction.StartedNight,1)], []),
        ([(TutorialAction.None,0)], [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),])
    ],
    [  
        ([(TutorialAction.Click, 1),(TutorialAction.StartedNight,2)],[
            (TutorialStructureType.Sheep, "What a stressful night, I really could use some fresh flowers!"),
            (TutorialStructureType.Tent ,"Theres still enough flowers here for at least one more night!"),
            (TutorialStructureType.Sheep, "Okay, okay, you're right. But if you move us again, you can use the move button in the bottom left corner."),
            (TutorialStructureType.Tent ,"Let me guess, thats also gonna cost sanity?"),
            (TutorialStructureType.Sheep, "You're a fast learner, yes it does, but that still cheaper than building new structures."),]),
        ([(TutorialAction.StartedNight,1)], []),
        ([(TutorialAction.None,0)], [
            (TutorialStructureType.Sheep, "HERE THEY COME!!"),])
    ],
    ];

    private static TimeSpan textTimer = TimeSpan.Zero;
    private static bool SheepDead() => mapManager.GetSheep() == null;

    public static void Update(GameTime gameTime)
    {
        if (SheepDead())
        {
            if (CurrentTutorialStructure != null)
            {
                CurrentTutorialStructure.ShouldDrawCard = false;
                CurrentTutorialStructure.tutorialText = null;
                CurrentTutorialStructure = null;
            }
            return;
        }
        textTimer += gameTime.ElapsedGameTime;
        while(tutorialSubStep >= 0 && tutorialSubStep <  tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2.Count && charIndex < tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.Length && textTimer > timePerChar)
        {
            if(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.AsSpan(charIndex).StartsWith("[SANITY]"))
            {
                charIndex += 8;
                textTimer -= timePerChar;
                CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2[..charIndex];
                continue;
            }
            charIndex++;
            textTimer -= timePerChar;
            CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2[..charIndex];
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
                        CurrentTutorialStructure = GetTutorialStructure(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item1);
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

    private static Structure GetTutorialStructure(TutorialStructureType type)
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
                return mapManager.GetSheep();
            default:
                throw new Exception("Tutorial type doesn't exist");
        }
    }

    public static void HandleTutorialAction(TutorialAction action)
    {
        if(!IsTutorialActive) return;
        if(SheepDead()) return;
        if(action == TutorialAction.Click)
        {
            if(tutorialSubStep >= 0 && tutorialSubStep <  tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2.Count && charIndex < tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.Length)
            {
                charIndex = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2.Length;
                CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2;
                return;
            }
            else if(tutorialSubStep < tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2.Count - 1)
            {
                charIndex = 1;
                textTimer = TimeSpan.Zero;
                tutorialTimer = TimeSpan.FromSeconds(5);
                tutorialSubStep++;
                if(CurrentTutorialStructure != null)
                {
                    CurrentTutorialStructure.ShouldDrawCard = false;
                    CurrentTutorialStructure.tutorialText = null;
                }
                CurrentTutorialStructure = GetTutorialStructure(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item1);
                // CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2;
                CurrentTutorialStructure.ShouldDrawCard = true;
                return;
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
                    CurrentTutorialStructure = GetTutorialStructure(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item1);
                    // CurrentTutorialStructure.tutorialText = tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item2; 
                    CurrentTutorialStructure.ShouldDrawCard = true;
                }
            }
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
                CurrentTutorialStructure = GetTutorialStructure(tutorialTexts[gamePhaseManager.DayCount-1][tutorialStep].Item2[tutorialSubStep].Item1);
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