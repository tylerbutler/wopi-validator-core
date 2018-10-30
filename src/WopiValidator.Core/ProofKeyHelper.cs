// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Microsoft.Office.WopiValidator.Core
{
	public class ProofKeyInput
	{
		public string AccessToken { get; private set; }
		public long Timestamp { get; private set; }
		public string Url { get; private set; }

		public ProofKeyInput(string accessToken, long timestamp, string url)
		{
			AccessToken = accessToken;
			Timestamp = timestamp;
			Url = url;
		}
	}

	public class ProofKeyOutput
	{
		public string AccessToken { get; private set; }
		public byte[] AccessTokenBytes;
		public int AccessTokenLength { get; private set; }
		public byte[] AccessTokenLengthBytes;

		public string Url { get; private set; }
		public byte[] UrlBytes;
		public int UrlLength { get; private set; }
		public byte[] UrlLengthBytes;

		public long TimeStamp { get; private set; }
		public byte[] TimeStampBytes;
		public int TimeStampLength { get; private set; }
		public byte[] TimeStampLengthBytes;

		public int PreSigningLength { get; private set; }
		public byte[] PreSigningBytes;

		public string SignedBase64ProofKey { get; set; }

		public ProofKeyOutput(
			string accessToken,
			byte[] accessTokenBytes,
			int accessTokenLength,
			byte[] accessTokenLengthBytes,
			string url,
			byte[] urlBytes,
			int urlLength,
			byte[] urlLengthBytes,
			long timestamp,
			byte[] timestampBytes,
			int timestampLength,
			byte[] timestampLengthBytes,
			byte[] preSigningBytes)
		{
			AccessToken = accessToken;
			AccessTokenBytes = accessTokenBytes;
			AccessTokenLength = accessTokenLength;
			AccessTokenLengthBytes = accessTokenLengthBytes;
			Url = url;
			UrlBytes = urlBytes;
			UrlLength = urlLength;
			UrlLengthBytes = urlLengthBytes;
			TimeStamp = timestamp;
			TimeStampBytes = timestampBytes;
			TimeStampLength = timestampLength;
			TimeStampLengthBytes = timestampLengthBytes;
			PreSigningLength = preSigningBytes.Length;
			PreSigningBytes = preSigningBytes;
		}
	}

	public class ProofKeysHelper
	{
		private static ProofKeyOutput GetProofData(ProofKeyInput proofData)
		{
			if (proofData.AccessToken == null) {
				throw new ProofKeySigningException(nameof(proofData.AccessToken));
			}

			// Get the final values we'll operate on
			string accessToken = proofData.AccessToken;
			string hostUrl = proofData.Url.ToUpperInvariant();
			long timeStamp = proofData.Timestamp;

			// Encode values from headers into byte[]
			byte[] accessTokenBytes = Encoding.UTF8.GetBytes(accessToken);
			byte[] hostUrlBytes = Encoding.UTF8.GetBytes(hostUrl);
			byte[] timeStampBytes = EncodeNumber(timeStamp);

			int accessTokenLength = accessTokenBytes.Length;
			int hostUrlLength = hostUrlBytes.Length;
			int timeStampLength = timeStampBytes.Length;

			byte[] accessTokenLengthBytes = EncodeNumber(accessTokenLength);
			byte[] hostUrlLengthBytes = EncodeNumber(hostUrlLength);
			byte[] timeStampLengthBytes = EncodeNumber(timeStampLength);

			// prepare a list that will be used to combine all those arrays together
			List<byte> expectedProof = new List<byte>(
				accessTokenLengthBytes.Length + accessTokenLength +
				hostUrlLengthBytes.Length + hostUrlLength +
				timeStampLengthBytes.Length + timeStampLength);

			expectedProof.AddRange(accessTokenLengthBytes);
			expectedProof.AddRange(accessTokenBytes);
			expectedProof.AddRange(hostUrlLengthBytes);
			expectedProof.AddRange(hostUrlBytes);
			expectedProof.AddRange(timeStampLengthBytes);
			expectedProof.AddRange(timeStampBytes);

			// create another byte[] from that list
			byte[] preSigningBytes = expectedProof.ToArray();

			return new ProofKeyOutput(
				accessToken,
				accessTokenBytes,
				accessTokenLength,
				accessTokenLengthBytes,
				hostUrl,
				hostUrlBytes,
				hostUrlLength,
				hostUrlLengthBytes,
				timeStamp,
				timeStampBytes,
				timeStampLength,
				timeStampLengthBytes,
				preSigningBytes);
		}

		public static ProofKeyOutput GetSignedProofData(ProofKeyInput proofData, RSACryptoServiceProvider rsaAlg)
		{
			ProofKeyOutput output = GetProofData(proofData);

			using (SHA256CryptoServiceProvider hashAlg = new SHA256CryptoServiceProvider())
			{
				byte[] signedProofBytes = rsaAlg.SignData(output.PreSigningBytes, hashAlg);
				output.SignedBase64ProofKey = Convert.ToBase64String(signedProofBytes);

				return output;
			}
		}

		/// <summary>
		/// Creates an RSACryptoServiceProvider from an X509 certificate.
		/// </summary>
		/// <param name="pathToCert">The path to the cert file.</param>
		/// <returns>An RSACryptoServiceProvider based on the cert.</returns>
		public static RSACryptoServiceProvider GetRSACryptoServiceProvider(string pathToCert)
		{
			var cert = new X509Certificate2(pathToCert);
			var parameters = GetCspParamsFromCertificate(cert);
			return new RSACryptoServiceProvider(parameters);
		}

		public static RSACryptoServiceProvider DefaultCurrentKeyProvider()
		{
			return GetRSACryptoServiceProvider("ProofKeysNew.cer");
		}

		public static RSACryptoServiceProvider DefaultOldKeyProvider()
		{
			return GetRSACryptoServiceProvider("ProofKeysOld.cer");
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

			CspParameters csp = new CspParameters
			{
				ProviderType = 24, // PROV_RSA_AES
				KeyContainerName = cspKeyContainerInfo.KeyContainerName,
				KeyNumber = (int)cspKeyContainerInfo.KeyNumber
			};

			if (cspKeyContainerInfo.MachineKeyStore)
			{
				csp.Flags = CspProviderFlags.UseMachineKeyStore;
			}

			return csp;
		}

		private static byte[] EncodeNumber(int value)
		{
			return BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(value));
		}

		private static byte[] EncodeNumber(long value)
		{
			return BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(value));
		}
	}

	public class ProofKeySigningException : ArgumentNullException
	{
		public ProofKeySigningException()
			: base()
		{
		}

		public ProofKeySigningException(string paramName)
			: base(paramName)
		{
		}
	}
}
