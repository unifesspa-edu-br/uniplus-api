namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O processo, as etapas e as conferências de banca que os testes da exigência de banca de recursos e do mínimo de integrantes
/// montam do mesmo jeito.
/// </summary>
internal static class ApoioDasBancasDoProcesso
{
    internal static readonly Guid Tipo = new("0197d6f0-0000-7000-8000-00000000b4c1");

    internal static RegraRecursoFase RegraDeRecurso() => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)),
        ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.DiasUteis),
        ProcessoConformeFactory.ProdutoPreliminarId).Value!;

    internal static readonly ReferenciaRegra CorrecaoIndependente =
        ReferenciaRegra.De(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", new string('d', 64));

    internal static readonly ReferenciaRegra MaioriaSimples =
        ReferenciaRegra.De(RegraConsolidacaoCodigo.ConceitoMaioriaSimples, "v1", new string('f', 64));

    internal static readonly ReferenciaRegra AvaliacaoUnica =
        ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('e', 64));

    internal static EtapaProcesso NovaEtapa(string nome, int ordem, ReferenciaRegra? consolidacao = null) => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: "RESULTADO_FINAL"),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: consolidacao is null ? FormaDeAvaliacao.Objetiva : FormaDeAvaliacao.Humana, RegraConsolidacao: consolidacao, EscalaDeAtendimento: null)).Value!;

    /// <summary>Dá à etapa o par preliminar e definitivo e um recurso cujo prazo conta do preliminar.</summary>
    internal static EtapaProcesso ComRecurso(EtapaProcesso etapa)
    {
        etapa.DefinirProdutos(
        [
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
        Guid preliminar = etapa.Produtos.Single(p => p.Papel == PapelProdutoFase.Preliminar).Id;
        etapa.DefinirRecursos([RecursoDaEtapa.Criar(
            AncoraDoRecurso.AtoPublicado, RegraDeRecurso().Regra, ParametrosDoRecursoDeTeste.DeclaradosDe(RegraDeRecurso().Parametros), preliminar).Value!]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    internal static ProcessoSeletivo ProcessoComEtapas(IReadOnlyList<BancaRequerida>? bancasDaFase, params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(bancas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), .. bancasDaFase ?? []]));
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();
        return processo;
    }

    internal static BancaDaEtapa DeRecursosDaEtapa(int integrantes, int quorum) =>
        BancasDeTeste.DeRecursosDaEtapa(BancasDeTeste.Regra(integrantes, quorum));

    internal static BancaRequerida DeRecursosDaFase(int integrantes, int quorum) =>
        BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.DeRecursos(Tipo), [], BancasDeTeste.Regra(integrantes, quorum));

    internal static CorretoresDasEtapas Corretores(params (ReferenciaRegra Regra, int Numero)[] porRegra) =>
        new(porRegra.ToDictionary(static par => par.Regra, static par => CorretoresDaRegra.SemNovoCorretor(par.Numero)));

    internal static DomainError? Pendencia(ProcessoSeletivo processo, CorretoresDasEtapas? corretores = null) =>
        QuemJulgaORecurso.SemBanca(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas))
            .Concat(CronogramaDoProcesso.Do(processo.CronogramaFases).RecusasDasDeclaracoes(ExigenciaDeBancaDeRecurso.CodigosDosCandidatosAtendidos))
            .Concat(ConferenciaDasBancas.ConferirRegrasDeDeliberacao(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas)))
            .Concat(Minimo(processo, corretores ?? CorretoresDeTeste.SemConsolidacao))
            .Select(static causa => causa.Error)
            .FirstOrDefault();

    internal static IEnumerable<string> CodigosDaConferencia(ProcessoSeletivo processo, CorretoresDasEtapas corretores) =>
        ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Com(corretores: corretores, motivosEscolhidos: MotivosDeDecisaoDoProcessoDeTeste.Cobrindo(processo)).ComRegrasDo(processo)).Errors
            .Where(static causa => causa.Error.Code != "ProcessoSeletivo.ConformidadeInsuficiente"
                && causa.Field?.StartsWith("identificacao", StringComparison.Ordinal) != true)
            .Select(static causa => causa.Error.Code);

    internal static IReadOnlyList<ItemConformidade> Itens(ProcessoSeletivo processo, CorretoresDasEtapas corretores) =>
        ConferenciaDasBancas.Itens(processo.OrigemCandidatos, CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas), corretores);

    internal static IReadOnlyList<FieldError> Minimo(ProcessoSeletivo processo, CorretoresDasEtapas corretores) =>
        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas), corretores);
}
