using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests.Diagnostics
{
    internal static class BuildLayoutTestUtility
    {
        // one bundle per name, each with one file holding an explicit asset and an implicit asset, every asset with objects
        internal static BuildLayout CreateLayout(string buildResultHash, params string[] bundleInternalNames)
        {
            BuildLayout layout = new BuildLayout();
            layout.BuildResultHash = buildResultHash;
            layout.AddressablesBuildSessionGUID = GUID.Generate();
            layout.BuildStart = new DateTime(2026, 1, 1);

            BuildLayout.Group group = new BuildLayout.Group { Name = "Group", Guid = "groupguid" };
            foreach (string internalName in bundleInternalNames)
            {
                BuildLayout.Bundle bundle = new BuildLayout.Bundle { Name = internalName + ".bundle", InternalName = internalName, Group = group };
                BuildLayout.File file = new BuildLayout.File { Name = "CAB-" + internalName, Bundle = bundle };

                BuildLayout.ExplicitAsset asset = new BuildLayout.ExplicitAsset
                {
                    Guid = "explicit_" + internalName, InternalId = "asset_" + internalName, AssetPath = "Assets/" + internalName + ".prefab",
                    Bundle = bundle, File = file, MainAssetType = AssetType.Prefab
                };
                asset.Objects.Add(new BuildLayout.ObjectData
                {
                    LocalIdentifierInFile = 1, ObjectName = "Root", AssetType = AssetType.GameObject, SerializedSize = 100,
                    References = new List<BuildLayout.ObjectReference> { new BuildLayout.ObjectReference { AssetId = 1, ObjectIds = new List<int> { 0 } } }
                });
                asset.Objects.Add(new BuildLayout.ObjectData { LocalIdentifierInFile = 2, ObjectName = "Root", ComponentName = "MeshRenderer", AssetType = AssetType.Component });

                BuildLayout.DataFromOtherAsset otherAsset = new BuildLayout.DataFromOtherAsset { AssetGuid = "implicit_" + internalName, AssetPath = "Assets/" + internalName + ".png", File = file };
                otherAsset.Objects.Add(new BuildLayout.ObjectData { LocalIdentifierInFile = 3, ObjectName = "Texture", AssetType = AssetType.Texture2D });

                file.Assets.Add(asset);
                file.OtherAssets.Add(otherAsset);
                bundle.Files.Add(file);
                bundle.AssetCount = 1;
                group.Bundles.Add(bundle);
            }
            layout.Groups.Add(group);
            return layout;
        }

        // writes the layout the way a build does: profiler file first, then the layout without objects
        internal static string WriteSplit(BuildLayout layout, string directory)
        {
            string layoutPath = Path.Combine(directory, "buildlayout_1.json");
            string profilerPath = Path.Combine(directory, "buildlayout_1.profiler");
            ProfilerData.MoveToFile(layout, profilerPath, layoutPath);
            layout.WriteToFile(layoutPath, true);
            return layoutPath;
        }
    }
}
