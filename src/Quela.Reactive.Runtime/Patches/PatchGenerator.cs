using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.Core.Patches;
using Quela.Reactive.Runtime.State;

namespace Quela.Reactive.Runtime.Patches;

/// <summary>
/// Generates UI patches from state changes.
/// </summary>
public sealed class PatchGenerator
{
    private readonly PatchGeneratorOptions _options;

    public PatchGenerator(PatchGeneratorOptions? options = null)
    {
        _options = options ?? new PatchGeneratorOptions();
    }

    /// <summary>
    /// Generates patches for all changed nodes.
    /// </summary>
    public IReadOnlyList<UIPatch> GeneratePatches(
        SessionState sessionState,
        IReadOnlySet<NodeId> changedNodes)
    {
        var patches = new List<UIPatch>();

        foreach (var nodeId in changedNodes)
        {
            var node = sessionState.Graph.GetNode(nodeId);
            var nodeState = sessionState.GetNodeState(nodeId);

            if (node == null) continue;

            var nodePatches = GenerateNodePatches(node, nodeState);
            patches.AddRange(nodePatches);
        }

        // Sort by priority
        return patches
            .OrderByDescending(p => p.Priority)
            .ToList();
    }

    private IEnumerable<UIPatch> GenerateNodePatches(INode node, NodeState nodeState)
    {
        var targetSelector = node.Metadata.TargetSelector;

        // Skip nodes without UI binding
        if (string.IsNullOrEmpty(targetSelector))
            yield break;

        // Generate value patch
        if (nodeState.Value != null || _options.EmitNullValues)
        {
            yield return UIPatch.SetValue(targetSelector, nodeState.Value);
        }

        // Generate validation patches
        var validationResult = nodeState.ValidationResult;

        if (validationResult.IsPending)
        {
            yield return UIPatch.SetValid(targetSelector, true);
            yield return UIPatch.AddClass(targetSelector, _options.ValidatingClass);
        }
        else if (validationResult.IsValid)
        {
            yield return UIPatch.SetValid(targetSelector, true);
            yield return UIPatch.RemoveClass(targetSelector, _options.InvalidClass);
            yield return UIPatch.RemoveClass(targetSelector, _options.ValidatingClass);
            yield return UIPatch.AddClass(targetSelector, _options.ValidClass);

            // Clear error message
            var errorSelector = $"{targetSelector}-error";
            yield return UIPatch.SetVisible(errorSelector, false);
        }
        else
        {
            var errorMessage = validationResult.Errors.FirstOrDefault()?.Message ?? "Validation failed";

            yield return UIPatch.SetValid(targetSelector, false, errorMessage);
            yield return UIPatch.RemoveClass(targetSelector, _options.ValidClass);
            yield return UIPatch.RemoveClass(targetSelector, _options.ValidatingClass);
            yield return UIPatch.AddClass(targetSelector, _options.InvalidClass);

            // Show error message
            var errorSelector = $"{targetSelector}-error";
            yield return UIPatch.SetText(errorSelector, errorMessage);
            yield return UIPatch.SetVisible(errorSelector, true);
        }

        // Generate visibility patches based on node type
        if (node.Type == NodeType.Conditional)
        {
            // Conditional nodes might affect visibility of other elements
        }
    }

    /// <summary>
    /// Generates a patch for partial content insertion.
    /// </summary>
    public UIPatch GeneratePartialPatch(
        string targetSelector,
        string html,
        InsertPosition position = InsertPosition.Replace)
    {
        return UIPatch.InsertPartial(targetSelector, html, position);
    }

    /// <summary>
    /// Generates patches for a form reset.
    /// </summary>
    public IReadOnlyList<UIPatch> GenerateResetPatches(SessionState sessionState)
    {
        var patches = new List<UIPatch>();

        foreach (var (nodeId, node) in sessionState.Graph.Nodes)
        {
            var targetSelector = node.Metadata.TargetSelector;
            if (string.IsNullOrEmpty(targetSelector)) continue;

            // Reset to default value
            var defaultValue = node switch
            {
                IStatefulNode statefulNode => statefulNode.GetValue(),
                _ => null
            };

            patches.Add(UIPatch.SetValue(targetSelector, defaultValue));
            patches.Add(UIPatch.RemoveClass(targetSelector, _options.InvalidClass));
            patches.Add(UIPatch.RemoveClass(targetSelector, _options.ValidClass));
            patches.Add(UIPatch.RemoveClass(targetSelector, _options.ValidatingClass));

            // Hide error messages
            var errorSelector = $"{targetSelector}-error";
            patches.Add(UIPatch.SetVisible(errorSelector, false));
        }

        return patches;
    }

    /// <summary>
    /// Generates patches for enabling/disabling form submission.
    /// </summary>
    public IReadOnlyList<UIPatch> GenerateSubmitStatePatches(
        string submitButtonSelector,
        bool canSubmit,
        string? loadingText = null)
    {
        var patches = new List<UIPatch>
        {
            UIPatch.SetEnabled(submitButtonSelector, canSubmit)
        };

        if (!canSubmit && loadingText != null)
        {
            patches.Add(UIPatch.SetText(submitButtonSelector, loadingText));
            patches.Add(UIPatch.AddClass(submitButtonSelector, _options.LoadingClass));
        }
        else
        {
            patches.Add(UIPatch.RemoveClass(submitButtonSelector, _options.LoadingClass));
        }

        return patches;
    }
}

/// <summary>
/// Options for the patch generator.
/// </summary>
public sealed record PatchGeneratorOptions
{
    public bool EmitNullValues { get; init; } = false;
    public string ValidClass { get; init; } = "is-valid";
    public string InvalidClass { get; init; } = "is-invalid";
    public string ValidatingClass { get; init; } = "is-validating";
    public string LoadingClass { get; init; } = "is-loading";
}
