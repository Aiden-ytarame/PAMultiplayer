## 1.4.1
* Story mode levels now are playable in Queues and Challenge, both in Multiplayer and Singleplayer
* Multiplayer Challenge loading time decreased.
* Joning mid level now correctly removes the scanlines effect
* Previous player effects like "Cant move, Cant Dash, Player Keyframes" no longer gets carried into Challenge voting

## 1.4.0
* Fixed mod for 1.0
* Now allows you to queue levels not yet subscribed, this includes for multiplayer
* Clients can now add levels to the queue if "Allow Client Levels" is enabled during lobby creation

This is possibly VERY broken so PLEASE report in PA MODDING in the discord

## 1.3.1
* Fixed bug from latest alpha where MP would not work due to IsArcade not being set to true quickly enough

## 1.3.0
* Another player no longer gets added on singleplayer challenge 
* Multiplayer End Screen now has better controller support
* Added Debug command to check MP points leaderboards
* Fixed bug where story mode checkpoints and sometimes arcade wouldnt heal the player
* Fixed bug where level reset modifiers wouldnt clear HitQueue, causing players to die on checkpoint hit
* Added Mp Points Leaderboards via the f5 console
* Fixed a few controller navigation issues, mainly, Y / Triangle now adds level to a queue in the level selection
* Added error screen for some common errors
* Challenge mode level selection is now a little prettier
* Fixed a race condition that softlocked you if a level loaded too fast
* Queue on lobby now shows how many levels are left past the screen, example "+10 levels"


This update changes how loading works, if theres any bugs please report on discord or gitgub

If you find a good level that can cause massive desync because of triggers let me know
