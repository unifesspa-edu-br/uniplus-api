namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

/// <summary>
/// A designação de Representante do controlador e a segunda etapa dos pedidos de quem já é Representante, pelo host real
/// (ADR-0162): só a designação do primeiro Representante dispensa a segunda etapa, sob a trava única do papel; havendo
/// Representante vigente, outro Representante decide, e ninguém decide o próprio pedido.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PedidosDoRepresentanteTests : PedidosDeConcessaoTestBase
{
    private const string SituacaoAprovado = "aprovado";
    private const string SituacaoNaSegundaEtapa = "aguardando_protecao_de_dados";
    private const string TipoDaDispensaNaTrilha = "segunda_aprovacao_dispensada";
    private const string TravaDaGravacaoDeConcessoes = "LOCK TABLE autorizacao.concessoes IN SHARE ROW EXCLUSIVE MODE";

    private static readonly Guid Representante = PerfisDeSistema.RepresentanteDoControladorId;

    public PedidosDoRepresentanteTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A designação do primeiro Representante exige o documento e passa só pelo gestor, com a dispensa na trilha")]
    public async Task PrimeiroRepresentante_PassaSoPeloGestor()
    {
        await ResetarAsync();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await DesignacaoAceitaAsync(Marlene);

        JsonElement aprovado = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        aprovado.GetProperty("situacao").GetString().Should().Be("aprovado");
        aprovado.GetProperty("admitePrazo").GetBoolean().Should().BeFalse();
        (await ConcessoesAsync(Marlene, Representante)).Should().ContainSingle();
        (await DetalheDaDispensaAsync(diogo, id)).Should().Contain("designacao_de_protecao_de_dados");
    }

    [Fact(DisplayName = "Com Representante vigente, a designação de outro vai à segunda etapa e é decidida por outro Representante")]
    public async Task SegundoRepresentante_DecididoPeloPrimeiro()
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Marlene, Representante, EscopoDaConcessao.DaInstituicao());
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await DesignacaoAceitaAsync(Leandro);

        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SituacaoAsync(diogo, id)).Should().Be("aguardando_protecao_de_dados");
        (await EnviarAsync(Cliente(Marlene), HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ConcessoesAsync(Leandro, Representante)).Should().ContainSingle();
        (await TiposDaTrilhaAsync(diogo, id)).Should().NotContain("segunda_aprovacao_dispensada");
    }

    [Fact(DisplayName = "O pedido sensível de quem é Representante vai à segunda etapa, que ele não decide e outro Representante decide")]
    public async Task BeneficiarioRepresentante_DecididoPorOutroRepresentante()
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Marlene, Representante, EscopoDaConcessao.DaInstituicao());
        await SemearConcessaoAsync(Isabella, Representante, EscopoDaConcessao.DaInstituicao());
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid id = await CriarAsync(Cliente(Betina), perfil, await SemearUnidadeAsync(), Marlene);
        await EnviarAsync(Cliente(Marlene), HttpMethod.Post, $"{Rota}/{id}/aceite");

        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SituacaoAsync(diogo, id)).Should().Be("aguardando_protecao_de_dados");
        await ConferirRecusaAsync(await EnviarAsync(Cliente(Marlene), HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "decisor_e_parte");
        (await EnviarAsync(Cliente(Isabella), HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConcessoesAsync(Marlene, perfil)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Duas designações simultâneas sem Representante vigente não aplicam ambas a dispensa: só uma conclui no gestor")]
    public async Task DesignacoesSimultaneas_SoUmaDispensa()
    {
        await ResetarAsync();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid[] pedidos = [await DesignacaoAceitaAsync(Leandro), await DesignacaoAceitaAsync(Marlene)];

        await AprovarSobrepostasAsync(diogo, pedidos);

        string[] situacoes = [await SituacaoAsync(diogo, pedidos[0]), await SituacaoAsync(diogo, pedidos[1])];
        situacoes.Should().BeEquivalentTo([SituacaoAprovado, SituacaoNaSegundaEtapa]);
        (await DispensasAsync(diogo, pedidos)).Should().Be(1);
    }

    // As duas aprovações correm de fato sobrepostas: a transação retida trava a gravação de concessões, e só é liberada
    // depois que o banco registra as duas aprovações em espera. Sem a trava do papel, as duas leriam que não há
    // Representante antes de qualquer uma gravar, e ambas aplicariam a dispensa.
    private async Task AprovarSobrepostasAsync(HttpClient diogo, Guid[] pedidos)
    {
        Task<HttpResponseMessage>[] aprovacoes;
        await using (TransacaoRetidaDeTeste retida = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services))
        {
            await retida.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(TravaDaGravacaoDeConcessoes);
            aprovacoes = [.. pedidos.Select(id => EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"))];
            await retida.AguardarBloqueioAsync(pedidos.Length);
            await retida.ConfirmarAsync();
        }

        (await Task.WhenAll(aprovacoes)).Should().OnlyContain(static r => r.StatusCode == HttpStatusCode.OK);
    }

    private static async Task<int> DispensasAsync(HttpClient client, Guid[] pedidos)
    {
        int dispensas = 0;
        foreach (Guid id in pedidos)
        {
            dispensas += (await LerAsync(client, $"{Rota}/{id}/trilha")).EnumerateArray()
                .Count(static e => e.GetProperty("tipo").GetString() == TipoDaDispensaNaTrilha);
        }

        return dispensas;
    }

    // O pedido de designação de Representante feito por Betina, com o documento verificado e aceito pelo beneficiário.
    private async Task<Guid> DesignacaoAceitaAsync(Persona beneficiario)
    {
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, Representante, null, beneficiario);
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_documento");
        await AnexarVerificadoAsync(betina, id);
        await EnviarAsync(Cliente(beneficiario), HttpMethod.Post, $"{Rota}/{id}/aceite");

        return id;
    }

    private static async Task<string?> DetalheDaDispensaAsync(HttpClient client, Guid id) =>
        (await LerAsync(client, $"{Rota}/{id}/trilha")).EnumerateArray()
            .Single(static e => e.GetProperty("tipo").GetString() == TipoDaDispensaNaTrilha)
            .GetProperty("detalhe").GetString();
}
