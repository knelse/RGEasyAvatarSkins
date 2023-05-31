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
    private static readonly
        SortedDictionary<string, Dictionary<SkinReplacementType, RGEasyAvatarSkinsAnimationReplacement>> skinPacks =
            new() { ["Default"] = new Dictionary<SkinReplacementType, RGEasyAvatarSkinsAnimationReplacement>() };

    private static readonly Dictionary<string, Dictionary<SkinReplacementType, object>> defaultSkinPacks = new();

    private const string _modOptionPrefix = "EasyAvatarSkins_";
    private const string _mnmOptionPrefix = "mnm_";
    private const string _mnmTogglePrefix = _modOptionPrefix + _mnmOptionPrefix + "toggle_";
    private const string _mnmIconPrefix = _modOptionPrefix + _mnmOptionPrefix + "icon_";
    private const string _mnmIdlePrefix = _modOptionPrefix + _mnmOptionPrefix + "idle_";
    private const string _mnmIdlehdPrefix = _modOptionPrefix + _mnmOptionPrefix + "idlehd_";
    private const string _mnmRunPrefix = _modOptionPrefix + _mnmOptionPrefix + "run_";
    private const string _mnmVictoryPrefix = _modOptionPrefix + _mnmOptionPrefix + "victory_";
    private const string _mnmGameoverPrefix = _modOptionPrefix + _mnmOptionPrefix + "gameover_";
    private readonly List<string> _loadedSkins = new();

    public override void OnModLoaded(ModData modData)
    {
        base.OnModLoaded(modData);
        Debug.Log($"EasyAvatarSkins loaded from: {modData.ModDirectory.FullName}");

        // load bundled skin
        var bundledSkinFiles = Directory.EnumerateFiles(Path.Combine(modData.ModDirectory.FullName, "Potato"), "*.png")
            .ToList();
        foreach (var skinFilePath in bundledSkinFiles)
        {
            AddSkinFile(skinFilePath);
        }

        // load workshop dependencies
        foreach (var skinMod in modData.DependedBy.Where(x => x.Enabled))
        {
            // this should be redundant
            if (!Directory.Exists(skinMod.ModDirectory.FullName))
            {
                continue;
            }

            // current folder
            var skinFiles = Directory.EnumerateFiles(skinMod.ModDirectory.FullName, "*.png").ToList();
            foreach (var skinFilePath in skinFiles.Where(x => Path.GetFileNameWithoutExtension(x) != "ModIcon" &&
                         Path.GetFileNameWithoutExtension(x) != "ModPreview"))
            {
                AddSkinFile(skinFilePath, skinMod.GetModName());
            }

            // subfolders
            foreach (var skinFileSubdirectory in Directory.EnumerateDirectories(skinMod.ModDirectory.FullName).ToList())
            {
                var skinFileSubdirectoryPaths = Directory.EnumerateFiles(skinFileSubdirectory).ToList();
                foreach (var skinFilePath in skinFileSubdirectoryPaths.Where(x =>
                             Path.GetExtension(x) == ".png" && Path.GetFileNameWithoutExtension(x) != "ModIcon" &&
                             Path.GetFileNameWithoutExtension(x) != "ModPreview"))
                {
                    AddSkinFile(skinFilePath);
                }
            }
        }

        var gameDirectoryModsFolder = Path.Combine(modData.ModDirectory.Parent.Parent.Parent.Parent.FullName,
            "Rogue Genesia", "Modded", "Mods");

        // load any remaining skins from game mods folder
        if (!Directory.Exists(gameDirectoryModsFolder))
        {
            return;
        }

        foreach (var modFolder in Directory.EnumerateDirectories(gameDirectoryModsFolder))
        {
            var skinFilePaths = Directory.EnumerateFiles(modFolder).ToList();
            // exlude duplicates if mod is already added as dependency above and limit scanning other mods' files
            if (skinFilePaths.Any(x => Path.GetExtension(x) == ".rgmod"))
            {
                continue;
            }

            // current folder
            foreach (var skinFilePath in skinFilePaths.Where(x =>
                         Path.GetExtension(x) == ".png" && Path.GetFileNameWithoutExtension(x) != "ModIcon" &&
                         Path.GetFileNameWithoutExtension(x) != "ModPreview"))
            {
                AddSkinFile(skinFilePath);
            }

            // subfolders
            foreach (var skinFileSubdirectory in Directory.EnumerateDirectories(modFolder).ToList())
            {
                var skinFileSubdirectoryPaths = Directory.EnumerateFiles(skinFileSubdirectory).ToList();
                foreach (var skinFilePath in skinFileSubdirectoryPaths.Where(x =>
                             Path.GetExtension(x) == ".png" && Path.GetFileNameWithoutExtension(x) != "ModIcon" &&
                             Path.GetFileNameWithoutExtension(x) != "ModPreview"))
                {
                    AddSkinFile(skinFilePath);
                }
            }
        }
    }

    public override void OnAllContentLoaded()
    {
        base.OnAllContentLoaded();
        var existingAvatars = GameDataGetter.GetAllAvatars();
        var persistentGameData = GameData.PersistantGameData;

        foreach (var existingAvatar in existingAvatars)
        {
            if (!defaultSkinPacks.ContainsKey(existingAvatar.name))
            {
                defaultSkinPacks.Add(existingAvatar.name, new Dictionary<SkinReplacementType, object>());
            }

            defaultSkinPacks[existingAvatar.name][SkinReplacementType.Icon] = existingAvatar.Animations.Icon;
            defaultSkinPacks[existingAvatar.name][SkinReplacementType.Idle] = existingAvatar.Animations.IdleAnimation;
            defaultSkinPacks[existingAvatar.name][SkinReplacementType.IdleHD] =
                existingAvatar.Animations.IdleHDAnimation;
            defaultSkinPacks[existingAvatar.name][SkinReplacementType.Run] = existingAvatar.Animations.RunAnimation;
            defaultSkinPacks[existingAvatar.name][SkinReplacementType.Victory] =
                existingAvatar.Animations.VictoryAnimation;
            defaultSkinPacks[existingAvatar.name][SkinReplacementType.GameOver] =
                existingAvatar.Animations.GameOverAnimation;

            CreateModOption(existingAvatar);
        }

        UpdateAvatarSkins();

        GameEventManager.OnOptionConfirmed.AddListener((optionName, optionValue) =>
        {
            if (!optionName.StartsWith(_modOptionPrefix))
            {
                return;
            }

            var skinName = _loadedSkins.ElementAt((int)optionValue);

            if (optionName.Contains("_toggle_"))
            {
                persistentGameData.SetStat(optionName, optionValue);
            }
            else
            {
                persistentGameData.SetStringValue(optionName, skinName);
            }

            var avatarName = "";
            // ideally this could be a substring from lastIndexOf "_" but who knows what is in the avatar names
            if (optionName.StartsWith(_mnmTogglePrefix))
            {
                avatarName = optionName.Substring(_mnmTogglePrefix.Length);
            }
            else if (optionName.StartsWith(_mnmIconPrefix))
            {
                avatarName = optionName.Substring(_mnmIconPrefix.Length);
            }
            else if (optionName.StartsWith(_mnmIdlePrefix))
            {
                avatarName = optionName.Substring(_mnmIdlePrefix.Length);
            }
            else if (optionName.StartsWith(_mnmIdlehdPrefix))
            {
                avatarName = optionName.Substring(_mnmIdlehdPrefix.Length);
            }
            else if (optionName.StartsWith(_mnmRunPrefix))
            {
                avatarName = optionName.Substring(_mnmRunPrefix.Length);
            }
            else if (optionName.StartsWith(_mnmVictoryPrefix))
            {
                avatarName = optionName.Substring(_mnmVictoryPrefix.Length);
            }
            else if (optionName.StartsWith(_mnmGameoverPrefix))
            {
                avatarName = optionName.Substring(_mnmGameoverPrefix.Length);
            }
            else if (optionName.StartsWith(_modOptionPrefix))
            {
                avatarName = optionName.Substring(_modOptionPrefix.Length);
            }

            UpdateAvatarSkins(avatarName);
        });
    }

    private void UpdateAvatarSkins(string? avatarToUpdate = null)
    {
        var existingAvatars = GameDataGetter.GetAllAvatars();

        var avatarsWithEnabledMnm = new HashSet<string>(GameData.PersistantGameData.StatsList
            .Where(x => x.Name.StartsWith(_mnmTogglePrefix) && x.Value > 0)
            .Select(x => x.Name.Substring(_mnmTogglePrefix.Length))
            .ToList());

        var avatarsToUpdate = avatarToUpdate is null || existingAvatars.All(x => x.name != avatarToUpdate)
            ? existingAvatars
            : new[] { existingAvatars.FirstOrDefault(x => x.name == avatarToUpdate) };

        foreach (var existingAvatar in avatarsToUpdate)
        {
            if (!avatarsWithEnabledMnm.Contains(existingAvatar.name))
            {
                var selectedSkinOption =
                    GameData.PersistantGameData.StringList.FirstOrDefault(x =>
                        x.Key == _modOptionPrefix + existingAvatar.name);

                if (selectedSkinOption?.Value is null)
                {
                    LogError($"No skin selected for avatar {existingAvatar.GetName()}");
                    continue;
                }

                var selectedSkinName = selectedSkinOption.Value;

                ReplaceSkin(existingAvatar.name, selectedSkinName);

                continue;
            }
            
            Debug.Log("EasyAvatarSkins: Mix & Match for " + existingAvatar.name);

            var iconSkinName =
                GameData.PersistantGameData.StringList
                    .FirstOrDefault(x => x.Key == _mnmIconPrefix + existingAvatar.name)
                    ?.Value ?? "Default";
            var idleSkinName =
                GameData.PersistantGameData.StringList
                    .FirstOrDefault(x => x.Key == _mnmIdlePrefix + existingAvatar.name)
                    ?.Value ?? "Default";
            var idleHdSkinName =
                GameData.PersistantGameData.StringList.FirstOrDefault(
                        x => x.Key == _mnmIdlehdPrefix + existingAvatar.name)
                    ?.Value ?? "Default";
            var runSkinName =
                GameData.PersistantGameData.StringList.FirstOrDefault(x => x.Key == _mnmRunPrefix + existingAvatar.name)
                    ?.Value ?? "Default";
            var victorySkinName =
                GameData.PersistantGameData.StringList.FirstOrDefault(
                        x => x.Key == _mnmVictoryPrefix + existingAvatar.name)
                    ?.Value ?? "Default";
            var gameoverSkinName =
                GameData.PersistantGameData.StringList.FirstOrDefault(
                        x => x.Key == _mnmGameoverPrefix + existingAvatar.name)
                    ?.Value ?? "Default";

            var skin = new Dictionary<SkinReplacementType, RGEasyAvatarSkinsAnimationReplacement>();

            if (iconSkinName != "Default" && skinPacks.ContainsKey(iconSkinName))
            {
                skin[SkinReplacementType.Icon] = skinPacks[iconSkinName][SkinReplacementType.Icon];
            }

            if (idleSkinName != "Default" && skinPacks.ContainsKey(idleSkinName))
            {
                skin[SkinReplacementType.Idle] = skinPacks[idleSkinName][SkinReplacementType.Idle];
            }

            if (idleHdSkinName != "Default" && skinPacks.ContainsKey(idleHdSkinName))
            {
                skin[SkinReplacementType.IdleHD] = skinPacks[idleHdSkinName][SkinReplacementType.IdleHD];
            }

            if (runSkinName != "Default" && skinPacks.ContainsKey(runSkinName))
            {
                skin[SkinReplacementType.Run] = skinPacks[runSkinName][SkinReplacementType.Run];
            }

            if (victorySkinName != "Default" && skinPacks.ContainsKey(victorySkinName))
            {
                skin[SkinReplacementType.Victory] = skinPacks[victorySkinName][SkinReplacementType.Victory];
            }

            if (gameoverSkinName != "Default" && skinPacks.ContainsKey(gameoverSkinName))
            {
                skin[SkinReplacementType.GameOver] = skinPacks[gameoverSkinName][SkinReplacementType.GameOver];
            }

            ReplaceSkin(existingAvatar.name, skin);
        }
    }

    private void CreateModOption(AvatarScriptableObject existingAvatar)
    {
        // corrupted avatar is a bit too long for game options
        var avatarName = existingAvatar.GetName().Replace("Corrupted Avatar", "Corrupted Rog");
        var avatarKey = existingAvatar.name;

        var avatarDropdownLocaleList = CreateEnglishLocalization(avatarName);

        var dropdownValues = new List<LocalizationDataList>();

        foreach (var skinName in skinPacks.Keys)
        {
            dropdownValues.Add(CreateEnglishLocalization(skinName));
            _loadedSkins.Add(skinName);
        }

        var tooltip = CreateEnglishLocalization("Selected skin for " + avatarName);

        var avatarDropdownOption = ModOption.MakeDropDownOption(_modOptionPrefix + avatarKey, avatarDropdownLocaleList,
            dropdownValues, LocalisedTooltip: tooltip);

        ModOption.AddModOption(avatarDropdownOption, "Skin Replacements", "Easy Avatar Skins");

        // Mix&Match stuff

        var mnmToggleLocaleList = CreateEnglishLocalization("Enable Mix & Match for " + avatarName);
        var mnmToggleTooltip = CreateEnglishLocalization("Use Mix & Match skin selection for " + avatarName +
                                                         " instead of a full skin chosen in the first section");
        var mnmIconDropdownLocaleList = CreateEnglishLocalization("Icon");
        var mnmIconTooltip = CreateEnglishLocalization("Icon displayed in the avatar choice list");
        var mnmIdleDropdownLocaleList = CreateEnglishLocalization("Idle");
        var mnmIdleTooltip = CreateEnglishLocalization("Idle animation (ingame)");
        var mnmIdleHdDropdownLocaleList = CreateEnglishLocalization("IdleHD");
        var mnmIdleHdTooltip = CreateEnglishLocalization("Idle animation (avatar selection screen, etc)");
        var mnmRunDropdownLocaleList = CreateEnglishLocalization("Run");
        var mnmRunTooltip = CreateEnglishLocalization("Running animation");
        var mnmVictoryDropdownLocaleList = CreateEnglishLocalization("Victory");
        var mnmVictoryTooltip = CreateEnglishLocalization("Victory animation (world saved)");
        var mnmGameoverDropdownLocaleList = CreateEnglishLocalization("Game Over");
        var mnmGameoverTooltip = CreateEnglishLocalization("Game over animation");

        var mnmToggle = ModOption.MakeToggleOption(_mnmTogglePrefix + avatarKey, mnmToggleLocaleList,
            false, mnmToggleTooltip);
        var mnmIconDropdown = ModOption.MakeDropDownOption(_mnmIconPrefix + avatarKey, mnmIconDropdownLocaleList,
            dropdownValues, LocalisedTooltip: mnmIconTooltip);
        var mnmIdleDropdown = ModOption.MakeDropDownOption(_mnmIdlePrefix + avatarKey, mnmIdleDropdownLocaleList,
            dropdownValues, LocalisedTooltip: mnmIdleTooltip);
        var mnmIdleHdDropdown = ModOption.MakeDropDownOption(_mnmIdlehdPrefix + avatarKey, mnmIdleHdDropdownLocaleList,
            dropdownValues, LocalisedTooltip: mnmIdleHdTooltip);
        var mnmRunDropdown = ModOption.MakeDropDownOption(_mnmRunPrefix + avatarKey, mnmRunDropdownLocaleList,
            dropdownValues, LocalisedTooltip: mnmRunTooltip);
        var mnmVictoryDropdown = ModOption.MakeDropDownOption(_mnmVictoryPrefix + avatarKey,
            mnmVictoryDropdownLocaleList, dropdownValues, LocalisedTooltip: mnmVictoryTooltip);
        var mnmGameoverDropdown = ModOption.MakeDropDownOption(_mnmGameoverPrefix + avatarKey,
            mnmGameoverDropdownLocaleList, dropdownValues, LocalisedTooltip: mnmGameoverTooltip);

        ModOption.AddModOption(mnmToggle, "M&M " + avatarName, "Easy Avatar Skins");
        ModOption.AddModOption(mnmIconDropdown, "M&M " + avatarName, "Easy Avatar Skins");
        ModOption.AddModOption(mnmIdleDropdown, "M&M " + avatarName, "Easy Avatar Skins");
        ModOption.AddModOption(mnmIdleHdDropdown, "M&M " + avatarName, "Easy Avatar Skins");
        ModOption.AddModOption(mnmRunDropdown, "M&M " + avatarName, "Easy Avatar Skins");
        ModOption.AddModOption(mnmVictoryDropdown, "M&M " + avatarName, "Easy Avatar Skins");
        ModOption.AddModOption(mnmGameoverDropdown, "M&M " + avatarName, "Easy Avatar Skins");
    }

    private static void LogError(string message) => Debug.LogError($"EasyAvatarSkins ERROR: {message}");

    private void AddSkinFile(string filePath, string? modName = null)
    {
        var skinPackName = modName ?? new DirectoryInfo(Path.GetDirectoryName(filePath)!).Name;
        SkinReplacementType animationType;
        var nameAndFramerate = Path.GetFileNameWithoutExtension(filePath)
            .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

        if (nameAndFramerate.Length != 3 &&
            !(nameAndFramerate.Length == 1 && nameAndFramerate[0].ToLowerInvariant() == "icon"))
        {
            LogError($"Unable to add skin file {filePath}\n" +
                     "\tFile name format should be: (animation type)_(x frames)_(y frames).png, e.g. \"idlehd_5_1.png\" -- OR \"icon.png\" for avatar icon. " +
                     "Name is not case sensitive\n" + "\tAnimation types are: idle, idlehd, run, victory, gameover");
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
                         $"\tIncorrect X frames value: {nameAndFramerate[1]}\n" + "\tExpected integer");
                return;
            }

            if (!int.TryParse(nameAndFramerate[2], out var _yFrames))
            {
                LogError($"Unable to add skin file {filePath}\n" +
                         $"\tIncorrect Y frames value: {nameAndFramerate[2]}\n" + "\tExpected integer");
                return;
            }

            xFrames = _xFrames;
            yFrames = _yFrames;
        }

        if (!skinPacks.ContainsKey(skinPackName))
        {
            skinPacks.Add(skinPackName, new Dictionary<SkinReplacementType, RGEasyAvatarSkinsAnimationReplacement>());
        }

        skinPacks[skinPackName]
            .Add(animationType,
                new RGEasyAvatarSkinsAnimationReplacement
                {
                    filePath = filePath, xFrames = xFrames, yFrames = yFrames
                });
    }

    private static void ReplaceSkin(string avatarName,
        Dictionary<SkinReplacementType, RGEasyAvatarSkinsAnimationReplacement> skinPack)
    {
        var animations = new AvatarAnimations();
        var avatar = GameDataGetter.GetAllAvatars().FirstOrDefault(x => x.name == avatarName);

        if (avatar is null || !defaultSkinPacks.ContainsKey(avatarName))
        {
            LogError($"Unable to find avatar: {avatarName}");
            return;
        }

        animations.Icon = (Sprite)defaultSkinPacks[avatarName][SkinReplacementType.Icon];
        animations.IdleAnimation = (PixelAnimationData)defaultSkinPacks[avatarName][SkinReplacementType.Idle];
        animations.IdleHDAnimation = (PixelAnimationData)defaultSkinPacks[avatarName][SkinReplacementType.IdleHD];
        animations.RunAnimation = (PixelAnimationData)defaultSkinPacks[avatarName][SkinReplacementType.Run];
        animations.VictoryAnimation = (PixelAnimationData)defaultSkinPacks[avatarName][SkinReplacementType.Victory];
        animations.GameOverAnimation = (PixelAnimationData)defaultSkinPacks[avatarName][SkinReplacementType.GameOver];

        foreach (var skinData in skinPack)
        {
            switch (skinData.Key)
            {
                case SkinReplacementType.Icon:
                    animations.Icon = ModGenesia.ModGenesia.LoadSprite(skinData.Value.filePath);
                    break;
                case SkinReplacementType.Idle:
                    animations.IdleAnimation = skinData.Value.ToPixelAnimationData();
                    break;
                case SkinReplacementType.IdleHD:
                    animations.IdleHDAnimation = skinData.Value.ToPixelAnimationData();
                    break;
                case SkinReplacementType.Run:
                    animations.RunAnimation = skinData.Value.ToPixelAnimationData();
                    break;
                case SkinReplacementType.Victory:
                    animations.VictoryAnimation = skinData.Value.ToPixelAnimationData();
                    break;
                case SkinReplacementType.GameOver:
                    animations.GameOverAnimation = skinData.Value.ToPixelAnimationData();
                    break;
                default:
                    LogError($"Incorrect animation type: {skinData.Key}");
                    return;
            }
        }

        ReplaceAvatarSkin(avatarName, animations);
    }

    private void ReplaceSkin(string avatarName, string skinName)
    {
        if (!skinPacks.ContainsKey(skinName))
        {
            LogError($"Unable to find skin pack: {skinName}");
            return;
        }

        var skinPack = skinPacks[skinName];

        Debug.Log($"EasyAvatarSkins: replacing {avatarName} with {skinName}");

        ReplaceSkin(avatarName, skinPack);
    }

    private static LocalizationDataList CreateEnglishLocalization(string defaultValue)
    {
        return new LocalizationDataList(defaultValue)
        {
            localization = new List<LocalizationData> { new() { Key = "en", Value = defaultValue } }
        };
    }
}

internal record RGEasyAvatarSkinsAnimationReplacement
{
    public string filePath = null!;
    public int xFrames;
    public int yFrames;

    public PixelAnimationData ToPixelAnimationData()
    {
        var texture = ModGenesia.ModGenesia.LoadPNGTexture(filePath);
        var framesVec = new Vector2Int(xFrames, yFrames);
        return new PixelAnimationData { Texture = texture, Frames = framesVec };
    }
}