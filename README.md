# **Project Arrhythmia Multiplayer**

**Work In Progress**

A multiplayer mod for Project arrhythmia Alpha.

Uses BepInEx.

Made using lidgren networking under the MIT license.

* Client Authorative 
* May require Port Forwarding

## **Known Issues**

The server Ip and Port disapears after leaving and re opening the main menu.

There's problems with closing the server and disconnecting a player.

## **Instalation**
**(WIP)**

* Download the [BepInEx bleeding Edge il2cpp build.](https://builds.bepinex.dev/projects/bepinex_be)
* Follow the BepInEx installation guide available on their [Github](https://github.com/BepInEx/BepInEx).
* Extract the PAMultiplayer.ZIP file in the generated BepInNex->Plugins folder.

* Enjoy!

## **How to use**

### Host

* open the game 
* Go to settings->Gameplay
* Toggle Host Server On.
* Type a Port
* Start any arcade level to start a server.
* Might require your to setup port forwarding on your router.

(note: the game might save the host server option on when quitting, may require to manually disable the host server on start up)


### Join 

* Go to settings->Gameplay
* Type the Host Ip address, and port.
* start any arcade level. (preferably the same as the host)

when a client joins, it should* automatically restart the level for everyone.

**NOTE: Pausing does not sync to other players, please do not pause unless you're gonna quit the level.**
