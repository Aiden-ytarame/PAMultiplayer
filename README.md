# **Project Arrhythmia Multiplayer**

**Work In Progress**

A multiplayer mod for Project arrhythmia Alpha.

Uses BepInEx.

Made using lidgren networking under the MIT license.

* Client Authorative 
* May require Port Forwarding

## **Known Issues**

There's problems with closing the server and disconnecting a player.

Not regenerating health on level restart.

Desync due to the time it takes to load a level(not 100% sure about this one, but the fix would be to trigger the level restart when the level starts not when it loads up)

Desync issues when dying close to a checkpoint.

Desync issues when taking damage before a player joins.

## **Plans**

* Interpolate player position between packets. 
* Sync random seed.
* Make a lobby instead of current system.
  

## **Instalation**
**(WIP)**

* Download the [BepInEx 6.0.0 il2cpp bleeding Edge build.](https://builds.bepinex.dev/projects/bepinex_be)
* Follow the BepInEx installation guide available on their [Github](https://github.com/BepInEx/BepInEx).
* Extract the PAMultiplayer.ZIP file in the generated BepInNex->Plugins folder.

* Enjoy!

## **How to use**

### Host

* Go to settings->Gameplay.
* Toggle Host Server On.
* Type a Port.
* Start any arcade level to start a server.
* Wait for players to join your lobby and load the level (the indicator beside their names tell you when they've loaded)
* Click start log and have fun!
* *Might require your to setup port forwarding on your router.

(note: the game might save the host server option on when quitting, may require to manually disable the host server on start up)


### Join 

* Go to settings->Gameplay.
* Type the Host Ip address, and port.
* start any arcade level. (preferably the same as the host)
* Wait for the host to start the level and have fun!

**NOTE: Pausing does not sync to other players, please do not pause unless you're gonna quit the level.**
