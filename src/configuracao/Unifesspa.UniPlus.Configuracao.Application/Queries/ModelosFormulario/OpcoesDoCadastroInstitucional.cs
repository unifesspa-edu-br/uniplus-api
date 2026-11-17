namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As opções que o processo oferta, quando ainda não há processo. No processo, a condição de atendimento
/// e o recurso de acessibilidade são os que o atendimento especializado dele declara, e os
/// municípios do bônus, os da base legal que ele adota; no modelo, a simulação oferece o cadastro
/// institucional vivo — os municípios de todas as bases legais, sem repetir —, na mesma ordem que o
/// processo usa. O tipo de deficiência é o cadastro vivo também no processo.
/// </summary>
public sealed class OpcoesDoCadastroInstitucional(
    ICondicaoAtendimentoRepository condicaoRepository,
    IRecursoAcessibilidadeRepository recursoRepository,
    ITipoDeficienciaRepository tipoDeficienciaRepository,
    IBaseLegalBonusRegionalRepository baseLegalBonusRepository)
{
    private const string FatoCondicaoAtendimento = "CONDICAO_ATENDIMENTO";
    private const string FatoRecursoAcessibilidade = "RECURSO_ACESSIBILIDADE";

    /// <summary>
    /// Preenche as opções que o catálogo deixou nulas e que o cadastro institucional supre: condição de
    /// atendimento, recurso de acessibilidade, tipo de deficiência e municípios do bônus.
    /// </summary>
    public async Task CompletarAsync(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes,
        IReadOnlyList<FatoCandidato> fatos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(fatos);

        await CompletarMunicipiosDoBonusAsync(opcoes, fatos, cancellationToken).ConfigureAwait(false);
        await CompletarCondicaoDeAtendimentoAsync(opcoes, cancellationToken).ConfigureAwait(false);
        await CompletarRecursoDeAcessibilidadeAsync(opcoes, cancellationToken).ConfigureAwait(false);
        await CompletarTiposDeDeficienciaAsync(opcoes, fatos, cancellationToken).ConfigureAwait(false);
    }

    private async Task CompletarMunicipiosDoBonusAsync(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, IReadOnlyList<FatoCandidato> fatos, CancellationToken cancellationToken)
    {
        string[] campos = CamposDaFonte(opcoes, fatos, FonteValoresFato.MunicipiosBonus);
        if (campos.Length == 0)
        {
            return;
        }

        IReadOnlyList<BaseLegalBonusRegional> bases =
            await baseLegalBonusRepository.ListarVivasParaLeituraAsync(cancellationToken).ConfigureAwait(false);
        Preencher(opcoes, campos, MunicipiosDasBases(bases));
    }

    private async Task CompletarCondicaoDeAtendimentoAsync(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, CancellationToken cancellationToken)
    {
        if (!SemOpcoes(opcoes, FatoCondicaoAtendimento))
        {
            return;
        }

        IReadOnlyList<CondicaoAtendimentoEspecializado> cadastro =
            await condicaoRepository.ListarVivosParaLeituraAsync(cancellationToken).ConfigureAwait(false);
        opcoes[FatoCondicaoAtendimento] = [.. cadastro
            .OrderBy(static c => c.Codigo.Valor, StringComparer.Ordinal)
            .Select(static (c, ordem) => new ValorSelecionavel(c.Codigo.Valor, c.Nome, ordem))];
    }

    private async Task CompletarRecursoDeAcessibilidadeAsync(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, CancellationToken cancellationToken)
    {
        if (!SemOpcoes(opcoes, FatoRecursoAcessibilidade))
        {
            return;
        }

        IReadOnlyList<RecursoAcessibilidade> cadastro =
            await recursoRepository.ListarVivosParaLeituraAsync(cancellationToken).ConfigureAwait(false);
        opcoes[FatoRecursoAcessibilidade] = [.. cadastro
            .OrderBy(static r => r.Codigo.Valor, StringComparer.Ordinal)
            .Select(static (r, ordem) => new ValorSelecionavel(r.Codigo.Valor, r.Nome, ordem))];
    }

    private async Task CompletarTiposDeDeficienciaAsync(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, IReadOnlyList<FatoCandidato> fatos, CancellationToken cancellationToken)
    {
        string[] campos = CamposDaFonte(opcoes, fatos, FonteValoresFato.TiposDeficiencia);
        if (campos.Length == 0)
        {
            return;
        }

        IReadOnlyList<TipoDeficiencia> cadastro =
            await tipoDeficienciaRepository.ListarVivosParaLeituraAsync(cancellationToken).ConfigureAwait(false);
        Preencher(opcoes, campos, [.. cadastro
            .OrderBy(static t => t.Codigo.Valor, StringComparer.Ordinal)
            .Select(static (t, ordem) => new ValorSelecionavel(t.Codigo.Valor, t.Nome, ordem))]);
    }

    private static ValorSelecionavel[] MunicipiosDasBases(IReadOnlyList<BaseLegalBonusRegional> bases) =>
        [.. bases
            .SelectMany(static b => b.Municipios)
            .DistinctBy(static m => m.CodigoIbge, StringComparer.Ordinal)
            .OrderBy(static m => OrdemAlfabetica.Chave(m.Nome), StringComparer.Ordinal)
            .ThenBy(static m => m.Nome, StringComparer.Ordinal)
            .ThenBy(static m => m.CodigoIbge, StringComparer.Ordinal)
            .Select(static (m, ordem) => new ValorSelecionavel(m.CodigoIbge, $"{m.Nome}/{m.Uf}", ordem))];

    private static string[] CamposDaFonte(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, IReadOnlyList<FatoCandidato> fatos, FonteValoresFato fonte) =>
        [.. fatos.Where(f => f.FonteValores == fonte && SemOpcoes(opcoes, f.Codigo)).Select(static f => f.Codigo)];

    private static bool SemOpcoes(Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, string campo) =>
        opcoes.TryGetValue(campo, out IReadOnlyList<ValorSelecionavel>? atuais) && atuais is null;

    private static void Preencher(
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, string[] campos, IReadOnlyList<ValorSelecionavel> valores)
    {
        foreach (string campo in campos)
        {
            opcoes[campo] = valores;
        }
    }
}
