namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As declarações que o processo exige para publicar além da taxa: o FADA (aplica ou
/// não), o número de opções de curso e, com inscrição própria, ao menos um critério de desempate. Para fixtures que precisam
/// de um processo publicável sem que essas dimensões sejam o assunto.
/// </summary>
internal static class DeclaracoesObrigatoriasDeTeste
{
    /// <summary>
    /// Declara "não aplica o FADA" e um critério de desempate, só onde falta: o que o teste já
    /// configurou fica como está. Os parâmetros deixam uma das três por declarar, para provar a
    /// recusa correspondente. O número de opções declarado é 1, que cabe em qualquer distribuição.
    /// </summary>
    public static void Declarar(ProcessoSeletivo processo, bool fada = true, bool desempate = true, bool opcoesDeCurso = true)
    {
        ArgumentNullException.ThrowIfNull(processo);
        DeclararProvaPresencial(processo);
        DeclararImportacaoDoResultado(processo);

        DeclararOpcoesDeCurso(processo, opcoesDeCurso);
        DeclararFada(processo, fada);
        if (desempate)
        {
            DeclararDesempate(processo);
        }
    }

    private static void DeclararOpcoesDeCurso(ProcessoSeletivo processo, bool opcoesDeCurso)
    {
        if (opcoesDeCurso && processo.NumeroDeOpcoesDeCurso is null)
        {
            ExigirSucesso(processo.DefinirOpcoesDeCurso(1));
        }
    }

    private static void DeclararFada(ProcessoSeletivo processo, bool fada)
    {
        if (fada && processo.AplicaFada is null)
        {
            ExigirSucesso(processo.DefinirFada(aplica: false, configuracao: null));
        }
    }

    private static void DeclararDesempate(ProcessoSeletivo processo)
    {
        if (processo.OrigemCandidatos == OrigemCandidatos.InscricaoPropria && processo.CriteriosDesempate.Count == 0)
        {
            ReferenciaRegra regra = ReferenciaRegra.De(RegraDeDesempate.MaiorIdade.Codigo(), "v1", new string('f', 64));
            CriterioDesempate criterio = CriterioDesempate.Criar(1, regra, new ParametrosDesempateMaiorIdade()).Value!;
            ExigirSucesso(processo.DefinirCriteriosDesempate([criterio]));
        }
    }

    private static void DeclararProvaPresencial(ProcessoSeletivo processo)
    {
        if (processo.ProvaPresencial is null)
        {
            ExigirSucesso(processo.DefinirProvaPresencial(false, [], 100));
        }
    }

    /// <summary>
    /// O processo de resultado importado declara a importação na primeira fase do cronograma e a coleta com um
    /// formulário obrigatório, quando o cronograma já tem fase e a importação ainda não foi declarada.
    /// </summary>
    internal static void DeclararImportacaoDoResultado(ProcessoSeletivo processo)
    {
        if (processo.OrigemCandidatos != OrigemCandidatos.ImportacaoExterna || processo.ImportacaoDoResultado is not null || processo.CronogramaFases.Count == 0)
        {
            return;
        }

        Guid faseId = processo.CronogramaFases.OrderBy(static fase => fase.Ordem).First().Id;
        ExigirSucesso(processo.DefinirImportacaoDoResultado([new RelatorioInformado("RELATORIO_DE_TESTE", "v1")], faseId));
        if (processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao) is null)
        {
            ExigirSucesso(processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, faseId, null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.Habilitacao)));
        }
    }

    /// <summary>
    /// Tira o formulário e a declaração da importação que dependem de uma fase que o cronograma novo não tem,
    /// para o cronograma poder ser trocado; <see cref="DeclararImportacaoDoResultado"/> os repõe depois.
    /// </summary>
    internal static void DesfazerImportacaoDaFaseQueSai(ProcessoSeletivo processo, IReadOnlyList<FaseCronograma> fasesNovas)
    {
        if (processo.ImportacaoDoResultado?.PrimeiraFaseDepoisDaImportacaoId is not { } faseId || fasesNovas.Any(fase => fase.Id == faseId))
        {
            return;
        }

        if (processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)?.FaseId == faseId)
        {
            ExigirSucesso(processo.RemoverFormulario(PapelDoFormularioDeTeste.Habilitacao));
        }

        ExigirSucesso(processo.DefinirImportacaoDoResultado([], null));
    }

    private static void ExigirSucesso(Unifesspa.UniPlus.Kernel.Results.Result resultado)
    {
        if (resultado.IsFailure)
        {
            throw new InvalidOperationException($"Fixture inválida: {resultado.Error?.Message}");
        }
    }
}
