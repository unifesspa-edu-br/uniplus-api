namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cenários do <see cref="AvaliadorConformidadeLegal"/> contra o agregado <see cref="ProcessoSeletivo"/> real:
/// o processo, a regra e o cadastro vivo que cada predicado confere.
/// </summary>
internal static class CenarioDeConformidadeLegal
{
    internal const string TipoProcessoAvaliado = "SISU";

    internal static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar(
            "PS Avaliador 2026",
            TiposDeProcessoDeTeste.Sisu,
            OrigemCandidatos.InscricaoPropria,
            Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            IdentificadoresDeTeste.Novo());

    internal static ObrigatoriedadeLegal NovaRegra(string regraCodigo, PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            TodosOsTiposDeProcesso.Codigo,
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            predicado,
            new TextoNormativo("Regra de teste", "Lei de teste", null, null),
            new DateOnly(2026, 1, 1),
            null).Value!;

    /// <summary>Avalia uma regra; sem identidades dadas, o cadastro vivo é deduzido do próprio processo.</summary>
    internal static RegraAvaliada Avaliar(ProcessoSeletivo processo, ObrigatoriedadeLegal regra, IdentidadesDeCadastro? identidades = null) =>
        AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra], identidades ?? IdentidadesDe(processo), FatosDeModalidadeDeTeste.DoCatalogo)
            .Regras.Single();

    internal static Dictionary<string, Guid> Mapa(string codigo, Guid origem) =>
        new(StringComparer.Ordinal) { [codigo] = origem };

    internal static TipoEtapaSnapshot NovoTipoEtapa(string codigo, string nome, Guid? origem = null) =>
        TipoEtapaSnapshot.Criar(
            origem ?? Guid.CreateVersion7(), codigo, nome, admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    internal static void DefinirEtapa(ProcessoSeletivo processo, string nome, TipoEtapaSnapshot tipo) =>
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: tipo, Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!]).IsSuccess.Should().BeTrue();

    internal static void DefinirAtendimento(ProcessoSeletivo processo, Guid origem, string codigo, string nome) =>
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar(
                [CondicaoOfertada.Criar(Guid.CreateVersion7(), AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
                [],
                [DeficienciaAtendida.Criar(origem, codigo, nome)]).Value!).IsSuccess.Should().BeTrue();

    /// <remarks>
    /// A identidade é parâmetro porque duas ofertas que selecionam a mesma modalidade
    /// referenciam a mesma linha do cadastro — e é a identidade que a avaliação compara.
    /// </remarks>
    internal static ModalidadeSelecionada NovaModalidade(string codigo, NaturezaLegalModalidade natureza, Guid? origemId = null) =>
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, natureza, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { Id = origemId ?? Guid.CreateVersion7(), RegraRemanejamento = RemanejamentoDa(natureza) }, VagasPorModalidade).Value!;

    private static RegraRemanejamentoModalidade? RemanejamentoDa(NaturezaLegalModalidade natureza) =>
        natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento : null;

    private const int VagasPorModalidade = 10;

    internal static ConfiguracaoDistribuicaoVagas NovaOferta(params ModalidadeSelecionada[] modalidades) =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = VagasPorModalidade * modalidades.Length, RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", new string('a', 64)), Modalidades = modalidades },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    internal static void DefinirOfertas(ProcessoSeletivo processo, params ConfiguracaoDistribuicaoVagas[] ofertas) =>
        processo.DefinirDistribuicaoVagas(ofertas, FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();

    internal static void DefinirDocumentos(ProcessoSeletivo processo, params DocumentoExigido[] documentos) =>
        processo.DefinirDocumentosExigidosNosFormulariosMinimos(
            [.. documentos.Select(d => RegraDeComprovacao.CriarRegraDeDocumento(d, 0).Value!)]).IsSuccess.Should().BeTrue();

    /// <summary>Define a fase de envio de documentos e uma oferta com a modalidade; devolve o id da fase.</summary>
    internal static Guid PrepararProcessoComModalidade(ProcessoSeletivo processo, string modalidadeCodigo)
    {
        FaseCronograma fase = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "ENVIO_DOCUMENTOS", OrigemData: OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        DefinirOfertas(processo, NovaOferta(NovaModalidade(modalidadeCodigo, NaturezaLegalModalidade.CotaReservada)));
        return fase.Id;
    }

    internal static DocumentoExigido ExigenciaGeral(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, Guid? tipoDocumentoOrigemId = null, bool obrigatorio = true) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(tipoDocumentoOrigemId ?? Guid.CreateVersion7(), tipoDocumentoCodigo, "Documento de teste", "CATEGORIA", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FormularioDeTeste.FormularioDaInscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(obrigatorio, null, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    internal static DocumentoExigido ExigenciaCondicionalPorModalidade(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string modalidadeCodigo, string? fatoExtra = null,
        Guid? tipoDocumentoOrigemId = null, string fatoDeModalidade = "MODALIDADE") =>
        new DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(tipoDocumentoOrigemId ?? Guid.CreateVersion7(), tipoDocumentoCodigo, "Documento de teste", "CATEGORIA", ProtecaoDoDocumentoDeTeste.Pessoal))
            .NaFase(exigidoNaFaseId)
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, CondicoesPorModalidade(modalidadeCodigo, fatoExtra, fatoDeModalidade)))
            .Construir();

    private static List<CondicaoGatilho> CondicoesPorModalidade(string modalidadeCodigo, string? fatoExtra, string fatoDeModalidade)
    {
        List<CondicaoGatilho> condicoes =
        [
            CondicaoGatilho.Criar(0, fatoDeModalidade, Operador.Igual, JsonSerializer.SerializeToElement(modalidadeCodigo)).Value!,
        ];
        if (fatoExtra is not null)
        {
            condicoes.Add(CondicaoGatilho.Criar(0, fatoExtra, Operador.Igual, JsonSerializer.SerializeToElement("QUALQUER")).Value!);
        }

        return condicoes;
    }

    /// <summary>
    /// Cadastro vivo deduzido do próprio processo: cada código resolve para a identidade que o processo
    /// congelou. É o cenário do caminho feliz — regra e processo apontando para o mesmo item. Os testes de
    /// renomeação e de reciclagem sobrescrevem a entrada que interessa.
    /// </summary>
    internal static IdentidadesDeCadastro IdentidadesDe(ProcessoSeletivo processo) =>
        new(
            MapaDe(processo.DocumentosExigidos, e => e.TipoDocumentoCodigo, e => e.TipoDocumentoOrigemId),
            MapaDe(processo.DistribuicaoVagas.SelectMany(d => d.Modalidades), m => m.Codigo, m => m.ModalidadeOrigemId),
            MapaDe(processo.Etapas, e => e.TipoEtapa.Codigo, e => e.TipoEtapa.OrigemId),
            MapaDe(
                processo.AtendimentoEspecializado?.TiposDeficiencia ?? [],
                t => t.TipoDeficienciaCodigo,
                t => t.TipoDeficienciaOrigemId));

    private static Dictionary<string, Guid> MapaDe<T>(IEnumerable<T> itens, Func<T, string> codigoDe, Func<T, Guid> origemDe) =>
        itens
            .GroupBy(codigoDe, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => origemDe(g.First()), StringComparer.Ordinal);
}
