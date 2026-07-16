namespace Unifesspa.UniPlus.Selecao.ArchTests;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Agregado mínimo e conforme, só para que os codecs tenham o que codificar nos fitness
/// tests. O corpus <b>rico</b> — o que prova a reidratação — vive nos testes de
/// integração; aqui o que se verifica é a <b>forma que o codec emite</b>, não o conteúdo.
/// </summary>
internal static class CorpusFitness
{
    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    internal static EntradaCanonicalizacao Entrada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Fitness", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente);

        // Cronograma mínimo (Story #851): UMA fase satisfaz as duas exigências do corpus —
        // AgrupaEtapas (há etapa acima) e ProduzResultado (há vagas ofertadas abaixo).
        processo.DefinirCronogramaFases([
            FaseCronograma.Criar(
                ordem: 1,
                faseCanonicaOrigemId: new Guid("44444444-4444-4444-4444-444444444444"),
                codigo: "RESULTADO_FINAL",
                donoInstitucional: "CEPS",
                origemData: OrigemDataFase.Propria,
                agrupaEtapas: true,
                permiteComplementacao: false,
                produzResultado: true,
                resultadoDefinitivo: true,
                coletaInscricao: false,
                inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                atoProduzidoCodigo: "RESULTADO_FINAL",
                atoProduzidoEfeitoIrreversivel: false,
                bancasRequeridas: [],
                regraRecurso: null).Value!,
        ], [], PrecondicaoIfMatch.Ausente);

        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        processo.DefinirDistribuicaoVagas([
            ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: new Guid("11111111-1111-1111-1111-111111111111"),
                voBase: 40,
                pr: 1m,
                regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
                referenciaDemografica: null,
                modalidades: [
                    ModalidadeSelecionada.Criar(
                        new Guid("22222222-2222-2222-2222-222222222222"), "AC", null,
                        NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                        RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                        [], null, "Res. Unifesspa 532/2021").Value!,
                ]).Value!,
        ], PrecondicaoIfMatch.Ausente);

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!, PrecondicaoIfMatch.Ausente);

        DadosEdital dados = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: new Guid("33333333-3333-3333-3333-333333333333")).Value!;

        return new EntradaCanonicalizacao(processo, dados, new string('a', 64));
    }
}
