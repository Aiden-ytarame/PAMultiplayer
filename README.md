# **Project Arrhythmia Multiplayer**

**Work In Progress**

A multiplayer mod for Project arrhythmia Alpha.

Uses BepInEx.

Made using lidgren networking under the MIT license.

* Client Authorative 
* May require Port Forwarding

## **Progress**

There is no UI to specify a host Ip or Port for now and there wasn't any significant multiplayer tests.

There's problems with closing the server and disconnecting a player.

## **Instalation**
**(WIP)**

* Download the [BepInEx bleeding Edge il2cpp build.](https://docs.bepinex.dev/master/articles/user_guide/installation/index.html)
* Follow the BepInEx installation process available in their documentation.

* Place the PAMultiplayer.dll and lidgren-networking.dll in the generated BepInNex->Plugins folder.

* Enjoy!

## **How to use**

**(To host, you currently have to change your port on Server.cs)**

### Host

* open the game 
* Go to settings->Gameplay
* Toggle Host Server On.
* Start any arcade level to start a server.

(note: the game might save the host server option on when quitting, may require to manually disable to join as clien)


### Join 

**(To join, you currently have to change your port and Server IP on PlayerPatcher.cs)**

* start any arcade level. (preferably the same as the host)

when a client joins, it should* automatically restart the level for everyone.

**NOTE: Pausing does not sync to other players, please do not pause unless you're gonna quit the level.**
