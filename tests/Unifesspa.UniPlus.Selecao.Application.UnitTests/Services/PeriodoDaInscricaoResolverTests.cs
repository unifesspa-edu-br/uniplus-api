namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cada passo que confere o período de inscrição fecha as inscrições com o próprio motivo, e a recusa continua
/// sendo a que as rotas do candidato já devolvem: o motivo é marcado por quem recusa, nunca deduzido do código.
/// </summary>
public sealed class PeriodoDaInscricaoResolverTests
{
    private static readonly DateTimeOffset DentroDoPeriodo = DivulgacaoDeTeste.DentroDoPeriodo;

    private static Task<InscricoesFechadas?> ConferirAsync(
        ProcessoDivulgadoParaOPeriodoDeTeste processo, DateTimeOffset instante, IResolvedorFusoInstitucional? fuso = null)
    {
        (ICertameDivulgadoRepository certames, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoDivulgadoParaOPeriodoDeTeste.Repositorios(processo);
        PeriodoDaInscricaoResolver resolver = new(certames, processos, codecs, fuso ?? DivulgacaoDeTeste.Fuso());

        return resolver.ConferirAsync(processo.ProcessoId, instante, CancellationToken.None);
    }

    [Fact(DisplayName = "Processo sem divulgação: o período não se confirma, com a recusa de processo não encontrado")]
    public async Task ConferirAsync_SemDivulgacao_PeriodoNaoConfirmado()
    {
        InscricoesFechadas? fechadas = await ConferirAsync(new ProcessoDivulgadoParaOPeriodoDeTeste { Divulgado = false }, DentroDoPeriodo);

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado);
        fechadas.Recusa.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Divulgação que aponta uma versão inexistente: o período não se confirma")]
    public async Task ConferirAsync_DivulgacaoSemAVersao_PeriodoNaoConfirmado()
    {
        InscricoesFechadas? fechadas = await ConferirAsync(new ProcessoDivulgadoParaOPeriodoDeTeste { ComVersaoDaDivulgacao = false }, DentroDoPeriodo);

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado);
        fechadas.Recusa.Code.Should().Be("Snapshot.VigenteAusente");
    }

