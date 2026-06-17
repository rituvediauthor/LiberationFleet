using LiberationFleet.Server.Application.Features.Recipients.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Recipients.Queries.GetReceptionOrder;

public record GetReceptionOrderQuery(int Limit = 30) : IRequest<ReceptionOrderResponse>;
