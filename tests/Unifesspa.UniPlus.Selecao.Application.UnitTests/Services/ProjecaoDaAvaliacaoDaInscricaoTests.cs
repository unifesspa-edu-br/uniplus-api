namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A avaliação da inscrição na forma que o candidato recebe: os documentos agrupados pelas fases do
/// cronograma da versão fixada, só os que se aplicam ou podem se aplicar, com a finalidade de cada um
/// (UNI-REQ-0069, UNI-REQ-0077).
/// </summary>
public sealed class ProjecaoDaAvaliacaoDaInscricaoTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid AtoId = Guid.CreateVersion7();
    private static readonly FaseCronograma FaseDaInscricao = Fase(1, "INSCRICAO");
    private static readonly FaseCronograma FaseDaHabilitacao = Fase(3, "HABILITACAO");

    [Fact(DisplayName = "Sem fase pedida, vêm todas as fases na ordem do cronograma, cada exigência na sua, e a não exigida não vem")]
    public void SemFase_TodasAsFasesSemONaoExigido()
    {
        DocumentoExigido daInscricao = Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao);
        DocumentoExigido daIsencao = Documento(FaseDaInscricao, FinalidadeFormulario.IsencaoTaxa);
        DocumentoExigido dispensado = Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao);
        DocumentoExigido daHabilitacao = Documento(FaseDaHabilitacao, FinalidadeFormulario.Habilitacao);

        AvaliacaoDaInscricaoDto avaliacao = Projetar(
            [
                (daHabilitacao, SituacaoDoDocumento.DependeDeFasePosterior),
                (daInscricao, SituacaoDoDocumento.Pendente),
                (daIsencao, SituacaoDoDocumento.PodeSerExigido),
                (dispensado, SituacaoDoDocumento.NaoExigido),
            ],
            faseId: null).Value!;

        avaliacao.Documentos.Fases.Select(static f => f.Codigo).Should().Equal("INSCRICAO", "HABILITACAO");
        avaliacao.Documentos.Fases[0].Documentos.Select(static d => (d.ExigenciaId, d.Finalidade, d.Situacao)).Should().Equal(
            [(daInscricao.Id, "INSCRICAO", "PENDENTE"), (daIsencao.Id, "ISENCAO_TAXA", "PODE_SER_EXIGIDO")],
            "a isenção na mesma fase aparece como da isenção, e não como exigência da inscrição");
        avaliacao.Documentos.Fases[1].Documentos.Select(static d => d.Situacao).Should().Equal("DEPENDE_DE_FASE_POSTERIOR");
    }

    [Fact(DisplayName = "Com a fase pedida, vêm só os documentos dela; fase fora do cronograma da versão é não encontrada")]
    public void ComFase_SoAFase_FaseDesconhecida_NaoEncontrada()
    {
        (DocumentoExigido, SituacaoDoDocumento)[] documentos =
        [
            (Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao), SituacaoDoDocumento.Pendente),
            (Documento(FaseDaHabilitacao, FinalidadeFormulario.Habilitacao), SituacaoDoDocumento.Pendente),
        ];

        Projetar(documentos, FaseDaHabilitacao.Id).Value!.Documentos.Fases.Select(static f => f.Codigo).Should().Equal("HABILITACAO");
        Projetar(documentos, Guid.CreateVersion7()).Error!.Code.Should().Be(InscricaoErrorCodes.FaseNaoEncontrada);
    }

    [Fact(DisplayName = "O modelo da exigência vem com o endereço no acervo do ato que publicou a versão")]
    public void Modelo_ComEnderecoDoAtoDaVersao()
    {
        ModeloDaExigencia modelo = new(Guid.CreateVersion7(), "Autodeclaração.odt", FormatoDeDocumento.Odt, new string('a', 64));

        DocumentoDaInscricaoDto documento = Projetar(
            [(Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao, modelo), SituacaoDoDocumento.Pendente)], faseId: null)
            .Value!.Documentos.Fases[0].Documentos.Single();

        documento.Modelo!.UrlDownload.ToString().Should().Contain($"/atos/{AtoId:D}/");
        documento.Modelo.NomeArquivo.Should().Be("Autodeclaração.odt");
    }

    [Fact(DisplayName = "Com a fase pedida, a pendência de outra fase não vem: ela não muda nada na fase pedida")]
    public void ComFase_SemPendenciaDeOutraFase()
    {
        (DocumentoExigido, SituacaoDoDocumento)[] documentos =
        [
            (Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao), SituacaoDoDocumento.Pendente),
            (Documento(FaseDaHabilitacao, FinalidadeFormulario.Habilitacao), SituacaoDoDocumento.PodeSerExigido),
        ];
        Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>> pendencias = new()
        {
            [FaseDaHabilitacao.Id] = [new PendenciaDaAvaliacao("PEDIU_FATOR", "Pediu o Fator?", "DADOS")],
        };

        Projetar(documentos, FaseDaInscricao.Id, pendencias).Value!.Documentos.Fases.Single().Pendencias.Should().BeEmpty();
        Projetar(documentos, faseId: null, pendencias).Value!.Documentos.Fases.Single(static f => f.Codigo == "HABILITACAO")
            .Pendencias.Select(static p => p.FatoCodigo).Should().Equal("PEDIU_FATOR");
    }

    private static Result<AvaliacaoDaInscricaoDto> Projetar(
        IEnumerable<(DocumentoExigido Documento, SituacaoDoDocumento Situacao)> documentos,
        Guid? faseId,
        IReadOnlyDictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>? pendencias = null)
    {
        ResultadoDaAvaliacaoDaInscricao resultado = FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Documentos = new DocumentosDaInscricao(
                [.. documentos.Select(static d => new DocumentoAvaliado(d.Documento, d.Situacao, null, null, [], null))],
                pendencias ?? new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), []),
        };
        ConfiguracaoAvaliavel configuracao = new(
            new DefinicaoFormulario([], [], []),
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            ArvoreExigenciasCongelada.DeGrafoReidratado([]),
            [], [], [],
            [FaseDaHabilitacao, FaseDaInscricao],
            []);
        ContextoDaVersaoFixada contexto = new(
            [], null, new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), new HashSet<Guid>(), new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)),
            configuracao, AtoId, configuracao, null, []);
        IEnderecoNoAcervoPublico acervo = Substitute.For<IEnderecoNoAcervoPublico>();
        acervo.De(Arg.Any<string>()).Returns(static chamada => new Uri($"https://acervo.teste/{chamada.Arg<string>()}"));

        return ProjecaoDaAvaliacaoDaInscricao.Projetar(resultado, contexto, ProcessoId, acervo, faseId);
    }

    private static DocumentoExigido Documento(FaseCronograma fase, FinalidadeFormulario finalidade, ModeloDaExigencia? modelo = null) =>
        DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "COMPROVANTE", "Comprovante", "CAT", Aplicabilidade.Geral, obrigatorio: true,
            consequenciaIndeferimento: null, [], [], null, FormatosPermitidos.Criar(true, null).Value!, null,
            modelo: modelo, finalidade: finalidade).Value!;

    private static FaseCronograma Fase(int ordem, string codigo) =>
        FaseCronograma.Reidratar(
            Guid.CreateVersion7(), ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: ordem == 1, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null, [], faseConcluinteCodigo: null, emiteParecerIndividual: false, [], regraRecurso: null);
}
