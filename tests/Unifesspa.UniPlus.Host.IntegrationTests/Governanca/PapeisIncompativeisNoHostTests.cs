namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

/// <summary>
/// A mesma pessoa não tem, ao mesmo tempo, concessões vigentes de Representante do controlador e de Encarregado de dados,
/// pelo host real e com transações concorrentes de verdade (ADR-0162). Leandro Viana é designado para os dois papéis ao
/// mesmo tempo, cada pedido aprovado na segunda etapa por um Representante diferente; a trava da pessoa serializa as duas
/// aprovações, e a que chega depois lê a concessão gravada pela outra.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PapeisIncompativeisNoHostTests : PedidosDeConcessaoTestBase
{
    private const string TravaDaGravacaoDeConcessoes = "LOCK TABLE autorizacao.concessoes IN SHARE ROW EXCLUSIVE MODE";
    private const string NaSegundaEtapa = "aguardando_protecao_de_dados";

    private static readonly Guid Representante = PerfisDeSistema.RepresentanteDoControladorId;
    private static readonly Guid Encarregado = PerfisDeSistema.EncarregadoDeDadosId;

    public PapeisIncompativeisNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Duas designações cruzadas e simultâneas da mesma pessoa, sem concessão prévia: só uma conclui e a outra recebe 409")]
    public async Task DesignacoesCruzadasSimultaneas_SemConcessaoPrevia_SoUmaConclui()
    {
        await ResetarAsync();
        Guid[] pedidos = await DesignacoesCruzadasNaSegundaEtapaAsync();

        HttpResponseMessage[] respostas = await AprovarSobrepostasAsync(pedidos);

        await ConferirUmaSoDesignacaoAsync(respostas);
    }

    public static TheoryData<Guid> PapeisVencidos => new() { Encarregado, Representante };

    [Theory(DisplayName = "Com a concessão vencida de um dos papéis, as designações cruzadas e simultâneas ainda concluem uma só")]
    [MemberData(nameof(PapeisVencidos))]
    public async Task DesignacoesCruzadasSimultaneas_ComConcessaoVencida_SoUmaConclui(Guid vencido)
    {
        await ResetarAsync();
        await SemearVencidaAsync(Leandro, vencido);
        Guid[] pedidos = await DesignacoesCruzadasNaSegundaEtapaAsync();

        HttpResponseMessage[] respostas = await AprovarSobrepostasAsync(pedidos);

        await ConferirUmaSoDesignacaoAsync(respostas);
    }

    // Os dois pedidos de Leandro, um para cada papel, aprovados pelo gestor e à espera da segunda etapa. Marlene e Isabella
    // são Representantes, e Leandro ainda não exerce papel nenhum.
    private async Task<Guid[]> DesignacoesCruzadasNaSegundaEtapaAsync()
    {
        await SemearConcessaoAsync(Marlene, Representante, EscopoDaConcessao.DaInstituicao());
        await SemearConcessaoAsync(Isabella, Representante, EscopoDaConcessao.DaInstituicao());
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid[] pedidos = [await DesignacaoAceitaAsync(Representante, Leandro), await DesignacaoAceitaAsync(Encarregado, Leandro)];
        foreach (Guid id in pedidos)
        {
            (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await SituacaoAsync(diogo, id)).Should().Be(NaSegundaEtapa);
        }

        return pedidos;
    }

    // As duas aprovações correm de fato sobrepostas: a transação retida trava a gravação de concessões e só é liberada
    // depois que o banco registra as duas em espera, uma na gravação e a outra atrás dela, na trava da pessoa. Sem essa
    // trava, as duas leriam que Leandro não tem o outro papel antes de qualquer uma gravar, e ambas concluiriam.
    private async Task<HttpResponseMessage[]> AprovarSobrepostasAsync(Guid[] pedidos)
    {
        Task<HttpResponseMessage>[] aprovacoes;
        await using (TransacaoRetidaDeTeste retida = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services))
        {
            await retida.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(TravaDaGravacaoDeConcessoes);
            aprovacoes = [EnviarAsync(Cliente(Marlene), HttpMethod.Post, $"{Rota}/{pedidos[0]}/aprovacoes"), EnviarAsync(Cliente(Isabella), HttpMethod.Post, $"{Rota}/{pedidos[1]}/aprovacoes")];
            await retida.AguardarBloqueioAsync(pedidos.Length);
            await retida.ConfirmarAsync();
        }

        return await Task.WhenAll(aprovacoes);
    }

    private async Task ConferirUmaSoDesignacaoAsync(HttpResponseMessage[] respostas)
    {
        respostas.Select(static r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        string recusa = await respostas.Single(static r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync();
        recusa.Should().Contain("uniplus.autorizacao.pedido.papeis_incompativeis");
        (await VigentesAsync(Leandro)).Should().ContainSingle("a pessoa não pode ter os dois papéis vigentes ao mesmo tempo");
    }

    private async Task<Concessao[]> VigentesAsync(Persona pessoa)
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        Concessao[] concessoes = [.. await ConcessoesAsync(pessoa, Representante), .. await ConcessoesAsync(pessoa, Encarregado)];
        return [.. concessoes.Where(c => c.EstaAtiva(agora))];
    }

    private async Task SemearVencidaAsync(Persona pessoa, Guid perfilId)
    {
        await SemearConcessaoAsync(pessoa, perfilId, EscopoDaConcessao.DaInstituicao());
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '3 days', valida_ate = now() - interval '1 minute' WHERE beneficiario_sub = {0} AND perfil_id = {1}",
            pessoa.Sub.ToString(),
            perfilId);
    }

    // O pedido de designação feito por Betina, com o documento verificado e aceito pelo beneficiário.
    private async Task<Guid> DesignacaoAceitaAsync(Guid perfilId, Persona beneficiario)
    {
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfilId, null, beneficiario);
        await AnexarVerificadoAsync(betina, id);
        (await AceitarAsync(Cliente(beneficiario), id)).StatusCode.Should().Be(HttpStatusCode.OK);

        return id;
    }
}
