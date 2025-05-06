using MediatR;
using System.Security.Principal;

public class UserPromptCommand : IRequest<Result<string>>
{
    public string Prompt { get; }

    public UserPromptCommand(string prompt) => Prompt = prompt;

    public class UserPromptCommandHandler : IRequestHandler<UserPromptCommand, Result<string>>
    {
        private readonly IMCPServerRequester mCPServerRequester;

        public UserPromptCommandHandler(IMCPServerRequester mCPServerRequester)
        {
            this.mCPServerRequester = mCPServerRequester;
        }

        public async Task<Result<string>> Handle(UserPromptCommand request, CancellationToken cancellationToken)
        {
            return await this.mCPServerRequester.RequestAsync(request.Prompt);
        }
    }
}