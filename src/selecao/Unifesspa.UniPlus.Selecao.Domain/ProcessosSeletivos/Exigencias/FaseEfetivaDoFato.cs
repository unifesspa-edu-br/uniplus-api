namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class FaseEfetivaDoFato
{
    /// <summary>
    /// A recusa de citar o fato numa condição de aplicação de exigência da fase dada (UNI-REQ-0144, UNI-REQ-0077):
    /// nenhum documento é pedido antes de o fato ser conhecido. O fato fica conhecido na mais
    /// tardia entre a fase do catálogo (<paramref name="pontoResolucaoPorFato"/>) e a fase do
    /// formulário que o produz; o derivado por regra, não antes das suas dependências; e o agregado
    /// sobre grupo repetível (<paramref name="membroPorAgregado"/>), não antes do fato de membro. Fato
    /// produzido só pelo formulário de isenção só é citado por documento do formulário de isenção: a
    /// habilitação usa, de outra finalidade, só fatos da inscrição. Inscrição e isenção podem dividir
    /// a fase, e por isso a regra olha a finalidade da exigência, e não a fase.
    /// </summary>
    /// <param name="publicadas">
    /// O que as versões publicadas coletaram, na retificação: o fato de outra finalidade tem de ter sido
    /// coletado em todas elas. Nula na escrita da exigência e na primeira publicação.
    /// </param>
    /// <remarks>
    /// Fato que o processo não coleta, não deriva e o catálogo não situa não é recusado aqui: a
    /// recusa por fato fora do processo é da conferência do universo. O derivado do sistema usa a
    /// fase do catálogo e a das dependências declaradas pelo mecanismo dele. A finalidade que falta
    /// ou que a fase não responde também não é recusada aqui: a recusa que orienta é a da própria
    /// finalidade, que a definição das exigências confere.
    /// </remarks>
    public static DomainError? RecusaDeFaseDoGatilho(
        ConfiguracaoDeclarada configuracao,
        string fato,
        Guid exigidoNaFaseId,
        FinalidadeFormulario? finalidadeDaExigencia,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        FatosDasVersoesPublicadas? publicadas = null)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentException.ThrowIfNullOrWhiteSpace(fato);
        ArgumentNullException.ThrowIfNull(pontoResolucaoPorFato);
        ArgumentNullException.ThrowIfNull(membroPorAgregado);

        if (FaseDaExigenciaQueAdmiteAFinalidade(configuracao, exigidoNaFaseId, finalidadeDaExigencia) is not { } faseDaExigencia)
        {
            return null;
        }

        return RecusaDaFaseEfetiva(configuracao, fato, faseDaExigencia, pontoResolucaoPorFato, membroPorAgregado)
            ?? RecusaDoFatoDaIsencaoEmOutraFinalidade(configuracao, fato, finalidadeDaExigencia, membroPorAgregado)
            ?? RecusaDaCitacaoNaoGarantida(configuracao, fato, faseDaExigencia, finalidadeDaExigencia, membroPorAgregado, publicadas);
    }

    private static FaseCronograma? FaseDaExigenciaQueAdmiteAFinalidade(
        ConfiguracaoDeclarada configuracao, Guid exigidoNaFaseId, FinalidadeFormulario? finalidadeDaExigencia) =>
        configuracao.Cronograma.CronogramaFases.FirstOrDefault(f => f.Id == exigidoNaFaseId) is { } fase
            && Services.ValidadorVinculoDaExigencia.RecusaDaFinalidade(finalidadeDaExigencia, fase) is null
                ? fase
                : null;

    private static DomainError? RecusaDaFaseEfetiva(
        ConfiguracaoDeclarada configuracao,
        string fato,
        FaseCronograma faseDaExigencia,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        OrdemDoCronograma ordem = OrdemDoCronograma.Do(configuracao.Cronograma.CronogramaFases);
        FaseConhecida conhecida = FaseEmQueOFatoFicaConhecido.Resolver(
            new OrigensDoFatoNaConfiguracaoDeclarada(configuracao, pontoResolucaoPorFato, membroPorAgregado), ordem, fato);

        return FaseEmQueOFatoFicaConhecido.RecusaDeFasePosterior(fato, conhecida, faseDaExigencia, ordem);
    }

    private static DomainError? RecusaDoFatoDaIsencaoEmOutraFinalidade(
        ConfiguracaoDeclarada configuracao,
        string fato,
        FinalidadeFormulario? finalidadeDaExigencia,
        IReadOnlyDictionary<string, string> membroPorAgregado) =>
        finalidadeDaExigencia != FinalidadeFormulario.IsencaoTaxa && DependeDeFatoSoDaIsencao(configuracao, fato, membroPorAgregado, [])
            ? new DomainError(
                DocumentoExigidoErrorCodes.FatoDaIsencaoEmOutraFinalidade,
                $"O fato '{fato}' vem do formulário de isenção e só é citado por documento exigido no formulário de isenção.")
            : null;

    /// <summary>
    /// As exigências de uma finalidade seguem a versão do formulário dela; o fato de outra finalidade tem de ter sido
    /// coletado em todas as versões publicadas (UNI-REQ-0144).
    /// </summary>
    private static DomainError? RecusaDaCitacaoNaoGarantida(
        ConfiguracaoDeclarada configuracao,
        string fato,
        FaseCronograma faseDaExigencia,
        FinalidadeFormulario? finalidadeDaExigencia,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        FatosDasVersoesPublicadas? publicadas) =>
        publicadas is null
            ? null
            : CitacoesDosFormularios.CitacaoNaoGarantida(
                configuracao,
                publicadas,
                [($"a condição de aplicação de um documento exigido na fase '{faseDaExigencia.Codigo}'", fato)],
                finalidadeDaExigencia,
                membroPorAgregado);

    /// <summary>Se o fato vem só do formulário de isenção, diretamente ou por uma dependência.</summary>
    internal static bool DependeDeFatoSoDaIsencao(ConfiguracaoDeclarada configuracao, string fato, IReadOnlyDictionary<string, string> membroPorAgregado, HashSet<string> emAvaliacao) =>
        configuracao.Coleta.Campos.Any(f => f.Finalidade == FinalidadeFormulario.IsencaoTaxa && string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal))
        || (emAvaliacao.Add(fato)
            && DependenciasDoFato(configuracao, fato, membroPorAgregado).Any(d => DependeDeFatoSoDaIsencao(configuracao, d, membroPorAgregado, emAvaliacao)));

    /// <summary>
    /// Os fatos de que o valor do fato depende no processo: os citados pela regra de derivação dele,
    /// os declarados pelo mecanismo do derivado do sistema, ou o fato de membro, quando ele é
    /// agregado sobre grupo repetível.
    /// </summary>
    internal static IEnumerable<string> DependenciasDoFato(ConfiguracaoDeclarada configuracao, string fato, IReadOnlyDictionary<string, string> membroPorAgregado) =>
        (configuracao.DerivacoesEfetivas().FirstOrDefault(r => string.Equals(r.CodigoFato, fato, StringComparison.Ordinal))?.FatosCitados ?? [])
            .Concat(DerivadosDoSistema.Dependencias.GetValueOrDefault(fato) ?? [])
            .Concat(membroPorAgregado.TryGetValue(fato, out string? membro) ? [membro] : []);
}
