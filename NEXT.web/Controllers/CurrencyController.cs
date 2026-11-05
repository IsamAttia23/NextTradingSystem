using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Features.Currencies;

namespace NEXT.web.Controllers;

[ApiController]
[Route("Currency")]
[Authorize(Policy = "ManageSecurity")]
public sealed class CurrencyController(ISender sender) : Controller
{
    [HttpGet("Settings")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Settings() => View("Index");

    [HttpGet]
    public Task<ExchangeRateDto> Get(CancellationToken ct) =>
        sender.Send(new GetExchangeRateQuery(), ct);

    [HttpPost]
    public Task<ExchangeRateDto> Set([FromForm] ExchangeRateInput input, CancellationToken ct) =>
        sender.Send(new SetExchangeRateCommand(input), ct);
}
