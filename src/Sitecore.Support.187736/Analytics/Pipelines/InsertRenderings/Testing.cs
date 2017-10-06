using Sitecore.Analytics;
using Sitecore.Analytics.Data.Items;
using Sitecore.Analytics.Testing;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Layouts;
using Sitecore.StringExtensions;
using System;
using System.Collections.Generic;
using Sitecore.Pipelines.InsertRenderings;
using Sitecore.SecurityModel;
using Sitecore.Exceptions;

namespace Sitecore.Support.Analytics.Pipelines.InsertRenderings
{
  public class Testing : Sitecore.Analytics.Pipelines.InsertRenderings.Testing
  {
    protected override void Evaluate([NotNull] InsertRenderingsArgs args, [NotNull] Item contextItem)
    {
      Debug.ArgumentNotNull(args, "args");
      Debug.ArgumentNotNull(contextItem, "contextItem");

      using (new SecurityDisabler())
      {
        var renderings = new List<RenderingReference>(args.Renderings);
        for (var i = renderings.Count - 1; i >= 0; i--)
        {
          var rendering = renderings[i];
          var testItemId = rendering.Settings.GetMultiVariateTestForLanguage(contextItem.Language);

          if (string.IsNullOrEmpty(testItemId))
          {
            continue;
          }

          var testItem = contextItem.Database.GetItem(testItemId, contextItem.Language);
          if (testItem == null || testItem.Versions.Count < 1)
          {
            continue;
          }

          var success = this.Test(renderings, rendering, contextItem, testItem);
          if (success)
          {
            args.TestingRenderingUniqueIds.Add(rendering.UniqueId);
          }
        }

        args.Renderings.Clear();
        args.Renderings.AddRange(renderings);
      }
    }

    private bool Test([NotNull] List<RenderingReference> renderings, [NotNull] RenderingReference rendering, [NotNull] Item contextItem, [NotNull] Item variableItem)
    {
      Debug.ArgumentNotNull(renderings, "renderings");
      Debug.ArgumentNotNull(rendering, "rendering");
      Debug.ArgumentNotNull(contextItem, "contextItem");
      Debug.ArgumentNotNull(variableItem, "variableItem");

      var combination = this.GetTestCombination(variableItem);      

      if (combination == null)
      {
        return false;
      }

      var variation = (MultivariateTestValueItem)variableItem.Children[combination[variableItem.ID.ToGuid()].ToString()];


      if (variation == null)
      {
        return false;
      }

      var testRunner = new ComponentTestRunner();
      var textContext = new ComponentTestContext(variation, rendering, renderings);
      try
      {
        testRunner.Run(textContext);
      }
      catch (Exception exc)
      {
        var id = rendering.RenderingID ?? ID.Null;
        Log.Warn("Failed to execute MV testing on component with id \"{0}\". Item ID:\"{1}\"".FormatWith(id, contextItem.ID), exc, this);
      }

      return true;
    }

    [CanBeNull]
    private TestCombination GetTestCombination([NotNull] Item variableItem)
    {
      Debug.ArgumentNotNull(variableItem, "variableItem");

      var testItem = variableItem.Parent;
      TestDefinitionItem testDefinitionItem = null;
      if (!TestDefinitionItem.TryParse(testItem, ref testDefinitionItem))
      {
        return null;
      }

      if (!testDefinitionItem.IsRunning)
      {
        return null;
      }

      if (!Tracker.CurrentPage.IsTestSetIdNull() && Tracker.CurrentPage.TestSetId == testDefinitionItem.ID.Guid)
      { // If a test is defined and it is my test, return existing combination.
        return Tracker.CurrentPage.GetTestCombination();
      }

      var testSet = TestManager.GetTestSet(testDefinitionItem);

      var strategy = TestManager.GetTestStrategy(testDefinitionItem);

      var combination = strategy.GetTestCombination(testSet);

      if (Tracker.CurrentPage.IsTestSetIdNull())
      { // If the page does not have a test defined yet
        combination.SaveTo(Tracker.CurrentPage);
      }

      return combination;
    }    
  }
}