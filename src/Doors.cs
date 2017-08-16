using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.Chatting;
using Pipliz.JSON;
using Pipliz.Threading;
using Pipliz.APIProvider.Recipes;
using Pipliz.APIProvider.Jobs;
using NPC;

namespace ScarabolMods
{
  [ModLoader.ModManager]
  public static class DoorsModEntries
  {
    public static string MOD_PREFIX = "mods.scarabol.doors.";
    public static string OPEN_SUFFIX = ".open";
    public static string ModDirectory;
    private static string AssetsDirectory;
    private static string DoorsDirectory;
    private static List<string> doorTypeKeys = new List<string>();

    [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "scarabol.doors.assemblyload")]
    public static void OnAssemblyLoaded(string path)
    {
      ModDirectory = Path.GetDirectoryName(path);
      AssetsDirectory = Path.Combine(ModDirectory, "assets");
      ModLocalizationHelper.localize(Path.Combine(AssetsDirectory, "localization"), "mods.scarabol.assets.", false);
      DoorsDirectory = Path.Combine(ModDirectory, "doors");
      ModLocalizationHelper.localize(Path.Combine(DoorsDirectory, "localization"), MOD_PREFIX, false);
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, "scarabol.doors.registercallbacks")]
    public static void AfterStartup()
    {
      Pipliz.Log.Write("Loaded Doors Mod 1.1 by Scarabol");
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterAddingBaseTypes, "scarabol.doors.addrawtypes")]
    public static void AfterAddingBaseTypes()
    {
      // TODO this is realy hacky (maybe better in future ModAPI)
      string relativeTexturesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "materials", "blocks", "albedo", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(DoorsDirectory, "textures"))).OriginalString;
      Pipliz.Log.Write(string.Format("Doors relative textures path is {0}", relativeTexturesPath));
      string relativeMeshesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "meshes", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(DoorsDirectory, "meshes"))).OriginalString;
      Pipliz.Log.Write(string.Format("Doors relative meshes path is {0}", relativeMeshesPath));
      Pipliz.Log.Write(string.Format("Started loading door texture mappings..."));
      JSONNode jsonTextureMapping;
      Pipliz.JSON.JSON.Deserialize(Path.Combine(DoorsDirectory, "doorstexturemapping.json"), out jsonTextureMapping, true);
      if (jsonTextureMapping.NodeType == NodeType.Object) {
        foreach (KeyValuePair<string,JSONNode> textureEntry in jsonTextureMapping.LoopObject()) {
          try {
            foreach (string suffix in new string[] { "", OPEN_SUFFIX }) {
              JSONNode mapping = new JSONNode();
              foreach (string textureType in new string[] { "albedo", "normal", "emissive", "height" }) {
                string textureTypeValue = textureEntry.Value.GetAs<string>(textureType);
                string realTextureTypeValue = textureTypeValue;
                if (!textureTypeValue.Equals("neutral")) {
                  realTextureTypeValue = MultiPath.Combine(relativeTexturesPath, textureType, textureTypeValue);
                }
                Pipliz.Log.Write(string.Format("Rewriting {0} texture path from '{1}' to '{2}'", textureType, textureTypeValue, realTextureTypeValue));
                mapping.SetAs(textureType, realTextureTypeValue);
              }
              string realkey = MOD_PREFIX + textureEntry.Key + suffix;
              Pipliz.Log.Write(string.Format("Adding texture mapping for '{0}'", realkey));
              ItemTypesServer.AddTextureMapping(realkey, mapping);
            }
          } catch (Exception exception) {
            Pipliz.Log.WriteError(string.Format("Exception while loading from {0}; {1}", "doorstexturemapping.json", exception.Message));
          }
        }
      }
      // TODO this is realy hacky (maybe better in future ModAPI)
      string relativeAssetsTexturesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "textures", "materials", "blocks", "albedo", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(AssetsDirectory, "textures"))).OriginalString;
      ItemTypesServer.AddTextureMapping(MOD_PREFIX + "doorram", new JSONNode()
        .SetAs("albedo", MultiPath.Combine(relativeAssetsTexturesPath, "albedo", "doorram"))
        .SetAs("normal", "neutral")
        .SetAs("emissive", "neutral")
        .SetAs("height", "neutral")
      );
      ItemTypes.AddRawType(MOD_PREFIX + "doorram",
        new JSONNode(NodeType.Object)
                           .SetAs<bool>("isRotatable", true)
                           .SetAs<bool>("needsBase", true)
                           .SetAs("sideall", "SELF")
                           .SetAs("icon", MultiPath.Combine(AssetsDirectory, "icons", "doorram.png"))
                           .SetAs("rotatablex+", MOD_PREFIX + "doorramx+")
                           .SetAs("rotatablex-", MOD_PREFIX + "doorramx-")
                           .SetAs("rotatablez+", MOD_PREFIX + "doorramz+")
                           .SetAs("rotatablez-", MOD_PREFIX + "doorramz-")
      );
      string relativeAssetsMeshesPath = new Uri(MultiPath.Combine(Path.GetFullPath("gamedata"), "meshes", "dummyfile")).MakeRelativeUri(new Uri(Path.Combine(AssetsDirectory, "meshes"))).OriginalString;
      foreach (string xz in new string[] { "x+", "x-", "z+", "z-" }) {
        ItemTypes.AddRawType(MOD_PREFIX + "doorram" + xz,
          new JSONNode(NodeType.Object)
                             .SetAs("parentType", MOD_PREFIX + "doorram")
                             .SetAs("mesh", Path.Combine(relativeAssetsMeshesPath, "doorram" + xz + ".obj"))
        );
      }
      Pipliz.Log.Write(string.Format("Started loading door types..."));
      JSONNode jsonTypes;
      Pipliz.JSON.JSON.Deserialize(Path.Combine(DoorsDirectory, "doorstypes.json"), out jsonTypes, true);
      if (jsonTypes.NodeType == NodeType.Object) {
        foreach (KeyValuePair<string,JSONNode> typeEntry in jsonTypes.LoopObject()) {
          try {
            foreach (string suffix in new string[] { "", OPEN_SUFFIX }) {
              JSONNode jsonType = new JSONNode();
              string icon;
              if (typeEntry.Value.TryGetAs("icon", out icon) && suffix.Length < 1) {
                // TODO try to use relative path here, too?
                string realicon = MultiPath.Combine(DoorsDirectory, "icons", icon);
                Pipliz.Log.Write(string.Format("Rewriting icon path from '{0}' to '{1}'", icon, realicon));
                jsonType.SetAs("icon", realicon);
              }
              string mesh;
              if (typeEntry.Value.TryGetAs("mesh" + suffix, out mesh)) {
                string realmesh = Path.Combine(relativeMeshesPath, mesh);
                Pipliz.Log.Write(string.Format("Rewriting mesh path from '{0}' to '{1}'", mesh, realmesh));
                jsonType.SetAs("mesh", realmesh);
              }
              string parentType;
              if (typeEntry.Value.TryGetAs("parentType", out parentType)) {
                string realParentType = MOD_PREFIX + parentType + suffix;
                Pipliz.Log.Write(string.Format("Rewriting parentType from '{0}' to '{1}'", parentType, realParentType));
                jsonType.SetAs("parentType", realParentType);
              }
              string realkey;
              if (!(typeEntry.Key.EndsWith("x+") || typeEntry.Key.EndsWith("x-") || typeEntry.Key.EndsWith("z+") || typeEntry.Key.EndsWith("z-"))) {
                jsonType
                  .SetAs<bool>("isSolid", suffix.Length < 1)
                  .SetAs<bool>("needsBase", false)
                  .SetAs<int>("destructionTime", 200)
                  .SetAs("onRemove", new JSONNode(NodeType.Array))
                  .SetAs<bool>("isRotatable", true)
                  .SetAs("sideall", "SELF")
                  .SetAs<int>("npcLimit", 0)
                ;
                foreach (string rotatable in new string[] { "rotatablex+", "rotatablex-", "rotatablez+", "rotatablez-" }) {
                  string key;
                  if (typeEntry.Value.TryGetAs(rotatable, out key)) {
                    string rotatablekey = MOD_PREFIX + key.Substring(0, key.Length-2) + suffix + key.Substring(key.Length-2);
                    Pipliz.Log.Write(string.Format("Rewriting rotatable key '{0}' to '{1}'", key, rotatablekey));
                    jsonType.SetAs(rotatable, rotatablekey);
                  } else {
                    Pipliz.Log.WriteError(string.Format("Attribute {0} not found for base type {1}", rotatable, typeEntry.Key));
                  }
                }
                if (suffix.Length < 1) {
                  foreach (string propName in new string[] { "onDoorOpenAudio", "onRemoveAudio" }) {
                    string openAudio;
                    if (typeEntry.Value.TryGetAs(propName, out openAudio)) {
                      jsonType.SetAs("onRemoveAudio", openAudio);
                      break;
                    }
                  }
                } else {
                  foreach (string propName in new string[] { "onDoorCloseAudio", "onPlaceAudio" }) {
                    string closeAudio;
                    if (typeEntry.Value.TryGetAs(propName, out closeAudio)) {
                      jsonType.SetAs("onRemoveAudio", closeAudio);
                      break;
                    }
                  }
                }
                realkey = MOD_PREFIX + typeEntry.Key + suffix;
                Pipliz.Log.Write(string.Format("Adding door base type '{0}'", realkey));
                if (suffix.Length < 1) {
                  doorTypeKeys.Add(MOD_PREFIX + typeEntry.Key);
                }
              } else {
                realkey = MOD_PREFIX + typeEntry.Key.Substring(0, typeEntry.Key.Length-2) + suffix + typeEntry.Key.Substring(typeEntry.Key.Length-2);
                Pipliz.Log.Write(string.Format("Adding door rotatable type '{0}'", realkey));
              }
              ItemTypes.AddRawType(realkey, jsonType);
            }
          } catch (Exception exception) {
            Pipliz.Log.WriteError(string.Format("Exception while loading door type {0}; {1}", typeEntry.Key, exception.Message));
          }
        }
      } else {
        Pipliz.Log.WriteError(string.Format("Expected json object in {0}, but got {1} instead", "doorstypes.json", jsonTypes.NodeType));
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesServer, "scarabol.doors.registertypes")]
    public static void AfterItemTypesServer()
    {
      ItemTypesServer.RegisterOnAdd(MOD_PREFIX + "doorram", RamBlockCode.OnAddRam);
      foreach (string typekey in doorTypeKeys) {
        if (!typekey.EndsWith("top")) {
          Pipliz.Log.Write(string.Format("Registering OnAddDoor as OnAdd for '{0}'", typekey));
          ItemTypesServer.RegisterOnAdd(typekey, DoorBlockCode.OnAddDoor);
        }
        Pipliz.Log.Write(string.Format("Registering OnOpenAction as OnRemove for '{0}'", typekey));
        ItemTypesServer.RegisterOnRemove(typekey, DoorBlockCode.OnOpenAction);
        Pipliz.Log.Write(string.Format("Registering OnCloseAction as OnRemove for '{0}'", typekey + OPEN_SUFFIX));
        ItemTypesServer.RegisterOnRemove(typekey + OPEN_SUFFIX, DoorBlockCode.OnCloseAction);
      }
    }

    [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "scarabol.doors.loadrecipes")]
    [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.registerrecipes")]
    public static void AfterItemTypesDefined()
    {
      try {
        JSONNode jsonCraftingRamRecipe = new JSONNode()
          .SetAs("results", new JSONNode(NodeType.Array)
                 .AddToArray(new JSONNode().SetAs("type", MOD_PREFIX + "doorram")))
          .SetAs("requires", new JSONNode(NodeType.Array)
                 .AddToArray(new JSONNode().SetAs("type", "planks"))
                 .AddToArray(new JSONNode().SetAs("type", "stonebricks").SetAs<int>("amount", 2))
                );
        RecipePlayer.AllRecipes.Add(new Recipe(jsonCraftingRamRecipe));
        Pipliz.Log.Write(string.Format("Started loading door recipes..."));
        JSONNode jsonCrafting;
        Pipliz.JSON.JSON.Deserialize(Path.Combine(DoorsDirectory, "doorscrafting.json"), out jsonCrafting, true);
        if (jsonCrafting.NodeType == NodeType.Array) {
          foreach (JSONNode craftingEntry in jsonCrafting.LoopArray()) {
            JSONNode jsonResults = craftingEntry.GetAs<JSONNode>("results");
            foreach (JSONNode jsonResult in jsonResults.LoopArray()) {
              string type = jsonResult.GetAs<string>("type");
              string realtype = MOD_PREFIX + type;
              Pipliz.Log.Write(string.Format("Replacing door recipe result type '{0}' with '{1}'", type, realtype));
              jsonResult.SetAs("type", realtype);
            }
            RecipePlayer.AllRecipes.Add(new Recipe(craftingEntry));
          }
        } else {
          Pipliz.Log.WriteError(string.Format("Expected json array in {0}, but got {1} instead", "doorscrafting.json", jsonCrafting.NodeType));
        }
      } catch (Exception exception) {
        Pipliz.Log.WriteError(string.Format("Exception while loading door recipes from {0}; {1}", "doorscrafting.json", exception.Message));
      }
    }
  }

  static class RamBlockCode
  {
    public static void OnAddRam(Vector3Int position, ushort wasType, Players.Player causedBy)
    {
      try {
        Chat.Send(causedBy, string.Format("You placed a doorram. This door will be gone in 5 seconds...", position));
        ThreadManager.InvokeOnMainThread(delegate ()
        {
          string dir = ItemTypes.IndexLookup.GetName(wasType);
          string xz = dir.Substring(dir.Length - 2);
          ushort realType;
          if (World.TryGetTypeAt(position, out realType) && realType == ItemTypes.IndexLookup.GetIndex(DoorsModEntries.MOD_PREFIX + "doorram" + xz)) {
            Chat.Send(causedBy, string.Format("BAM!!!", position));
            int ox = 0, oz = 0;
            if (xz.Equals("x+")) {
              ox = 1;
            } else if (xz.Equals("x-")) {
              ox = -1;
            } else if (xz.Equals("z-")) {
              oz = -1;
            } else {
              oz = 1;
            }
            ServerManager.TryChangeBlock(position, ItemTypes.IndexLookup.GetIndex("air"));
            ServerManager.TryChangeBlock(position.Add(ox, 0, oz), ItemTypes.IndexLookup.GetIndex("air"));
            ServerManager.TryChangeBlock(position.Add(ox, 1, oz), ItemTypes.IndexLookup.GetIndex("air"));
          }
        }, 5.0);
      } catch (Exception exception) {
        Pipliz.Log.WriteError(string.Format("Exception in OnAddRam; {0}", exception.Message));
      }
    }
  }

  static class DoorBlockCode
  {
    public static void OnOpenAction(Vector3Int position, ushort wasType, Players.Player causedBy)
    {
      try {
        if (causedBy != null) {
          string wasTypeName = ItemTypes.IndexLookup.GetName(wasType); // e.g. mods.scarabol.doors.woodendoorz+
          string xz = wasTypeName.Substring(wasTypeName.Length-2); // e.g. z+
          string doorBaseName = wasTypeName.Substring(0, wasTypeName.Length-2); // e.g. mods.scarabol.doors.woodendoor
          string newTypeName = doorBaseName + DoorsModEntries.OPEN_SUFFIX + xz; // e.g. mods.scarabol.doors.woodendoor.openz+
//          Chat.Send(causedBy, string.Format("You opened a {0} at {1}, will change to {2}", wasTypeName, position, newTypeName));
          ServerManager.TryChangeBlock(position, ItemTypes.IndexLookup.GetIndex(newTypeName));
          Vector3Int otherPos = position.Add(0, 1, 0);
          string otherName = doorBaseName + "top" + DoorsModEntries.OPEN_SUFFIX + xz; // e.g. mods.scarabol.doors.woodendoortop.openz+
          if (doorBaseName.EndsWith("top")) {
            otherPos = position.Add(0, -1, 0);
            otherName = doorBaseName.Substring(0, doorBaseName.Length-"top".Length) + DoorsModEntries.OPEN_SUFFIX + xz; // e.g. mods.scarabol.doors.woodendoor.openz+
          }
//          Chat.Send(causedBy, string.Format("Other door part at {0} is changed to {1}", otherPos, otherName));
          ServerManager.TryChangeBlock(otherPos, ItemTypes.IndexLookup.GetIndex(otherName));
        } else {
          Pipliz.Log.Write(string.Format("OnOpenAction called by nobody"));
        }
      } catch (Exception exception) {
        Pipliz.Log.WriteError(string.Format("Exception in OnOpenAction; {0}", exception.Message));
      }
    }

    public static void OnCloseAction(Vector3Int position, ushort wasType, Players.Player causedBy)
    {
      try {
        Players.Player closest;
        if ((Players.TryFindClosest(position.Vector, out closest) && Pipliz.Math.ManhattanDistance(position, new Vector3Int(closest.Position)) < 2) ||
            (Players.TryFindClosest(position.Add(0, 1, 0).Vector, out closest) && Pipliz.Math.ManhattanDistance(position.Add(0, 1, 0), new Vector3Int(closest.Position)) < 2)) {
          ServerManager.TryChangeBlock(position, wasType);
          return;
        }
        if (causedBy != null) {
          string wasTypeName = ItemTypes.IndexLookup.GetName(wasType); // e.g. mods.scarabol.doors.woodendoor.openz+
          string wasTypeBaseName = wasTypeName.Substring(0, wasTypeName.Length-2);
          string xz = wasTypeName.Substring(wasTypeName.Length-2);
          string doorBaseName = wasTypeBaseName.Substring(0, wasTypeBaseName.Length-DoorsModEntries.OPEN_SUFFIX.Length);
          string newTypeName = doorBaseName + xz;
//          Chat.Send(causedBy, string.Format("You closed a {0} at {1}, will change to {2}", wasTypeName, position, newTypeName));
          ServerManager.TryChangeBlock(position, ItemTypes.IndexLookup.GetIndex(newTypeName));
          Vector3Int otherPos = position.Add(0, 1, 0);
          string otherName = doorBaseName + "top" + xz;
          if (doorBaseName.EndsWith("top")) {
            otherPos = position.Add(0, -1, 0);
            otherName = doorBaseName.Substring(0, doorBaseName.Length-"top".Length) + xz;
          }
//          Chat.Send(causedBy, string.Format("Other door part at {0} is changed to {1}", otherPos, otherName));
          ServerManager.TryChangeBlock(otherPos, ItemTypes.IndexLookup.GetIndex(otherName));
        } else {
          Pipliz.Log.Write(string.Format("OnCloseAction called by nobody"));
        }
      } catch (Exception exception) {
        Pipliz.Log.WriteError(string.Format("Exception in OnCloseAction; {0}", exception.Message));
      }
    }

    public static void OnAddDoor(Vector3Int position, ushort doorType, Players.Player causedBy)
    {
      try {
        if (causedBy != null) {
          string doorTypeName = ItemTypes.IndexLookup.GetName(doorType);
          string aboveTypeName = doorTypeName.Substring(0, doorTypeName.Length-2) + "top" + doorTypeName.Substring(doorTypeName.Length-2);
//          Chat.Send(causedBy, string.Format("You placed a {0} will place {1} above", doorTypeName, aboveTypeName));
          ServerManager.TryChangeBlock(position.Add(0, 1, 0), ItemTypes.IndexLookup.GetIndex(aboveTypeName));
        } else {
          Pipliz.Log.Write(string.Format("Door placed by nobody"));
        }
      } catch (Exception exception) {
        Pipliz.Log.WriteError(string.Format("Exception in OnAddDoor; {0}", exception.Message));
      }
    }
  }
}
