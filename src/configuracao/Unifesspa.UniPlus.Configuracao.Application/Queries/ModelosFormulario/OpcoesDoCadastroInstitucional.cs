namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As opções da condição de atendimento e do tipo de deficiência quando ainda não há processo. No
/// processo, elas são as que a oferta de atendimento especializado dele declara; no modelo, a simulação
/// oferece o cadastro institucional vivo, na ordem do código, como a oferta as ordena.
/// </summary>
internal static class OpcoesDoCadastroInstitucional
{
    private const string FatoCondicaoAtendimento = "CONDICAO_ATENDIMENTO";
    private const string FatoTipoDeficiencia = "TIPO_DEFICIENCIA";

    /// <summary>Preenche as opções dos dois campos que o modelo coleta e que o catálogo deixou sem opções.</summary>
    public static async Task CompletarAsync(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes,
        ICondicaoAtendimentoRepository condicaoRepository,
        ITipoDeficienciaRepository tipoDeficienciaRepository,
        CancellationToken cancellationToken)
    {
        if (opcoes.TryGetValue(FatoCondicaoAtendimento, out IReadOnlyList<ValorSelecionavel>? condicoes) && condicoes is null)
        {
            IReadOnlyList<CondicaoAtendimentoEspecializado> cadastro =
                await condicaoRepository.ListarVivosParaLeituraAsync(cancellationToken).ConfigureAwait(false);
            opcoes[FatoCondicaoAtendimento] = [.. cadastro
                .OrderBy(static c => c.Codigo.Valor, StringComparer.Ordinal)
                .Select(static (c, ordem) => new ValorSelecionavel(c.Codigo.Valor, c.Nome, ordem))];
        }

        if (opcoes.TryGetValue(FatoTipoDeficiencia, out IReadOnlyList<ValorSelecionavel>? tipos) && tipos is null)
        {
            IReadOnlyList<TipoDeficiencia> cadastro =
                await tipoDeficienciaRepository.ListarVivosParaLeituraAsync(cancellationToken).ConfigureAwait(false);
            opcoes[FatoTipoDeficiencia] = [.. cadastro
                .OrderBy(static t => t.Codigo.Valor, StringComparer.Ordinal)
                .Select(static (t, ordem) => new ValorSelecionavel(t.Codigo.Valor, t.Nome, ordem))];
        }
    }
}
