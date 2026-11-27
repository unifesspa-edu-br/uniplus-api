namespace Unifesspa.UniPlus.Infrastructure.Core.CorpoDaRequisicao;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Liga o limite de tamanho do corpo da requisição.</summary>
public static class LimiteDoCorpoDaRequisicaoConfiguration
{
    /// <summary>Lê <see cref="LimiteDoCorpoDaRequisicaoOptions"/> da configuração; valor que não é positivo derruba a subida.</summary>
    public static IServiceCollection AddLimiteDoCorpoDaRequisicao(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LimiteDoCorpoDaRequisicaoOptions>()
            .Bind(configuration.GetSection(LimiteDoCorpoDaRequisicaoOptions.SectionName))
            .Validate(
                static opcoes => opcoes.TamanhoMaximoEmBytes > 0,
                "LimiteDoCorpoDaRequisicaoOptions.TamanhoMaximoEmBytes deve ser positivo.")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Aplica o limite ao pipeline. Vai depois do CORS, para a recusa levar os cabeçalhos de origem, e antes da autenticação, para
    /// valer a quem chama sem login ou sem permissão.
    /// </summary>
    public static IApplicationBuilder UseLimiteDoCorpoDaRequisicao(this IApplicationBuilder app) =>
        app.UseMiddleware<LimiteDoCorpoDaRequisicaoMiddleware>();
}
