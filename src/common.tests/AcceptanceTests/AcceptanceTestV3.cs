﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Internal;
using Xunit.Runner.Common;
using Xunit.Sdk;
using Xunit.v3;

public class AcceptanceTestV3
{
	public Task<List<_MessageSinkMessage>> RunAsync(
		Type type,
		bool preEnumerateTheories = true) =>
			RunAsync(new[] { type }, preEnumerateTheories);

	public Task<List<_MessageSinkMessage>> RunAsync(
		Type[] types,
		bool preEnumerateTheories = true)
	{
		var tcs = new TaskCompletionSource<List<_MessageSinkMessage>>();

		ThreadPool.QueueUserWorkItem(async _ =>
		{
			try
			{
				var diagnosticMessageSink = _NullMessageSink.Instance;
				await using var testFramework = new XunitTestFramework(diagnosticMessageSink, configFileName: null);

				var assemblyInfo = Reflector.Wrap(Assembly.GetEntryAssembly()!);
				var discoverer = testFramework.GetDiscoverer(assemblyInfo);
				var testCases = new List<_ITestCase>();
				await discoverer.Find(testCase => { testCases.Add(testCase); return new(true); }, _TestFrameworkOptions.ForDiscovery(preEnumerateTheories: preEnumerateTheories), types);

				using var runSink = SpyMessageSink<_TestAssemblyFinished>.Create();
				var executor = testFramework.GetExecutor(assemblyInfo);
				await executor.RunTestCases(testCases, runSink, _TestFrameworkOptions.ForExecution());

				tcs.TrySetResult(runSink.Messages.ToList());
			}
			catch (Exception ex)
			{
				tcs.TrySetException(ex);
			}
		});

		return tcs.Task;
	}

	public async Task<List<TMessageType>> RunAsync<TMessageType>(
		Type type,
		bool preEnumerateTheories = true)
			where TMessageType : _MessageSinkMessage
	{
		var results = await RunAsync(type, preEnumerateTheories);
		return results.OfType<TMessageType>().ToList();
	}

	public async Task<List<TMessageType>> RunAsync<TMessageType>(
		Type[] types,
		bool preEnumerateTheories = true)
			where TMessageType : _MessageSinkMessage
	{
		var results = await RunAsync(types, preEnumerateTheories);
		return results.OfType<TMessageType>().ToList();
	}

	public async Task<List<ITestResultWithDisplayName>> RunForResultsAsync(
		Type type,
		bool preEnumerateTheories = true)
	{
		var results = await RunAsync(type, preEnumerateTheories);
		return
			results
				.OfType<_TestResultMessage>()
				.Select(result => TestResultFactory(result, results.OfType<_TestStarting>().Where(ts => ts.TestUniqueID == result.TestUniqueID).Single().TestDisplayName))
				.WhereNotNull()
				.ToList();
	}

	public async Task<List<TResult>> RunForResultsAsync<TResult>(
		Type type,
		bool preEnumerateTheories = true)
			where TResult : ITestResultWithDisplayName
	{
		var results = await RunForResultsAsync(type, preEnumerateTheories);
		return results.OfType<TResult>().ToList();
	}

	public static ITestResultWithDisplayName? TestResultFactory(
		_TestResultMessage result,
		string testDisplayName)
	{
		if (result is _TestPassed passed)
			return new TestPassedWithDisplayName(passed, testDisplayName);
		if (result is _TestFailed failed)
			return new TestFailedWithDisplayName(failed, testDisplayName);
		if (result is _TestSkipped skipped)
			return new TestSkippedWithDisplayName(skipped, testDisplayName);

		return null;
	}
}
