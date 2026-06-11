using System.Reflection;

namespace Poc.Bff.Application.Common.Results;

public static class ResultFactory
{
    public static TResponse Failure<TResponse>(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var method = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
                .MakeGenericMethod(valueType);
            return (TResponse)method.Invoke(null, new object[] { error })!;
        }

        throw new InvalidOperationException(
            $"Request response type {typeof(TResponse).Name} must be Result or Result<T>.");
    }
}
