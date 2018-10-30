// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Office.WopiValidator.Core;
using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace WopiValidator.Discovery
{
	class Program
	{
		private static readonly string discoveryTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
				<wopi-discovery>
					<proof-key value=""{0}"" modulus=""{1}"" exponent=""{2}"" oldvalue = ""{3}"" oldmodulus=""{4}"" oldexponent=""{5}"" />
				</wopi-discovery>";

		public static void Main(string[] args)
		{
			RSACryptoServiceProvider currentKey = ProofKeysHelper.DefaultCurrentKeyProvider();
			RSACryptoServiceProvider oldKey = ProofKeysHelper.DefaultOldKeyProvider();

			var currentProofData = new ProofKeyDiscoveryData(currentKey);
			var oldProofData = new ProofKeyDiscoveryData(oldKey);

			var discoveryData = String.Format(discoveryTemplate,
				currentProofData.CspBlob, currentProofData.Modulus, currentProofData.Exponent,
				oldProofData.CspBlob, oldProofData.Modulus, oldProofData.Exponent);

			Console.WriteLine(discoveryData);
			if (Debugger.IsAttached)
			{
				Console.WriteLine("Press any key to exit");
				Console.ReadLine();
			}
		}

		internal class ProofKeyDiscoveryData
		{
			readonly RSACryptoServiceProvider _provider;

			internal ProofKeyDiscoveryData(RSACryptoServiceProvider serviceProvider)
			{
				_provider = serviceProvider;
			}

			public string CspBlob
			{
				get
				{
					return Convert.ToBase64String(_provider.ExportCspBlob(includePrivateParameters: false));
				}
			}

			public string Exponent
			{
				get
				{
					var parameters = _provider.ExportParameters(includePrivateParameters: false);
					return Convert.ToBase64String(parameters.Exponent);
				}
			}

			public string Modulus
			{
				get
				{
					var parameters = _provider.ExportParameters(includePrivateParameters: false);
					return Convert.ToBase64String(parameters.Modulus);
				}
			}
		}
	}
}
