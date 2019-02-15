// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Office.WopiValidator.Core;

namespace Microsoft.Office.WopiValidator.UnitTests
{
	internal class FilterOptionsMock : IFilterOptions
	{
		public string TestName { get; set; }

		public TestCategory? TestCategory { get; set; }

		public string TestGroup { get; set; }
	}
}
