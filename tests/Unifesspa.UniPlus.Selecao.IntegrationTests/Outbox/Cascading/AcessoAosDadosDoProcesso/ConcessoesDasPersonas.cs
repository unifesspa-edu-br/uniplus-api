namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Dá às personas a permissão do cenário do jeito que a produção dá, gravando a concessão de um perfil com escopo na
/// unidade informada, e tira as concessões que um cenário anterior deixou no banco compartilhado.
/// </summary>
internal sealed class ConcessoesDasPersonas(CascadingFixture fixture)
{
    public async Task RemoverDeTodasAsync()
    {
        foreach (string persona in PersonasDoAcesso.Todas)
        {
            await RemoverDaAsync(persona);
        }
    }

    public Task RemoverDaAsync(string persona) =>
        ConcessoesDeTeste.RemoverDaPessoaAsync(fixture.Factory.Services, persona);

    public async Task ConcederAsync(string persona, Guid unidade, params PermissaoSelecao[] permissoes)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(fixture.Factory.Services, [.. permissoes.Select(static p => p.Codigo)]);
        await ConcessoesDeTeste.ConcederAsync(fixture.Factory.Services, persona, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }
}
