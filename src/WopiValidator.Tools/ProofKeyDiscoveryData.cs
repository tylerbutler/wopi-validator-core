// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography;

namespace Microsoft.Office.WopiValidator.Tools
{
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
