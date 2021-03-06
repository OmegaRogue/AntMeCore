﻿using AntMe.Runtime;
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;
using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace AntMe.Generator
{
    /// <summary>
    /// Code Generator to build a localization- and summarization-Assembly.
    /// </summary>
    public class ModpackGenerator
    {
        private string cultureTwoLetterISOCode;
        private string outputPath;
        private ITypeMapper typeMapper;
        private List<BaseParseNode> _parseNodeTrees;
        private string[] BlackList = {  @"AntMe.Basics.ItemProperties(([.|+]\w*)*):ToString",
                                        @"AntMe.Basics.ItemProperties(([.|+]\w*)*):Equals",
                                        @"AntMe.Basics.ItemProperties(([.|+]\w*)*):GetHashCode",
                                        @"AntMe.Basics.ItemProperties(([.|+]\w*)*):GetType",
                                        @"AntMe.Basics.ItemProperties(([.|+]\w*)*):Item",
                                        @"AntMe.Basics.ItemInfo(([.|+]\w*)*):ToString",
                                        @"AntMe.Basics.ItemInfo(([.|+]\w*)*):Equals",
                                        @"AntMe.Basics.ItemInfo(([.|+]\w*)*):GetHashCode",
                                        @"AntMe.ItemInfo(([.|+]\w*)*):ToString",
                                        @"AntMe.ItemInfo(([.|+]\w*)*):Equals",
                                        @"AntMe.ItemInfo(([.|+]\w*)*):GetHashCode",
                                        @"AntMe.Basics.Items(([.|+]\w*)*):ToString",
                                        @"AntMe.Basics.Items(([.|+]\w*)*):Equals",
                                        @"AntMe.Basics.Items(([.|+]\w*)*):GetHashCode",};
        private List<BaseParseNode> parseNodeTrees
        {
            get
            {
                if (_parseNodeTrees == null)
                    _parseNodeTrees = GenerateParseNodeTrees(cultureTwoLetterISOCode);
                return _parseNodeTrees;
            }
        }

        public KeyValueStore Localization { get; private set; }
        public KeyValueStore EnglishLocalization { get; private set; }
 
        public ModpackGenerator(string cultureTwoLetterISOCode, ITypeMapper typeMapper)
        {
            this.cultureTwoLetterISOCode = cultureTwoLetterISOCode;
            this.typeMapper = typeMapper;
            Localization = ExtensionLoader.GetDictionary(cultureTwoLetterISOCode);
            EnglishLocalization = cultureTwoLetterISOCode == "en" ? Localization : ExtensionLoader.GetDictionary("en");
        }

        public static string Generate(string cultureTwoLetterISOCode, string outputpath, ITypeMapper typeMapper)
        {
            return new ModpackGenerator(cultureTwoLetterISOCode, typeMapper).Generate(outputpath);
        }

        public string Generate(string outputPath)
        {

            string outputFile = "Summary.dll_";
            List<Type> typeReferences = new List<Type>();
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                                                        .WithOverflowChecks(true)
                                                        .WithOptimizationLevel(OptimizationLevel.Release);

            foreach (BaseParseNode node in parseNodeTrees)
            {
                //collect all References.
                typeReferences.AddRange(node.GetReferences());

                //generate and adding syntaxtrees.
                syntaxTrees.Add(SyntaxFactory.SyntaxTree(SyntaxFactory.CompilationUnit().AddMembers(node.Generate())));
            }

            //collect all distinct References and adding them to the referenceList.
            List<MetadataReference> references = new List<MetadataReference>();
            foreach (string location in typeReferences.Select(c => c.Assembly.Location).Distinct())
            {
                references.Add(MetadataReference.CreateFromFile(location));
            }

            //generating the Output-Assembly as Code-File
            StreamWriter streamWriter = new StreamWriter(File.Open(Path.Combine(outputPath, "Assembly.cs"), FileMode.Create));
            syntaxTrees[0].GetRoot().NormalizeWhitespace().GetText().Write(streamWriter);
            streamWriter.Flush();
            streamWriter.Close();

            //generating the Assembly
            CSharpCompilation compilation = CSharpCompilation.Create(outputFile, syntaxTrees, references, options);
            var result = compilation.Emit(Path.Combine(outputPath, outputFile));

            //returning all copilationerrors as new exception
            if (!result.Success)
                throw new Exception(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));

            return Path.Combine(outputPath, outputFile);

        }

        public KeyValueStore GenerateLocaKeys(string cultureTwoLetterISOCode)
        {
            KeyValueStore result = new KeyValueStore();
            foreach (BaseParseNode node in GenerateParseNodeTrees(cultureTwoLetterISOCode))
            {
                result.Merge(node.GetLocaKeys());
            }
            return result;
        }

        private List<BaseParseNode> GenerateParseNodeTrees(string cultureTwoLetterISOCode)
        {
            List<BaseParseNode> trees = new List<BaseParseNode>();
            
            #region Info-Wrapper

            BaseParseNode root = new NamespaceParseNode(string.Format("AntMe.{0}", cultureTwoLetterISOCode), WrapType.InfoWrap, this);
            List<Type> wrapped = new List<Type>();

            // Collect all ItemInfos and link them to the Localized ItemInfos
            foreach (var item in typeMapper.Items)
            {
                if (item.InfoType == null)
                    continue;
                Type t = item.InfoType;

                //iterrate down the typetree
                while (t != typeof(PropertyList<ItemInfoProperty>) && t != null && !wrapped.Contains(t))
                {
                    if (t == null || CheckBlackList(t.FullName))
                        continue;
                    ClassParseNode classNode = new ClassParseNode(t, WrapType.InfoWrap, this);

                    //Adding all Methods of the Type
                    foreach (MethodInfo methodInfo in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (methodInfo.IsSpecialName || CheckBlackList(string.Format("{0}:{1}", methodInfo.DeclaringType, methodInfo.Name)))
                            continue;
                        classNode.add(new MethodParseNode(methodInfo, WrapType.InfoWrap, this));
                    }

                    //Adding all Properties of the Type
                    foreach (PropertyInfo propertyInfo in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (CheckBlackList(string.Format("{0}:{1}", propertyInfo.DeclaringType, propertyInfo.Name)))
                            continue;
                        classNode.add(new PropertyParseNode(propertyInfo, WrapType.InfoWrap, this));
                    }

                    //Adding Attached ItemProperties
                    if (typeMapper.Items.Select(x => x.InfoType).Contains(t))
                    {
                        foreach (IAttachmentTypeMapperEntry attachedItem in typeMapper.ItemAttachments.Where(p => p.Type == typeMapper.Items.First(x => x.InfoType == t).Type))
                        {
                            if (CheckBlackList(attachedItem.AttachmentType.FullName))
                                continue;
                            classNode.add(new AttachmentParseNode(attachedItem.AttachmentType, WrapType.InfoWrap, this));
                        }
                    }
                    wrapped.Add(t);
                    root.add(classNode);
                    t = t.BaseType;
                }
            }

            trees.Add(root);
            #endregion

            return trees;
        }

               public bool CheckBlackList(string fullKey)
        {
            foreach (string pattern in BlackList)
            {
                if (Regex.IsMatch(fullKey, pattern))
                    return true;
            }
            return false;
        }

        //private static void AnalyseType<T>(Type type, Dictionary<Type, List<string>> dict)
        //{
        //    Type t = type;
        //    AnalyseType(t, dict);

        //    while (t != typeof(T) && t != null)
        //    {
        //        t = t.BaseType;
        //        AnalyseType(t, dict);
        //    }
        //}

        //private static void AnalyseType(Type type, Dictionary<Type, List<string>> dict)
        //{
        //    if (dict.ContainsKey(type)) return;

        //    if (type.IsGenericType) return;

        //    List<string> result = new List<string>();
        //    dict.Add(type, result);

        //    // Name
        //    string name = type.Name;
        //    if (!result.Contains(type.Name))
        //        result.Add(type.Name);

        //    // Enum
        //    if (type.IsEnum)
        //        foreach (var value in Enum.GetNames(type))
        //            result.Add(value);

        //    // Methods / Parameter
        //    foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        //    {
        //        if (method.IsSpecialName || method.IsAbstract || method.IsVirtual)
        //            continue;

        //        if (method.DeclaringType != type)
        //            continue;

        //        if (!result.Contains(method.Name))
        //            result.Add(method.Name);

        //        foreach (var parameter in method.GetParameters())
        //        {
        //            if (parameter.ParameterType.FullName.StartsWith("AntMe."))
        //                AnalyseType(parameter.ParameterType, dict);

        //            if (!result.Contains(parameter.Name))
        //                result.Add(parameter.Name);
        //        }
        //    }

        //    // Properties
        //    foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        //    {
        //        if (property.DeclaringType != type)
        //            continue;

        //        if (property.PropertyType.FullName.StartsWith("AntMe."))
        //            AnalyseType(property.PropertyType, dict);

        //        if (!result.Contains(property.Name))
        //            result.Add(property.Name);
        //    }

        //    // Events
        //    foreach (var e in type.GetEvents(BindingFlags.Instance | BindingFlags.Public))
        //    {
        //        // TODO: Check Parameter

        //        if (!result.Contains(e.Name))
        //            result.Add(e.Name);
        //    }
        //}

    }
}
