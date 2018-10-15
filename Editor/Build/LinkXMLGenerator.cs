using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml;
using System;
using System.Reflection;

internal class LinkXMLGenerator
{
    List<Type> m_types = new List<Type>();
    public void AddTypes(params Type[] t)
    {
        if(t != null)
            m_types.AddRange(t);
    }

    public void Save(string path)
    {
        var assemblyMap = new Dictionary<Assembly, List<Type>> ();
        foreach (var t in m_types)
        {
            var a = t.Assembly;
            List<Type> types;
            if (!assemblyMap.TryGetValue(a, out types))
                assemblyMap.Add(a, types = new List<Type>());
            types.Add(t);
        }
        XmlDocument doc = new XmlDocument();
        var linker = doc.AppendChild(doc.CreateElement("linker"));
        foreach (var k in assemblyMap)
        {
            var assembly = linker.AppendChild(doc.CreateElement("assembly"));
            var attr = doc.CreateAttribute("fullname");
            attr.Value = k.Key.FullName;
            assembly.Attributes.Append(attr);
            foreach (var t in k.Value)
            {
                var typeEl = assembly.AppendChild(doc.CreateElement("type"));
                var tattr = doc.CreateAttribute("fullname");
                tattr.Value = t.FullName;
                typeEl.Attributes.Append(tattr);
                var pattr = doc.CreateAttribute("preserve");
                pattr.Value = "all";
                typeEl.Attributes.Append(pattr);
            }
        }
        doc.Save(path);
    }
}
