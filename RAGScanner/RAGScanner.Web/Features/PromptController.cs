using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public class PromptController : ApiController
{

    [HttpPost]
    [Authorize(Policy = "Prompt", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route(nameof(SendUserPromptAsync))]
    public async Task<ActionResult<string>> SendUserPromptAsync([FromBody] UserPromptCommand command) => await Send(command);
}

