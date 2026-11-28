namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// A publicação e a retificação do edital com o documento, o bloco do ato, o órgão, a série, o ano, a data, o assinante, o tipo ou o documento de um anexo
/// ausentes: a recusa é 422 com o código de campo obrigatório, um item por campo no campo do pedido, acumulada com a
/// recusa do tipo do ato, que não depende deles. O que depende do campo ausente não é conferido, e nada é gravado.
/// </summary>
public sealed class CampoObrigatorioAusenteNoEditalTests
{
    public static TheoryData<OperacaoQueCongela, string> CamposAusentes()
    {
        TheoryData<OperacaoQueCongela, string> dados = [];
        foreach (OperacaoQueCongela operacao in Enum.GetValues<OperacaoQueCongela>())
        {
            foreach (string campo in new[] { "documentoEditalId", "ato", "ato.orgao", "ato.serie", "ato.ano", "ato.dataDoAto", "ato.assinante", "ato.tipoAtoCodigo" })
            {
                dados.Add(operacao, campo);
            }
        }

        return dados;
    }

    [Theory(DisplayName = "O campo ausente é recusado com o código de campo obrigatório, no campo do pedido, sem gravar nada")]
    [MemberData(nameof(CamposAusentes))]
    public async Task CampoAusente_RecusaComCodigoObrigatorio(OperacaoQueCongela operacao, string campo)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        (Guid? documento, DadosDoAto? ato) = SemOCampo(campo);

        (Result resposta, IEnumerable<object> eventos) = await ExecutarAsync(cenario, operacao, documento, ato);

        Recusa(resposta).Should().Equal((campo, CodigosDeValidacao.Obrigatorio));
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
        cenario.AfirmarQueNaoTransitou();
    }

    [Theory(DisplayName = "O documento do edital ausente não é consultado, porque a conferência dele depende do identificador")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task DocumentoAusente_NaoConsultaODocumento(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);

        await ExecutarAsync(cenario, operacao, null, NovoAto());

        await cenario.Servicos.Documentos.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Os campos ausentes acumulam na ordem do pedido numa resposta só")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task VariosCamposAusentes_AcumulamNaOrdemDoPedido(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);

        (Result resposta, _) = await ExecutarAsync(
            cenario, operacao, null, NovoAto() with { Ano = null, DataDoAto = null }, [new(null, "Quadro")]);

        Recusa(resposta).Select(static r => r.Campo).Should().Equal("documentoEditalId", "ato.ano", "ato.dataDoAto", "anexos[0].documentoId");
        resposta.Errors.Should().OnlyContain(static e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Theory(DisplayName = "O órgão em branco e o ano ausente acumulam numa resposta só, na ordem do bloco")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task OrgaoEmBrancoEAnoAusente_Acumulam(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);

        (Result resposta, _) = await ExecutarAsync(cenario, operacao, Guid.CreateVersion7(), NovoAto() with { Orgao = "   ", Ano = null });

        Recusa(resposta).Should().Equal(
            ("ato.orgao", CodigosDeValidacao.Obrigatorio), ("ato.ano", CodigosDeValidacao.Obrigatorio));
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    [Theory(DisplayName = "O tipo do ato em branco é recusado no campo e o catálogo de tipos não é consultado")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task TipoEmBranco_NaoConsultaOCatalogo(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.TipoDeAto);

        (Result resposta, _) = await ExecutarAsync(cenario, operacao, Guid.CreateVersion7(), NovoAto() with { TipoAtoCodigo = " " });

        Recusa(resposta).Should().Equal(("ato.tipoAtoCodigo", CodigosDeValidacao.Obrigatorio));
    }

    [Theory(DisplayName = "A ausência acumula com a recusa do tipo do ato, que não depende dela, e a ausência vem primeiro")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task AusenciaETipoDoAtoRecusado_Acumulam(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.TipoDeAto);

        (Result resposta, _) = await ExecutarAsync(cenario, operacao, Guid.CreateVersion7(), NovoAto() with { Ano = null });

        Recusa(resposta).Should().Equal(
            ("ato.ano", CodigosDeValidacao.Obrigatorio), (null, CodigoDa(DefeitoDoCongelamento.TipoDeAto)));
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    [Theory(DisplayName = "O documento recusado impede o tipo do ato, e a ausência acumula com ele")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task AusenciaEDocumentoRecusado_Acumulam(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.Documento).Com(DefeitoDoCongelamento.TipoDeAto);

        (Result resposta, _) = await ExecutarAsync(cenario, operacao, Guid.CreateVersion7(), NovoAto() with { DataDoAto = null });

        Recusa(resposta).Should().Equal(
            ("ato.dataDoAto", CodigosDeValidacao.Obrigatorio), ("documentoEditalId", CodigoDa(DefeitoDoCongelamento.Documento)));
    }

    [Theory(DisplayName = "O anexo sem documento é recusado na posição dele e a lista de documentos não é lida")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task AnexoSemDocumento_RecusaNaPosicao(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);

        (Result resposta, _) = await ExecutarAsync(
            cenario, operacao, Guid.CreateVersion7(), NovoAto(), [new(Guid.CreateVersion7(), "A"), new(null, "B")]);

        Recusa(resposta).Should().Equal(("anexos[1].documentoId", CodigosDeValidacao.Obrigatorio));
        await cenario.Servicos.Documentos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    private static (Guid? Documento, DadosDoAto? Ato) SemOCampo(string campo) => campo switch
    {
        "documentoEditalId" => (null, NovoAto()),
        "ato" => (Guid.CreateVersion7(), null),
        "ato.orgao" => (Guid.CreateVersion7(), NovoAto() with { Orgao = null }),
        "ato.serie" => (Guid.CreateVersion7(), NovoAto() with { Serie = null }),
        "ato.ano" => (Guid.CreateVersion7(), NovoAto() with { Ano = null }),
        "ato.assinante" => (Guid.CreateVersion7(), NovoAto() with { Assinante = null }),
        "ato.tipoAtoCodigo" => (Guid.CreateVersion7(), NovoAto() with { TipoAtoCodigo = null }),
        "ato.dataDoAto" => (Guid.CreateVersion7(), NovoAto() with { DataDoAto = null }),
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, "Campo que o pedido não declara."),
    };

    private static (string? Campo, string Codigo)[] Recusa(Result resposta) => [.. resposta.Errors.Select(static e => (e.Field, e.Error.Code))];

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> ExecutarAsync(
        CenarioDeRecusaNoCongelamento cenario,
        OperacaoQueCongela operacao,
        Guid? documento,
        DadosDoAto? ato,
        IReadOnlyList<AnexoSolicitadoDoEdital>? anexos = null) => operacao switch
        {
            OperacaoQueCongela.Publicar => cenario.Servicos.PublicarAsync(
                new PublicarProcessoSeletivoCommand(cenario.Processo.Id, "001/2026", null, null, documento, ato, anexos)),
            _ => cenario.Servicos.RetificarAsync(
                new RetificarProcessoSeletivoCommand(cenario.Processo.Id, "Correção do prazo", "001/2026-R1", null, null, documento, ato, anexos)),
        };
}
