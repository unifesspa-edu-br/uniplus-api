namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O número da versão nos contratos de concessão e de pedido é sempre o da versão que a própria entidade segue, e não o
/// da vigente: a concessão presa à versão antiga e o pedido encerrado continuam dizendo a versão deles.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class VersaoNosContratosDoHostTests : PedidosDeConcessaoTestBase
{
    private const string Operar = "teste:inscricoes:operar";
    private const string Analisar = "teste:atendimento:analisar";
    private const string Laudos = "teste:laudos:consultar";
    private const string RotaDasConcessoes = "/api/autorizacao/concessoes";

    public VersaoNosContratosDoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A concessão presa à versão antiga diz a versão dela, no detalhe e na lista, e a concessão nova nasce na vigente")]
    public async Task Concessao_DizAVersaoDela()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Análise de atendimento", Analisar);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, Administrar);
        Guid antiga = await ConcessoesDeTeste.ConcederAsync(Fixture.Factory.Services, Leandro.Sub.ToString(), perfil, EscopoDaConcessao.DaUnidade(unidade));

        await TrocarComposicaoAsync(perfil, Analisar, Laudos);
        Guid nova = await ConcessoesDeTeste.ConcederAsync(Fixture.Factory.Services, Betina.Sub.ToString(), perfil, EscopoDaConcessao.DaUnidade(unidade));

        (await LerAsync(diogo, $"{RotaDasConcessoes}/{antiga}")).GetProperty("versao").GetInt32().Should().Be(1);
        (await LerAsync(diogo, $"{RotaDasConcessoes}/{nova}")).GetProperty("versao").GetInt32().Should().Be(2);
        JsonElement lista = await LerAsync(diogo, $"{RotaDasConcessoes}?perfilId={perfil}");
        JsonElement itens = lista.ValueKind == JsonValueKind.Array ? lista : lista.GetProperty("items");
        itens.EnumerateArray().Where(i => i.GetProperty("perfil").GetProperty("id").GetGuid() == perfil)
            .ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("versao").GetInt32())
            .Should().BeEquivalentTo(new Dictionary<Guid, int> { [antiga] = 1, [nova] = 2 });
    }

    [Fact(DisplayName = "O pedido aprovado na versão 1 diz a versão 1 depois que a 2 entra em vigor, e a concessão que ele gerou também")]
    public async Task PedidoAprovado_DizAVersaoQueSeguiu()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Operação de inscrições", Operar);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional, Administrar);
        Guid pedido = await CriarAsync(betina, perfil, unidade, null);
        JsonElement aprovado = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes"), HttpStatusCode.OK);
        Guid concessao = aprovado.GetProperty("concessaoId").GetGuid();

        await TrocarComposicaoAsync(perfil, Operar, Laudos);

        JsonElement lido = await LerAsync(diogo, $"{Rota}/{pedido}");
        lido.GetProperty("situacao").GetString().Should().Be("aprovado");
        lido.GetProperty("versao").GetInt32().Should().Be(1);
        lido.GetProperty("trocasDeVersao").GetArrayLength().Should().Be(0);
        (await LerAsync(diogo, $"{RotaDasConcessoes}/{concessao}")).GetProperty("versao").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "O pedido de perfil com versão pendente segue a vigente e é aprovado nela")]
    public async Task PedidoComVersaoPendente_SegueAVigente()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Operação de inscrições", Operar);
        await CriarVersaoPendenteAsync(perfil, Operar, Laudos);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        Guid pedido = await CriarAsync(betina, perfil, unidade, null);

        JsonElement criado = await LerAsync(betina, $"{Rota}/{pedido}");
        criado.GetProperty("versao").GetInt32().Should().Be(1);
        JsonElement aprovado = await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes"), HttpStatusCode.OK);
        aprovado.GetProperty("situacao").GetString().Should().Be("aprovado");
        aprovado.GetProperty("versao").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "A concessão de governança não tem versão")]
    public async Task Governanca_SemVersao()
    {
        await ResetarAsync();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, Administrar);
        Guid concessao = await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Leandro.Sub.ToString(), TipoDeGovernanca.ProtecaoDeDados);

        JsonElement lida = await LerAsync(diogo, $"{RotaDasConcessoes}/{concessao}");

        lida.GetProperty("versao").ValueKind.Should().Be(JsonValueKind.Null);
        lida.GetProperty("perfil").GetProperty("id").GetGuid().Should().Be(PerfisDeSistema.ProtecaoDeDadosId);
    }

    [Fact(DisplayName = "A lista de pedidos diz a versão que cada pedido segue: a antiga do devolvido, a vigente do novo e nenhuma na governança")]
    public async Task ListaDePedidos_DizAVersaoDeCada()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilAsync("Operação de inscrições", Operar);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        Guid devolvido = await CriarAsync(betina, perfil, unidade, null);
        await TrocarComposicaoAsync(perfil, Operar, Laudos);
        Guid novo = await CriarAsync(betina, perfil, unidade, null);
        Guid governanca = await CriarAsync(betina, PerfisDeSistema.GestorDeAcessosId, null, Leandro);

        JsonElement lista = await LerAsync(betina, $"{Rota}?fila=meus");

        JsonElement itens = lista.ValueKind == JsonValueKind.Array ? lista : lista.GetProperty("items");
        Dictionary<Guid, int?> versoes = itens.EnumerateArray().ToDictionary(
            i => i.GetProperty("id").GetGuid(),
            i => i.GetProperty("versao").ValueKind == JsonValueKind.Null ? (int?)null : i.GetProperty("versao").GetInt32());
        versoes[devolvido].Should().Be(1);
        versoes[novo].Should().Be(2);
        versoes[governanca].Should().BeNull();
    }

    private async Task CriarVersaoPendenteAsync(Guid perfilId, params string[] permissoes)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        ICatalogoDePermissoesConsultavel catalogo = scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>();
        Perfil perfil = (await scope.ServiceProvider.GetRequiredService<IPerfilRepository>().ObterParaEditarAsync(perfilId))!;
        PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(perfil,
            PerfisDeTeste.Conteudo(perfil.Nome, perfil.Descricao, permissoes, catalogo),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.AutoriaDoAutor,
            PerfisDeTeste.Instante,
            catalogo));
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }
}
