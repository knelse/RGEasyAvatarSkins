using ModGenesia;
using RogueGenesia.Data;
using RogueGenesia.GameManager;
using UnityEngine;
using static ModGenesia.AvatarAPI;

namespace RGEasyAvatarSkins;

internal enum SkinReplacementType
{
    Idle = EAnimationType.Idle,
    IdleHD = EAnimationType.IdleHD,
    Run = EAnimationType.Run,
    Victory = EAnimationType.Victory,
    GameOver = EAnimationType.GameOver,
    Icon = int.MaxValue
}

// ReSharper disable once UnusedType.Global
public class RGEasyAvatarSkinsMod : RogueGenesiaMod
{
    // Skin pack name -> animations for that skin pack
    private readonly SortedDictionary<string, List<RGEasyAvatarSkinsAnimationReplacement>> skinPacks = new ()
    {
        ["Default"] = new List<RGEasyAvatarSkinsAnimationReplacement>() 
    };
    private const string _modOptionPrefix = "EasyAvatarSkins_";
    private readonly List<string> _loadedSkins = new();

    public override void OnModLoaded(ModData modData)
    {
        base.OnModLoaded(modData);
        Debug.Log($"EasyAvatarSkins loaded from: {modData.ModDirectory.FullName}");

        var skinFolders = Directory.EnumerateDirectories(modData.ModDirectory.FullName).ToList();
        
        foreach (var skinFolder in skinFolders)
        {
            var skinFilePaths = Directory.EnumerateFiles(skinFolder, "*.png").ToList();
            foreach (var skinFilePath in skinFilePaths)
            {
                AddSkinFile(skinFilePath);
            }
        }
    }

    public override void OnModUnloaded()
    {
        base.OnModUnloaded();
        Debug.Log($"EasyAvatarSkins unloaded");
    }

    public override void OnAllContentLoaded()
    {
        base.OnAllContentLoaded();
        var existingAvatars = GameDataGetter.GetAllAvatars();
        var persistantGameData = GameData.PersistantGameData;

        foreach (var existingAvatar in existingAvatars)
        {
            var avatarName = existingAvatar.GetName();
            var avatarKey = existingAvatar.name;
            
            var skinChoice = persistantGameData.GetStringValue(_modOptionPrefix + avatarKey);
            if (string.IsNullOrWhiteSpace(skinChoice) || skinPacks.Keys.All(x => x != skinChoice))
            {
                skinChoice = "Default";
            }
            
            var avatarDropdownLocaleList = CreateEnglishLocalization(avatarName);

            var dropdownValues = new List<LocalizationDataList> ();

            foreach (var skinName in skinPacks.Keys)
            {
                dropdownValues.Add(CreateEnglishLocalization(skinName));
                _loadedSkins.Add(skinName);
            }

            var tooltip = CreateEnglishLocalization("Selected skin for " + avatarName);

            var avatarDropdownOption =
                ModOption.MakeDropDownOption(_modOptionPrefix + avatarKey, avatarDropdownLocaleList, dropdownValues, LocalisedTooltip: tooltip);

            ModOption.AddModOption(avatarDropdownOption, "Skin Replacements", "Easy Avatar Skins");
            
            ReplaceSkin(avatarKey, skinChoice);
        }

        GameEventManager.OnOptionConfirmed.AddListener((optionName, optionValue) =>
        {
            if (!optionName.StartsWith(_modOptionPrefix))
            {
                return;
            }

            var avatarName = optionName.Substring(_modOptionPrefix.Length);
            var skinName = _loadedSkins.ElementAt((int)optionValue);
            
            ReplaceSkin(avatarName, skinName);
            persistantGameData.SetStringValue(_modOptionPrefix + avatarName, skinName);
        });
    }
    
    private static void LogError (string message) => Debug.LogError($"EasyAvatarSkins ERROR: {message}");

