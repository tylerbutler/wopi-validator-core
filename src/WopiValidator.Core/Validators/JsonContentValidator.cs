﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Office.WopiValidator.Core.Validators
{
	/// <summary>
	/// Validates that response content is a JSON encoded string that contains provided set of properties with values matching expecting ones.
	/// </summary>
	class JsonContentValidator : IValidator
	{
		private readonly IJsonPropertyValidator[] _propertyValidators;

		public JsonContentValidator(IJsonPropertyValidator propertyValidator = null)
		{
			_propertyValidators = propertyValidator == null ? new IJsonPropertyValidator[0] : new[] { propertyValidator };
		}

		public JsonContentValidator(IEnumerable<IJsonPropertyValidator> propertyValidators)
		{
			_propertyValidators = (propertyValidators ?? Enumerable.Empty<IJsonPropertyValidator>()).ToArray();
		}

		public string Name
		{
			get { return "JsonContentValidator"; }
		}

		public ValidationResult Validate(IResponseData data, IResourceManager resourceManager, Dictionary<string, string> savedState)
		{
			string responseContentString = data.GetResponseContentAsString();
			if (!data.IsTextResponse || String.IsNullOrEmpty(responseContentString))
				return new ValidationResult("Couldn't read resource content.");

			return ValidateJsonContent(responseContentString, savedState);
		}

		private ValidationResult ValidateJsonContent(string jsonString, Dictionary<string, string> savedState)
		{
			try
			{
				JObject jObject = JObject.Parse(jsonString);

				List<string> errors = new List<string>();
				foreach (IJsonPropertyValidator propertyValidator in _propertyValidators)
				{
					JToken propertyValue = jObject.SelectToken(propertyValidator.Key);

					string errorMessage;
					bool result = propertyValidator.Validate(propertyValue, savedState, out errorMessage);

					if (!result)
						errors.Add(string.Format("Incorrect value for '{0}' property. {1}", propertyValidator.Key, errorMessage));
				}

				if (errors.Count == 0)
					return new ValidationResult();

				return new ValidationResult(errors.ToArray());
			}
			catch(JsonReaderException ex)
			{
				return new ValidationResult($"{Name}: {ex.GetType().Name} thrown while parsing JSON. Are you sure the response is JSON?");
			}
			catch (JsonException ex)
			{
				return new ValidationResult($"{Name}: {ex.GetType().Name} thrown while parsing JSON content: '{ex.Message}'");
			}
		}

		public interface IJsonPropertyValidator
		{
			string Key { get; }
			bool Validate(JToken actualValue, Dictionary<string, string> savedState, out string errorMessage);
		}

		public abstract class JsonPropertyValidator : IJsonPropertyValidator
		{
			private readonly string _validationFailureMessage;

			protected JsonPropertyValidator(string key, bool isRequired, string validationFailedMessage)
			{
				Key = key;
				IsRequired = isRequired;
				ValidationFailedMessage = validationFailedMessage;
			}

			protected bool IsActualValueNullOrEmpty(JToken actualValue)
			{
				return (actualValue == null) ||
						(actualValue.Type == JTokenType.Array && !actualValue.HasValues) ||
						(actualValue.Type == JTokenType.Object && !actualValue.HasValues) ||
						(actualValue.Type == JTokenType.String && string.IsNullOrEmpty(actualValue.Value<string>())) ||
						(actualValue.Type == JTokenType.Null);
			}

			protected string GetErrorMessage(string message)
			{
				return !string.IsNullOrEmpty(ValidationFailedMessage) ? ValidationFailedMessage : message;
			}

			protected string ValidationFailedMessage { get; private set; }

			public string Key { get; private set; }

			public bool IsRequired { get; private set; }

			public abstract bool Validate(JToken actualValue, Dictionary<string, string> savedState, out string errorMessage);
		}


		public class JsonAbsoluteUrlPropertyValidator : JsonPropertyValidator
		{
			public string ExpectedStateKey { get; private set; }
			private readonly bool _mustIncludeAccessToken = false;

			public JsonAbsoluteUrlPropertyValidator(string key, bool isRequired, bool mustIncludeAccessToken, string expectedStateKey, string validationFailedMessage = null)
				: base(key, isRequired, validationFailedMessage)
			{
				ExpectedStateKey = expectedStateKey;
				_mustIncludeAccessToken = mustIncludeAccessToken;
			}

			public override bool Validate(JToken actualValue, Dictionary<string, string> savedState, out string errorMessage)
			{
				errorMessage = ValidationFailedMessage;

				if (IsActualValueNullOrEmpty(actualValue))
				{
					if (IsRequired)
					{
						errorMessage = GetErrorMessage("Value is required but not provided.");
						return false;
					}

					return true;
				}
				else
				{
					string value = actualValue.Value<string>();

					Uri uri;
					if (Uri.TryCreate(value, UriKind.Absolute, out uri))
					{
						if (_mustIncludeAccessToken && IncludesAccessToken(value))
						{
							errorMessage = GetErrorMessage($"URL '{value}' does not include the 'access_token' query parameter");
							return false;
						}

						return true;
					}
					else
					{
						errorMessage = GetErrorMessage($"Cannot parse {value} as absolute URL");
						return false;
					}
				}
			}

			/// <summary>
			/// Returns true if the URI includes an access_token query string parameter; false otherwise.
			/// </summary>
			private bool IncludesAccessToken(string url)
			{
				return UrlHelper.GetQueryParameterValue(url, "access_token") == null;
			}
		}

		public abstract class JsonPropertyEqualityValidator<T> : JsonPropertyValidator
			where T : IEquatable<T>
		{
			protected JsonPropertyEqualityValidator(string key, bool isRequired, T expectedValue, bool hasExpectedValue, string expectedStateKey, string validationFailedMessage=null)
				: base(key, isRequired, validationFailedMessage)
			{
				DefaultExpectedValue = expectedValue;
				HasExpectedValue = hasExpectedValue;
				ExpectedStateKey = expectedStateKey;
			}

			public override bool Validate(JToken actualValue, Dictionary<string, string> savedState, out string errorMessage)
			{
				if (IsActualValueNullOrEmpty(actualValue))
				{
					if (IsRequired)
					{
						errorMessage = GetErrorMessage("Required property missing");
						return false;
					}
					else
					{
						errorMessage = "";
						return true;
					}
				}

				// If the "ExpectedValue" and "ExpectedStateKey" attributes are non-empty on a Validator, then ExpectedStateKey will take precedence.
				// But if the mentioned "ExpectedStateKey" is invalid or doesn't have a saved state value, then the logic below will default to the value set in
				// "ExpectedValue" attribute of the Validator.
				T expectedValue = DefaultExpectedValue;
				bool hasExpectedStateValue = false;
				if (savedState != null && ExpectedStateKey != null && savedState.ContainsKey(ExpectedStateKey) && !string.IsNullOrEmpty(savedState[ExpectedStateKey]))
				{
					try
					{
						expectedValue = (T)Convert.ChangeType(savedState[ExpectedStateKey], typeof(T));
						hasExpectedStateValue = true;
					}
					catch (FormatException)
					{
						if (!HasExpectedValue)
						{
							errorMessage = GetErrorMessage($"ExpectedStateValue should be of type : {typeof(T).FullName}");
							return false;
						}
					}
				}

				if (!HasExpectedValue && !hasExpectedStateValue)
				{
					errorMessage = "";
					return true;
				}

				return Compare(actualValue, expectedValue, out errorMessage);
			}

			protected virtual bool Compare(JToken actualValue, T expectedValue, out string errorMessage)
			{
				string formattedActualValue;
				bool isValid = false;
				try
				{
					T typedActualValue = actualValue.Value<T>();
					formattedActualValue = FormatValue(typedActualValue);

					isValid = typedActualValue.Equals(expectedValue);
				}
				catch (FormatException)
				{
					formattedActualValue = actualValue.Value<string>();
					isValid = false;
				}

				errorMessage = GetErrorMessage($"Expected: '{FormattedExpectedValue}', Actual: '{formattedActualValue}'");
				return isValid;
			}

			public T DefaultExpectedValue { get; private set; }

			public bool HasExpectedValue { get; private set; }

			public string ExpectedStateKey { get; private set; }

			public string FormattedExpectedValue { get { return FormatValue(DefaultExpectedValue); } }

			public abstract string FormatValue(T value);
		}

		public class JsonIntegerPropertyValidator : JsonPropertyEqualityValidator<int>
		{
			public JsonIntegerPropertyValidator(string key, bool isRequired, int expectedValue, bool hasExpectedValue, string expectedStateKey, string validationFailedMessage = null)
				: base(key, isRequired, expectedValue, hasExpectedValue, expectedStateKey, validationFailedMessage)
			{
			}

			public override string FormatValue(int value)
			{
				return value.ToString(CultureInfo.InvariantCulture);
			}
		}

		public class JsonLongPropertyValidator : JsonPropertyEqualityValidator<long>
		{
			public JsonLongPropertyValidator(string key, bool isRequired, long expectedValue, bool hasExpectedValue, string expectedStateKey, string validationFailedMessage = null)
				: base(key, isRequired, expectedValue, hasExpectedValue, expectedStateKey, validationFailedMessage)
			{
			}

			public override string FormatValue(long value)
			{
				return value.ToString(CultureInfo.InvariantCulture);
			}
		}

		public class JsonBooleanPropertyValidator : JsonPropertyEqualityValidator<bool>
		{
			public JsonBooleanPropertyValidator(string key, bool isRequired, bool expectedValue, bool hasExpectedValue, string expectedStateKey, string validationFailedMessage = null)
				: base(key, isRequired, expectedValue, hasExpectedValue, expectedStateKey, validationFailedMessage)
			{
			}

			public override string FormatValue(bool value)
			{
				return value.ToString(CultureInfo.InvariantCulture);
			}
		}

		public class JsonStringPropertyValidator : JsonPropertyEqualityValidator<string>
		{
			private readonly string _endsWithValue;

			public JsonStringPropertyValidator(string key, bool isRequired, string expectedValue, bool hasExpectedValue, string endsWithValue, string expectedStateKey, string validationFailedMessage = null)
				: base(key, isRequired, expectedValue, hasExpectedValue, expectedStateKey, validationFailedMessage)
			{
				_endsWithValue = endsWithValue;
			}

			public override string FormatValue(string value)
			{
				return value;
			}

			public override bool Validate(JToken actualValue, Dictionary<string, string> savedState, out string errorMessage)
			{
				if (!base.Validate(actualValue, savedState, out errorMessage))
					return false;

				errorMessage = "";
				if (String.IsNullOrWhiteSpace(_endsWithValue))
					return true;

				string typedActualValue = actualValue.Value<string>();
				string formattedActualValue = FormatValue(typedActualValue);

				if (!formattedActualValue.EndsWith(_endsWithValue))
				{
					errorMessage = GetErrorMessage($"Expected to end with: '{_endsWithValue}', Actual: '{formattedActualValue}'");
					return false;
				}

				return true;
			}
		}

		public class JsonStringRegexPropertyValidator : JsonPropertyEqualityValidator<string>
		{
			private readonly Regex _regex;
			private readonly bool _shouldMatch;

			public JsonStringRegexPropertyValidator(string key, bool isRequired, string expectedValue, bool hasExpectedValue, string expectedStateKey, bool shouldMatch, string validationFailedMessage = null)
				: base(key, isRequired, expectedValue, hasExpectedValue, expectedStateKey, validationFailedMessage)
			{
				_regex = new Regex(expectedValue, RegexOptions.Compiled);
				_shouldMatch = shouldMatch;
			}

			public override string FormatValue(string value)
			{
				return value;
			}

			public override bool Validate(JToken actualValue, Dictionary<string, string> savedState, out string errorMessage)
			{
				errorMessage = "";

				if (actualValue == null && !IsRequired)
					return true;

				string typedActualValue = actualValue.Value<string>();
				string formattedActualValue = FormatValue(typedActualValue);

				bool isMatch = _regex.IsMatch(typedActualValue);

				if (_shouldMatch)
				{
					if (isMatch)
					{
						return true;
					}
					errorMessage = GetErrorMessage($"Value '{formattedActualValue}' doesn't match the expected regular expression '{_regex}'");
					return false;
				}
				else // _isMatchShouldBe == false
				{
					if (!isMatch)
					{
						errorMessage = "";
						return true;
					}

					errorMessage = GetErrorMessage($"Value '{formattedActualValue}' matched the regular expression, but should not '{_regex}'");
					return false;
				}
			}
		}

		public class JsonArrayPropertyValidator : JsonPropertyEqualityValidator<string>
		{
			public JsonArrayPropertyValidator(string key, bool isRequired, string containsValue, bool hasContainsValue, string expectedStateKey, string validationFailedMessage = null)
				: base(key, isRequired, containsValue, hasContainsValue, expectedStateKey, validationFailedMessage)
			{
			}

			protected override bool Compare(JToken actualArrayOfValues, string expectedValue, out string errorMessage)
			{
				string formattedActualValue;
				bool isValid = false;

				try
				{
					IList<string> typedActualValue = actualArrayOfValues.ToObject<List<string>>();
					formattedActualValue = typedActualValue.ToString();

					isValid = typedActualValue.Contains(expectedValue, StringComparer.OrdinalIgnoreCase);
				}
				catch (FormatException)
				{
					formattedActualValue = "";
					isValid = false;
				}

				errorMessage = GetErrorMessage($"Expected: '{FormattedExpectedValue}', Actual: '{formattedActualValue}'");
				return isValid;
			}

			public override string FormatValue(string value)
			{
				return value;
			}
		}
	}
}
