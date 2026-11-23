namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
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

    /// <summary>Os endereços do acervo de teste: o certame não está divulgado, então nenhum endereço é exposto.</summary>
    public static EnderecosDosDocumentosDoAto Enderecos() =>
        new(Substitute.For<ICertameDivulgadoRepository>(), Substitute.For<IEnderecoNoAcervoPublico>());

    /// <summary>O ETag que o candidato tem depois de o rascunho adotar a versão divulgada: a adoção sobe a revisão uma vez.</summary>
    public static string ETagDepoisDeAdotar(Inscricao rascunho, VersaoConfiguracao divulgada) =>
        $"\"{rascunho.Id}:{rascunho.Revisao + 1}:{divulgada.Id}\"";

    public static IResolvedorFusoInstitucional Fuso()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return fuso;
    }

    /// <summary>
    /// Uma versão publicada do processo conforme de teste, legível pelo contexto da versão fixada: a
    /// configuração do processo e o bloco de documentos exigidos, sem referência temporal.
    /// </summary>
    public static (VersaoConfiguracao Versao, ConfiguracaoPublicada Configuracao) VersaoConforme(Guid processoId) =>
        (VersaoSemReferenciaTemporal(processoId), ConfiguracaoDe(ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Inscrição")));

    /// <summary>A configuração publicada do próprio processo, com o formulário, os fatos e os grupos que ele declara.</summary>
    public static ConfiguracaoPublicada ConfiguracaoDe(ProcessoSeletivo processo) => new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processo.CronogramaFases]),
            new AvaliacaoDeclarada([.. processo.Etapas], [.. processo.CriteriosDesempate], processo.Classificacao!),
            new VagasDeclaradas([.. processo.DistribuicaoVagas], 1, processo.BonusRegional is not null,
                processo.BonusRegional, null, processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processo.DocumentosExigidos], []),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta([.. processo.FatosColetados], [.. processo.GruposColetados]), [.. processo.GruposColetados], [.. processo.Formularios], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

    /// <summary>
    /// A versão cujo bloco de documentos exigidos não declara referência temporal: o processo de teste não a tem, e o
    /// contexto da versão fixada lê esse bloco do JSON cru.
    /// </summary>
    public static VersaoConfiguracao VersaoSemReferenciaTemporal(Guid processoId) => VersaoConfiguracao.Abrir(
        processoId,
        new DocumentoCongelado(Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":null}}"""), "0.0.22", "canonical-json/sha256@v1", new string('a', 64)),
        Guid.CreateVersion7(), "user-conta-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    /// <summary>A versão é a fixada pela inscrição e a divulgada pelo certame.</summary>
    public static void ConfigurarVersao(
        IProcessoSeletivoRepository processoRepository, IRegistroCodecsEnvelope registroCodecs, VersaoConfiguracao versao, ConfiguracaoPublicada configuracao)
    {
        processoRepository.ObterVersoesAsync(versao.ProcessoSeletivoId, Arg.Any<CancellationToken>()).Returns([versao]);
        ConfigurarDivulgada(processoRepository, registroCodecs, versao, configuracao);
    }

    /// <summary>A versão é só a divulgada pelo certame, com o período publicado até <paramref name="fimDoPeriodo"/>.</summary>
    public static void ConfigurarDivulgada(
        IProcessoSeletivoRepository processoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        VersaoConfiguracao versao,
        ConfiguracaoPublicada configuracao,
        DateTimeOffset? fimDoPeriodo = null)
    {
        processoRepository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([versao]);
        ConfigurarReidratacao(registroCodecs, versao, configuracao, fimDoPeriodo ?? FimDoPeriodo);
    }

    /// <summary>
    /// A inscrição fixou uma versão, e o certame divulga outra, que a retificou: cada uma com o seu
    /// período publicado.
    /// </summary>
    public static void ConfigurarFixadaEDivulgada(
        IProcessoSeletivoRepository processoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ConfiguracaoPublicada configuracao,
        (VersaoConfiguracao Versao, DateTimeOffset FimDoPeriodo) fixada,
        (VersaoConfiguracao Versao, DateTimeOffset FimDoPeriodo) divulgada)
    {
        processoRepository.ObterVersoesAsync(fixada.Versao.ProcessoSeletivoId, Arg.Any<CancellationToken>())
            .Returns([fixada.Versao, divulgada.Versao]);
        ConfigurarReidratacao(registroCodecs, fixada.Versao, configuracao, fixada.FimDoPeriodo);
        ConfigurarDivulgada(processoRepository, registroCodecs, divulgada.Versao, configuracao, divulgada.FimDoPeriodo);
    }

    private static void ConfigurarReidratacao(
        IRegistroCodecsEnvelope registroCodecs, VersaoConfiguracao versao, ConfiguracaoPublicada configuracao, DateTimeOffset fimDoPeriodo)
    {
        EnvelopeReidratado envelopeReidratado = new(
            configuracao, DadosEdital.Criar("001/2026", InicioDoPeriodo, fimDoPeriodo, Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null, dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada());

        registroCodecs.LeAVersao("0.0.22").Returns(true);
        registroCodecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelopeReidratado));
    }
}
