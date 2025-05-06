using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;

public class RAGScannerController : ApiController
{
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route(nameof(ScanUrlAsync))]
    public async Task<ActionResult> ScanUrlAsync(ScanUrlCommand command)
        => await Send(command);

    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route(nameof(RAGSearchAsync))]
    public async Task<ActionResult<List<RAGSearchResult>>> RAGSearchAsync(RAGSearchCommand command)
        => await Send(command);
}
