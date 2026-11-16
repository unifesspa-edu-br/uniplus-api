namespace Unifesspa.UniPlus.Selecao.ArchTests;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Agregado mínimo e conforme, só para que os codecs tenham o que codificar nos fitness
/// tests. O corpus <b>rico</b> — o que prova a reidratação — vive nos testes de
/// integração; aqui o que se verifica é a <b>forma que o codec emite</b>, não o conteúdo.
/// </summary>
internal static class CorpusFitness
{
    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    internal static EntradaCanonicalizacao Entrada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Fitness", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ], PrecondicaoIfMatch.Ausente);

        // Cronograma mínimo (Story #851): UMA fase satisfaz as duas exigências do corpus —
        // AgrupaEtapas (há etapa acima) e ProduzResultado (há vagas ofertadas abaixo).
        processo.DefinirCronogramaFases([
            FaseCronograma.Criar(
                new OrigemCanonicaDaFase(
                    OrigemId: new Guid("44444444-4444-4444-4444-444444444444"), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
                    ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
                new DecisoesDeCronogramaDaFase(
                    Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
                    UnidadeResponsavel: UnidadesDeTeste.Ceps),
                new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
                bancasRequeridas: []).Value!,
        ], [], PrecondicaoIfMatch.Ausente);

        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        processo.DefinirDistribuicaoVagas([
            ConfiguracaoDistribuicaoVagas.Criar(new Guid("11111111-1111-1111-1111-111111111111"),
                new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [
                    ModalidadeSelecionada.Criar(
                        new Guid("22222222-2222-2222-2222-222222222222"), "AC", null,
                        NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null,
                        RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                        RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
                ] },
                new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!,
        ], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente);

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
        }).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirOpcoesDeCurso(1, PrecondicaoIfMatch.Ausente);

        DadosEdital dados = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            documentoEditalId: new Guid("33333333-3333-3333-3333-333333333333")).Value!;

        return new EntradaCanonicalizacao(processo, dados, new string('a', 64), FusoInstitucional.ZoneId);
    }
}
