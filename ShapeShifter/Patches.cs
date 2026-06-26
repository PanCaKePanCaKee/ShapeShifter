using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HarmonyLib;
using UnityEngine;

namespace OutfitChanger;

[HarmonyPatch]
public class Patches
{
    static List<Customization> Outfits = new();
    static int CurrentPage = 0;
    [HarmonyPatch(typeof(PlayerCustomizationMenu), nameof(PlayerCustomizationMenu.Start))]
    [HarmonyPostfix]
    public static void Start()
    {
        Outfits = new();

        var folder = OutfitChangerPlugin.OutfitFolder.Value;
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        var files = Directory.GetFiles(folder, "*.txt");

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var root = JsonSerializer.Deserialize<Root>(json);

                if (root?.customization != null)
                    Outfits.Add(root.customization);
            }
            catch (Exception e)
            {
                OutfitChangerPlugin.Logger.LogError($"Failed loading outfit {file}: {e}");
            }
        }

        OutfitChangerPlugin.Logger.LogWarning($"{Outfits.Count} outfits found");
    }

    [HarmonyPatch(typeof(PlayerCustomizationMenu), nameof(PlayerCustomizationMenu.Update))]
    [HarmonyPostfix]
    public static void Update(PlayerCustomizationMenu __instance)
    {
        if (Outfits.Count == 0) return;

        if (CheckInput(true))
        {
            CurrentPage--;
            if (CurrentPage < 0) CurrentPage = Outfits.Count - 1;

            SetOutfit(Outfits[CurrentPage]);
            __instance.PreviewArea.UpdateFromLocalPlayer(PlayerMaterial.MaskType.None);
        }
        else if (CheckInput(false))
        {
            CurrentPage++;
            if (CurrentPage >= Outfits.Count) CurrentPage = 0;

            SetOutfit(Outfits[CurrentPage]);
            __instance.PreviewArea.UpdateFromLocalPlayer(PlayerMaterial.MaskType.None);
        }
    }

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
    [HarmonyPostfix]
    public static void LateUpdate(ModManager __instance)
    {
        __instance.ShowModStamp();
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
    [HarmonyPrefix]
    public static bool SendChat(ChatController __instance)
    {
        if (!LobbyBehaviour.Instance) return true;

        string text = __instance.freeChatField.Text;

        if (!text.StartsWith("/addfit", StringComparison.OrdinalIgnoreCase))
            return true;

        var folder = OutfitChangerPlugin.OutfitFolder.Value;
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        var player = PlayerControl.LocalPlayer;

        var customization = new Customization
        {
            colorID = player.CurrentOutfit.ColorId,
            hat = player.CurrentOutfit.HatId,
            skin = player.CurrentOutfit.SkinId,
            visor = player.CurrentOutfit.VisorId,
            pet = player.CurrentOutfit.PetId,
            namePlate = player.CurrentOutfit.NamePlateId
        };

        var root = new Root { customization = customization };

        string json = JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        int i = 1;
        string dest;

        do
        {
            dest = Path.Combine(folder, $"outfit{i}.txt");
            i++;
        }
        while (File.Exists(dest));

        File.WriteAllText(dest, json);

        __instance.freeChatField.Clear();
        __instance.UpdateChatMode();

        OutfitChangerPlugin.Logger.LogWarning($"Saved outfit to {dest}");

        return false;
    }


    public static void SetOutfit(Customization outfit)
    {
        PlayerControl.LocalPlayer.CmdCheckColor((byte)outfit.colorID);
        PlayerControl.LocalPlayer.RpcSetHat(outfit.hat ?? "hat_NoHat");
        PlayerControl.LocalPlayer.RpcSetPet(outfit.pet ?? "pet_EmptyPet");
        PlayerControl.LocalPlayer.RpcSetSkin(outfit.skin ?? "skin_None");
        PlayerControl.LocalPlayer.RpcSetVisor(outfit.visor ?? "visor_EmptyVisor");
        PlayerControl.LocalPlayer.RpcSetNamePlate(outfit.namePlate ?? "nameplate_NoPlate");

    }


    public static bool CheckInput(bool toLeft)
    {
        if (toLeft && Input.GetKeyDown(KeyCode.LeftArrow))
            return true;

        if (!toLeft && Input.GetKeyDown(KeyCode.RightArrow))
            return true;

        var controller = PassiveButtonManager.Instance?.controller;
        if (controller == null) return false;

        if (controller.AnyTouchUp && controller.Touches.Count > 0)
        {
            var touch = controller.Touches[0];
            var movement = touch.Position - touch.DownAt;

            if (movement.x < -1 && toLeft) return true;
            if (movement.x > 1 && !toLeft) return true;
        }

        return false;
    }
}

public class Customization
{
    public int colorID { get; set; }
    public string pet { get; set; }
    public string hat { get; set; }
    public string skin { get; set; }
    public string visor { get; set; }
    public string namePlate { get; set; }
}

public class Root
{
    public Customization customization { get; set; }
}
