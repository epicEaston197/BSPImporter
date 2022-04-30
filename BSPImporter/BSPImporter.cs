using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using CodeX;
using System;
using System.IO;

namespace BSPImporter
{
    public class BSPImporter : NeosMod
    {
        public override string Name => "BSPImporter";
        public override string Author => "epicEaston197";
        public override string Version => "1.0.0";
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("epiceaston197.bspimporter");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(ModelImporter), "ImportModel")]
        class FileImporterPatch
        {
            public static bool Prefix(ref string file, Slot targetSlot, ModelImportSettings settings, Slot assetsSlot, IProgressIndicator progressIndicator)
            {
                // If its not a bsp, have Neos handle import like normal - return true
                if (!string.Equals(Path.GetExtension(file), ".bsp"))
                {
                    return true;
                }

                // Blender is needed - we use python script to convert
                if (!BlenderInterface.IsAvailable)
                {
                    Error($"Failed to find blender path for BSP file {file}");
                    progressIndicator.ProgressFail($"Failed to find blender for BSP: {Path.GetFileName(file)}");
                    // We return false as to not crash to game
                    // Neos crashes if you try to import bsp
                    return false;
                }

                string escapedFile = file.Replace("/", "\\").Replace("\\", "\\\\");

                string tempPath = Engine.Current.LocalDB.TemporaryPath;
                tempPath = tempPath.Replace("/", "\\").Replace("\\", "\\\\");

                string myScript =
                     $@"
import bpy
from SourceIO.library.shared.content_providers.content_manager import ContentManager
from SourceIO.blender_bindings.source1.bsp.import_bsp import BSP, BPSPropCache

content_manager = ContentManager()
content_manager.scan_for_content('{escapedFile}')

bsp_map = BSP('{escapedFile}', scale=0.01905)
bpy.context.scene['content_manager_data'] = content_manager.serialize()

BPSPropCache().purge()

bsp_map.load_disp()
bsp_map.load_entities()
bsp_map.load_static_props()
#if self.import_cubemaps:
# bsp_map.load_cubemap()
#if self.import_decal:
# bsp_map.load_overlays()
#if self.import_textures:
# bsp_map.load_materials(self.use_bvlg)
content_manager.flush_cache()
content_manager.clean()

bpy.ops.export_scene.gltf(filepath='{tempPath + "\\\\" + Path.GetFileNameWithoutExtension(file)}.glb')
                    ";

                Debug($"Running blender ({BlenderInterface.Executable}) with script\n" + myScript);

                AccessTools.Method(typeof(BlenderInterface), "RunScript").Invoke(null, new object[] {
                    myScript,
                    "--background --python \"{0}\""});

                // Have neos import like a normal glb
                Msg($"Importing converted bsp file {file}");
                file = $"{tempPath}\\\\{Path.GetFileNameWithoutExtension(file)}.glb";
                return true;
            }
        }
    }
}