using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.U2D;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    class CheckSpriteAtlasDupeDependencies : BundleRuleBase
    {
        public override bool CanFix
        {
            get { return false; }
        }


        public override string ruleName
        {
            get { return "Check Sprite Atlas to Addressable Duplicate Dependencies"; }
        }

        internal struct SpriteDuplicationData
        {
            public string SpritePath;
            public string FileName;
        }

        public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            ClearAnalysis();

            var atlasPaths = GetAllAddressableSpriteAtlasPaths(settings);

            CalculateInputDefinitions(settings);
            RefreshBuild(GetBuildContext(settings));

            foreach (string atlasPath in atlasPaths)
            {
                var atlasGuid = new GUID(AssetDatabase.AssetPathToGUID(atlasPath));
                var atlasFile = m_ExtractData.WriteData.AssetToFiles[atlasGuid][0];

                var files = GetDuplicatedSpriteData(atlasPath);

                foreach (var file in files)
                {
                    if (file.FileName != atlasFile)
                    {
                        m_Results.Add(new AnalyzeResult()
                        {

                            resultName = m_ExtractData.WriteData.FileToBundle[atlasFile] + kDelimiter +
                                         atlasPath + kDelimiter +
                                         m_ExtractData.WriteData.FileToBundle[file.FileName] + kDelimiter +
                                         file.SpritePath
                        });
                    }
                }
            }

            if(m_Results.Count == 0)
                m_Results.Add(noErrors);

            return m_Results;
        }

        public override void FixIssues(AddressableAssetSettings settings)
        {
            //Do nothing.  There's nothing to fix.
        }

        internal string[] GetAllAddressableSpriteAtlasPaths(AddressableAssetSettings settings)
        {
           return (from addrGroup in settings.groups
                   where addrGroup != null
                   from asset in addrGroup.entries
                   where asset.MainAssetType == typeof(SpriteAtlas)
                   select asset.AssetPath).ToArray();
        }

        internal List<SpriteDuplicationData> GetDuplicatedSpriteData(string atlasPath)
        {
            return (from spritePath in AssetDatabase.GetDependencies(atlasPath)
                    let spriteGuid = new GUID(AssetDatabase.AssetPathToGUID(spritePath))
                    from fileName in m_ExtractData.WriteData.FileToObjects.Keys
                    from objectId in m_ExtractData.WriteData.FileToObjects[fileName]
                    where objectId.guid == spriteGuid
                    select new SpriteDuplicationData() { FileName = fileName, SpritePath = spritePath }).ToList();
        }
    }

    [InitializeOnLoad]
    class RegisterCheckSpriteAtlasDupeDependencies
    {
        static RegisterCheckSpriteAtlasDupeDependencies()
        {
            AnalyzeSystem.RegisterNewRule<CheckSpriteAtlasDupeDependencies>();
        }
    }
}