using Notifier.Messages.Application.Abstractions;
using NotifierAPI.Services;

using AppSendResult = Notifier.Messages.Application.Abstractions.SendResult;

namespace Notifier.Messages.Infrastructure.Esendex;

public sealed class EsendexSmsSenderAdapter : ISmsSender
{
    private readonly ISendService _sendService;

    public EsendexSmsSenderAdapter(ISendService sendService)
    {
        _sendService = sendService;
    }

    public async Task<AppSendResult> SendAsync(string toE164, string message, string? accountRef, CancellationToken ct)
    {
        var result = await _sendService.SendAsync(toE164, message, accountRef, ct);
        return new AppSendResult(result.Id, result.SubmittedUtc);
    }
}
