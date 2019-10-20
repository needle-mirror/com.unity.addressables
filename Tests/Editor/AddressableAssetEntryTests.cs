using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetEntryTests : AddressableAssetTestBase
    {
        private static Dictionary<Type, Type> _editorToRuntimeTypeConversion;

        [OneTimeSetUp]
        public void Setup()
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == "UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

            _editorToRuntimeTypeConversion = new Dictionary<Type, Type>()
            {
                { asm.GetType("UnityEditor.Audio.AudioMixerGroupController"), typeof(AudioMixerGroup) },
                { asm.GetType("UnityEditor.Audio.AudioMixerController"), typeof(AudioMixer) },
                { typeof(UnityEditor.Animations.AnimatorController), typeof(RuntimeAnimatorController) }
            };
        }

        [Test]
        public void CheckForEditorAssembly_TestCorrectTypeConversion()
        {
            foreach (Type key in _editorToRuntimeTypeConversion.Keys)
            {
                Type type = key;
                AddressableAssetEntry.CheckForEditorAssembly(ref type, "", false);
                Assert.AreEqual(type, _editorToRuntimeTypeConversion[key]);
            }
        }
    }
} 