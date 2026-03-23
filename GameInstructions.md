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
|pause  | P |
|quit   | esc |


| Card | Action | Use on... |
| ------ | -------- |--| 
| Tower of one Destruction| Single target 'sniper' tower | free tile| 
| Tower of many Destruction  | area of effect tower | free tile |
| Dream Catcher | wall against flying enemy | free tile |
| Fence | wall normal enemy | free tile |
| Meadow | sanity generation | free tile |
| Shuffle | reorders shop cards | shop |
| Discount | next shop card is cheaper | shop |
| Top 3 | don't have to buy top card | shop |
| Discard | shop cards are redrawn | shop |
| Polymorph | make enemies ignore sheep | sheep |
| Move | move a placed tile | existing tile, then free tile |