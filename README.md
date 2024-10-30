![Logo](Logo400x400.png)

# Project Arrhythmia Multiplayer

A multiplayer mod for Project arrhythmia Alpha.

Supports up to 16 players!

Downloads the lobby's level if you dont have it.

Syncs random seed and speed/health modifiers.

Press Tab on keyboard, L1/LB on controller to show player names in game if youre lost.

Uses BepInEx.

## Installation
**(WIP)**

* Download the latest [BepInEx 6.0.0 il2cpp for Win x64 games build](https://builds.bepinex.dev/projects/bepinex_be/704/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.704%2B6b38cee.zip).
* Extract the files from the BepInEx zip into your game's exe folder (this can be found by right clicking PA on your steam library and going "manage->browse local files").
* Open the game, a terminal should show up. just wait for the game to open.
  
> [!NOTE]\
> The first time you open the game after every game update, it'll take a while to open.

* Close the game and download the [PAM.zip](https://github.com/Aiden-ytarame/PAMultiplayer/releases/latest/download/PAM.zip) from latest release of the Multiplayer mod.
* In your game's folder there will be a new folder called "bepinex". Extract PAM.zip inside the generated **/bepinex/plugins/** folder.
* Open the game and enjoy!

## How to use

### Host

* Select any arcade level
* Start the level by pressing the Mutliplayer button
* Invite friends through Steam or Discord
* Wait for players to join your lobby and load the level (the indicator beside their names tell you when they've loaded)
* Click start log and have fun!


### Join 

* Join the host's game through a Discord or Steam invite
* Wait for the host to start the level and have fun!

## Other Features

### Queue

In Arcade now levels have a '+' button on them, pressing it queues the levels. after finishing a level click the continue button to load the next level in queue! works in Singleplayer and Multiplayer with a proper queue list in the lobby.


### Discord Integration

The mod overhauls the discord rich presence of the game, it shows if you're in menus, the level youre currently playing or the level youre currently editing.

![Rich Presence Showcase](https://github.com/user-attachments/assets/0c6c0785-23b0-482b-8d22-800590a484c7)

Along with that, it shows the players in your lobby and allows you to invite players through Discord! To do that, when in a lobby and having the discord desktop app open, click the '+' icon and click **Invite ___ to Play Project Arrhythmia Multiplayer**.

![HowToShowcase](https://github.com/user-attachments/assets/0a5474f1-80f2-42f2-a69c-1f2e8e2cef80)

The invite also shows the level you're hosting!

![InviteShowcase](https://github.com/user-attachments/assets/945fa985-c88e-459e-ae0e-49c80993de8b)


### Settings

The mod adds a few Multiplayer specific settings you should check out in the in-game settings menu.
* Player Hit SFX - Changes which players triggers the hit sound. Default is All Players.
* Player Hit Warp SFX - Changes which players triggers the warp effect on the song when a player is hit. Default is Local Player Only.
* Transparent Nanos - Makes every player other than yourself transparent to make it easier to see yourself.

### Update Popup

When a new Multiplayer update is available a "Update Multiplayer" button will appear in the main menu!

## Building
To build the mod from source, make a "lib" folder where the csproj is and put get all the assemblies required from the interop folder bepinex generates with the exception of 3 assemblies. For these exception you can either use the ones available in any Multiplayer release, or:
* Facepunch.Steamworks.Win64 - you have build it yourself from the Facepunch.Steamworks source.
> [!NOTE]\
> You have to build **Facepunch.Steamworks** yourself, the Facepunch.Steamworks release does not work.

* steam_api64 - You can use the steam_api64 available in Facepunch.Steamworks source as well.
* DiscordRPC - Use the latest release of Lachee's discord-rpc-csharp.


## Thanks!
Pidge! for helping me and showing some of the source :)

Cozm for the logo!

Reimnop for some misc stuff(code that I "borrowed")

Vyrmax for helping me test for hours and hours a day :)
