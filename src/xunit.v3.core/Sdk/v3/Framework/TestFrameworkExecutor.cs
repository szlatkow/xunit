﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Internal;
using Xunit.Sdk;

namespace Xunit.v3
{
	/// <summary>
	/// A reusable implementation of <see cref="_ITestFrameworkExecutor"/> which contains the basic behavior
	/// for running tests.
	/// </summary>
	/// <typeparam name="TTestCase">The type of the test case used by the test framework. Must
	/// derive from <see cref="_ITestCase"/>.</typeparam>
	public abstract class TestFrameworkExecutor<TTestCase> : _ITestFrameworkExecutor, IAsyncDisposable
		where TTestCase : _ITestCase
	{
		_IReflectionAssemblyInfo assemblyInfo;
		_IMessageSink diagnosticMessageSink;
		bool disposed;
		_ISourceInformationProvider sourceInformationProvider;

		/// <summary>
		/// Initializes a new instance of the <see cref="TestFrameworkExecutor{TTestCase}"/> class.
		/// </summary>
		/// <param name="assemblyInfo">The test assembly.</param>
		/// <param name="sourceInformationProvider">The source line number information provider.</param>
		/// <param name="diagnosticMessageSink">The message sink which receives <see cref="_DiagnosticMessage"/> messages.</param>
		protected TestFrameworkExecutor(
			_IReflectionAssemblyInfo assemblyInfo,
			_ISourceInformationProvider sourceInformationProvider,
			_IMessageSink diagnosticMessageSink)
		{
			this.assemblyInfo = Guard.ArgumentNotNull(nameof(assemblyInfo), assemblyInfo);
			this.sourceInformationProvider = Guard.ArgumentNotNull(nameof(sourceInformationProvider), sourceInformationProvider);
			this.diagnosticMessageSink = Guard.ArgumentNotNull(nameof(diagnosticMessageSink), diagnosticMessageSink);
		}

		/// <summary>
		/// Gets the assembly information of the assembly under test.
		/// </summary>
		protected _IReflectionAssemblyInfo AssemblyInfo
		{
			get => assemblyInfo;
			set => assemblyInfo = Guard.ArgumentNotNull(nameof(AssemblyInfo), value);
		}

		/// <summary>
		/// Gets the message sink to send diagnostic messages to.
		/// </summary>
		protected _IMessageSink DiagnosticMessageSink
		{
			get => diagnosticMessageSink;
			set => diagnosticMessageSink = Guard.ArgumentNotNull(nameof(DiagnosticMessageSink), value);
		}

		/// <summary>
		/// Gets the disposal tracker for the test framework discoverer.
		/// </summary>
		protected DisposalTracker DisposalTracker { get; } = new DisposalTracker();

		/// <summary>
		/// Gets the source information provider.
		/// </summary>
		protected _ISourceInformationProvider SourceInformationProvider
		{
			get => sourceInformationProvider;
			set => sourceInformationProvider = Guard.ArgumentNotNull(nameof(SourceInformationProvider), value);
		}

		/// <summary>
		/// Override to create a test framework discoverer that can be used to discover
		/// tests when the user asks to run all test.
		/// </summary>
		/// <returns>The test framework discoverer</returns>
		protected abstract _ITestFrameworkDiscoverer CreateDiscoverer();

		/// <summary>
		/// Override to change the way test cases are deserialized. By default, uses <see cref="SerializationHelper"/>
		/// to do the deserialization work to restore an <see cref="_ITestCase"/> object.
		/// </summary>
		/// <param name="serializedTestCase">The serialized test case value</param>
		/// <returns>The deserialized test case</returns>
		protected virtual _ITestCase Deserialize(string serializedTestCase)
		{
			Guard.ArgumentNotNull(nameof(serializedTestCase), serializedTestCase);

			return SerializationHelper.Deserialize<_ITestCase>(serializedTestCase) ?? throw new ArgumentException($"Could not deserialize test case: {serializedTestCase}", nameof(serializedTestCase));
		}

		/// <inheritdoc/>
		public virtual ValueTask DisposeAsync()
		{
			if (disposed)
				throw new ObjectDisposedException(GetType().FullName);

			disposed = true;

			return DisposalTracker.DisposeAsync();
		}

		/// <inheritdoc/>
		public virtual async void RunAll(
			_IMessageSink executionMessageSink,
			_ITestFrameworkDiscoveryOptions discoveryOptions,
			_ITestFrameworkExecutionOptions executionOptions)
		{
			Guard.ArgumentNotNull("executionMessageSink", executionMessageSink);
			Guard.ArgumentNotNull("discoveryOptions", discoveryOptions);
			Guard.ArgumentNotNull("executionOptions", executionOptions);

			var discoverySink = new TestDiscoveryVisitor();

			await using var tracker = new DisposalTracker();
			var discoverer = CreateDiscoverer();
			tracker.Add(discoverer);

			discoverer.Find(discoverySink, discoveryOptions);
			discoverySink.Finished.WaitOne();

			var testCases =
				discoverySink
					.TestCases
					.Cast<TTestCase>()
					.CastOrToReadOnlyCollection();

			RunTestCases(testCases, executionMessageSink, executionOptions);
		}

		/// <inheritdoc/>
		public virtual void RunTests(
			IReadOnlyCollection<string> serializedTestCases,
			_IMessageSink executionMessageSink,
			_ITestFrameworkExecutionOptions executionOptions)
		{
			Guard.ArgumentNotNull(nameof(serializedTestCases), serializedTestCases);
			Guard.ArgumentNotNull(nameof(executionMessageSink), executionMessageSink);
			Guard.ArgumentNotNull(nameof(executionOptions), executionOptions);

			var testCases =
				serializedTestCases
					.Select(x => Deserialize(x))
					.Cast<TTestCase>()
					.CastOrToReadOnlyCollection();

			RunTestCases(testCases, executionMessageSink, executionOptions);
		}

		/// <summary>
		/// Override to run test cases.
		/// </summary>
		/// <param name="testCases">The test cases to be run.</param>
		/// <param name="executionMessageSink">The message sink to report run status to.</param>
		/// <param name="executionOptions">The user's requested execution options.</param>
		protected abstract void RunTestCases(
			IReadOnlyCollection<TTestCase> testCases,
			_IMessageSink executionMessageSink,
			_ITestFrameworkExecutionOptions executionOptions
		);
	}
}
