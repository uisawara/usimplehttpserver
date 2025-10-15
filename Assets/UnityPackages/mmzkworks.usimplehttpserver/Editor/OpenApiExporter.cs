#nullable enable
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace mmzkworks.SimpleHttpServer.OpenApi.Editor
{
    // 設定アセット（ProjectSettings と同階層に一つ作成推奨）
    public sealed class OpenApiExportSettings : ScriptableObject
    {
        public string title = "uSimpleHttpServer API";
        public string version = "v0.1.0";

        [Tooltip("プロジェクトからの相対パス。例: Assets/OpenAPI/openapi.yml や Assets/StreamingAssets/openapi.yml")]
        public string outputRelativePath = "Assets/OpenAPI/openapi.yml";

        [Tooltip("スキャンするアセンブリ名（空なら Assembly-CSharp のみ）")]
        public string[] assemblyNames = Array.Empty<string>();

        public static OpenApiExportSettings LoadOrCreate()
        {
            const string path = "Assets/Settings/OpenApiExportSettings.asset";
            var s = AssetDatabase.LoadAssetAtPath<OpenApiExportSettings>(path);
            if (s == null)
            {
                var dirPath = Path.GetDirectoryName(path);
                if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
                s = CreateInstance<OpenApiExportSettings>();
                AssetDatabase.CreateAsset(s, path);
                AssetDatabase.SaveAssets();
            }

            return s;
        }
    }

    public sealed class OpenApiExporter
    {
        public int callbackOrder => 0;

        [MenuItem("Tools/uSimpleHttpServer/Generate OpenAPI YAML")]
        public static void GenerateMenu()
        {
            var settings = OpenApiExportSettings.LoadOrCreate();
            Generate(settings);
            EditorUtility.DisplayDialog("OpenAPI YAML", $"Generated: {settings.outputRelativePath}", "OK");
        }

        private static void Generate(OpenApiExportSettings settings)
        {
            var asms = settings.assemblyNames.Length == 0
                ? new[] { Assembly.Load("Assembly-CSharp") }
                : settings.assemblyNames.Select(Assembly.Load).ToArray();

            var doc = OpenApiGenerator.BuildOpenApi(settings.title, settings.version, asms);

            var outPath = settings.outputRelativePath.Replace('\\', '/');
            var dir = Path.GetDirectoryName(outPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using var sw = new StreamWriter(outPath, false, new UTF8Encoding(false));
            SimpleYamlEmitter.WriteYaml(doc, sw);
            sw.Flush();
            AssetDatabase.ImportAsset(outPath);
        }
    }
}
#endif