using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

public class JwksController : ApiController
{

    [HttpGet]
    [Route(nameof(GetPublicKeyAsync))]
    public async Task<ActionResult<JsonWebKey>> GetPublicKeyAsync([FromQuery] GetPublicKeyCommand command) => await Send(command);
}
