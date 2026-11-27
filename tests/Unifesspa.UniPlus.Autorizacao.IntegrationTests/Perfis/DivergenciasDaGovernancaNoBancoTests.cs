namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// As divergências da governança gravadas e lidas pelo repositório real: cada tipo devolve só o par de valores que lhe cabe,
/// com a classificação convertida do código do banco e o escopo como valor lógico, e os outros pares nulos.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class DivergenciasDaGovernancaNoBancoTests : PerfilEndpointTestBase, IAsyncLifetime
{
    private const string Codigo = "configuracao:cursos:manter";

    private readonly CenarioDaReclassificacao _cenario;

    public DivergenciasDaGovernancaNoBancoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
        _cenario = new CenarioDaReclassificacao(Fixture);
    }

    public Task InitializeAsync() => _cenario.ConciliarAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A divergência de classificação volta do banco com o par de classificações e sem o par de escopos")]
    public async Task Classificacao_VoltaComOParDeClassificacoes()
    {
        MudancaDaClassificacao mudanca = new(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Sensivel);

        DivergenciaDaGovernanca lida = await GravarELerAsync(
            (implantacao, perfil) => DivergenciaDaGovernanca.DaClassificacao(implantacao, perfil, Codigo, mudanca, DateTimeOffset.UtcNow, 0));

        lida.Tipo.Should().Be(TipoDeDivergencia.Classificacao);
        lida.MudancaDaClassificacao.Should().Be(mudanca);
        lida.MudancaDoEscopo.Should().BeNull();
    }

    [Theory(DisplayName = "A divergência de escopo volta do banco com o par lógico e sem o par de classificações")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Escopo_VoltaComOParLogico(bool deUnidadeAnterior, bool deUnidadeNovo)
    {
        MudancaDoEscopo mudanca = new(deUnidadeAnterior, deUnidadeNovo);

        DivergenciaDaGovernanca lida = await GravarELerAsync(
            (implantacao, perfil) => DivergenciaDaGovernanca.DoEscopo(implantacao, perfil, Codigo, mudanca, DateTimeOffset.UtcNow, 0));

        lida.Tipo.Should().Be(TipoDeDivergencia.Escopo);
        lida.MudancaDoEscopo.Should().Be(mudanca);
        lida.MudancaDaClassificacao.Should().BeNull();
    }

    [Theory(DisplayName = "A divergência de composição volta do banco sem nenhum dos pares de valores")]
    [InlineData(TipoDeDivergencia.ComposicaoFaltante)]
    [InlineData(TipoDeDivergencia.ComposicaoExcedente)]
    public async Task Composicao_VoltaSemValores(TipoDeDivergencia tipo)
    {
        DivergenciaDaGovernanca lida = await GravarELerAsync(
            (implantacao, perfil) => DivergenciaDaGovernanca.DaComposicao(implantacao, perfil, Codigo, tipo, DateTimeOffset.UtcNow, 0));

        lida.Tipo.Should().Be(tipo);
        lida.MudancaDaClassificacao.Should().BeNull();
        lida.MudancaDoEscopo.Should().BeNull();
    }

    private async Task<DivergenciaDaGovernanca> GravarELerAsync(Func<Guid, Guid, DivergenciaDaGovernanca> criar)
    {
        Guid id = await GravarAsync(criar);

        await using AsyncServiceScope leitura = Fixture.Factory.Services.CreateAsyncScope();
        IReadOnlyList<DivergenciaDaGovernanca> todas = await leitura.ServiceProvider
            .GetRequiredService<IReclassificacaoRepository>().ListarDivergenciasAsync();

        return todas.Should().ContainSingle(d => d.Id == id).Which;
    }

    private async Task<Guid> GravarAsync(Func<Guid, Guid, DivergenciaDaGovernanca> criar)
    {
        await using AsyncServiceScope escrita = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escrita.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid implantacao = await db.ImplantacoesDasPermissoes.OrderByDescending(c => c.Sequencia).Select(c => c.Id).FirstAsync();
        DivergenciaDaGovernanca divergencia = criar(implantacao, PerfisDeSistema.GestorDeAcessosId);
        db.Set<DivergenciaDaGovernanca>().Add(divergencia);
        await db.SaveChangesAsync();

        return divergencia.Id;
    }
}
