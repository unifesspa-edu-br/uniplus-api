namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// As decisões de acesso que o ponto de decisão do host de teste registrou, recortadas pelo que cada teste confere: o
/// ator, os dados exibidos, a revelação de um campo e a abertura de um documento.
/// </summary>
internal sealed class DecisoesDeAcessoRegistradas(CascadingFixture fixture)
{
    private IReadOnlyList<RegistroDecisaoAcesso> Todas =>
        fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados;

    public IEnumerable<RegistroDecisaoAcesso> DoAtor(string persona) =>
        Todas.Where(r => r.AtorSubject == persona);

    public IReadOnlyList<RegistroDecisaoAcesso> DosDados(Guid processoId, PermissaoSelecao permissao) =>
        [.. Todas.Where(r => r.ProcessoId == processoId && r.Permissao == permissao.Codigo && r.ResourceId is not null)];

    public IReadOnlyList<RegistroDecisaoAcesso> DaRevelacao(CenarioDeAcessoAosDados cenario, Guid inscricaoId) =>
        [.. Todas.Where(r => r.ProcessoId == cenario.Processo.Id
            && r.Permissao == PermissaoSelecao.DadosIdentificadoresRevelar.Codigo
            && r.InscricaoId == inscricaoId)];

    public IReadOnlyList<RegistroDecisaoAcesso> DoDocumento(CenarioDeAcessoAosDados cenario) =>
        [.. Todas.Where(r => r.ProcessoId == cenario.Processo.Id && r.ResourceTipo == ResourceTiposSelecao.DocumentoDaInscricao)];
}
