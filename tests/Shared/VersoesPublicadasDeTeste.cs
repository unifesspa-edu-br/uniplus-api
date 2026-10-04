namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As versões publicadas de um processo, para os testes que abrem a retificação sem passar pelo
/// envelope: só o que diz que fatos cada formulário coletou — os formulários, os itens e os grupos.
/// As demais dimensões do grafo são mínimas, porque a leitura dos fatos não as consulta.
/// </summary>
internal static class VersoesPublicadasDeTeste
{
    private static readonly ConfiguracaoClassificacao ClassificacaoMinima = ConfiguracaoClassificacao.Criar(
        regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
        regraArredondamento: null,
        casasArredondamento: null,
        regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
        nOpcoesAlocacao: 1,
        regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!;

    /// <summary>Uma versão publicada com estes formulários, itens e grupos.</summary>
    public static GrafoConfiguracao Versao(
        IEnumerable<FormularioProcesso> formularios, IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado>? grupos = null) => new(
        [], OfertaAtendimentoEspecializado.Criar([], [], []).Value!, [], null, [], ClassificacaoMinima, [], [], [], null,
        fatosColetados: [.. itens],
        formularios: [.. formularios],
        gruposColetados: [.. grupos ?? []]);

    /// <summary>A versão que o processo publicaria agora, com os formulários, os itens e os grupos que ele tem.</summary>
    public static GrafoConfiguracao DoProcesso(ProcessoSeletivo processo) =>
        Versao(processo.Formularios, processo.FatosColetados, processo.GruposColetados);

    /// <summary>O processo publicado uma vez só, com o que ele tem agora.</summary>
    public static FatosDasVersoesPublicadas SoAVigente(ProcessoSeletivo processo) =>
        FatosDasVersoesPublicadas.De([DoProcesso(processo)]);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;
}
