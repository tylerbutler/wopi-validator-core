// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommandLine;
using Microsoft.Office.WopiValidator.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Office.WopiValidator.Tools
{
	internal enum ExitCode
	{
		Success = 0,
		Failure = 1,
	}

	internal class Program
	{
		private static readonly string discoveryTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
				<wopi-discovery>
					<net-zone name=""external-https"" />
					<proof-key value=""{0}"" modulus=""{1}"" exponent=""{2}"" oldvalue = ""{3}"" oldmodulus=""{4}"" oldexponent=""{5}"" />
				</wopi-discovery>";

		public static int Main(string[] args)
		{
			ExitCode exitCode;
			try
			{
				exitCode = Parser.Default.ParseArguments<ProofKeyExportOptions>(args)
					.MapResult(
						(ProofKeyExportOptions options) => ExecuteProofKeyExport(options),
						parseErrors => ExitCode.Failure);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				exitCode = ExitCode.Failure;
			}
			return (int)exitCode;
		}

		private static ExitCode ExecuteProofKeyExport(ProofKeyExportOptions options)
		{
			RSACryptoServiceProvider currentKey = ProofKeysHelper.DefaultCurrentKeyProvider();
			RSACryptoServiceProvider oldKey = ProofKeysHelper.DefaultOldKeyProvider();

			var currentProofData = new ProofKeyDiscoveryData(currentKey);
			var oldProofData = new ProofKeyDiscoveryData(oldKey);

			var discoveryData = String.Format(discoveryTemplate,
				currentProofData.CspBlob, currentProofData.Modulus, currentProofData.Exponent,
				oldProofData.CspBlob, oldProofData.Modulus, oldProofData.Exponent);

			// "pretty-print" the XML
			discoveryData = FormatXml(discoveryData);

			using (MemoryStream mStream = new MemoryStream())
			{
				using (XmlTextWriter writer = new XmlTextWriter(mStream, Encoding.Unicode))
				{
					writer.Formatting = Formatting.Indented;
				}
			}

			if (!string.IsNullOrWhiteSpace(options.OutputFileName))
			{
				File.WriteAllText(options.OutputFileName, discoveryData);
				return ExitCode.Success;
			}

			Console.WriteLine(discoveryData);
			if (Debugger.IsAttached)
			{
				Console.WriteLine("Press any key to exit");
				Console.ReadLine();
			}
			return ExitCode.Success;
		}

		private static string FormatXml(string xml)
		{
			var settings = new XmlWriterSettings
			{
				Indent = true,
				Encoding = Encoding.UTF8,
				NewLineOnAttributes = true
			};

			var element = XElement.Parse(xml);

			using (MemoryStream mStream = new MemoryStream())
			{
				using (var writer = XmlWriter.Create(mStream, settings))
				{
					element.Save(writer);
				}
				return Encoding.UTF8.GetString(mStream.ToArray());
			}
		}
	}
}
