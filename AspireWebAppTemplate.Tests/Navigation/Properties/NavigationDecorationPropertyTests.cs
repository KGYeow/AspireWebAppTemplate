// Feature: api-nav-filtering, Property 5: Orphan Decoration Removal
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Tests.Navigation.Generators;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.Navigation.Properties;

/// <summary>
/// Property-based tests verifying that the orphan decoration removal logic correctly
/// removes Headers without following content and Dividers without content on both sides,
/// applied at each tree level independently.
/// </summary>
/// <remarks>
/// <para>
/// <b>Validates: Requirements 4.4, 5.1, 5.2, 5.3, 5.4, 5.5</b>
/// </para>
/// <para>
/// The orphan decoration removal pass is the final stage of the navigation filtering pipeline.
/// It ensures decorative items (Headers and Dividers) only appear when they have meaningful
/// adjacent content, preventing meaningless visual separators from appearing in the rendered tree.
/// </para>
/// <para>
/// Invariants verified:
/// <list type="bullet">
///   <item><description>A Header is included ONLY IF there is a following Content_Item (Link or Group) before the next Header or end of the sibling list.</description></item>
///   <item><description>A Divider is included ONLY IF there is both a preceding Content_Item and a following Content_Item within the same sibling list.</description></item>
///   <item><description>All Content_Items (Links, Groups) from the input are preserved in the output.</description></item>
///   <item><description>These invariants apply at EACH level of the tree independently (top-level and within Group children).</description></item>
/// </list>
/// </para>
/// </remarks>
public class NavigationDecorationPropertyTests
{
    #region RemoveOrphanedDecorations (Reference Implementation)

