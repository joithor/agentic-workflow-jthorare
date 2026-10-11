using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.ListWorkflows;

internal sealed class ListWorkflowsService(IWorkflowStore store) : IService<ListWorkflowsRequest, ListWorkflowsResponse>
{
    public async ValueTask<Result<ListWorkflowsResponse>> Handle(ListWorkflowsRequest request, CancellationToken cancellationToken) =>
        (await store.List(new InstanceQuery(request.All), cancellationToken).ConfigureAwait(false)).Map(list => new ListWorkflowsResponse(list));
}
