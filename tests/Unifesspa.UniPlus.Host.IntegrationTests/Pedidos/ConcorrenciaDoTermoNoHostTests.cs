namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// A vigência do termo entre a publicação e a criação de pedido protegido, com transações reais sobrepostas (ADR-0162): a
/// criação lê a vigente com trava compartilhada e a publicação toma a exclusiva. Cada teste segura uma transação aberta,
/// espera o banco registrar a outra bloqueada e só então confirma; sem a trava compartilhada da criação, os dois primeiros
/// testes falham, porque nada espera. O segundo passa pela criação real, pela rota, e prova que o fluxo de criação toma a
/// trava; o primeiro reproduz no banco os passos da criação para manter a transação dela aberta.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConcorrenciaDoTermoNoHostTests : TermoNoHostTestBase
{
    public ConcorrenciaDoTermoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A publicação espera a criação que leu a vigente: o pedido fixa a versão anterior e a nova entra depois")]
    public async Task PublicacaoEsperaACriacao_PedidoFixaAAnterior()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient diogo = await PublicadorAsync();
        await using TransacaoRetidaDeTeste criacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        PedidoDeConcessao pedido = await CriarNaTransacaoAsync(criacao.Servicos, perfil, EscopoDaConcessao.DaUnidade(unidade));

        Task<HttpResponseMessage> publicacao = PublicarAsync(diogo, "segunda versão", Vigente!.Id);
        await criacao.AguardarBloqueioAsync();
        publicacao.IsCompleted.Should().BeFalse();
        await criacao.ConfirmarAsync();

        (await CorpoAsync(await publicacao, HttpStatusCode.Created)).GetProperty("numero").GetInt32().Should().Be(2);
        (await TermoNoHostDeTeste.FixadaAsync(Fixture.Factory.Services, pedido.Id)).Should().Be(Vigente.Id);
    }

    [Fact(DisplayName = "A criação que chega durante a publicação espera e responde conflito, sem gravar o pedido")]
    public async Task CriacaoDuranteAPublicacao_RespondeConflito()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        await using TransacaoRetidaDeTeste publicacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await PublicarNaTransacaoAsync(publicacao.Servicos);

        Task<HttpResponseMessage> criacao = PedirAsync(betina, perfil, unidade, Vigente!.Id);
        await publicacao.AguardarBloqueioAsync();
        criacao.IsCompleted.Should().BeFalse();
        await publicacao.ConfirmarAsync();

        await ConferirAsync(await criacao, HttpStatusCode.Conflict, "pedido.termo_divergente");
        (await ContarPedidosAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "Duas publicações com a mesma vigente esperada geram uma só versão nova; a segunda recebe conflito")]
    public async Task DuasPublicacoes_UmaSoVersao()
    {
        using HttpClient diogo = await PublicadorAsync();
        await using TransacaoRetidaDeTeste primeira = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await PublicarNaTransacaoAsync(primeira.Servicos);

        Task<HttpResponseMessage> segunda = PublicarAsync(diogo, "concorrente", Vigente!.Id);
        await primeira.AguardarBloqueioAsync();
        await primeira.ConfirmarAsync();

        await ConferirAsync(await segunda, HttpStatusCode.Conflict, "termo.vigente_alterado");
        (await ContarVersoesAsync()).Should().Be(2);
    }

    [Fact(DisplayName = "A publicação repetida com a mesma chave devolve a resposta guardada, sem executar de novo")]
    public async Task Idempotencia_DaPublicacao_Reproduz()
    {
        using HttpClient diogo = await PublicadorAsync();
        string chave = Guid.NewGuid().ToString();
        JsonElement nova = await CorpoAsync(await PublicarAsync(diogo, "segunda versão", Vigente!.Id, chave), HttpStatusCode.Created);

        // Executada de novo, a publicação seria recusada: a vigente esperada já não é a vigente.
        JsonElement repetida = await CorpoAsync(await PublicarAsync(diogo, "segunda versão", Vigente.Id, chave), HttpStatusCode.Created);

        repetida.GetProperty("id").GetGuid().Should().Be(nova.GetProperty("id").GetGuid());
        (await ContarVersoesAsync()).Should().Be(2, "a repetição não gravou outra versão");
    }

    [Fact(DisplayName = "O conflito de divergência libera a chave, que não grava pedido ao repetir; a reapresentação usa chave nova e cria o pedido")]
    public async Task Idempotencia_DoConflito_LiberaAChave_EAReapresentacaoUsaChaveNova()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        string chave = Guid.NewGuid().ToString();
        Guid anterior = Vigente!.Id;
        await TermoNoHostDeTeste.PublicarAsync(Fixture.Factory.Services, "segunda versão");
        await ConferirAsync(await PedirAsync(betina, perfil, unidade, anterior, chave: chave), HttpStatusCode.Conflict, "pedido.termo_divergente");

        // O 409 não grava nada e por isso não fica guardado na chave (ADR-0027): sem versão vigente, a mesma chave executa
        // de novo e responde que não há termo publicado.
        await TermoNoHostDeTeste.ReiniciarAsync(Fixture.Factory.Services);
        await ConferirAsync(await PedirAsync(betina, perfil, unidade, anterior, chave: chave), HttpStatusCode.UnprocessableEntity, "pedido.sem_termo_vigente");
        (await ContarPedidosAsync()).Should().Be(0);

        await ReapresentarComChaveNovaAsync(betina, perfil, unidade);
    }

    // A reapresentação é outro pedido de gravação: a versão nova, com chave nova, cria o pedido.
    private async Task ReapresentarComChaveNovaAsync(HttpClient betina, Guid perfil, Guid unidade)
    {
        VersaoDoTermo terceira = await TermoNoHostDeTeste.PublicarAsync(Fixture.Factory.Services, "terceira versão");
        (await PedirAsync(betina, perfil, unidade, terceira.Id)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await ContarPedidosAsync()).Should().Be(1);
    }

    // O efeito da criação de pedido protegido no banco, na ordem dela: o perfil sob trava compartilhada, a vigência do termo
    // sob trava compartilhada e a inserção do pedido, sem confirmar.
    private static async Task<PedidoDeConcessao> CriarNaTransacaoAsync(IServiceProvider servicos, Guid perfilId, EscopoDaConcessao escopo)
    {
        Perfil perfil = (await servicos.GetRequiredService<IConcessaoRepository>().ObterParaConcederAsync(perfilId))!;
        VersaoDoTermo? vigente = await servicos.GetRequiredService<ITermoDeResponsabilidadeRepository>().ObterVigenteSobTravaCompartilhadaAsync();
        PedidoDeConcessao pedido = ParaSiMesmo(servicos, perfil, escopo);
        pedido.FixarTermo(new TermoNaCriacao(vigente, vigente!.Id, true), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        await servicos.GetRequiredService<IPedidoDeConcessaoRepository>().AdicionarAsync(pedido);
        await servicos.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();

        return pedido;
    }

    private static PedidoDeConcessao ParaSiMesmo(IServiceProvider servicos, Perfil perfil, EscopoDaConcessao escopo) =>
        PedidoDeConcessao.Criar(
            new PartesDoPedido(PessoaDe(Betina), null),
            perfil,
            escopo,
            new DeclaracaoDoPedido(Justificativa),
            servicos.GetRequiredService<IDominiosInstitucionais>(),
            servicos.GetRequiredService<ICatalogoDePermissoesConsultavel>(),
            DateTimeOffset.UtcNow).Value!;

    // A publicação no banco, sob a trava exclusiva da vigência, sem confirmar.
    private static async Task PublicarNaTransacaoAsync(IServiceProvider servicos)
    {
        ITermoDeResponsabilidadeRepository termos = servicos.GetRequiredService<ITermoDeResponsabilidadeRepository>();
        (TermoDeResponsabilidade termo, VersaoDoTermo? vigente) = await termos.ObterParaPublicarAsync();
        VersaoDoTermo nova = termo.Publicar(
            ConteudoDoTermo.Criar(TermoNoHostDeTeste.Texto("publicada na transação retida"), TermoNoHostDeTeste.Ato).Value!,
            new Identidade(TestAuthHandler.DefaultIssuer, Diogo.Sub.ToString()), vigente, vigente?.Id, DateTimeOffset.UtcNow).Value!;
        await termos.AdicionarVersaoAsync(nova);
        await servicos.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();
    }

    private static PessoaNoToken PessoaDe(Persona persona) =>
        new(new Identidade(TestAuthHandler.DefaultIssuer, persona.Sub.ToString(), persona.Nome), persona.Nome, persona.Email, true);
}
