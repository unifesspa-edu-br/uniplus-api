namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A resolução de Pesos por Área que a classificação cita e o quadro dela, lido dos cadastros: só a classificação que calcula pelos
/// pesos por área consulta o quadro, e as outras levam a resolução como veio, para a fábrica da classificação recusá-la como indevida.
/// Sem a fórmula da nota confirmada no catálogo não se sabe se o quadro se aplica, e nada se consulta.
/// </summary>
internal sealed record LeituraDoQuadroDePesos(
    string? Resolucao, IReadOnlyList<GrupoPesoAreaEnemCongelado> Grupos, IReadOnlyList<FieldError> Recusas)
{
    internal static LeituraDoQuadroDePesos Ler(
        ReferenciaRegra? regraCalculo, bool baseadoEmEnem, string? resolucao, CadastrosCitadosNoDocumento cadastros)
    {
        if (regraCalculo is null
            || !ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem(regraCalculo, baseadoEmEnem)
            || string.IsNullOrWhiteSpace(resolucao))
        {
            return new(resolucao, [], []);
        }

        List<FieldError> forma = ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(resolucao);

        return forma.Count > 0 ? new(resolucao, [], forma) : DoCadastro(resolucao, cadastros.QuadroDePesos(resolucao));
    }

    /// <summary>
    /// As recusas da conferência dos campos, sem as que são consequência de a resolução já ter sido recusada, e depois as
    /// do cadastro de Pesos por Área: a consulta externa vem depois dos campos (ADR-0125).
    /// </summary>
    internal IReadOnlyList<FieldError> RecusasJuntoCom(IReadOnlyList<FieldError> dosCampos) =>
    [
        .. Recusas.Count == 0 ? dosCampos : ConfiguracaoClassificacao.SemConsequenciasDaResolucaoRecusada(dosCampos),
        .. Recusas,
    ];

    private static LeituraDoQuadroDePesos DoCadastro(string resolucao, QuadroDePesosCitado? quadro) => quadro switch
    {
        null => Recusado(resolucao, "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada", "A resolução de Pesos por Área informada não tem pesos cadastrados."),
        { Completo: false } => Recusado(
            resolucao,
            "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIncompleta",
            $"A resolução de Pesos por Área não tem pesos cadastrados para: {string.Join(", ", quadro.GruposAusentes)}."),
        _ => new(resolucao, quadro.Grupos, []),
    };

    private static LeituraDoQuadroDePesos Recusado(string resolucao, string codigo, string mensagem) =>
        new(resolucao, [], [new FieldError(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError(codigo, mensagem))]);
}
