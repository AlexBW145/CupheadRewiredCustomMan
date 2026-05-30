# Cuphead Rewired Custom Input Manager
## For Modders
Just follow the example code in the debug class in `RewiredPatches.cs`, however the simple way of creating a digital input is always this:
```c#
RewiredPlusManager.CreateNewInput(string, string, [behaviorID: RewiredPlusManager.InputBehaviorID, categoryID: RewiredPlusManager.InputMapCategory, key: KeyCode, joystickElementId: int, mouseElementId: int]);
```
With an example being:
```c#
RewiredPlusManager.CreateNewInput("MyOwnInput", "This Input Appears", key: KeyCode.Q, joystickElementId: 6);
```
Afterwards, you can just perform regular input checks from the player input instance such as:
```c#
cupheadPlayer.input.GetButton("MyOwnInput")
```
Make sure that the custom inputs are created right before the slot selection screen!
## For Users
Defined inputs have a default binding on what the coder binds to, controller support is indeed available in the game.

Saved custom bindings in Cuphead are packed into a json and supports multiple players, the save system for custom bindings has been tested twice and works fine.

This manager is not dependent to any APIs, but it's only specifically dependent to Cuphead on versions where the DLC has been released.