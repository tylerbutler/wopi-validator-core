// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Office.WopiValidator.Core
{
	public interface IFilterOptions
	{
		string TestName { get; }
		TestCategory? TestCategory { get; }
		string TestGroup { get; }
	}

	public static class IFilterOptionsExtensions
	{
		/// <summary>
		/// Applies IFilterOptions to a collection of tests.
		///
		/// If the TestName filter option is provided, other options will be ignored.
		///
		/// The TestCategory filter behaves as follows:
		///
		///   All: includes all tests.
		///   WopiCore: includes ONLY the WopiCore tests.
		///   OfficeNativeClient: includes OfficeNativeClient AND WopiCore tests.
		///   OfficeOnline: includes OfficeOnline AND WopiCore tests.
		///
		/// If both TestGroup and TestCategory are provided, they will be logically ANDed.
		/// That is, the returned items will match both the category and group. However, note
		/// the TestCategory filter logic described above.
		/// </summary>
		/// <returns>A filtered collection.</returns>
		public static IEnumerable<TestExecutionData> ApplyFilters(this IEnumerable<TestExecutionData> testData, IFilterOptions options)
		{
			var toReturn = testData;

			// Filter by test name
			if (!string.IsNullOrEmpty(options.TestName))
			{
				toReturn = toReturn.Where(t => t.TestCase.Name == options.TestName);
				if (toReturn.Count() == 1)
				{
					return toReturn;
				}
			}

			// Filter by test category
			if (options.TestCategory != null)
			{
				toReturn = toReturn.Where(t => t.TestCategoryMatches(options.TestCategory));
			}

			// Filter by test group
			if (!string.IsNullOrEmpty(options.TestGroup))
			{
				toReturn = toReturn.Where(t => t.TestGroupName.Equals(options.TestGroup, StringComparison.InvariantCultureIgnoreCase));
			}

			return toReturn;
		}

		public static IEnumerable<TestExecutionData> ApplyToData(this IFilterOptions filters, IEnumerable<TestExecutionData> testData)
		{
			return testData.ApplyFilters(filters);
		}
	}
}
