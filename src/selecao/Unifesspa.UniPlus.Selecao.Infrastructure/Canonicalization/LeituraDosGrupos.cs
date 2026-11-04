namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura dos grupos de coleta repetíveis, com seus campos e agregados.
/// </summary>
internal static class LeituraDosGrupos
{
    /// <summary>
    /// Os grupos repetíveis (UNI-REQ-0146), reconstruídos por <see cref="GrupoColetado.Criar"/>, que
    /// revalida a forma; os campos de cada um são lidos como os itens, e os valores selecionáveis
    /// deles entram no mesmo dicionário dos itens. Os agregados sobre cada grupo entram em
    /// <paramref name="agregados"/>: cada um agrega um campo do próprio grupo, na ordem do código.
    /// </summary>
    internal static IReadOnlyList<GrupoColetado> LerGruposColetados(
        LeitorEnvelope leitor,
        JsonObject payload,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis,
        List<DefinicaoAgregado> agregados)
    {
        JsonArray array = leitor.Array(payload, "gruposColetados", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<GrupoColetado> grupos = [];
        for (int i = 0; i < array.Count; i++)
        {
            GrupoColetado? grupo = LerGrupo(
                leitor, leitor.ItemObjeto(array, i, "gruposColetados"), $"gruposColetados[{i}]", valoresSelecionaveis, agregados);
            if (grupo is null)
            {
                return [];
            }

            grupos.Add(grupo);
        }

        return grupos;
    }

    private static GrupoColetado? LerGrupo(
        LeitorEnvelope leitor,
        JsonObject item,
        string path,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis,
        List<DefinicaoAgregado> agregados)
    {
        leitor.ExigirChaves(
            item, path, "codigo", "finalidade", "etapaCodigo", "ordem", "rotulo", "minimo", "maximo", "incluiCandidato", "exibicao", "obrigatoriedade", "subitens", "agregados");

        DeclaracaoDoGrupo d = LerDeclaracaoDoGrupo(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (d.Finalidade == FinalidadeFormulario.Nenhuma)
        {
            return leitor.Recusar<GrupoColetado?>($"'{path}.finalidade' fora do vocabulário de finalidades.");
        }

        GrupoColetado? grupo = CriarGrupo(leitor, d, item, path, valoresSelecionaveis);
        return grupo is not null && LerAgregadosDoGrupo(leitor, item, path, grupo, agregados) ? grupo : null;
    }

    private static GrupoColetado? CriarGrupo(
        LeitorEnvelope leitor,
        DeclaracaoDoGrupo d,
        JsonObject item,
        string path,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis)
    {
        Result<PredicadoDnf?> exibicaoLida = LeituraDasCondicoes.PredicadoOpcional(d.Exibicao);
        if (exibicaoLida.IsFailure)
        {
            return leitor.Propagar<GrupoColetado?>(exibicaoLida.Error!);
        }

        if (LeituraDasCondicoes.LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade)
        {
            return null;
        }

        IReadOnlyList<FatoColetado> campos = LeituraDosFatos.LerListaDeFatos(leitor, d.Subitens, $"{path}.subitens", valoresSelecionaveis);
        if (leitor.Falhou)
        {
            return null;
        }

        if (campos.FirstOrDefault(c => c.Finalidade != d.Finalidade || c.EtapaCodigo is not null) is { } foraDoGrupo)
        {
            return leitor.Recusar<GrupoColetado?>(
                $"'{path}.subitens': o campo '{foraDoGrupo.FatoCodigo}' tem finalidade ou seção própria, mas o campo do grupo segue o grupo.");
        }

        Result<GrupoColetado> grupo = GrupoColetado.Criar(
            d.Codigo, d.Ordem, d.EtapaCodigo, d.Rotulo, d.Minimo, d.Maximo, exibicaoLida.Value, obrigatoriedade, campos, d.Finalidade, d.IncluiCandidato);
        return grupo.IsFailure ? leitor.Propagar<GrupoColetado?>(grupo.Error!) : grupo.Value;
    }

    private static DeclaracaoDoGrupo LerDeclaracaoDoGrupo(LeitorEnvelope leitor, JsonObject item, string path) => new()
    {
        Codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.Fato),
        Finalidade = EstruturaFormulario.FinalidadeDoToken(leitor.TextoNaoVazio(item, "finalidade", path)),
        EtapaCodigo = leitor.TextoOpcional(item, "etapaCodigo", path, LimitesDoEnvelope.CodigoEtapaFormulario),
        Ordem = leitor.Inteiro(item, "ordem", path),
        Rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.NomeDeCadastro),
        Minimo = leitor.Inteiro(item, "minimo", path),
        Maximo = leitor.InteiroOpcional(item, "maximo", path),
        IncluiCandidato = leitor.Booleano(item, "incluiCandidato", path),
        Exibicao = LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path),
        Subitens = leitor.Array(item, "subitens", path),
    };

    private sealed class DeclaracaoDoGrupo
    {
        public string Codigo { get; init; } = string.Empty;

        public FinalidadeFormulario Finalidade { get; init; }

        public string? EtapaCodigo { get; init; }

        public int Ordem { get; init; }

        public string Rotulo { get; init; } = string.Empty;

        public int Minimo { get; init; }

        public int? Maximo { get; init; }

        public bool IncluiCandidato { get; init; }

        public IReadOnlyList<LinhaDoPredicado> Exibicao { get; init; } = [];

        public JsonArray Subitens { get; init; } = [];
    }

    internal static bool LerAgregadosDoGrupo(LeitorEnvelope leitor, JsonObject item, string path, GrupoColetado grupo, List<DefinicaoAgregado> agregados)
    {
        JsonArray array = leitor.Array(item, "agregados", path);
        string? anterior = null;
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            string caminho = $"{path}.agregados[{i}]";
            JsonObject agregado = leitor.ItemObjeto(array, i, $"{path}.agregados");
            leitor.ExigirChaves(agregado, caminho, "codigo", "fatoDeMembro", "operacao");
            string codigo = leitor.TextoNaoVazio(agregado, "codigo", caminho, LimitesDoEnvelope.Fato);
            string fatoDeMembro = leitor.TextoNaoVazio(agregado, "fatoDeMembro", caminho, LimitesDoEnvelope.Fato);
            OperacaoAgregado operacao = AgregadoDeGrupo.DoToken(leitor.TextoNaoVazio(agregado, "operacao", caminho));
            if (leitor.Falhou)
            {
                break;
            }

            string? defeito = operacao == OperacaoAgregado.Nenhuma ? "a operação não é EXISTE nem VALORES_PRESENTES"
                : !grupo.Subitens.Any(s => string.Equals(s.FatoCodigo, fatoDeMembro, StringComparison.Ordinal)) ? $"o fato de membro '{fatoDeMembro}' não é campo do grupo"
                : anterior is not null && string.CompareOrdinal(anterior, codigo) >= 0 ? "os agregados não estão na ordem do código"
                : null;
            if (defeito is not null)
            {
                _ = leitor.Propagar<object>(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{caminho}': {defeito}."));
                break;
            }

            agregados.Add(new DefinicaoAgregado(codigo, grupo.Codigo, fatoDeMembro, operacao));
            anterior = codigo;
        }

        return !leitor.Falhou;
    }
}
