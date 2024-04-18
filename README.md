# **Project Arrhythmia Multiplayer**

**Work In Progress**

A multiplayer mod for Project arrhythmia Alpha.

Made using lidgren networking under the MIT license

* Client Authorative 
* May require Port Forwarding


## **Progress**

There is no UI for now and there wasn't any significant multiplayer tests.


## **Instalation**

## **How to use**

* (To host, you currently have to change your port on Server.cs)

To host, open the game, go to settings->Gameplay and toggle Host Server On.(note: the game might save the host server option on when quitting, may require to manually disable to join as clien)

then start any arcade level to start a server.

* (To join, you currently have to change your port and Server IP on PlayerPatcher.cs)

to join, start any arcade level. (preferably the same as the host)

when a client joins, it should* automatically restart the level for everyone.

* NOTE: Pausing does not sync to other players, please do not pause unless you're gonna quit the level.
