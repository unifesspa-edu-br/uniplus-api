namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O apoio comum aos testes da avaliação dos documentos da inscrição: o cadastro mínimo, as respostas e a avaliação, por composição.
/// </summary>
internal static class ApoioDaAvaliacaoDosDocumentos
{
    internal const string Secao = "DADOS";
    internal const string BaixaRenda = "BAIXA_RENDA";
    internal const string Familia = "FAMILIA";
    internal const string RendaDoMembro = "RENDA_DO_MEMBRO";
    internal const string PediuFator = "PEDIU_FATOR";

    /// <summary>A modalidade da convocação, que só existe depois do resultado: nenhum formulário a pergunta.</summary>
    internal const string ModalidadeDaConvocacao = "MODALIDADE_CONVOCACAO";

    internal static readonly FaseCronograma FaseDaInscricao = Fase(1, "INSCRICAO");
    internal static readonly FaseCronograma FaseDaHabilitacao = Fase(3, "HABILITACAO");
    internal static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;

    internal static DocumentoExigido Geral() =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "DOCUMENTO", "Documento", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(FaseDaInscricao.Id, null, FormularioDeTeste.FormularioDaInscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [], false))
            .ComRequisitos(new RequisitosDoDocumento(Qualquer, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    internal static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Enviados(params DocumentoExigido[] documentos) =>
        documentos.ToDictionary(static d => d.Id, static _ => (IReadOnlyList<ApresentacaoDocumento>)[new(Guid.CreateVersion7())]);


    internal static SituacaoDoDocumento Situacao(ResultadoDaAvaliacaoDaInscricao resultado, DocumentoExigido? documento) =>
        resultado.Documentos.Itens.Single(d => documento is null || d.Documento.Id == documento.Id).Situacao;

    internal static ResultadoDaAvaliacaoDaInscricao Avaliar(
        ConfiguracaoAvaliavel configuracao,
        Dictionary<string, JsonElement> respostas,
        IReadOnlyList<OcorrenciaRespondida>? membros = null,
        bool naFaseDaInscricao = true,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos = new(StringComparer.Ordinal);
        if (membros is not null)
        {
            grupos[Familia] = membros;
        }

        Kernel.Results.Result<ResultadoDaAvaliacaoDaInscricao> resultado = AvaliacaoDaInscricao.Avaliar(
            configuracao,
            new EntradaDaAvaliacaoDaInscricao(
                respostas, grupos, new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal),
                Envio: null, Apresentacoes: apresentacoes ?? EntradaDaAvaliacaoDaInscricao.SemApresentacoes, NaFaseDaInscricao: naFaseDaInscricao));
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value!;
    }

    internal static Dictionary<string, JsonElement> Respostas(params (string Fato, bool Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => JsonSerializer.SerializeToElement(r.Valor), StringComparer.Ordinal);

    internal static OcorrenciaRespondida[] Membros(params (string Id, bool Renda)[] membros) =>
        [.. membros.Select(static m => new OcorrenciaRespondida(m.Id, Respostas((RendaDoMembro, m.Renda))))];

    /// <param name="repetePorFamilia">Se a regra de <paramref name="documento"/> se repete por membro da família.</param>
    /// <param name="extra">Outra exigência, repetida por membro da família.</param>
    /// <param name="soNaHabilitacao">Outra exigência, fora de repetição.</param>
    internal static ConfiguracaoAvaliavel Configuracao(
        DocumentoExigido documento, bool repetePorFamilia = false, DocumentoExigido? extra = null, DocumentoExigido? soNaHabilitacao = null)
    {
        List<RegraDeComprovacao> regrasSemGrupo = [RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, grupoDoFormularioParaRepeticao: repetePorFamilia ? Familia : null).Value!];
        if (extra is not null)
        {
            regrasSemGrupo.Add(RegraDeComprovacao.CriarRegraDeDocumento(extra, 1, grupoDoFormularioParaRepeticao: Familia).Value!);
        }

        if (soNaHabilitacao is not null)
        {
            regrasSemGrupo.Add(RegraDeComprovacao.CriarRegraDeDocumento(soNaHabilitacao, 2).Value!);
        }

        return ComRegrasSemGrupo(regrasSemGrupo);
    }

    /// <param name="exibicaoDaFamilia">Quando o grupo da família aparece; sempre, quando nula.</param>
    internal static DefinicaoFormulario DefinicaoDoFormularioDaInscricao(PredicadoDnf? exibicaoDaFamilia) =>
        new(
            [new DefinicaoPagina(
                DefinicaoDoProcesso.CodigoDaPagina(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Inscricao), Secao),
                null,
                [new DefinicaoItem(BaixaRenda, null, Obrigatoriedade.Sempre, []), new DefinicaoItem(PediuFator, null, Obrigatoriedade.Sempre, [])],
                [DefinicaoDoGrupoDaFamilia(exibicaoDaFamilia)])],
            [],
            []);

    internal static DefinicaoGrupo DefinicaoDoGrupoDaFamilia(PredicadoDnf? exibicao) => new(
        Familia, exibicao, Obrigatoriedade.Sempre, 0, null, [new DefinicaoItem(RendaDoMembro, null, Obrigatoriedade.Sempre, [])], incluiCandidato: false);

    /// <param name="exibicaoDaFamilia">Quando o grupo da família aparece; sempre, quando nula.</param>
    internal static ConfiguracaoAvaliavel ComRegrasSemGrupo(IReadOnlyList<RegraDeComprovacao> regrasSemGrupo, PredicadoDnf? exibicaoDaFamilia = null)
    {
        FormularioProcesso inscricao = Formulario(PapelDoFormularioDeTeste.Inscricao, FaseDaInscricao);

        return new(
            DefinicaoDoFormularioDaInscricao(exibicaoDaFamilia),
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            RegrasDeComprovacaoDaVersao.DaConfiguracaoPublicada(regrasSemGrupo),
            [Campo(BaixaRenda).Em(inscricao), Campo(PediuFator).Em(inscricao)],
            [GrupoDaFamilia().Em(inscricao)],
            [],
            [FaseDaInscricao, FaseDaHabilitacao],
            [inscricao, Formulario(PapelDoFormularioDeTeste.Habilitacao, FaseDaHabilitacao)]);
    }

    internal static GrupoColetado GrupoDaFamilia() =>
        GrupoColetadoDeTeste.Agrupando(Familia, 1)
            .NaSecao(Secao)
            .ComRotulo("Família")
            .ComOcorrencias(0, null)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(Campo(RendaDoMembro, etapa: null))
            .Construir();

    /// <param name="etapa">A seção do campo; o subitem de grupo segue a do grupo, e não tem a sua.</param>
    internal static FatoColetado Campo(string fato, string? etapa = Secao) =>
        FatoColetadoDeTeste.Coletando(fato, 0)
            .ComRotulo($"Rótulo de {fato}")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .NaSecao(etapa)
            .Construir();

    internal static DocumentoExigido Condicional(FaseCronograma fase, params (string Fato, bool Valor)[] atomos) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COMPROVANTE", "Comprovante", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(fase.Id, null, FormularioDeTeste.FormularioDaInscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [.. atomos.Select(static a => CondicaoGatilho.Criar(0, a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!)]))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [], false))
            .ComRequisitos(new RequisitosDoDocumento(Qualquer, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    internal static FormularioProcesso Formulario(PapelDoFormularioDeTeste papel, FaseCronograma fase) =>
        FormularioProcesso.Criar(
            FormularioDeTeste.DeclaradoNoPapel(papel, fase.Id), CronogramaDoProcesso.Do([fase]), null,
            [PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 0, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!]).Value!;

    internal static FaseCronograma Fase(int ordem, string codigo) =>
        FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            Guid.CreateVersion7(),
            OrigensDoTipoDeFaseDeTeste.Da(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: ordem == 1, coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []);
}
