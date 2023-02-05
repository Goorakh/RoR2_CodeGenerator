using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine.AddressableAssets;

using UnityObject = UnityEngine.Object;

namespace CodeGenerator
{
    internal static class AddressablePaths
    {
        static bool isID(string key)
        {
            foreach (char c in key)
            {
                if (!char.IsDigit(c) && !(char.IsLower(c) && c >= 'a' && c <= 'f'))
                {
                    return false;
                }
            }

            return true;
        }

        static IEnumerable<string> getAddressableKeys()
        {
            return Addressables.ResourceLocators.SelectMany(locator => locator.Keys).Select(key => key?.ToString()).Where(key =>
            {
                if (string.IsNullOrEmpty(key))
                {
#if DEBUG
                    Log.Debug("Skipping key: null");
#endif
                    return false;
                }

                if (int.TryParse(key, out _))
                {
#if DEBUG
                    Log.Debug($"Skipping key {key}: number");
#endif
                    return false;
                }

                if (isID(key))
                {
#if DEBUG
                    Log.Debug($"Skipping key {key}: id");
#endif
                    return false;
                }

                // These assets cause the game to freeze indefinitely when trying to load them, so just manually exclude them all
                switch (key)
                {
                    case "Advanced_Pressed_mini":
                    case "Advanced_UnPressed_mini":
                    case "Button_Off":
                    case "Button_On":
                    case "DebugUI Canvas":
                    case "DebugUI Persistent Canvas":
                    case "Icon":
                    case "Materials/Collider":
                    case "Materials/EdgePicker":
                    case "Materials/EdgePickerHDRP":
                    case "Materials/FacePicker":
                    case "Materials/FacePickerHDRP":
                    case "Materials/InvisibleFace":
                    case "Materials/NoDraw":
                    case "Materials/ProBuilderDefault":
                    case "Materials/StandardVertexColorHDRP":
                    case "Materials/StandardVertexColorLWRP":
                    case "Materials/Trigger":
                    case "Materials/UnlitVertexColor":
                    case "Materials/VertexPicker":
                    case "Materials/VertexPickerHDRP":
                    case "Missing Object":
                    case "Textures/GridBox_Default":
#if DEBUG
                        Log.Debug($"Skipping key {key}: blacklist");
#endif
                        return false;
                }

                return true;
            }).Distinct().OrderBy(s => s);
        }

        readonly struct AddressableAssetDef
        {
            public readonly Type AssetType;
            public readonly string Key;

            public readonly string FieldName;

            static readonly StringBuilder _sharedStringBuilder = new StringBuilder();

            public AddressableAssetDef(UnityObject asset, string key)
            {
                AssetType = asset.GetType();
                Key = key;

                FieldName = filterFieldName(asset.name);
            }

            static string filterFieldName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return string.Empty;

                if (!char.IsLetter(name[0]))
                    name = '_' + name;

                _sharedStringBuilder.Clear();

                foreach (char c in name)
                {
                    if (char.IsLetterOrDigit(c) || c == '_')
                    {
                        _sharedStringBuilder.Append(c);
                    }
                    else
                    {
                        _sharedStringBuilder.Append('_');
                    }
                }

                return _sharedStringBuilder.ToString();
            }
        }

        static IEnumerable<AddressableAssetDef> getAllAssetDefs(string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];

                Log.Info($"Loading asset {i + 1}/{keys.Length}: {key}");

                UnityObject asset = Addressables.LoadAssetAsync<UnityObject>(key).WaitForCompletion();
                if (asset)
                {
                    yield return new AddressableAssetDef(asset, key);
                }
                else
                {
                    Log.Info($"Failed to load asset {key}");
                }
            }
        }

        [RoR2.ConCommand(commandName = "cg_addressables")]
        static void CCGenerateAddressablesCodeFile(RoR2.ConCommandArgs args)
        {
            const GenerationMode GENERATION_MODE_DEFAULT_VALUE = GenerationMode.RefClass;

            GenerationMode generationMode;
            if (args.Count > 0)
            {
                generationMode = args.TryGetArgEnum<GenerationMode>(0) ?? GENERATION_MODE_DEFAULT_VALUE;
            }
            else
            {
                generationMode = GENERATION_MODE_DEFAULT_VALUE;
            }

            generate(Main.ModFolder, generationMode);
        }

        enum GenerationMode : byte
        {
            RefClass,
            Constants
        }

        static void generate(string targetDirectory, GenerationMode generationMode)
        {
            const string CLASS_NAME = "AddressableAssets";
            const string FILE_NAME = $"{CLASS_NAME}.cs";

            string[] keys = getAddressableKeys().ToArray();
            AddressableAssetDef[] allAssets = getAllAssetDefs(keys).ToArray();

            Log.Info($"Loaded {allAssets.Length}/{keys.Length} assets");

            StringBuilder sb = new StringBuilder();

            if (generationMode == GenerationMode.RefClass)
            {
                sb.AppendLine(
@"internal class AddressableAssetRef<T> where T : UnityEngine.Object
{
    public readonly string Key;

    bool _hasLoadedAsset;
    T _cachedAsset;

    public T Asset
    {
        get
        {
            if (!_hasLoadedAsset)
            {
                _cachedAsset = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(Key).WaitForCompletion();
                _hasLoadedAsset = true;
            }

            return _cachedAsset;
        }
    }

    internal AddressableAssetRef(string key)
    {
        Key = key;
    }
}");

                sb.AppendLine();
            }

            sb.AppendLine($"// This class was automatically generated, used {allAssets.Length}/{keys.Length} asset keys");

            sb.AppendLine($"internal static class {CLASS_NAME}")
              .AppendLine("{");

            foreach (IGrouping<Type, AddressableAssetDef> assetGrouping in from assetDef in allAssets
                                                                           orderby assetDef.AssetType.Name
                                                                           group assetDef by assetDef.AssetType)
            {
                string className = assetGrouping.Key.Name;

                sb.AppendLine($"\tpublic static class {className}")
                  .AppendLine("\t{");

                string typeName = assetGrouping.Key.FullName;

                Dictionary<string, int> usedFieldNamesCount = new Dictionary<string, int>();

                foreach (AddressableAssetDef addressableAssetDef in assetGrouping.OrderBy(assetDef => assetDef.FieldName))
                {
                    string fieldName = addressableAssetDef.FieldName;
                    if (usedFieldNamesCount.TryGetValue(fieldName, out int usedCount))
                    {
                        usedFieldNamesCount[fieldName] = usedCount + 1;

                        while (usedFieldNamesCount.ContainsKey(fieldName + $"_{usedCount}"))
                        {
                            usedCount++;
                        }

                        fieldName += $"_{usedCount}";
                    }

                    if (fieldName == className)
                    {
                        fieldName += '_';
                    }

                    usedFieldNamesCount.Add(fieldName, 1);
                    sb.Append("\t\t");

                    switch (generationMode)
                    {
                        case GenerationMode.RefClass:
                            sb.AppendLine($"public static readonly AddressableAssetRef<global::{typeName}> {fieldName} = new AddressableAssetRef<global::{typeName}>(\"{addressableAssetDef.Key}\");");
                            break;
                        case GenerationMode.Constants:
                            sb.AppendLine($"public const string {fieldName} = \"{addressableAssetDef.Key}\";");
                            break;
                        default:
                            break;
                    }
                }

                sb.AppendLine("\t}");
            }

            sb.Append('}');

            string filePath = Path.Combine(targetDirectory, FILE_NAME);
            File.WriteAllText(filePath, sb.ToString());

            Log.Info($"Finished generating {CLASS_NAME}, output path: {filePath}");
        }
    }
}