namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

/// <summary>
/// Semeia o cenário de acesso aos dados do processo: publica o processo e grava as inscrições na versão que a
/// publicação congelou. Cada chamada cria um processo novo, de nome único, para que os testes não se vejam.
/// </summary>
internal sealed class SemeadorDoCenarioDeAcesso(CascadingFixture fixture)
{
    private readonly MontagemDoProcesso _processo = new(fixture, new RequisicoesAutenticadas(fixture));
    private readonly MontagemDasInscricoes _inscricoes = new(fixture);

    public async Task<CenarioDeAcessoAosDados> SemearAsync(string nome) =>
        await _inscricoes.SemearAsync(await _processo.PublicarAsync(nome));
}
