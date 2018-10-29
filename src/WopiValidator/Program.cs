﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CommandLine;
using Microsoft.Office.WopiValidator.Core;

namespace Microsoft.Office.WopiValidator
{
	internal enum ExitCode
	{
		Success = 0,
		Failure = 1,
	}

	internal class Program
	{
		private static TestCaseExecutor GetTestCaseExecutor(TestExecutionData testExecutionData, Options options, TestCategory inputTestCategory)
		{
			TestCategory testCategory;
			if (!Enum.TryParse(testExecutionData.TestCase.Category, true /* ignoreCase */, out testCategory))
			{
				throw new Exception(string.Format(CultureInfo.InvariantCulture, "Invalid TestCategory for TestCase : {0}", testExecutionData.TestCase.Name));
			}

			string userAgent = (inputTestCategory == TestCategory.OfficeNativeClient || testCategory == TestCategory.OfficeNativeClient) ? Constants.HeaderValues.OfficeNativeClientUserAgent : null;

			var proofKeyProviderNew = GetRSACryptoServiceProvider("ProofKeysNew.cer");
			var proofKeyProviderOld = GetRSACryptoServiceProvider("ProofKeysOld.cer");

			return new TestCaseExecutor(testExecutionData, options.WopiEndpoint, options.AccessToken, options.AccessTokenTtl, userAgent, proofKeyProviderNew, proofKeyProviderOld);
		}

		private static RSACryptoServiceProvider GetRSACryptoServiceProvider(string pathToCert)
		{
			var cert = new X509Certificate2(pathToCert);
			var parameters = GetCspParamsFromCertificate(cert);
			return new RSACryptoServiceProvider(parameters);
		}

		private static CspParameters GetCspParamsFromCertificate(X509Certificate2 cert)
		{
			if (cert == null)
			{
				return null;
			}

			RSACryptoServiceProvider privateKey = cert.PrivateKey as RSACryptoServiceProvider;
			if (privateKey == null)
			{
				return null;
			}

			CspKeyContainerInfo cspKeyContainerInfo = privateKey.CspKeyContainerInfo;

			// Create a CspParameters object with the following properties:
			//   KeyContainerName matching the cert's csp:  Use the private key from the cert
			//       (The key for all ProviderTypes is stored in a shared physical location)
			//   KeyNumber matching the cert's csp:  The value of the -sky param for makecert.exe (Exchange or Signature)
			//   UseMachineKeyStore according to the cert's csp:  LOCAL_MACHINE cert store, if specified
			CspParameters csp = new CspParameters();

			csp.ProviderType = 24; // PROV_RSA_AES
			csp.KeyContainerName = cspKeyContainerInfo.KeyContainerName;
			csp.KeyNumber = (int)cspKeyContainerInfo.KeyNumber;
			if (cspKeyContainerInfo.MachineKeyStore)
			{
				csp.Flags = CspProviderFlags.UseMachineKeyStore;
			}

			return csp;
		}

		private static int Main(string[] args)
		{
			// Wrapping all logic in a top-level Exception handler to ensure that exceptions are
			// logged to the console and don't cause Windows Error Reporting to kick in.
			ExitCode exitCode = ExitCode.Success;
			try
			{
				exitCode = Parser.Default.ParseArguments<Options>(args)
					.MapResult(
						(Options options) => Execute(options),
						parseErrors => ExitCode.Failure);
			}
			catch (Exception ex)
			{
				WriteToConsole(ex.ToString(), ConsoleColor.Red);
				exitCode = ExitCode.Failure;
			}

			if (Debugger.IsAttached)
			{
				WriteToConsole("Press any key to exit", ConsoleColor.White);
				Console.ReadLine();
			}
			return (int)exitCode;
		}

