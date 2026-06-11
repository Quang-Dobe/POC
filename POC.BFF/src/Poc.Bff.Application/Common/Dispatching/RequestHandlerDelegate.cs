namespace Poc.Bff.Application.Common.Dispatching;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
