namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// Token sem CPF não autentica: o CPF, que o provedor de identidade sempre entrega, é o que identifica a pessoa
/// para o ponto de decisão e para o titular dos dados.
/// </summary>
internal static class JwtBearerCpfEvents
{
    internal const string MotivoDaRecusa = "O token não traz o CPF da pessoa.";

    /// <summary>Encadeia em <see cref="JwtBearerEvents.OnTokenValidated"/> a recusa do token sem CPF.</summary>
    public static JwtBearerEvents ExigindoCpf(this JwtBearerEvents events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Func<TokenValidatedContext, Task> anterior = events.OnTokenValidated;
        events.OnTokenValidated = async context =>
        {
            await anterior(context).ConfigureAwait(false);

            if (context.Result is null && !TemCpf(context.Principal))
            {
                context.Fail(MotivoDaRecusa);
            }
        };

        return events;
    }

    private static bool TemCpf(ClaimsPrincipal? principal)
        => principal?.FindFirst(UniPlusClaims.Cpf) is { Value: var cpf } && !string.IsNullOrWhiteSpace(cpf);
}
