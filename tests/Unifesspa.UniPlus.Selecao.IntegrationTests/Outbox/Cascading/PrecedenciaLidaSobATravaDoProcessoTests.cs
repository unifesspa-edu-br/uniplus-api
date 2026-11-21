namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Monolito.Persistencia;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CorpoDoCronogramaComMarca;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.FaseDoCronogramaPublicavelDeTeste;

/// <summary>
/// A publicação lê o cadastro de precedência entre fases depois de obter a trava do processo, e não antes. A prova usa duas
/// conexões e nenhuma espera por tempo: uma sessão retém a linha do processo, a publicação chega e fica esperando, a relação é
/// cadastrada e confirmada enquanto ela espera, e só então a trava é solta. Lida antes da trava, a publicação não veria a relação.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class PrecedenciaLidaSobATravaDoProcessoTests(CascadingFixture fixture)
{
    [Fact(DisplayName = "A relação de precedência confirmada enquanto a publicação espera a trava do processo é vista por ela e a recusa")]
    public async Task RelacaoConfirmadaDuranteAEspera_ARecusaVemDaPublicacao()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Precedência lida sob a trava", 1, Criar("AVALIACAO_BIOPSICOSSOCIAL", 2, 3, noMesmoMomentoDaAnterior: true));
        await using SessaoQueRetemOProcesso retida = await SessaoQueRetemOProcesso.AbrirAsync(fixture.ConnectionString, processo.ProcessoId);
        Task<HttpResponseMessage> publicacao = Task.Run(processo.PublicarAsync);
        await retida.AguardarOperacaoEsperandoAsync();
        publicacao.IsCompleted.Should().BeFalse("a publicação espera a trava do processo");
        await using PrecedenciaCadastradaNoTeste relacao = await PrecedenciaCadastradaNoTeste.CadastrarAsync(
            fixture.Factory.Services, "RESULTADO_FINAL", "AVALIACAO_BIOPSICOSSOCIAL");

        await retida.LiberarAsync();

        using HttpResponseMessage resposta = await publicacao;
        (await ErrosDaRecusaAsync(resposta)).Should().Contain(e => e.Codigo == "uniplus.selecao.processo_seletivo.fases_com_precedencia_no_mesmo_momento");
        (await processo.VersoesPublicadasAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "Sem relação nova durante a espera, a mesma publicação segue depois da trava e é aceita")]
    public async Task SemRelacaoDuranteAEspera_PublicacaoAceitaDepoisDaTrava()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Precedência lida sob a trava, sem relação", 1, Criar("AVALIACAO_BIOPSICOSSOCIAL", 2, 3, noMesmoMomentoDaAnterior: true));
        await using SessaoQueRetemOProcesso retida = await SessaoQueRetemOProcesso.AbrirAsync(fixture.ConnectionString, processo.ProcessoId);
        Task<HttpResponseMessage> publicacao = Task.Run(processo.PublicarAsync);
        await retida.AguardarOperacaoEsperandoAsync();

        await retida.LiberarAsync();

        using HttpResponseMessage resposta = await publicacao;
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        (await processo.VersoesPublicadasAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "A trava do processo continua retida enquanto a publicação lê o cadastro de precedência: outra conexão não a obtém")]
    public async Task DuranteALeituraDaPrecedencia_OutraConexaoNaoObtemATravaDoProcesso()
    {
        PausaNaLeituraDaPrecedencia pausa = new();
        await using WebApplicationFactory<HostAssemblyMarker> host = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
            servicos.ConfigureDbContext<MonolitoDbContext>(opcoes => opcoes.AddInterceptors(pausa))));
        using HttpClient cliente = host.CreateClient();
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(fixture, "Trava retida na leitura", 1, Criar("ENSALAMENTO", 2, 3));
        Task<HttpResponseMessage> publicacao = Task.Run(() => processo.PublicarPeloClienteAsync(cliente));
        await pausa.AguardarALeituraAsync();

        Func<Task> obterATrava = () => TentarTravarSemEsperarAsync(processo.ProcessoId);

        (await obterATrava.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.LockNotAvailable);
        pausa.Liberar();
        using HttpResponseMessage resposta = await publicacao;
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private async Task TentarTravarSemEsperarAsync(Guid processoId)
    {
        await using NpgsqlConnection conexao = new(fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new("SELECT 1 FROM selecao.processos_seletivos WHERE id = @processo FOR UPDATE NOWAIT", conexao);
        comando.Parameters.AddWithValue("processo", processoId);
        await comando.ExecuteNonQueryAsync();
    }
}
