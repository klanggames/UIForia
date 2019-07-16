using System.IO;
using UnityEngine;

namespace UIForia {

    public class UIForiaSettings : ScriptableObject {

        public bool loadTemplatesFromStreamingAssets = false;
        public Material svgxMaterial;
        public string[] defaultNamespaces;
        
        internal static string s_InternalStreamingPath;
        internal static string s_InternalNonStreamingPath;
        internal static string s_UserNonStreamingPath;
        internal static string s_UserStreamingPath;
        public bool usePreCompiledTemplates;

        public void OnEnable() {
            loadTemplatesFromStreamingAssets = loadTemplatesFromStreamingAssets || !UnityEngine.Application.isEditor;
            s_InternalStreamingPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "UIForiaInternal");
            s_InternalNonStreamingPath = Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "UIForia", "Src");
            s_UserNonStreamingPath = UnityEngine.Application.dataPath;
            s_UserStreamingPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "UIForia");
        }

        public string GetInternalTemplatePath(string fileName) {
            if (loadTemplatesFromStreamingAssets) {
                return Path.GetFullPath(Path.Combine(s_InternalStreamingPath, fileName));
            }
            else {
                return Path.GetFullPath(Path.Combine(s_InternalNonStreamingPath, fileName));
            }
        }
        
        public string GetTemplatePath(string appRoot, string fileName) {
            if (loadTemplatesFromStreamingAssets) {
                return Path.GetFullPath(Path.Combine(s_UserStreamingPath, appRoot, fileName));
            }
            else {
                return Path.GetFullPath(Path.Combine(s_UserNonStreamingPath, appRoot, fileName));
            }
        }

        public string GetStylePath(string appRoot, string fileName) {
            if (loadTemplatesFromStreamingAssets) {
                return Path.GetFullPath(Path.Combine(s_UserStreamingPath, appRoot, fileName));
            }
            else {
                return Path.GetFullPath(Path.Combine(s_UserNonStreamingPath, appRoot, fileName));
            }
        }
    }

}