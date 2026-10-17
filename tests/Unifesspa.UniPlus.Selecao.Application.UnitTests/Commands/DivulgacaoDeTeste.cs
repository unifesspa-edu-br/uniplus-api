namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A versão que o certame divulga, para os handlers da inscrição: as regras vêm da versão fixada, e o
/// período, da divulgada. Nos testes em que as duas coincidem, a mesma versão responde às duas leituras.
/// </summary>
internal static class DivulgacaoDeTeste
{
    /// <summary>O período de inscrição que o ato de teste publica: janeiro de 2026.</summary>
    public static readonly DateTimeOffset InicioDoPeriodo = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset FimDoPeriodo = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset DentroDoPeriodo = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static TimeProvider Relogio { get; } = new RelogioFixo(DentroDoPeriodo);

    public static ICertameDivulgadoRepository Certame(Guid processoId)
    {
        DateTimeOffset instante = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        CertameDivulgado divulgado = CertameDivulgado.Criar(
            processoId, numeroVersao: 1, Guid.CreateVersion7(), new string('a', 64), versaoProjecao: "1",
            new FacetasDoCertameDivulgado("certame-inscricao", "Certame", "001/2026", ["AC"], instante, instante.AddDays(30)),
            """{"nome":"documento"}""", instante);

        ICertameDivulgadoRepository repositorio = Substitute.For<ICertameDivulgadoRepository>();
        repositorio.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(divulgado);
        return repositorio;
    }

    public static IResolvedorFusoInstitucional Fuso()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return fuso;
    }

    /// <summary>
    /// Uma versão publicada do processo conforme de teste, legível pelo contexto da versão fixada: o
    /// grafo do processo e o bloco de documentos exigidos, sem referência temporal.
    /// </summary>
    public static (VersaoConfiguracao Versao, GrafoConfiguracao Grafo) VersaoConforme(Guid processoId)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Inscrição");
        GrafoConfiguracao grafo = new(
            [.. processo.Etapas], processo.OfertaAtendimento!, [.. processo.DistribuicaoVagas],
            processo.BonusRegional, [.. processo.CriteriosDesempate], processo.Classificacao!,
            [.. processo.CronogramaFases], [.. processo.DocumentosExigidos], [], null,
            fatosColetados: [.. processo.FatosColetados],
            formularios: [.. processo.Formularios],
            gruposColetados: [.. processo.GruposColetados],
            numeroDeOpcoesDeCurso: 1);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":null}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        return (versao, grafo);
    }

    /// <summary>A versão é a fixada pela inscrição e a divulgada pelo certame.</summary>
    public static void ConfigurarVersao(
        IProcessoSeletivoRepository processoRepository, IRegistroCodecsEnvelope registroCodecs, VersaoConfiguracao versao, GrafoConfiguracao grafo)
    {
        processoRepository.ObterVersoesAsync(versao.ProcessoSeletivoId, Arg.Any<CancellationToken>()).Returns([versao]);
        ConfigurarDivulgada(processoRepository, registroCodecs, versao, grafo);
    }

    /// <summary>A versão é só a divulgada pelo certame, com o período publicado até <paramref name="fimDoPeriodo"/>.</summary>
    public static void ConfigurarDivulgada(
        IProcessoSeletivoRepository processoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        VersaoConfiguracao versao,
        GrafoConfiguracao grafo,
        DateTimeOffset? fimDoPeriodo = null)
    {
        processoRepository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([versao]);
        ConfigurarReidratacao(registroCodecs, versao, grafo, fimDoPeriodo ?? FimDoPeriodo);
    }

    /// <summary>
    /// A inscrição fixou uma versão, e o certame divulga outra, que a retificou: cada uma com o seu
    /// período publicado.
    /// </summary>
    public static void ConfigurarFixadaEDivulgada(
        IProcessoSeletivoRepository processoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        GrafoConfiguracao grafo,
        (VersaoConfiguracao Versao, DateTimeOffset FimDoPeriodo) fixada,
        (VersaoConfiguracao Versao, DateTimeOffset FimDoPeriodo) divulgada)
    {
        processoRepository.ObterVersoesAsync(fixada.Versao.ProcessoSeletivoId, Arg.Any<CancellationToken>())
            .Returns([fixada.Versao, divulgada.Versao]);
        ConfigurarReidratacao(registroCodecs, fixada.Versao, grafo, fixada.FimDoPeriodo);
        ConfigurarDivulgada(processoRepository, registroCodecs, divulgada.Versao, grafo, divulgada.FimDoPeriodo);
    }

    private static void ConfigurarReidratacao(
        IRegistroCodecsEnvelope registroCodecs, VersaoConfiguracao versao, GrafoConfiguracao grafo, DateTimeOffset fimDoPeriodo)
    {
        EnvelopeReidratado envelopeReidratado = new(
            grafo, DadosEdital.Criar("001/2026", InicioDoPeriodo, fimDoPeriodo, Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null);

        registroCodecs.Capacidades.Returns([new CapacidadeCodec("0.0.22", TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        registroCodecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelopeReidratado));
    }
}
