﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

public class CollectionPerAssemblyTestCollectionFactoryTests
{
	[Fact]
	public static void ReturnsDefaultTestCollectionForUndecoratedTestClass()
	{
		var type1 = Mocks.TypeInfo("type1");
		var type2 = Mocks.TypeInfo("type2");
		var assemblyFileName = Path.DirectorySeparatorChar == '/' ? "/foo/bar.dll" : @"C:\Foo\bar.dll";
		var assembly = Mocks.TestAssembly(assemblyFileName);
		var factory = new CollectionPerAssemblyTestCollectionFactory(assembly, SpyMessageSink.Create());

		var result1 = factory.Get(type1);
		var result2 = factory.Get(type2);

		Assert.Same(result1, result2);
		Assert.Equal("Test collection for bar.dll", result1.DisplayName);
	}

	[Fact]
	public static void ClassesDecoratedWithSameCollectionNameAreInSameTestCollection()
	{
		var attr = Mocks.CollectionAttribute("My Collection");
		var type1 = Mocks.TypeInfo("type1", attributes: new[] { attr });
		var type2 = Mocks.TypeInfo("type2", attributes: new[] { attr });
		var assembly = Mocks.TestAssembly(@"C:\Foo\bar.dll");
		var factory = new CollectionPerAssemblyTestCollectionFactory(assembly, SpyMessageSink.Create());

		var result1 = factory.Get(type1);
		var result2 = factory.Get(type2);

		Assert.Same(result1, result2);
		Assert.Equal("My Collection", result1.DisplayName);
	}

	[Fact]
	public static void ClassesWithDifferentCollectionNamesHaveDifferentCollectionObjects()
	{
		var type1 = Mocks.TypeInfo("type1", attributes: new[] { Mocks.CollectionAttribute("Collection 1") });
		var type2 = Mocks.TypeInfo("type2", attributes: new[] { Mocks.CollectionAttribute("Collection 2") });
		var assembly = Mocks.TestAssembly(@"C:\Foo\bar.dll");
		var factory = new CollectionPerAssemblyTestCollectionFactory(assembly, SpyMessageSink.Create());

		var result1 = factory.Get(type1);
		var result2 = factory.Get(type2);

		Assert.NotSame(result1, result2);
		Assert.Equal("Collection 1", result1.DisplayName);
		Assert.Equal("Collection 2", result2.DisplayName);
	}

	[Fact]
	public static void UsingTestCollectionDefinitionSetsTypeInfo()
	{
		var testType = Mocks.TypeInfo("type", attributes: new[] { Mocks.CollectionAttribute("This is a test collection") });
		var collectionDefinitionType = Mocks.TypeInfo("collectionDefinition", attributes: new[] { Mocks.CollectionDefinitionAttribute("This is a test collection") });
		var assembly = Mocks.TestAssembly(@"C:\Foo\bar.dll", types: new[] { collectionDefinitionType });
		var factory = new CollectionPerAssemblyTestCollectionFactory(assembly, SpyMessageSink.Create());

		var result = factory.Get(testType);

		Assert.Same(collectionDefinitionType, result.CollectionDefinition);
	}

	[Fact]
	public static void MultiplyDeclaredCollectionsRaisesEnvironmentalWarning()
	{
		var messages = new List<_MessageSinkMessage>();
		var spy = SpyMessageSink.Create(messages: messages);
		var testType = Mocks.TypeInfo("type", attributes: new[] { Mocks.CollectionAttribute("This is a test collection") });
		var collectionDefinition1 = Mocks.TypeInfo("collectionDefinition1", attributes: new[] { Mocks.CollectionDefinitionAttribute("This is a test collection") });
		var collectionDefinition2 = Mocks.TypeInfo("collectionDefinition2", attributes: new[] { Mocks.CollectionDefinitionAttribute("This is a test collection") });
		var assembly = Mocks.TestAssembly(@"C:\Foo\bar.dll", types: new[] { collectionDefinition1, collectionDefinition2 });
		var factory = new CollectionPerAssemblyTestCollectionFactory(assembly, spy);

		factory.Get(testType);

		var msg = Assert.Single(messages.OfType<_DiagnosticMessage>().Select(m => m.Message));
		Assert.Equal("Multiple test collections declared with name 'This is a test collection': collectionDefinition1, collectionDefinition2", msg);
	}
}
