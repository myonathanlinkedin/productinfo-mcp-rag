using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public class IdentityController : ApiController
{
    [HttpPost]
    [Route(nameof(RegisterAsync))]
    public async Task<ActionResult> RegisterAsync(
        RegisterUserCommand command)
        => await Send(command);

    [HttpPost]
    [Route(nameof(LoginAsync))]
    public async Task<ActionResult<UserResponseModel>> LoginAsync(
        LoginUserCommand command)
        => await Send(command);

    [HttpPut]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route(nameof(ChangePasswordAsync))]
    public async Task<ActionResult> ChangePasswordAsync(
        ChangePasswordCommand command)
        => await Send(command);

    [HttpPost]
    [Route(nameof(ResetPasswordAsync))]
    public async Task<ActionResult> ResetPasswordAsync(
        ResetPasswordCommand command)
        => await Send(command);
}
