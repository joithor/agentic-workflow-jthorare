namespace Aw.Sindri.Host.Bodies;

/// <summary>The body of POST …/unpark: budget granted on top.</summary>
internal sealed record BudgetBody(long Tokens = 0, int Turns = 0);
