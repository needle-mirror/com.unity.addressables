using UnityEngine;
using NUnit.Framework;
using System;
using UnityEngine.AddressableAssets.Initialization;

namespace AddrRuntimePropertiesTests
{
    public class AddrRuntimePropertiesTests
    {
        [Test]
        public void RuntimeProperties_CanAddValue()
        {
            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            AddressablesRuntimeProperties.SetPropertyValue("name", "val");

            Assert.AreEqual(1, AddressablesRuntimeProperties.GetCachedValueCount());
        }
        
        [Test]
        public void RuntimeProperties_CanSetValueMultipleTimes()
        {
            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            AddressablesRuntimeProperties.SetPropertyValue("name", "val");
            AddressablesRuntimeProperties.SetPropertyValue("name", "val2");

            Assert.AreEqual(1, AddressablesRuntimeProperties.GetCachedValueCount());
        }
        
        [Test]
        public void RuntimeProperties_ClearCacheClears()
        {
            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            AddressablesRuntimeProperties.SetPropertyValue("name", "val");
            AddressablesRuntimeProperties.ClearCachedPropertyValues();

            Assert.AreEqual(0, AddressablesRuntimeProperties.GetCachedValueCount());
        }
        
        [Test]
        public void RuntimeProperties_EvaluatePropertyCanEvaluateSetValue()
        {
            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            string expectedResult = "myVal";
            string key = "myName";
            AddressablesRuntimeProperties.SetPropertyValue(key, expectedResult);

            string actualResult = AddressablesRuntimeProperties.EvaluateProperty(key);
            
            Assert.AreEqual(expectedResult, actualResult);
        }

        public static string ReflectableStringValue = "myReflectionResult";
        [Test]
        public void RuntimeProperties_CanEvaluateReflection()
        {
            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            string expectedResult = ReflectableStringValue;
            string actualResult = AddressablesRuntimeProperties.EvaluateProperty("AddrRuntimePropertiesTests.AddrRuntimePropertiesTests.ReflectableStringValue");
            
            Assert.AreEqual(expectedResult, actualResult);
        }
        
        
        [Test]
        public void RuntimeProperties_EvaluateStringCanParseAutomaticTokens()
        {
            string tok1 = "cheese";
            string tok2 = "cows";
            string tok3 = "moo";

            string toEval = tok1 + '{' + tok2 + '}' + tok3;
            string expectedResult = tok1 + tok2 + tok3;

            string actualResult = AddressablesRuntimeProperties.EvaluateString(toEval);

            Assert.AreEqual(expectedResult, actualResult);
        }
        
        [Test]
        public void RuntimeProperties_EvaluateStringCanParseInExplicitOverride()
        {
            string tok1 = "cheese";
            string tok2 = "cows";
            string tok3 = "moo";
            string replacement = "_parsed_";
            char delim = '?';
            
            string toEval = tok1 + delim + tok2 + delim + tok3;
            
            string expectedResult = tok1 + replacement + tok3;

            string actualResult = AddressablesRuntimeProperties.EvaluateString(toEval, delim,delim, s =>
            {
                return replacement;
            });

            Assert.AreEqual(expectedResult, actualResult);
        }
        
        [Test]
        public void RuntimeProperties_EvaluateStringIgnoresSingleDelim()
        {
            string tok1 = "cheese";
            string tok2 = "cows";
            string tok3 = "moo";

            string toEval = tok1 + tok2 + '}' + tok3;
            string expectedResult = toEval;
            string actualResult = AddressablesRuntimeProperties.EvaluateString(toEval);
            Assert.AreEqual(expectedResult, actualResult);
            
            
            toEval = tok1 + '{' + tok2 + tok3;
            expectedResult = toEval;
            actualResult = AddressablesRuntimeProperties.EvaluateString(toEval);
            Assert.AreEqual(expectedResult, actualResult);
            
            
            string replacement = "_parsed_";
            char delim = '?';
            toEval = tok1 + tok2 + delim + tok3;
            expectedResult = toEval;
            actualResult = AddressablesRuntimeProperties.EvaluateString(toEval, delim,delim, s =>
            {
                return replacement;
            });
            Assert.AreEqual(expectedResult, actualResult);
            
            toEval = tok1 + delim + tok2 + tok3;
            expectedResult = toEval;
            actualResult = AddressablesRuntimeProperties.EvaluateString(toEval, delim,delim, s =>
            {
                return replacement;
            });
            Assert.AreEqual(expectedResult, actualResult);
        }
    }
}
