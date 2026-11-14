namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

using Wolverine.Tracking;

/// <summary>
/// Duas transações disputam a vaga do mesmo objeto: a segunda passa nas conferências, porque a primeira ainda não
/// confirmou, e só a gravação enxerga o conflito. A porta traduz a violação de unicidade em recusa nomeada, e nada da segunda
/// transação fica no banco: nem a Seleção, nem o ato, nem o envelope do evento.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConflitoDeVagaEmCorridaNoHostTests : IClassFixture<HostComHandlersDeTransacao>
{
    private readonly IServiceProvider _servicos;

    public ConflitoDeVagaEmCorridaNoHostTests(MonolitoPostgresFixture fixture, HostComHandlersDeTransacao host)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(host);
        _servicos = host.Servicos(fixture);
    }

    [Fact(DisplayName = "A segunda transação na vaga recebe a tradução do conflito da gravação, e nada dela fica no banco")]
    public async Task SegundaTransacao_RecebeAARecusaNomeada_ESemResiduo()
    {
        string tipo = CodigoDeTesteDaTransacao.Novo("TIPO_CORRIDA");
        await CatalogoDeAtosDeTeste.SemearTipoAsync(_servicos, tipo, unicoPorObjeto: true);
        Guid objeto = Guid.CreateVersion7();
        RegistrarAtoPelaPorta primeira = NovoRegistro(objeto, tipo, "PRIMEIRA");
        RegistrarAtoPelaPorta segunda = NovoRegistro(objeto, tipo, "SEGUNDA");

        ExecucaoDeComandoNoHost execucao = await ExecutarDisputandoAVagaAsync(primeira, segunda);

        execucao.Resultado!.Error!.Code.Should().Be(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo);
        execucao.Sessao!.Sent.MessagesOf<GravacaoEmDoisModulosConcluida>().Should().BeEmpty();
        await ExigirSoAPrimeiraAsync(primeira, segunda);
    }

    /// <summary>A primeira transação segura a vaga sem confirmar; a segunda chega, espera o índice único e só prossegue quando a primeira confirma.</summary>
    private async Task<ExecucaoDeComandoNoHost> ExecutarDisputandoAVagaAsync(RegistrarAtoPelaPorta primeira, RegistrarAtoPelaPorta segunda)
    {
        await using TransacaoRetidaDeTeste retida = await TransacaoRetidaDeTeste.AbrirAsync(_servicos);
        await RegistrarSemConfirmarAsync(retida, primeira);
        Task<ExecucaoDeComandoNoHost> corrida = Task.Run(() => ExecucaoDeComandoNoHost.ExecutarAsync(_servicos, segunda));
        await retida.AguardarBloqueioAsync();
        await retida.ConfirmarAsync();

        return await corrida;
    }

    private async Task ExigirSoAPrimeiraAsync(RegistrarAtoPelaPorta primeira, RegistrarAtoPelaPorta segunda)
    {
        (await ConsultasDaTransacaoNoBanco.AtosAsync(_servicos, primeira.AtoId)).Should().Be(1, "a primeira confirmou");
        (await ConsultasDaTransacaoNoBanco.AtosAsync(_servicos, segunda.AtoId)).Should().Be(0);
        (await ConsultasDaTransacaoNoBanco.MotivosAsync(_servicos, segunda.Codigo)).Should().Be(0);
        (await ConsultasDaTransacaoNoBanco.EnvelopesDoEventoAsync(_servicos, segunda.Codigo)).Should().Be(0);
        (await ConsultasDaTransacaoNoBanco.VagasDoObjetoAsync(_servicos, primeira.ObjetoId)).Should().Be(1, "só a vaga da primeira existe");
    }

    private static RegistrarAtoPelaPorta NovoRegistro(Guid objeto, string tipo, string prefixo) =>
        new(CodigoDeTesteDaTransacao.Novo(prefixo), Guid.CreateVersion7(), objeto, tipo, UnicoPorObjeto: true, DesfechoDaGravacao.Sucesso);

    private static async Task RegistrarSemConfirmarAsync(TransacaoRetidaDeTeste retida, RegistrarAtoPelaPorta comando)
    {
        Result registrado = await retida.Servicos.GetRequiredService<IRegistroDeAtoNormativo>()
            .RegistrarAsync(RegistrarAtoPelaPortaHandler.AtoDe(comando), CancellationToken.None);
        registrado.IsSuccess.Should().BeTrue(registrado.Error?.Message);
        await retida.Servicos.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync(CancellationToken.None);
    }
}
