![Logo](Logo400x400.png)

# Project Arrhythmia Multiplayer

A multiplayer mod for Project arrhythmia Alpha.

Supports up to 16 players!

Downloads the lobby's level if you dont have it.

Syncs random seed and speed/health modifiers.

Press Tab on keyboard, L1/LB on controller to show player names in game if youre lost.

## Installation
**(WIP)**

### r2modman (recommended)

* Download the [r2modman](https://thunderstore.io/package/ebkr/r2modman/) mod manager.
* Search for **Project Arrhythmia** and go the **Online** tab, look for **Project Arrhythmia Multiplayer**W and download.
* Then click **Start Modded** in the top left of r2modman and youre all set.

### Manual
* Download bepinex from [BepInEx 5.4](https://thunderstore.io/c/project-arrhythmia/p/BepInEx/BepInExPack/).
* Extract the files from the BepInEx zip into your game's exe folder (this can be found by right clicking PA on your steam library and going "manage->browse local files").
  * Note: Extract the zip contents into the PA folder like the image below, not a new folder.
  
<img src="https://github.com/user-attachments/assets/7d57b1cf-39cf-4441-8841-e23ad77ed3e6" width="50%">

* Download the [Project Arrhythmia Multiplayer mod](https://thunderstore.io/c/project-arrhythmia/p/aiden_ytarame/Project_Arrhythmia_Multiplayer/) from latest release available on thunderstore.
* In your game's folder there will be a new folder called "bepinex". Merge the Bepinex folder on the Multiplayer zip file with the one in your game's folder.
* Download [PaApi](https://thunderstore.io/c/project-arrhythmia/p/aiden_ytarame/PaApi/) from the latest release available on thunderstore and follow the same installation steps from above.
* Open the game and enjoy!

## How to use

### Host

* Select any arcade level, or click 'Remote Challenge' after Custom Logs
* Start the level by pressing the Mutliplayer button
* Invite friends through Steam or Discord
* Wait for players to join your lobby and load the level (the indicator beside their names tell you when they've loaded)
* Click start log and have fun!


### Join 

* Join the host's game through a Discord or Steam invite. or join a random host via the 'Join Researcher' button after Custom Logs
* Wait for the host to start the level and have fun!

## Other Features

### Queue

In Arcade now levels have a '+' button on them, pressing it queues the levels. after finishing a level click the continue button to load the next level in queue! works in Singleplayer and Multiplayer with a proper queue list in the lobby.

### Challenge

Play an endless mode, where after every level you vote between 6 randomly picked levels you have downloaded. also works in multiplayer!


### Discord Integration

The mod overhauls the discord rich presence of the game, it shows if you're in menus, the level youre currently playing or the level youre currently editing.

![Rich Presence Showcase](https://github.com/user-attachments/assets/0c6c0785-23b0-482b-8d22-800590a484c7)

Along with that, it shows the players in your lobby and allows you to invite players through Discord! To do that, when in a lobby and having the discord desktop app open, click the '+' icon and click **Invite ___ to Play Project Arrhythmia Multiplayer**.

![HowToShowcase](https://github.com/user-attachments/assets/0a5474f1-80f2-42f2-a69c-1f2e8e2cef80)

The invite also shows the level you're hosting!

![InviteShowcase](https://github.com/user-attachments/assets/945fa985-c88e-459e-ae0e-49c80993de8b)


### Settings

The mod adds a few Multiplayer specific settings you should check out in the in-game settings menu.

A few settings that you should note are:
* Player Hit SFX - Changes which players triggers the hit sound, available in the Audio tab.
* Player Hit Warp SFX - Changes which players triggers the warp effect on the song when a player is hit, available in the Audio tab.
* Transparent Nanos - Makes every player other than yourself transparent to make it easier to see yourself, available in the Multiplayer tab.


## Building
To build the mod from source, make a "lib" folder where the csproj is and put all the assemblies required from the **Project Arrhythmia_Data/Managed** folder bepinex generates with the exception of 3 assemblies. For these exception you can either use the ones available in any Multiplayer release, or:
* Facepunch.Steamworks.Win64 - Version 2.4.1 available in their github, taken from the unity package folder.
* steam_api64 - You can use the steam_api64 available in Facepunch.Steamworks release as well.
* DiscordRPC - Use version 1.6.1 of Lachee's discord-rpc-csharp available on Github.
* PaApi - Download the latest release available on [github](https://github.com/Aiden-ytarame/Pa-Api)

## Thanks!
Pidge! for helping me and showing some of the source :)

Cozm for the logo!

Reimnop amd Enchart for some help!

Vyrmax for helping me test for hours and hours a day :>
