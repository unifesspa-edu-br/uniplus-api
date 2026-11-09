namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// As operações que mexem no perfil, nos pedidos e nas concessões seguem a mesma ordem de travas (o perfil primeiro),
/// então duas transações concorrentes se resolvem pela que trava antes. Cada teste segura uma transação aberta com
/// transações reais, espera o banco registrar a outra bloqueada e só então confirma.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CorridasDaTrocaDeVersaoNoHostTests : PedidosDeConcessaoTestBase
{
    private const string Analisar = "teste:atendimento:analisar";
    private const string Laudos = "teste:laudos:consultar";
    private const string RotaDasConcessoes = "/api/autorizacao/concessoes";

    public CorridasDaTrocaDeVersaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A aprovação que espera a troca sem acréscimo encontra o pedido na versão nova e segue")]
    public async Task AprovacaoEsperaATroca_SemAcrescimo_SegueNaVersaoNova()
    {
        (Guid perfil, Guid pedido) = await PedidoDoGestorAsync(Analisar, Laudos);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        await using TransacaoRetidaDeTeste troca = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await TrocarComposicaoNoEscopoAsync(troca.Servicos, perfil, [Analisar]);

        Task<HttpResponseMessage> aprovacao = EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes");
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        aprovacao.IsCompleted.Should().BeFalse();
        await troca.ConfirmarAsync();

        using HttpResponseMessage resposta = await aprovacao;
        JsonElement corpo = await CorpoAsync(resposta, HttpStatusCode.OK);
        corpo.GetProperty("situacao").GetString().Should().Be("aguardando_protecao_de_dados");
        corpo.GetProperty("versao").GetInt32().Should().Be(2);
    }

    [Fact(DisplayName = "A aprovação que espera a troca com acréscimo encontra o pedido devolvido, recusa e não concede")]
    public async Task AprovacaoEsperaATroca_ComAcrescimo_EncontraODevolvido()
    {
        (Guid perfil, Guid pedido) = await PedidoDoGestorAsync(Analisar);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        await using TransacaoRetidaDeTeste troca = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await TrocarComposicaoNoEscopoAsync(troca.Servicos, perfil, [Analisar, Laudos]);

        Task<HttpResponseMessage> aprovacao = EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes");
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        await troca.ConfirmarAsync();

        await ConferirRecusaAsync(await aprovacao, HttpStatusCode.UnprocessableEntity, "situacao_invalida");
        (await SituacaoAsync(diogo, pedido)).Should().Be("devolvido");
        (await ConcessoesAsync(Leandro, perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A troca espera a decisão que já travou o perfil e depois leva o pedido para a versão nova")]
    public async Task TrocaEsperaADecisao_PedidoPassaParaAVersaoNova()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Análise de atendimento", Analisar, Laudos);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid pedido = await CriarAsync(betina, perfil, unidade, null);
        await using TransacaoRetidaDeTeste decisao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await decisao.Servicos.GetRequiredService<IConcessaoRepository>().ObterParaConcederAsync(perfil);

        Task<IReadOnlyList<EnviarEmail>> troca = TrocarComposicaoAsync(perfil, Analisar);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        troca.IsCompleted.Should().BeFalse();
        await decisao.ConfirmarAsync();
        await troca;

        (await SituacaoAsync(diogo, pedido)).Should().Be("aguardando_gestor");
        (await LerAsync(diogo, $"{Rota}/{pedido}")).GetProperty("versao").GetInt32().Should().Be(2);
    }

    [Fact(DisplayName = "A criação do pedido que espera a troca nasce na versão nova")]
    public async Task CriacaoEsperaATroca_NasceNaVersaoNova()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Análise de atendimento", Analisar);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        await using TransacaoRetidaDeTeste troca = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await TrocarComposicaoNoEscopoAsync(troca.Servicos, perfil, [Analisar, Laudos]);

        Task<HttpResponseMessage> criacao = CriarBrutoAsync(betina, perfil, unidade, null);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        criacao.IsCompleted.Should().BeFalse();
        await troca.ConfirmarAsync();

        using HttpResponseMessage resposta = await criacao;
        JsonElement corpo = await CorpoAsync(resposta, HttpStatusCode.Created);
        corpo.GetProperty("versao").GetInt32().Should().Be(2);
        corpo.GetProperty("trocasDeVersao").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "A revogação que vence a trava da concessão deixa a troca de fora dela: a revogada não migra")]
    public async Task RevogacaoVenceATrava_ConcessaoRevogadaNaoMigra()
    {
        (Guid perfil, Guid concessao) = await PerfilComConcessaoAsync();
        await using TransacaoRetidaDeTeste revogacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await RevogarNaTransacaoAsync(revogacao.Servicos, concessao);

        Task troca = TrocarComposicaoAsync(perfil, Analisar);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        troca.IsCompleted.Should().BeFalse();
        await revogacao.ConfirmarAsync();
        await troca;

        Concessao gravada = await LerConcessaoAsync(concessao);
        gravada.RevogadaEm.Should().NotBeNull();
        gravada.VersaoId.Should().Be(await VersaoDoPerfilAsync(perfil, 1));
    }

    [Fact(DisplayName = "A troca que vence a trava migra a concessão, e a revogação que esperava a encontra na versão nova, sem deadlock")]
    public async Task TrocaVenceATrava_RevogacaoEncontraAConcessaoMigrada()
    {
        (Guid perfil, Guid concessao) = await PerfilComConcessaoAsync();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, Administrar);
        await using TransacaoRetidaDeTeste troca = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await TrocarComposicaoNoEscopoAsync(troca.Servicos, perfil, [Analisar]);

        Task<HttpResponseMessage> revogacao = EnviarAsync(
            diogo,
            HttpMethod.Post,
            $"{RotaDasConcessoes}/{concessao}/revogacao",
            new { motivo = "Revogação concorrente com a troca de versão.", confirmacaoEmail = ConcessoesDeTeste.EmailDeTeste });
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        await troca.ConfirmarAsync();

        using HttpResponseMessage resposta = await revogacao;
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        Concessao gravada = await LerConcessaoAsync(concessao);
        gravada.RevogadaEm.Should().NotBeNull();
        gravada.VersaoId.Should().Be(await VersaoDoPerfilAsync(perfil, 2));
    }

    private async Task<(Guid Perfil, Guid Pedido)> PedidoDoGestorAsync(params string[] permissoes)
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Análise de atendimento", permissoes);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        Guid pedido = await CriarAsync(betina, perfil, unidade, null);

        return (perfil, pedido);
    }

    private async Task<(Guid Perfil, Guid Concessao)> PerfilComConcessaoAsync()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Análise de atendimento", Analisar, Laudos);
        Guid concessao = await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), perfil, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));

        return (perfil, concessao);
    }

    private static async Task RevogarNaTransacaoAsync(IServiceProvider servicos, Guid concessaoId)
    {
        Concessao concessao = (await servicos.GetRequiredService<IConcessaoRepository>().ObterParaAlterarAsync(concessaoId))!;
        concessao.Revogar(IdentidadeDe(Diogo), "Revogação concorrente com a troca de versão.", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        await servicos.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    private async Task<Concessao> LerConcessaoAsync(Guid concessaoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes
            .AsNoTracking().SingleAsync(c => c.Id == concessaoId);
    }

    private async Task<Guid> VersaoDoPerfilAsync(Guid perfilId, int numero)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().VersoesDosPerfis
            .Where(v => v.PerfilId == perfilId && v.Numero == numero).Select(v => v.Id).SingleAsync();
    }
}
