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

Desync issues when dying close to a checkpoint.

This uses reflection to make a new instance of a packet for every packet recieved, thats not very good. a better approach would to hold an Processor for each packet, but the laziness got the best of me.

## **Plans**

* Interpolate player position between packets. 
* Sync random seed.
  

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

I recommend that all players join before starting the lobby, joining mid level restarts the level for everyone but it desyncs the joining player slightly

**NOTE: Pausing does not sync to other players, please do not pause unless you're gonna quit the level.**
