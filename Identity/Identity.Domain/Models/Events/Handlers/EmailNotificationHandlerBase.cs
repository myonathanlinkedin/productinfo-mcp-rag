﻿using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

public abstract class EmailNotificationHandlerBase<TEvent> : IEventHandler<TEvent>
        where TEvent : IDomainEvent
{
    private readonly IEmailSender emailSenderService;
    private readonly IMCPServerRequester mcpServerRequester;
    private readonly ILogger logger;

    protected EmailNotificationHandlerBase(
        IEmailSender emailSenderService,
        IMCPServerRequester mcpServerRequester,
        ILogger logger)
    {
        this.emailSenderService = emailSenderService;
        this.mcpServerRequester = mcpServerRequester;
        this.logger = logger;
    }

    public async Task Handle(TEvent domainEvent)
    {
        var (email, password, subject, prompt) = GetEmailData(domainEvent);

        logger.LogInformation("Requesting email body generation for: {Email}", email);
        var result = await mcpServerRequester.RequestAsync(prompt, ChatRole.User, false);

        if (!result.Succeeded)
        {
            logger.LogError("Failed to generate email body for: {Email}. Reason: {Errors}", email, result.Errors);
            return;
        }

        var body = result.Data.Replace("[EMAIL]", email);
        if (!string.IsNullOrEmpty(password))
            body = body.Replace("[PASSWORD]", password).Replace("[NEW_PASSWORD]", password);

        var fullHtml = $"""
                        <html>
                            <body>
                                <p>{body}</p>
                                <footer><p>{GetFooter()}</p></footer>
                            </body>
                        </html>
                        """;

        logger.LogInformation("Sending email to {Email}", email);
        await emailSenderService.SendEmailAsync(email, subject, fullHtml);
        logger.LogInformation("Email successfully sent to {Email}", email);
    }

    protected abstract (string Email, string Password, string Subject, string Prompt) GetEmailData(TEvent domainEvent);
    protected abstract string GetFooter();
}