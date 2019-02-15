// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Office.WopiValidator.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Office.WopiValidator.UnitTests
{
	// TODO: create a test case definition file for tests rather than using TestCases.xml
	[TestClass]
	public class FilterTests
	{
		private static readonly IEnumerable<TestExecutionData> AllTests = ConfigParser.ParseExecutionData("TestCases.xml");

		[TestMethod]
		public void Filter_ByName_Matches()
		{
			IFilterOptions inputFilter = new FilterOptionsMock() { TestName = "PutRelativeFile.SuggestedExtension" };

			var filteredTests = AllTests.ApplyFilters(inputFilter).ToList();
			Assert.AreEqual(1, filteredTests.Count);
			Assert.AreEqual("PutRelativeFile.SuggestedExtension", filteredTests[0].TestCase.Name);
		}

		[TestMethod]
		public void Filter_ByName_WithOtherOptions()
		{
			IFilterOptions inputFilter = new FilterOptionsMock()
			{
				TestName = "PutRelativeFile.SuggestedExtension",
				TestCategory = TestCategory.OfficeNativeClient,
				TestGroup = "Locks"
			};

			var filteredTests = AllTests.ApplyFilters(inputFilter).ToList();
			Assert.AreEqual(1, filteredTests.Count);
			Assert.AreEqual("PutRelativeFile.SuggestedExtension", filteredTests[0].TestCase.Name);
		}

		[TestMethod]
		public void Filter_ByName_NoMatch()
		{
			IFilterOptions inputFilter = new FilterOptionsMock() { TestName = "FakeTest" };

			var filteredTests = AllTests.ApplyFilters(inputFilter).ToList();
			Assert.AreEqual(0, filteredTests.Count);
		}

		[TestMethod]
		public void Filter_ByCategory_Matches()
		{
			IFilterOptions inputFilter = new FilterOptionsMock() { TestCategory = TestCategory.WopiCore };

			var filteredTests = AllTests.ApplyFilters(inputFilter).ToList();
			filteredTests.ForEach(x => Assert.AreEqual(TestCategory.WopiCore, x.TestCase.TestCategory));
		}

		[TestMethod]
		public void Filter_ByTestGroup_Matches()
		{
			IFilterOptions inputFilter = new FilterOptionsMock() { TestGroup = "Locks" };

			var filteredTests = AllTests.ApplyFilters(inputFilter).ToList();
			filteredTests.ForEach(x => Assert.AreEqual("Locks", x.TestGroupName));
		}

		[TestMethod]
		public void Filter_ByTestGroup_NoMatch()
		{
			IFilterOptions inputFilter = new FilterOptionsMock() { TestGroup = "FakeGroup" };

			var filteredTests = AllTests.ApplyFilters(inputFilter).ToList();
			Assert.AreEqual(0, filteredTests.Count);
		}

		[TestMethod]
		public void Filter_ByCategoryAndGroup_Matches()
		{
			IFilterOptions inputFilter = new FilterOptionsMock()
			{
				TestCategory = TestCategory.OfficeNativeClient,
				TestGroup = "PutRelativeFile"
			};

			var filteredTests = AllTests.ApplyFilters(inputFilter).ToList();
			foreach (var test in filteredTests)
			{
				bool testGroupMatch = test.TestGroupName == "PutRelativeFile";
				bool categoryMatches = test.TestCase.TestCategory != TestCategory.OfficeOnline;

				Console.WriteLine(test.TestCase.TestCategory.ToString());
				Assert.IsTrue(testGroupMatch && categoryMatches);
			}
		}

		[TestMethod]
		public void Filter_ByCategoryAndGroup_NoMatch()
		{
			IFilterOptions inputFilter = new FilterOptionsMock()
			{
				TestCategory = TestCategory.OfficeNativeClient,
				TestGroup = "FakeGroup"
			};

			var filteredTests = AllTests.ApplyFilters(inputFilter).ToList();
			Assert.AreEqual(0, filteredTests.Count);
		}
	}
}