		private static ExitCode Execute(Options options)
		{
			// get run configuration from XML
			IEnumerable<TestExecutionData> testData = ConfigParser.ParseExecutionData(options.RunConfigurationFilePath, options.TestCategory);

			if (!String.IsNullOrEmpty(options.TestGroup))
			{
				testData = testData.Where(d => d.TestGroupName == options.TestGroup);
			}

			IEnumerable<TestExecutionData> executionData;
			if (!String.IsNullOrWhiteSpace(options.TestName))
			{
				executionData = new TestExecutionData[] { TestExecutionData.GetDataForSpecificTest(testData, options.TestName) };
			}
			else
			{
				executionData = testData;
			}

			// Create executor groups
			var executorGroups = executionData.GroupBy(d => d.TestGroupName)
				.Select(g => new
				{
					Name = g.Key,
					Executors = g.Select(x => GetTestCaseExecutor(x, options, options.TestCategory))
				});

			ConsoleColor baseColor = ConsoleColor.White;
			HashSet<ResultStatus> resultStatuses = new HashSet<ResultStatus>();
			foreach (var group in executorGroups)
			{
				WriteToConsole($"\nTest group: {group.Name}\n", ConsoleColor.White);

				// define execution query - evaluation is lazy; test cases are executed one at a time
				// as you iterate over returned collection
				var results = group.Executors.Select(x => x.Execute());

				// iterate over results and print success/failure indicators into console
				foreach (TestCaseResult testCaseResult in results)
				{
					resultStatuses.Add(testCaseResult.Status);
					switch (testCaseResult.Status)
					{
						case ResultStatus.Pass:
							baseColor = ConsoleColor.Green;
							WriteToConsole($"Pass: {testCaseResult.Name}\n", baseColor, 1);
							break;

						case ResultStatus.Skipped:
							baseColor = ConsoleColor.Yellow;
							if (!options.IgnoreSkipped)
							{
								WriteToConsole($"Skipped: {testCaseResult.Name}\n", baseColor, 1);
							}
							break;

						case ResultStatus.Fail:
						default:
							baseColor = ConsoleColor.Red;
							WriteToConsole($"Fail: {testCaseResult.Name}\n", baseColor, 1);
							break;
					}

					if (testCaseResult.Status == ResultStatus.Fail ||
						(testCaseResult.Status == ResultStatus.Skipped && !options.IgnoreSkipped))
					{
						foreach (var request in testCaseResult.RequestDetails)
						{
							var responseStatus = (HttpStatusCode)request.ResponseStatusCode;
							var color = request.ValidationFailures.Count == 0 ? ConsoleColor.DarkGreen : baseColor;
							WriteToConsole($"{request.Name}, response code: {request.ResponseStatusCode} {responseStatus}\n", color, 2);
							foreach (var failure in request.ValidationFailures)
							{
								foreach (var error in failure.Errors)
									WriteToConsole($"{error.StripNewLines()}\n", baseColor, 3);
							}
						}

						WriteToConsole($"Re-run command: .\\wopivalidator.exe -n {testCaseResult.Name} -w {options.WopiEndpoint} -t {options.AccessToken} -l {options.AccessTokenTtl}\n", baseColor, 2);
						Console.WriteLine();
					}
				}

				if (options.IgnoreSkipped && !resultStatuses.ContainsAny(ResultStatus.Pass, ResultStatus.Fail))
				{
					WriteToConsole($"All tests skipped.\n", baseColor, 1);
				}
			}

			// If skipped tests are ignored, don't consider them when determining whether the test run passed or failed
			if (options.IgnoreSkipped)
			{
				if (resultStatuses.Contains(ResultStatus.Fail))
				{
					return ExitCode.Failure;
				}
			}
			// Otherwise consider skipped tests as failures
			else if (resultStatuses.ContainsAny(ResultStatus.Skipped, ResultStatus.Fail))
			{
				return ExitCode.Failure;
			}
			return ExitCode.Success;
		}

		private static void WriteToConsole(string message, ConsoleColor color, int indentLevel = 0)
		{
			ConsoleColor currentColor = Console.ForegroundColor;
			Console.ForegroundColor = color;
			string indent = new string(' ', indentLevel * 2);
			Console.Write(indent + message);
			Console.ForegroundColor = currentColor;
		}
	}

	internal static class ExtensionMethods
	{
		internal static bool ContainsAny<T>(this HashSet<T> set, params T[] items)
		{
			return set.Intersect(items).Any();
		}

		internal static string StripNewLines(this string str)
		{
			StringBuilder sb = new StringBuilder(str);
			bool newLineAtStart = str.StartsWith(Environment.NewLine);
			bool newLineAtEnd = str.EndsWith(Environment.NewLine);
			sb.Replace(Environment.NewLine, " ");

			if (newLineAtStart)
			{
				sb.Insert(0, Environment.NewLine);
			}

			if (newLineAtEnd)
			{
				sb.Append(Environment.NewLine);
			}
			return sb.ToString();
		}
	}
}
