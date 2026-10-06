namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As opções que o processo oferta, quando ainda não há processo. No processo, a condição de atendimento
/// e o tipo de deficiência são os que a oferta de atendimento especializado dele declara, e os municípios
/// do bônus, os da base legal que ele adota; no modelo, a simulação oferece o cadastro institucional vivo
/// — os municípios de todas as bases legais, sem repetir —, na mesma ordem que o processo usa.
/// </summary>
internal static class OpcoesDoCadastroInstitucional
{
    private const string FatoCondicaoAtendimento = "CONDICAO_ATENDIMENTO";
    private const string FatoTipoDeficiencia = "TIPO_DEFICIENCIA";

    /// <summary>
    /// Preenche as opções que o catálogo deixou nulas e que o cadastro institucional supre: condição de
    /// atendimento, tipo de deficiência e municípios do bônus.
    /// </summary>
    public static async Task CompletarAsync(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes,
        IReadOnlyList<FatoCandidato> fatos,
        ICondicaoAtendimentoRepository condicaoRepository,
        ITipoDeficienciaRepository tipoDeficienciaRepository,
        IBaseLegalBonusRegionalRepository baseLegalBonusRepository,
        CancellationToken cancellationToken)
    {
        string[] camposDoBonus = [.. fatos
            .Where(f => f.FonteValores == FonteValoresFato.MunicipiosBonus && opcoes.TryGetValue(f.Codigo, out IReadOnlyList<ValorSelecionavel>? atuais) && atuais is null)
            .Select(static f => f.Codigo)];
        if (camposDoBonus.Length > 0)
        {
            IReadOnlyList<BaseLegalBonusRegional> bases =
                await baseLegalBonusRepository.ListarVivasParaLeituraAsync(cancellationToken).ConfigureAwait(false);
            ValorSelecionavel[] municipios = [.. bases
                .SelectMany(static b => b.Municipios)
                .DistinctBy(static m => m.CodigoIbge, StringComparer.Ordinal)
                .OrderBy(static m => OrdemAlfabetica.Chave(m.Nome), StringComparer.Ordinal)
                .ThenBy(static m => m.Nome, StringComparer.Ordinal)
                .ThenBy(static m => m.CodigoIbge, StringComparer.Ordinal)
                .Select(static (m, ordem) => new ValorSelecionavel(m.CodigoIbge, $"{m.Nome}/{m.Uf}", ordem))];
            foreach (string campo in camposDoBonus)
            {
                opcoes[campo] = municipios;
            }
        }

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
