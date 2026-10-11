using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Features.ValidateRecipe;

internal sealed class ValidateRecipeService(IRecipeCatalog catalog) : IService<ValidateRecipeRequest, CatalogEntry>
{
    public ValueTask<Result<CatalogEntry>> Handle(ValidateRecipeRequest request, CancellationToken cancellationToken) =>
        catalog.Inspect(request.Path, cancellationToken);
}