    private void AddSkinFile(string filePath)
    {
        var skinPackName = new DirectoryInfo(Path.GetDirectoryName(filePath)!).Name;
        SkinReplacementType animationType;
        var nameAndFramerate = Path.GetFileNameWithoutExtension(filePath)
            .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

        if (nameAndFramerate.Length != 3 && !(nameAndFramerate.Length == 1 && nameAndFramerate[0].ToLowerInvariant() == "icon"))
        {
            LogError($"Unable to add skin file {filePath}\n" + 
                     "\tFile name format should be: (animation type)_(x frames)_(y frames).png, e.g. \"idlehd_5_1.png\" -- OR \"icon.png\" for avatar icon" +
                     "Name is not case sensitive\n" + 
                     "\tAnimation types are: idle, idlehd, run, victory, gameover");
            return;
        }

        var isIcon = false;

        switch (nameAndFramerate[0].ToLowerInvariant())
        {
            case "idle":
                animationType = SkinReplacementType.Idle;
                break;
            case "idlehd":
                animationType = SkinReplacementType.IdleHD;
                break;
            case "run":
                animationType = SkinReplacementType.Run;
                break;
            case "victory":
                animationType = SkinReplacementType.Victory;
                break;
            case "gameover":
                animationType = SkinReplacementType.GameOver;
                break;
            case "icon":
                isIcon = true;
                animationType = SkinReplacementType.Icon;
                break;
            default:
                LogError($"Unable to add skin file {filePath}\n" + 
                         $"\tIncorrect animation type: {nameAndFramerate[0].ToLowerInvariant()}\n" + 
                         "\tAnimation types are: idle, idlehd, run, victory, gameover");
                return;
        }
        var xFrames = 0;
        var yFrames = 0;
        
        if (!isIcon)
        {
            if (!int.TryParse(nameAndFramerate[1], out var _xFrames))
            {
                LogError($"Unable to add skin file {filePath}\n" +
                         $"\tIncorrect X frames value: {nameAndFramerate[1]}\n" +
                         "\tExpected integer");
                return;
            }

            if (!int.TryParse(nameAndFramerate[2], out var _yFrames))
            {
                LogError($"Unable to add skin file {filePath}\n" +
                         $"\tIncorrect Y frames value: {nameAndFramerate[2]}\n" +
                         "\tExpected integer");
                return;
            }

            xFrames = _xFrames;
            yFrames = _yFrames;
        }

        if (!skinPacks.ContainsKey(skinPackName))
        {
            skinPacks.Add(skinPackName, new List<RGEasyAvatarSkinsAnimationReplacement>());
        }
        skinPacks[skinPackName].Add(new RGEasyAvatarSkinsAnimationReplacement
        {
            animationType = animationType,
            filePath = filePath,
            xFrames = xFrames,
            yFrames = yFrames
        });
    }

    private void ReplaceSkin(string avatarName, string skinName)
    {
        if (!skinPacks.ContainsKey(skinName))
        {
            LogError($"Unable to find skin pack: {skinName}");
            return;
        }
        var skinPack = skinPacks[skinName];
        var animations = new AvatarAnimations();
        var avatar = GameDataGetter.GetAllAvatars().FirstOrDefault(x => x.name == avatarName);

        if (avatar is null)
        {
            LogError($"Unable to find avatar: {avatarName}");
            return;
        }
        
        Debug.Log($"EasyAvatarSkins: replacing {avatarName} with {skinName}");

        animations.IdleAnimation = avatar.Animations.IdleAnimation;
        animations.Icon = avatar.Animations.Icon;
        animations.IdleHDAnimation = avatar.Animations.IdleHDAnimation;
        animations.RunAnimation = avatar.Animations.RunAnimation;
        animations.VictoryAnimation = avatar.Animations.VictoryAnimation;
        animations.GameOverAnimation = avatar.Animations.GameOverAnimation;

        foreach (var skinData in skinPack)
        {
            switch (skinData.animationType)
            {
                case SkinReplacementType.Icon:
                    animations.Icon = ModGenesia.ModGenesia.LoadSprite(skinData.filePath);
                    break;
                case SkinReplacementType.Idle:
                    animations.IdleAnimation = skinData.ToPixelAnimationData();
                    break;
                case SkinReplacementType.IdleHD:
                    animations.IdleHDAnimation = skinData.ToPixelAnimationData();
                    break;
                case SkinReplacementType.Run:
                    animations.RunAnimation = skinData.ToPixelAnimationData();
                    break;
                case SkinReplacementType.Victory:
                    animations.VictoryAnimation = skinData.ToPixelAnimationData();
                    break;
                case SkinReplacementType.GameOver:
                    animations.GameOverAnimation = skinData.ToPixelAnimationData();
                    break;
                default:
                    LogError($"Incorrect animation type: {skinData.animationType}");
                    return;
            }
        }
        ReplaceAvatarSkin(avatarName, animations);
    }

    private static LocalizationDataList CreateEnglishLocalization(string defaultValue)
    {
        return new LocalizationDataList(defaultValue)
        {
            localization = new List<LocalizationData>
            {
                new()
                {
                    Key = "en",
                    Value = defaultValue
                }
            }
        };
    }
}

internal record RGEasyAvatarSkinsAnimationReplacement
{
    public SkinReplacementType animationType;
    public string filePath = null!;
    public int xFrames;
    public int yFrames;

    public PixelAnimationData ToPixelAnimationData()
    {
        var texture = ModGenesia.ModGenesia.LoadPNGTexture(filePath);
        var framesVec = new Vector2Int(xFrames, yFrames);
        return new PixelAnimationData
        {
            Texture = texture,
            Frames = framesVec
        };
    }
}