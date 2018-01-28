using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;
using Pipliz.Threading;
using NPC;
using BlockTypes.Builtin;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class DoorsModEntries
  {
    public static string MOD_PREFIX = "mods.scarabol.doors.";
    public static string OPEN_SUFFIX = ".open";
    public static string ModDirectory;
    private static string DoorsDirectory;
    private static List<string> doorClosedTypeKeys = new List<string> ();
    private static List<string> doorOpenTypeKeys = new List<string> ();
    private static List<ushort> allDoorTypes = new List<ushort> ();

    [ModLoader.ModCallback (ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.doors.assemblyload")]
    public static void OnAssemblyLoaded (string path)
    {
      ModDirectory = Path.GetDirectoryName (path);
      DoorsDirectory = Path.Combine (ModDirectory, "doors");
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterStartup, "scarabol.doors.registercallbacks")]
    public static void AfterStartup ()
    {
      Pipliz.Log.Write ("Loaded Doors Mod 5.3.1 by Scarabol");
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.doors.addrawtypes")]
    public static void AfterAddingBaseTypes (Dictionary<string, ItemTypesServer.ItemTypeRaw> itemTypes)
    {
      Pipliz.Log.Write (string.Format ("Started loading door types..."));
      JSONNode jsonTypes;
      if (!JSON.Deserialize (Path.Combine (DoorsDirectory, "doorstypes.json"), out jsonTypes, false)) {
        Pipliz.Log.Write ("No door types file found, so no doors loaded");
        return;
      }
      if (jsonTypes.NodeType != NodeType.Object) {
        Pipliz.Log.WriteError (string.Format ("Expected json object in {0}, but got {1} instead", "doorstypes.json", jsonTypes.NodeType));
        return;
      }
      foreach (KeyValuePair<string,JSONNode> typeEntry in jsonTypes.LoopObject()) {
        try {
          foreach (string suffix in new string[] { "", OPEN_SUFFIX }) {
            JSONNode jsonType = new JSONNode ();
            string icon;
            if (typeEntry.Value.TryGetAs ("icon", out icon) && suffix.Length < 1) {
              string realicon = MultiPath.Combine (DoorsDirectory, "icons", icon);
              Pipliz.Log.Write (string.Format ("Rewriting icon path from '{0}' to '{1}'", icon, realicon));
              jsonType.SetAs ("icon", realicon);
            }
            string mesh;
            if (typeEntry.Value.TryGetAs ("mesh" + suffix, out mesh)) {
              string realmesh = MultiPath.Combine (DoorsDirectory, "meshes", mesh);
              Pipliz.Log.Write (string.Format ("Rewriting mesh path from '{0}' to '{1}'", mesh, realmesh));
              jsonType.SetAs ("mesh", realmesh);
            }
            string parentType;
            if (typeEntry.Value.TryGetAs ("parentType", out parentType)) {
              string realParentType = MOD_PREFIX + parentType + suffix;
              Pipliz.Log.Write (string.Format ("Rewriting parentType from '{0}' to '{1}'", parentType, realParentType));
              jsonType.SetAs ("parentType", realParentType);
            }
            JSONNode onRemove;
            if (typeEntry.Value.TryGetAs<JSONNode> ("onRemove", out onRemove)) {
              JSONNode jsonOnRemove = new JSONNode ();
              foreach (JSONNode onRemovePart in onRemove.LoopArray()) {
                string removeType;
                if (onRemovePart.TryGetAs ("type", out removeType)) {
                  string realOnRemove = MOD_PREFIX + removeType;
                  Pipliz.Log.Write (string.Format ("Rewriting onRemove type from '{0}' to '{1}'", removeType, realOnRemove));
                  jsonOnRemove.SetAs ("type", realOnRemove);
                  jsonOnRemove.SetAs ("amount", 1);
                  jsonOnRemove.SetAs ("chance", 1);
                }
              }
              JSONNode jsonOnRemoveGroup = new JSONNode (NodeType.Array);
              jsonOnRemoveGroup.AddToArray (jsonOnRemove);
              jsonType.SetAs ("onRemove", jsonOnRemoveGroup);
            } else if (suffix.Length > 0) {
              string plainKey = MOD_PREFIX + typeEntry.Key;
              if (typeEntry.Key.EndsWith ("x+") || typeEntry.Key.EndsWith ("x-") || typeEntry.Key.EndsWith ("z+") || typeEntry.Key.EndsWith ("z-")) {
                plainKey = plainKey.Substring (0, plainKey.Length - 2);
              }
              ItemTypesServer.ItemTypeRaw removeType;
              if (itemTypes.TryGetValue (plainKey, out removeType)) {
                plainKey = ItemTypes.IndexLookup.GetName (removeType.OnRemoveItems [0].item.Type);
              }
              JSONNode jsonOnRemove = new JSONNode ();
              jsonOnRemove.SetAs ("type", plainKey);
              jsonOnRemove.SetAs ("amount", 1);
              jsonOnRemove.SetAs ("chance", 1);
              JSONNode jsonOnRemoveGroup = new JSONNode (NodeType.Array);
              jsonOnRemoveGroup.AddToArray (jsonOnRemove);
              jsonType.SetAs ("onRemove", jsonOnRemoveGroup);
              Pipliz.Log.Write (string.Format ("Setting onRemove type for '{0}' to '{1}'", typeEntry.Key + suffix, plainKey));
            }
            string realkey;
            if (!(typeEntry.Key.EndsWith ("x+") || typeEntry.Key.EndsWith ("x-") || typeEntry.Key.EndsWith ("z+") || typeEntry.Key.EndsWith ("z-"))) {
              jsonType
                .SetAs ("isSolid", suffix.Length < 1)
                .SetAs ("needsBase", false)
                .SetAs ("isRotatable", true)
                .SetAs ("sideall", "SELF")
                .SetAs ("npcLimit", 0);
              foreach (string rotatable in new string[] { "rotatablex+", "rotatablex-", "rotatablez+", "rotatablez-" }) {
                string key;
                if (typeEntry.Value.TryGetAs (rotatable, out key)) {
                  string rotatablekey = MOD_PREFIX + key.Substring (0, key.Length - 2) + suffix + key.Substring (key.Length - 2);
                  Pipliz.Log.Write (string.Format ("Rewriting rotatable key '{0}' to '{1}'", key, rotatablekey));
                  jsonType.SetAs (rotatable, rotatablekey);
                } else {
                  Pipliz.Log.WriteError (string.Format ("Attribute {0} not found for base type {1}", rotatable, typeEntry.Key));
                }
              }
              if (suffix.Length < 1) {
                foreach (string propName in new string[] { "onDoorOpenAudio", "onRemoveAudio" }) {
                  string openAudio;
                  if (typeEntry.Value.TryGetAs (propName, out openAudio)) {
                    jsonType.SetAs ("onRemoveAudio", openAudio);
                    break;
                  }
                }
              } else {
                foreach (string propName in new string[] { "onDoorCloseAudio", "onPlaceAudio" }) {
                  string closeAudio;
                  if (typeEntry.Value.TryGetAs (propName, out closeAudio)) {
                    jsonType.SetAs ("onRemoveAudio", closeAudio);
                    break;
                  }
                }
              }
              realkey = MOD_PREFIX + typeEntry.Key + suffix;
              Pipliz.Log.Write (string.Format ("Adding door base type '{0}'", realkey));
              if (suffix.Length < 1) {
                doorClosedTypeKeys.Add (realkey);
              } else {
                doorOpenTypeKeys.Add (realkey);
              }
            } else {
              realkey = MOD_PREFIX + typeEntry.Key.Substring (0, typeEntry.Key.Length - 2) + suffix + typeEntry.Key.Substring (typeEntry.Key.Length - 2);
              Pipliz.Log.Write (string.Format ("Adding door rotatable type '{0}'", realkey));
            }
            itemTypes.Add (realkey, new ItemTypesServer.ItemTypeRaw (realkey, jsonType));
          }
        } catch (Exception exception) {
          Pipliz.Log.WriteError (string.Format ("Exception while loading door type {0}; {1}", typeEntry.Key, exception.Message));
        }
      }
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterSelectedWorld, "scarabol.doors.registertexturemappings")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.server.registertexturemappingtextures")]
    public static void AfterSelectedWorld ()
    {
      Pipliz.Log.Write (string.Format ("Started loading door texture mappings..."));
      JSONNode jsonTextureMapping;
      JSON.Deserialize (Path.Combine (DoorsDirectory, "doorstexturemapping.json"), out jsonTextureMapping, true);
      if (jsonTextureMapping.NodeType == NodeType.Object) {
        foreach (KeyValuePair<string,JSONNode> textureEntry in jsonTextureMapping.LoopObject()) {
          try {
            foreach (string suffix in new string[] { "", OPEN_SUFFIX }) {
              ItemTypesServer.TextureMapping textureMapping = new ItemTypesServer.TextureMapping (new JSONNode ());
              string textureTypeValue;
              if (textureEntry.Value.TryGetAs<string> ("albedo", out textureTypeValue) && !textureTypeValue.Equals ("neutral")) {
                string realTextureTypeValue = MultiPath.Combine (DoorsDirectory, "textures", "albedo", textureTypeValue + ".png");
                Pipliz.Log.Write (string.Format ("Rewriting {0} texture path from '{1}' to '{2}'", "albedo", textureTypeValue, realTextureTypeValue));
                textureMapping.AlbedoPath = realTextureTypeValue;
              }
              if (textureEntry.Value.TryGetAs<string> ("normal", out textureTypeValue) && !textureTypeValue.Equals ("neutral")) {
                string realTextureTypeValue = MultiPath.Combine (DoorsDirectory, "textures", "normal", textureTypeValue + ".png");
                Pipliz.Log.Write (string.Format ("Rewriting {0} texture path from '{1}' to '{2}'", "normal", textureTypeValue, realTextureTypeValue));
                textureMapping.NormalPath = realTextureTypeValue;
              }
              if (textureEntry.Value.TryGetAs<string> ("emissive", out textureTypeValue) && !textureTypeValue.Equals ("neutral")) {
                string realTextureTypeValue = MultiPath.Combine (DoorsDirectory, "textures", "emissive", textureTypeValue + ".png");
                Pipliz.Log.Write (string.Format ("Rewriting {0} texture path from '{1}' to '{2}'", "emissive", textureTypeValue, realTextureTypeValue));
                textureMapping.EmissivePath = realTextureTypeValue;
              }
              if (textureEntry.Value.TryGetAs<string> ("height", out textureTypeValue) && !textureTypeValue.Equals ("neutral")) {
                string realTextureTypeValue = MultiPath.Combine (DoorsDirectory, "textures", "height", textureTypeValue + ".png");
                Pipliz.Log.Write (string.Format ("Rewriting {0} texture path from '{1}' to '{2}'", "height", textureTypeValue, realTextureTypeValue));
                textureMapping.HeightPath = realTextureTypeValue;
              }
              string realkey = MOD_PREFIX + textureEntry.Key + suffix;
              Pipliz.Log.Write (string.Format ("Adding texture mapping for '{0}'", realkey));
              ItemTypesServer.SetTextureMapping (realkey, textureMapping);
            }
          } catch (Exception exception) {
            Pipliz.Log.WriteError (string.Format ("Exception while loading from {0}; {1}", "doorstexturemapping.json", exception.Message));
          }
        }
      }
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.doors.loadrecipes")]
    [ModLoader.ModCallbackDependsOn ("pipliz.server.loadresearchables")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.server.loadsortorder")]
    public static void LoadRecipes ()
    {
      try {
        Pipliz.Log.Write (string.Format ("Started loading door recipes..."));
        JSONNode jsonCrafting;
        if (!JSON.Deserialize (Path.Combine (DoorsDirectory, "doorscrafting.json"), out jsonCrafting, false)) {
          Pipliz.Log.Write ("No door recipes file found, so no recipes loaded");
          return;
        }
        if (jsonCrafting.NodeType != NodeType.Array) {
          Pipliz.Log.WriteError (string.Format ("Expected json array in {0}, but got {1} instead", "doorscrafting.json", jsonCrafting.NodeType));
          return;
        }
        foreach (JSONNode craftingEntry in jsonCrafting.LoopArray()) {
          JSONNode jsonResults = craftingEntry.GetAs<JSONNode> ("results");
          foreach (JSONNode jsonResult in jsonResults.LoopArray()) {
            string type = jsonResult.GetAs<string> ("type");
            string realtype = MOD_PREFIX + type;
            Pipliz.Log.Write (string.Format ("Replacing door recipe result type '{0}' with '{1}'", type, realtype));
            jsonResult.SetAs ("type", realtype);
          }
          RecipePlayer.AddDefaultRecipe (new Recipe (craftingEntry));
        }
      } catch (Exception exception) {
        Pipliz.Log.WriteError (string.Format ("Exception while loading door recipes from {0}; {1}", "doorscrafting.json", exception.Message));
      }
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.AfterWorldLoad, "scarabol.doors.afterworldload")]
    [ModLoader.ModCallbackDependsOn ("pipliz.server.localization.waitforloading")]
    [ModLoader.ModCallbackProvidesFor ("pipliz.server.localization.convert")]
    public static void AfterWorldLoad ()
    {
      ModLocalizationHelper.localize (Path.Combine (DoorsDirectory, "localization"), MOD_PREFIX);
      foreach (string xz in new string[] { "x+", "x-", "z+", "z-" }) {
        foreach (string typekey in doorClosedTypeKeys) {
          allDoorTypes.Add (ItemTypes.IndexLookup.GetIndex (typekey + xz));
        }
        foreach (string typekey in doorOpenTypeKeys) {
          allDoorTypes.Add (ItemTypes.IndexLookup.GetIndex (typekey + xz));
        }
      }
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.OnTryChangeBlockUser, "scarabol.doors.trychangeblock")]
    public static bool OnTryChangeBlockUser (ModLoader.OnTryChangeBlockUserData userData)
    {
      Players.Player requestedBy = userData.requestedBy;
      Vector3Int position = userData.VoxelToChange;
      if (userData.isPrimaryAction && allDoorTypes.Contains (userData.typeTillNow)) {
        return DoorBlockTracker.RemoveDoor (position, requestedBy);
      } else if (allDoorTypes.Contains (userData.typeToBuild)) {
        return DoorBlockTracker.AddDoor (position, userData.typeToBuild, requestedBy);
      }
      return true;
    }

    [ModLoader.ModCallback (ModLoader.EModCallbackType.OnPlayerClicked, "scarabol.doors.onplayerclicked")]
    public static void OnPlayerClicked (Players.Player player, Pipliz.Box<Shared.PlayerClickedData> boxedData)
    {
      var clickedData = boxedData.item1;
      if (clickedData.clickType == Shared.PlayerClickedData.ClickType.Left) {
        if (clickedData.rayCastHit.rayHitType == Shared.RayHitType.Block) {
          DoorBlockTracker.ToggleDoor (clickedData.rayCastHit.voxelHit, player);
        }
      }
    }
  }

  public static class DoorBlockTracker
  {
    private static Dictionary<Vector3Int, Vector3Int> doorParts = new Dictionary<Vector3Int, Vector3Int> ();

    public static bool AddDoor (Vector3Int position, ushort type, Players.Player causedBy)
    {
      string typename = ItemTypes.IndexLookup.GetName (type);
      ushort upperType;
      ushort actualType;
      string topname = typename.Substring (0, typename.Length - 2) + "top" + typename.Substring (typename.Length - 2);
      if (ItemTypes.IndexLookup.TryGetIndex (topname, out upperType)) {
        Vector3Int upperPos = position.Add (0, 1, 0);
        if (World.TryGetTypeAt (upperPos, out actualType) && actualType != BuiltinBlocks.Air) {
          return false;
        }
        if (ServerManager.TryChangeBlock (upperPos, upperType)) {
          doorParts.Add (position, upperPos);
          doorParts.Add (upperPos, position);
          return true;
        } else {
          return false;
        }
      } else {
        doorParts.Add (position, Vector3Int.invalidPos);
        return true;
      }
    }

    public static bool RemoveDoor (Vector3Int position, Players.Player causedBy)
    {
      Vector3Int otherPos;
      if (doorParts.TryGetValue (position, out otherPos) && otherPos != Vector3Int.invalidPos) {
        if (!ServerManager.TryChangeBlock (otherPos, BuiltinBlocks.Air)) {
          return false;
        }
        doorParts.Remove (otherPos);
      }
      doorParts.Remove (position);
      return true;
    }

    public static void ToggleDoor (Vector3Int position, Players.Player causedBy)
    {
      Vector3Int otherPos;
      if (doorParts.TryGetValue (position, out otherPos)) {
        ToggleDoorBlock (position, causedBy);
        if (otherPos != Vector3Int.invalidPos) {
          ToggleDoorBlock (otherPos, causedBy);
        }
      }
    }

    private static void ToggleDoorBlock (Vector3Int position, Players.Player causedBy)
    {
      ushort actualType;
      if (World.TryGetTypeAt (position, out actualType)) {
        string typename = ItemTypes.IndexLookup.GetName (actualType);
        string baseTypename = typename.Substring (0, typename.Length - 2);
        string xz = typename.Substring (typename.Length - 2);
        string otherTypename;
        if (baseTypename.EndsWith (DoorsModEntries.OPEN_SUFFIX)) {
          otherTypename = baseTypename.Substring (0, baseTypename.Length - DoorsModEntries.OPEN_SUFFIX.Length) + xz;
        } else {
          otherTypename = baseTypename + DoorsModEntries.OPEN_SUFFIX + xz;
        }
        ushort otherType;
        if (ItemTypes.IndexLookup.TryGetIndex (otherTypename, out otherType)) {
          ServerManager.TryChangeBlock (position, otherType);
        }
      }
    }
  }
}
