using MediatR;

namespace NEXT.Application.Features.Currencies;

public sealed record GetExchangeRateQuery : IRequest<ExchangeRateDto>;
