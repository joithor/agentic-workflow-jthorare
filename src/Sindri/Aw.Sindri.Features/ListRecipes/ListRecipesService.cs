using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Features.ListRecipes;

internal sealed class ListRecipesService(IRecipeCatalog catalog) : IService<ListRecipesRequest, ListRecipesResponse>
{
    public async ValueTask<Result<ListRecipesResponse>> Handle(ListRecipesRequest request, CancellationToken cancellationToken) =>
        (await catalog.List(cancellationToken).ConfigureAwait(false)).Map(list => new ListRecipesResponse(list));
}
