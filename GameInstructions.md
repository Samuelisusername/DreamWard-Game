# Run (Linux)

```
sudo pacman -S dotnet-sdk
dotnet run --project Src/Gamelab.csproj
```

# Dreamward

Number of players: Singleplayer

Input method: Mouse & Keyboard

## Controls
The game starts at day time. The player can navigate to a cards shop (top right).
In the shop the player can uncover action cards and spend sanity on build cards.
After acquiring some cards, they can be used by navigating back to the map (top left), clicking "USE" (bottom right) and selecting the appropriate tile.

Having made appropriate preparations, the player can choose to start the night (spacebar).
If the defenses are successful, the monsters are defeated and the player has sanity left, daytime starts anew.

Gamepad is not supported.

| Action | Keyboard | 
| ------ | -------- | 
|start night| spacebar | 
|pause  | P or esc |
|quit   | esc + esc |


| Card | Action | Use on... |
| ------ | -------- |--| 
| Piercer of Veils | Single target 'sniper' tower | free tile| 
| Banisher of Horrors  | area of effect tower | free tile |
| Catcher of Dreams | wall against flying enemy | free tile |
| Warden of Thresholds | wall normal enemy | free tile |
| Meadow of Slumber | sanity generation | free tile |
| Shuffler of Fates | reorders shop cards | shop |
| Discounter of Burdens | next shop card is cheaper | shop |
| Choice of Illusions | don't have to buy top card | shop |
| Burning of Hands | shop cards are redrawn | shop |
| Vermin of Shadows | make enemies ignore sheep | sheep |
| Shifter of Roots | move a placed tile | existing tile, then free tile |
| Forgetter of Visions | destroy a placed tile | existing tile |
| Absence of Field | intraversible void tile | free tile |
| Bed of Thorns | damaging thorns tile | free tile |
| Lure of Phantoms | lures nightmares | free tile |
| Kindle of Resolve | Buff tower | free tile |