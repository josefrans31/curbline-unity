// Editor menu: Curbline > Create Phase 1 test scene.
// Makes a scene with a camera, a sun and the Phase 1 bootstrap, saves it and puts it first in the build list.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Curbline.EditorTools
{
    public static class CurblineMenu
    {
        const string ScenePath = "Assets/_Curbline/Scenes/Phase1.unity";

        [MenuItem("Curbline/Create Phase 1 test scene", priority = 1)]
        public static void CreatePhase1Scene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Curbline", "The Phase 1 scene already exists. Replace it?", "Replace", "Open it instead"))
            {
                OpenPhase1Scene();
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Phase 1").AddComponent<Phase1Bootstrap>();
            System.IO.Directory.CreateDirectory("Assets/_Curbline/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Curbline: Phase 1 scene saved to " + ScenePath + ". Press Play to drive.");
        }

        [MenuItem("Curbline/Open Phase 1 test scene", priority = 2)]
        public static void OpenPhase1Scene()
        {
            if (!System.IO.File.Exists(ScenePath)) { CreatePhase1Scene(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
