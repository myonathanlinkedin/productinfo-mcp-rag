using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public class PromptController : ApiController
{

    [HttpPost]
    [Authorize]
    [Route(nameof(SendUserPromptAsync))]
    public async Task<ActionResult<string>> SendUserPromptAsync([FromBody] UserPromptCommand command) => await Send(command);
}

