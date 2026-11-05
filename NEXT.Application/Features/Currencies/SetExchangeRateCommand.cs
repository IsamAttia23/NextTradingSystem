using MediatR;

namespace NEXT.Application.Features.Currencies;

public sealed record SetExchangeRateCommand(ExchangeRateInput Input) : IRequest<ExchangeRateDto>;
