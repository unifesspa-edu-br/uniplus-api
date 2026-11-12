namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

/// <summary>
/// O conflito de designações recusado nas duas ordens, sem concorrência: o Encarregado de dados vigente não pede para si o
/// papel de Representante do controlador, e o Representante vigente, gestor ou não, não aceita o pedido de Encarregado.
/// O gestor de acessos acumula com o papel de Representante (ADR-0162).
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PapeisIncompativeisEmSequenciaNoHostTests : PedidosDeConcessaoTestBase
{
    private static readonly Guid Gestor = PerfisDeSistema.GestorDeAcessosId;
    private static readonly Guid Representante = PerfisDeSistema.RepresentanteDoControladorId;
    private static readonly Guid Encarregado = PerfisDeSistema.EncarregadoDeDadosId;

    public PapeisIncompativeisEmSequenciaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O Encarregado de dados vigente que pede para si o papel de Representante é recusado na criação, com 409")]
    public async Task EncarregadoVigente_PedeRepresentanteParaSi_RecusadoNaCriacao()
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Leandro, Encarregado, EscopoDaConcessao.DaInstituicao());

        using HttpResponseMessage resposta = await CriarBrutoAsync(Cliente(Leandro), Representante, null, null);

        await ConferirRecusaAsync(resposta, HttpStatusCode.Conflict, "papeis_incompativeis");
    }

    [Theory(DisplayName = "O Representante vigente, gestor ou não, não aceita o pedido de Encarregado: 409 no aceite, e o pedido fica onde estava")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepresentanteVigente_AceitaEncarregado_RecusadoNoAceite(bool tambemGestor)
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Leandro, Representante, EscopoDaConcessao.DaInstituicao());
        await SemearSeAsync(tambemGestor, Leandro, Gestor);
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, Encarregado, null, Leandro);
        await AnexarVerificadoAsync(betina, id);
        string antes = await SituacaoAsync(betina, id);

        await ConferirRecusaAsync(await EnviarAsync(Cliente(Leandro), HttpMethod.Post, $"{Rota}/{id}/aceite"), HttpStatusCode.Conflict, "papeis_incompativeis");

        (await SituacaoAsync(betina, id)).Should().Be(antes);
    }

    [Fact(DisplayName = "O gestor de acessos designado Representante do controlador acumula os dois papéis")]
    public async Task Gestor_DesignadoRepresentante_Conclui()
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Marlene, Gestor, EscopoDaConcessao.DaInstituicao());
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await CriarAsync(Cliente(Betina), Representante, null, Marlene);
        await AnexarVerificadoAsync(Cliente(Betina), id);
        await EnviarAsync(Cliente(Marlene), HttpMethod.Post, $"{Rota}/{id}/aceite");

        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ConcessoesAsync(Marlene, Gestor)).Should().ContainSingle();
        (await ConcessoesAsync(Marlene, Representante)).Should().ContainSingle();
    }

    private Task SemearSeAsync(bool semear, Persona pessoa, Guid perfilId) =>
        semear ? SemearConcessaoAsync(pessoa, perfilId, EscopoDaConcessao.DaInstituicao()) : Task.CompletedTask;
}
