namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>
/// O mínimo de integrantes da banca é o maior entre o quórum e os corretores da regra de consolidação que a etapa
/// referencia no catálogo (UNI-REQ-0387). A publicação, a retificação de ato único e o fechamento da sessão congelam
/// pelo mesmo serviço, e as três leem o número do catálogo.
/// </summary>
public sealed class MinimoDeIntegrantesNoCongelamentoTests
{
    private static void AfirmarRecusaPeloMinimo(Result resposta, IEnumerable<object> eventos, int minimo)
    {
        resposta.Error!.Code.Should().Be(BancaErrorCodes.IntegrantesAbaixoDoMinimo);
        resposta.Error.Message.Should().Contain($"o mínimo exigido é {minimo}");
        eventos.Should().BeEmpty();
    }

    [Theory(DisplayName = "Publicar recusa a banca com menos integrantes do que os corretores da correção independente e aceita a que os alcança")]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Publicar_BancaDaEtapa_ExigeOsCorretoresDaRegra(int integrantes, bool aceita)
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaEtapa(integrantes);

        (Result resposta, IEnumerable<object> eventos) = await ProcessoComCorrecaoIndependente.Servicos(processo).PublicarAsync(processo.Id);

        resposta.IsSuccess.Should().Be(aceita, resposta.Error?.Message);
        if (!aceita)
        {
            AfirmarRecusaPeloMinimo(resposta, eventos, minimo: 3);
        }
    }

    [Theory(DisplayName = "Retificar recusa a banca com menos integrantes do que os corretores da correção independente e aceita a que os alcança")]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Retificar_BancaDaEtapa_ExigeOsCorretoresDaRegra(int integrantes, bool aceita)
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versao) = ProcessoComCorrecaoIndependente.PublicadoComBancaRedeclarada(integrantes);

        (Result resposta, IEnumerable<object> eventos) = await ProcessoComCorrecaoIndependente.Servicos(processo)
            .ComVersaoVigente(processo, versao)
            .RetificarAsync(processo.Id);

        resposta.IsSuccess.Should().Be(aceita, resposta.Error?.Message);
        if (!aceita)
        {
            AfirmarRecusaPeloMinimo(resposta, eventos, minimo: 3);
        }
    }

    [Theory(DisplayName = "Fechar a retificação recusa a banca com menos integrantes do que os corretores da correção independente e aceita a que os alcança")]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task FecharRetificacao_BancaDaEtapa_ExigeOsCorretoresDaRegra(int integrantes, bool aceita)
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versao) = ProcessoComCorrecaoIndependente.PublicadoComBancaRedeclarada(integrantes);
        PublicacaoDeTeste.AbrirRetificacao(processo, versao);

        (Result resposta, IEnumerable<object> eventos) = await ProcessoComCorrecaoIndependente.Servicos(processo)
            .ComVersaoVigente(processo, versao)
            .FecharRetificacaoAsync(processo.Id);

        resposta.IsSuccess.Should().Be(aceita, resposta.Error?.Message);
        if (!aceita)
        {
            AfirmarRecusaPeloMinimo(resposta, eventos, minimo: 3);
        }
    }

    [Theory(DisplayName = "A banca da fase que julga uma etapa de avaliação única e outra de correção independente exige três integrantes")]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Publicar_BancaDaFase_ExigeOMaiorNumeroDeCorretores(int integrantes, bool aceita)
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaFase(integrantes);

        (Result resposta, IEnumerable<object> eventos) = await ProcessoComCorrecaoIndependente.Servicos(processo).PublicarAsync(processo.Id);

        resposta.IsSuccess.Should().Be(aceita, resposta.Error?.Message);
        if (!aceita)
        {
            AfirmarRecusaPeloMinimo(resposta, eventos, minimo: 3);
        }
    }

    private static IRegraCatalogoReader CatalogoSemACorrecaoIndependente() =>
        RegrasDeConsolidacaoDeTeste.Catalogo(RegrasDeConsolidacaoDeTeste.AvaliacaoUnica);

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> Congelar(string transicao, Func<ServicosDoCongelamentoDeTeste, ServicosDoCongelamentoDeTeste> ajusta)
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versao) = ProcessoComCorrecaoIndependente.PublicadoComBancaRedeclarada(integrantes: 3);
        if (transicao == nameof(ServicosDoCongelamentoDeTeste.FecharRetificacaoAsync))
        {
            PublicacaoDeTeste.AbrirRetificacao(processo, versao);
        }

        ServicosDoCongelamentoDeTeste servicos = ajusta(ProcessoComCorrecaoIndependente.ServicosSemCalendario(processo)).ComVersaoVigente(processo, versao);

        return transicao == nameof(ServicosDoCongelamentoDeTeste.FecharRetificacaoAsync)
            ? servicos.FecharRetificacaoAsync(processo.Id)
            : servicos.RetificarAsync(processo.Id);
    }

    [Fact(DisplayName = "Publicar recusa com a regra de consolidação não resolvida quando o catálogo não a tem, em vez de lançar")]
    public async Task Publicar_RegraForaDoCatalogo_RecusaComRegraNaoResolvida()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaEtapa(integrantes: 5);

        (Result resposta, IEnumerable<object> eventos) = await ProcessoComCorrecaoIndependente.Servicos(processo)
            .ComRegrasDeConsolidacao(CatalogoSemACorrecaoIndependente())
            .PublicarAsync(processo.Id);

        resposta.Error!.Code.Should().Be(BancaErrorCodes.RegraDeConsolidacaoNaoResolvida);
        eventos.Should().BeEmpty();
    }

    [Theory(DisplayName = "Retificar e fechar a retificação recusam com a regra de consolidação não resolvida quando o catálogo não a tem")]
    [InlineData(nameof(ServicosDoCongelamentoDeTeste.RetificarAsync))]
    [InlineData(nameof(ServicosDoCongelamentoDeTeste.FecharRetificacaoAsync))]
    public async Task Retificar_RegraForaDoCatalogo_RecusaComRegraNaoResolvida(string transicao)
    {
        (Result resposta, IEnumerable<object> eventos) = await Congelar(
            transicao, servicos => servicos.ComCalendario(CalendarioVigenteReaderDeTeste.ComVigente()).ComRegrasDeConsolidacao(CatalogoSemACorrecaoIndependente()));

        resposta.Error!.Code.Should().Be(BancaErrorCodes.RegraDeConsolidacaoNaoResolvida);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Publicar mantém a recusa de negócio anterior ao portão das bancas quando a regra de consolidação também não se resolve")]
    public async Task Publicar_RecusaAnterior_VenceARegraNaoResolvida()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaEtapa(integrantes: 5);

        (Result resposta, _) = await ProcessoComCorrecaoIndependente.ServicosSemCalendario(processo)
            .ComRegrasDeConsolidacao(CatalogoSemACorrecaoIndependente())
            .PublicarAsync(processo.Id);

        resposta.Error!.Code.Should().Be("ProcessoSeletivo.CalendarioVigenteAusente");
    }

    [Theory(DisplayName = "Retificar e fechar a retificação mantêm a recusa de negócio anterior ao portão das bancas quando a regra de consolidação também não se resolve")]
    [InlineData(nameof(ServicosDoCongelamentoDeTeste.RetificarAsync))]
    [InlineData(nameof(ServicosDoCongelamentoDeTeste.FecharRetificacaoAsync))]
    public async Task Retificar_RecusaAnterior_VenceARegraNaoResolvida(string transicao)
    {
        (Result resposta, _) = await Congelar(transicao, servicos => servicos.ComRegrasDeConsolidacao(CatalogoSemACorrecaoIndependente()));

        resposta.Error!.Code.Should().Be("ProcessoSeletivo.CalendarioVigenteAusente");
    }
}
