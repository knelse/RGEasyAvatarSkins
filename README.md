# RGEasyAvatarSkins
Easy avatar skin changer for Rogue: Genesia with a simple UI.

Choose a new skin pack for each avatar, or mix&match them to create a uniquely cursed combo. 

# How to use

To choose a skin, navigate to Options -> Mods Option -> Easy Avatar Skins and select it from dropdown list.

The mod should pick up skins you download from Steam Workshop automatically.

To create a new skin, navigate to mods folder, create a new folder there and add PNG files for the animations. Any missing files would be replaced by default animations for that particular avatar.

Available folders for new skins are:
* Steam Workshop directory (something like c:\Program Files (x86)\Steam\steamapps\workshop\content\2067920\2966368775) 
* Game directory (c:\Program Files (x86)\Steam\steamapps\common\Rogue Genesia\Modded\Mods\EasyAvatarSkins\)

If you don't see EasyAvatarSkins in game directory, create that folder.

Once you create a folder for your new skin pack, any PNG file with valid name should be picked up by the mod automatically.

File format: `(animation type)_(X axis frames)_(Y axis frames).png`. Frame counts are integers. For a basic example, refer to Potato skinpack bundled with the mod.

Currently supported animation types:

* Icon - avatar icon on the avatar selection screen (not an actual animation, just 1 frame)
* Idle - idle animation in game, when you're not running
* IdleHD - idle animation on the avatar selection screen, plays on the background
* Run - running animation in game
* Victory - animation on victory (World Saved / successful completion screen)
* GameOver - animation on game over screen

If you want to upload your newly created skin to Steam Workshop, refer to the guide by Plexus: https://github.com/PlexusDuMenton/RogueGenesiaExampleMod/wiki/Mod-Creation#mod-file-settup.
You will need to add EasyAvatarSkins as a dependency (`"HardDependencies": ["knelse.EasyAvatarSkins@0.2"]`).

Enjoy!