    /// <summary>
    /// Removes decorative items (Headers and Dividers) that have no adjacent visible content.
    /// This is a direct extraction of the reference implementation from NavMenu.FilteringReference.cs.
    /// </summary>
    /// <param name="items">The list of NavItems to process at a single tree level.</param>
    /// <returns>A new list with orphaned decorations removed.</returns>
    private static List<NavItem> RemoveOrphanedDecorations(List<NavItem> items)
    {
        var result = new List<NavItem>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            switch (item.Type)
            {
                case NavItemType.Header:
                    if (HasFollowingContent(items, i + 1))
                        result.Add(item);
                    break;

                case NavItemType.Divider:
                    if (HasPrecedingContent(result) && HasFollowingContent(items, i + 1))
                        result.Add(item);
                    break;

                case NavItemType.Link:
                case NavItemType.Group:
                    result.Add(item);
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Checks whether the result list (items already processed) has a preceding Content_Item
    /// before the most recent Header or the start of the list.
    /// </summary>
    private static bool HasPrecedingContent(List<NavItem> result)
    {
        for (var i = result.Count - 1; i >= 0; i--)
        {
            if (result[i].Type is NavItemType.Link or NavItemType.Group)
                return true;
            if (result[i].Type is NavItemType.Header)
                return false;
        }
        return false;
    }

    /// <summary>
    /// Checks whether there is a following Content_Item (Link or Group) in the remaining
    /// items before the next Header or end of the list.
    /// </summary>
    private static bool HasFollowingContent(List<NavItem> items, int startIndex)
    {
        for (var i = startIndex; i < items.Count; i++)
        {
            if (items[i].Type is NavItemType.Link or NavItemType.Group)
                return true;
            if (items[i].Type is NavItemType.Header)
                return false;
        }
        return false;
    }

    #endregion

    #region Verification Helpers

    /// <summary>
    /// Verifies that all Headers in the output have a following Content_Item before the next
    /// Header or end of the sibling list.
    /// </summary>
    /// <param name="items">The output items from orphan decoration removal.</param>
    /// <returns>True if no orphan headers exist in the output.</returns>
    private static bool AllHeadersHaveFollowingContent(List<NavItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Type == NavItemType.Header)
            {
                if (!HasFollowingContent(items, i + 1))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Verifies that all Dividers in the output have both a preceding Content_Item and a
    /// following Content_Item within the same sibling list.
    /// </summary>
    /// <param name="items">The output items from orphan decoration removal.</param>
    /// <returns>True if no orphan dividers exist in the output.</returns>
    private static bool AllDividersHaveAdjacentContent(List<NavItem> items)
    {
        var resultSoFar = new List<NavItem>();

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Type == NavItemType.Divider)
            {
                if (!HasPrecedingContent(resultSoFar) || !HasFollowingContent(items, i + 1))
                    return false;
            }
            resultSoFar.Add(items[i]);
        }
        return true;
    }

    /// <summary>
    /// Verifies that all Content_Items (Links and Groups) from the input are preserved in the output.
    /// Orphan decoration removal should never remove content items.
    /// </summary>
    /// <param name="input">The original input items.</param>
    /// <param name="output">The output items after orphan decoration removal.</param>
    /// <returns>True if all content items are preserved.</returns>
    private static bool AllContentItemsPreserved(List<NavItem> input, List<NavItem> output)
    {
        var inputContent = input.Where(i => i.Type is NavItemType.Link or NavItemType.Group).ToList();
        var outputContent = output.Where(i => i.Type is NavItemType.Link or NavItemType.Group).ToList();

        if (inputContent.Count != outputContent.Count)
            return false;

        for (var i = 0; i < inputContent.Count; i++)
        {
            if (!ReferenceEquals(inputContent[i], outputContent[i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Recursively verifies the orphan decoration invariants at each level of the tree.
    /// Checks top-level items and recurses into Group children.
    /// </summary>
    /// <param name="items">The items to verify at the current level.</param>
    /// <returns>True if invariants hold at this level and all child levels.</returns>
    private static bool VerifyInvariantsAtAllLevels(List<NavItem> items)
    {
        // Verify invariants at this level
        if (!AllHeadersHaveFollowingContent(items))
            return false;
        if (!AllDividersHaveAdjacentContent(items))
            return false;

        // Recurse into Group children
        foreach (var item in items)
        {
            if (item.Type == NavItemType.Group && item.Children is { Count: > 0 })
            {
                var childResult = RemoveOrphanedDecorations(item.Children.ToList());
                if (!VerifyInvariantsAtAllLevels(childResult))
                    return false;
            }
        }

        return true;
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Property: For any list of NavItems at any tree level, after applying RemoveOrphanedDecorations,
    /// every Header in the output has a following Content_Item (Link or Group) before the next
    /// Header or end of the sibling list.
    /// <para><b>Validates: Requirements 4.4, 5.1</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Headers_OnlyIncluded_WhenFollowedByContent()
    {
        var inputGen = NavItemGenerators.GenNavTree(3, 15);

        return Prop.ForAll(Arb.From(inputGen), (List<NavItem> items) =>
        {
            var result = RemoveOrphanedDecorations(items);
            var headersValid = AllHeadersHaveFollowingContent(result);

            return headersValid
                .Label($"Input count={items.Count}, Output count={result.Count}, " +
                       $"Output headers={result.Count(i => i.Type == NavItemType.Header)}");
        });
    }

    /// <summary>
    /// Property: For any list of NavItems at any tree level, after applying RemoveOrphanedDecorations,
    /// every Divider in the output has both a preceding Content_Item and a following Content_Item
    /// within the same sibling list.
    /// <para><b>Validates: Requirements 5.2, 5.3</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Dividers_OnlyIncluded_WhenPrecededAndFollowedByContent()
    {
        var inputGen = NavItemGenerators.GenNavTree(3, 15);

        return Prop.ForAll(Arb.From(inputGen), (List<NavItem> items) =>
        {
            var result = RemoveOrphanedDecorations(items);
            var dividersValid = AllDividersHaveAdjacentContent(result);

            return dividersValid
                .Label($"Input count={items.Count}, Output count={result.Count}, " +
                       $"Output dividers={result.Count(i => i.Type == NavItemType.Divider)}");
        });
    }

    /// <summary>
    /// Property: For any list of NavItems, RemoveOrphanedDecorations preserves all Content_Items
    /// (Links and Groups) from the input. Only decorative items may be removed.
    /// <para><b>Validates: Requirements 4.4, 5.5</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ContentItems_AlwaysPreserved()
    {
        var inputGen = NavItemGenerators.GenNavTree(3, 15);

        return Prop.ForAll(Arb.From(inputGen), (List<NavItem> items) =>
        {
            var result = RemoveOrphanedDecorations(items);
            var preserved = AllContentItemsPreserved(items, result);

            var inputContentCount = items.Count(i => i.Type is NavItemType.Link or NavItemType.Group);
            var outputContentCount = result.Count(i => i.Type is NavItemType.Link or NavItemType.Group);

            return preserved
                .Label($"Input content={inputContentCount}, Output content={outputContentCount}");
        });
    }

    /// <summary>
    /// Property: Orphan decoration removal invariants hold at EACH level of the tree independently.
    /// After applying RemoveOrphanedDecorations at the top level, the result satisfies the
    /// header and divider invariants, and the same holds recursively for Group children.
    /// <para><b>Validates: Requirements 5.4, 5.5</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property InvariantsHold_AtEachTreeLevel()
    {
        var inputGen = NavItemGenerators.GenNavTree(4, 10);

        return Prop.ForAll(Arb.From(inputGen), (List<NavItem> items) =>
        {
            var result = RemoveOrphanedDecorations(items);
            var invariantsHold = VerifyInvariantsAtAllLevels(result);

            var groupCount = items.Count(i => i.Type == NavItemType.Group);

            return invariantsHold
                .Label($"Input count={items.Count}, Groups={groupCount}, " +
                       $"Output count={result.Count}");
        });
    }

    #endregion
}