    [Fact(DisplayName = "Versão divulgada que o sistema não sabe reabrir: o período não se confirma")]
    public async Task ConferirAsync_VersaoQueNaoReabre_PeriodoNaoConfirmado()
    {
        InscricoesFechadas? fechadas = await ConferirAsync(new ProcessoDivulgadoParaOPeriodoDeTeste { VersaoLegivel = false }, DentroDoPeriodo);

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado);
        fechadas.Recusa.Code.Should().Be(ErrosCodecEnvelope.VersaoDesconhecida);
    }

    [Fact(DisplayName = "Processo sem formulário de inscrição não recebe inscrição pelo sistema")]
    public async Task ConferirAsync_SemFormularioDeInscricao_ProcessoSemInscricao()
    {
        InscricoesFechadas? fechadas = await ConferirAsync(new ProcessoDivulgadoParaOPeriodoDeTeste { ComFormularioDeInscricao = false }, DentroDoPeriodo);

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.ProcessoSemInscricao);
        fechadas.Recusa.Code.Should().Be(InscricaoErrorCodes.ProcessoSemInscricao);
    }

    [Fact(DisplayName = "Sem formulário de inscrição, o motivo é do processo mesmo que o fuso falhe: a ordem dos passos não muda")]
    public async Task ConferirAsync_SemFormularioEFusoIndisponivel_ProcessoSemInscricao()
    {
        InscricoesFechadas? fechadas = await ConferirAsync(
            new ProcessoDivulgadoParaOPeriodoDeTeste { ComFormularioDeInscricao = false }, DentroDoPeriodo, new ResolvedorFusoIndisponivelDeTeste());

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.ProcessoSemInscricao);
    }

    [Fact(DisplayName = "Fuso institucional que não se resolve: o período não se confirma, com a recusa do fuso")]
    public async Task ConferirAsync_FusoIndisponivel_PeriodoNaoConfirmado()
    {
        InscricoesFechadas? fechadas = await ConferirAsync(new ProcessoDivulgadoParaOPeriodoDeTeste(), DentroDoPeriodo, new ResolvedorFusoIndisponivelDeTeste());

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado);
        fechadas.Recusa.Code.Should().Be("ProcessoSeletivo.FusoInstitucionalNaoReconhecido");
    }

    [Fact(DisplayName = "Antes do início do período, o motivo é período não iniciado, com a recusa de fora do período")]
    public async Task ConferirAsync_AntesDoInicio_PeriodoNaoIniciado()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste processo = new();

        InscricoesFechadas? fechadas = await ConferirAsync(processo, processo.InicioDoPeriodo.AddTicks(-1));

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoNaoIniciado);
        fechadas.Recusa.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }

    [Fact(DisplayName = "Depois do fim do período, o motivo é período encerrado, com a recusa de fora do período")]
    public async Task ConferirAsync_DepoisDoFim_PeriodoEncerrado()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste processo = new();

        InscricoesFechadas? fechadas = await ConferirAsync(processo, processo.FimDoPeriodo.AddTicks(1));

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoEncerrado);
        fechadas.Recusa.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }

    [Fact(DisplayName = "No primeiro e no último instante do período, as inscrições estão abertas")]
    public async Task ConferirAsync_NosLimitesDoPeriodo_Abertas()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste processo = new();

        (await ConferirAsync(processo, processo.InicioDoPeriodo)).Should().BeNull();
        (await ConferirAsync(processo, processo.FimDoPeriodo)).Should().BeNull();
    }

    [Fact(DisplayName = "A conferência sobre a versão já resolvida fecha com o mesmo motivo do período")]
    public void Conferir_SobreAVersaoResolvida_FechaComOMotivoDoPeriodo()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste processo = new();
        (ICertameDivulgadoRepository certames, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoDivulgadoParaOPeriodoDeTeste.Repositorios(processo);
        PeriodoDaInscricaoResolver resolver = new(certames, processos, codecs, DivulgacaoDeTeste.Fuso());

        resolver.Conferir(processo.Versao, processo.FimDoPeriodo.AddTicks(1))!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoEncerrado);
        resolver.Conferir(processo.Versao, DentroDoPeriodo).Should().BeNull();
    }

    [Fact(DisplayName = "A conferência sobre a versão cujo conteúdo não se reabre não confirma o período")]
    public void Conferir_VersaoQueNaoReabre_PeriodoNaoConfirmado()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste processo = new() { VersaoLegivel = false };
        (ICertameDivulgadoRepository certames, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoDivulgadoParaOPeriodoDeTeste.Repositorios(processo);
        PeriodoDaInscricaoResolver resolver = new(certames, processos, codecs, DivulgacaoDeTeste.Fuso());

        resolver.Conferir(processo.Versao, DentroDoPeriodo)!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado);
    }

    [Fact(DisplayName = "A versão do codec que falha ao reabrir o conteúdo também não confirma o período")]
    public void Conferir_CodecQueFalhaAoReabrir_PeriodoNaoConfirmado()
    {
        ProcessoDivulgadoParaOPeriodoDeTeste processo = new();
        (ICertameDivulgadoRepository certames, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoDivulgadoParaOPeriodoDeTeste.Repositorios(processo);
        codecs.Reidratar(processo.Versao).Returns(Result<EnvelopeReidratado>.Failure(new DomainError("EnvelopeCodec.Corrompido", "O conteúdo não pôde ser lido.")));
        PeriodoDaInscricaoResolver resolver = new(certames, processos, codecs, DivulgacaoDeTeste.Fuso());

        InscricoesFechadas? fechadas = resolver.Conferir(processo.Versao, DentroDoPeriodo);

        fechadas!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoNaoConfirmado);
        fechadas.Recusa.Code.Should().Be("EnvelopeCodec.Corrompido");
    }
}
