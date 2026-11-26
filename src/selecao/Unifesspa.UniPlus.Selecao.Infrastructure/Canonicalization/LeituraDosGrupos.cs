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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
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
        List<DefinicaoAgregado> agregados,
        FormulariosDoDocumento formularios)
    {
        JsonArray array = leitor.Array(payload, "gruposDosFormularios", "$");
        List<GrupoColetado> grupos = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            if (LerGrupo(leitor, leitor.ItemObjeto(array, i, "gruposDosFormularios"), $"gruposDosFormularios[{i}]", valoresSelecionaveis, agregados, formularios) is not { } grupo)
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
        List<DefinicaoAgregado> agregados,
        FormulariosDoDocumento formularios)
    {
        if (LerDeclaracaoDoGrupo(leitor, item, path, formularios) is not { } declaracao)
        {
            return null;
        }

        GrupoColetado? grupo = CriarGrupo(leitor, declaracao, item, path, valoresSelecionaveis, formularios);
        return grupo is not null && LerAgregadosDoGrupo(leitor, item, path, grupo, agregados) ? grupo : null;
    }

    /// <summary>A declaração do grupo lida das chaves exatas do envelope; recusa o código de formulário que o documento não traz.</summary>
    private static DeclaracaoDoGrupo? LerDeclaracaoDoGrupo(LeitorEnvelope leitor, JsonObject item, string path, FormulariosDoDocumento formularios)
    {
        leitor.ExigirChaves(
            item, path, "codigo", "formularioCodigo", "paginaCodigo", "ordem", "rotulo", "minimo", "maximo", "incluiCandidato", "exibicao", "obrigatoriedade", "subitens", "agregados");
        DeclaracaoDoGrupo declaracao = LerCamposDaDeclaracaoDoGrupo(leitor, item, path, formularios);
        return leitor.Falhou ? null
            : declaracao.Formulario is null
                ? leitor.Recusar<DeclaracaoDoGrupo?>($"'{path}.formularioCodigo' não é o código de um formulário do documento.")
                : declaracao;
    }

    private static GrupoColetado? CriarGrupo(
        LeitorEnvelope leitor,
        DeclaracaoDoGrupo d,
        JsonObject item,
        string path,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis,
        FormulariosDoDocumento formularios)
    {
        if (LerRegrasDoGrupo(leitor, d, item, path) is not { } regras
            || LerCamposDoGrupo(leitor, d, path, valoresSelecionaveis, formularios) is not { } campos)
        {
            return null;
        }

        Result<GrupoColetado> grupo = GrupoColetado.Criar(
            d.Codigo, d.Ordem, d.PaginaCodigo, d.Rotulo, new OcorrenciasDoGrupo(d.Minimo, d.Maximo, d.IncluiCandidato), regras, campos);
        return grupo.IsFailure ? leitor.Propagar<GrupoColetado?>(CausasDoGrupoNoDocumento(grupo.Errors), path) : grupo.Value;
    }

    private static IReadOnlyList<FieldError> CausasDoGrupoNoDocumento(IReadOnlyList<FieldError> causas) =>
        [.. causas.Select(static causa => causa.Field is { } campo
            && (campo.StartsWith("ocorrencias.", StringComparison.Ordinal) || campo.Contains("regras.", StringComparison.Ordinal))
                ? LeitorEnvelope.NoProprioItem([causa])[0] : causa)];

    private static ExibicaoEObrigatoriedade? LerRegrasDoGrupo(LeitorEnvelope leitor, DeclaracaoDoGrupo d, JsonObject item, string path)
    {
        Result<PredicadoDnf?> exibicao = LeituraDasCondicoes.PredicadoOpcional(d.Exibicao);
        if (exibicao.IsFailure)
        {
            return leitor.Propagar<ExibicaoEObrigatoriedade?>(exibicao.Errors, $"{path}.exibicao");
        }

        return LeituraDasCondicoes.LerObrigatoriedade(leitor, item, path) is { } obrigatoriedade
            ? new ExibicaoEObrigatoriedade(exibicao.Value, obrigatoriedade)
            : null;
    }

    private static IReadOnlyList<FatoColetado>? LerCamposDoGrupo(
        LeitorEnvelope leitor,
        DeclaracaoDoGrupo d,
        string path,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis,
        FormulariosDoDocumento formularios)
    {
        IReadOnlyList<FatoColetado> campos = LeituraDosFatos.LerListaDeFatos(leitor, d.Subitens, $"{path}.subitens", valoresSelecionaveis, formularios);
        if (leitor.Falhou)
        {
            return null;
        }

        return campos.FirstOrDefault(c => c.FormularioId != d.Formulario!.Id || c.PaginaCodigo is not null) is { } foraDoGrupo
            ? leitor.Recusar<IReadOnlyList<FatoColetado>?>(
                $"'{path}.subitens': um campo tem formulário ou seção própria, mas o campo do grupo segue o grupo.")
            : campos;
    }

    private static DeclaracaoDoGrupo LerCamposDaDeclaracaoDoGrupo(LeitorEnvelope leitor, JsonObject item, string path, FormulariosDoDocumento formularios) => new()
    {
        Codigo = leitor.TextoNaoVazio(item, "codigo", path, FormaDoGrupo.CodigoMaxLength),
        Formulario = formularios.De(leitor.TextoNaoVazio(item, "formularioCodigo", path, CodigoDoFormulario.TamanhoMaximo)),
        PaginaCodigo = leitor.TextoOpcional(item, "paginaCodigo", path, LimitesDoEnvelope.CodigoPaginaFormulario),
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

        public FormularioProcesso? Formulario { get; init; }

        public string? PaginaCodigo { get; init; }

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
            if (LerAgregado(leitor, leitor.ItemObjeto(array, i, $"{path}.agregados"), $"{path}.agregados[{i}]", grupo, anterior) is not { } agregado)
            {
                break;
            }

            agregados.Add(agregado);
            anterior = agregado.Codigo;
        }

        return !leitor.Falhou;
    }

    private static DefinicaoAgregado? LerAgregado(LeitorEnvelope leitor, JsonObject agregado, string caminho, GrupoColetado grupo, string? anterior)
    {
        leitor.ExigirChaves(agregado, caminho, "codigo", "codigoDoDadoDoMembro", "operacao");
        string codigo = leitor.TextoNaoVazio(agregado, "codigo", caminho, LimitesDoEnvelope.Fato);
        string fatoDeMembro = leitor.TextoNaoVazio(agregado, "codigoDoDadoDoMembro", caminho, LimitesDoEnvelope.Fato);
        OperacaoAgregado operacao = leitor.CodigoDeContrato<OperacaoAgregado>(agregado, "operacao", caminho);
        if (leitor.Falhou)
        {
            return null;
        }

        return DefeitoDoAgregado(fatoDeMembro, codigo, anterior, grupo) is { } defeito
            ? leitor.Propagar<DefinicaoAgregado?>(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{caminho}': {defeito}."))
            : new DefinicaoAgregado(codigo, grupo.Codigo, fatoDeMembro, operacao);
    }

    private static string? DefeitoDoAgregado(string fatoDeMembro, string codigo, string? anterior, GrupoColetado grupo) =>
        !grupo.Subitens.Any(s => string.Equals(s.FatoCodigo, fatoDeMembro, StringComparison.Ordinal)) ? "o fato de membro não é campo do grupo"
            : anterior is not null && string.CompareOrdinal(anterior, codigo) >= 0 ? "os agregados não estão na ordem do código"
            : null;
}
