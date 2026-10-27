namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-AGENT-001..003 (#1269): a state's agent is approved for Steps, a 'differentFrom' names an earlier agent state, and the two forms are not mixed. Stub (red): no findings yet.</summary>
internal sealed class AgentRules : IRecipeRules
{
    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return [];
    }
}
