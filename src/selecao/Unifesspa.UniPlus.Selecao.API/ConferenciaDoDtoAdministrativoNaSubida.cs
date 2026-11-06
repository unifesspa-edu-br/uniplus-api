namespace Unifesspa.UniPlus.Selecao.API;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Confere, quando a aplicação sobe, que o DTO administrativo da Seleção não classifica uma propriedade lida direto da
/// coluna abaixo da classificação do modelo, e que o valor derivado declara a transformação (ADR-0153). Construir o
/// modelo não conecta ao banco. A violação aborta a subida: a aplicação não serve uma resposta que classifica o dado
/// abaixo de onde ele está guardado.
/// </summary>
internal sealed class ConferenciaDoDtoAdministrativoNaSubida : IHostedService
{
    private readonly IServiceScopeFactory _escopos;

    public ConferenciaDoDtoAdministrativoNaSubida(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ExigirDtoConforme();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ExigirDtoConforme()
    {
        using IServiceScope escopo = _escopos.CreateScope();
        SelecaoDbContext contexto = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        IReadOnlyList<string> violacoes = ConferenciaDoDtoClassificado.Conferir(
            typeof(InscricaoAdministrativaDto).Assembly.GetExportedTypes(),
            ConferenciaDaClassificacaoDoModelo.Classificacoes(contexto.Model));

        if (violacoes.Count > 0)
        {
            throw new InvalidOperationException(
                "O DTO administrativo da Seleção classifica o dado abaixo do modelo: " + string.Join(" ", violacoes));
        }
    }
}
