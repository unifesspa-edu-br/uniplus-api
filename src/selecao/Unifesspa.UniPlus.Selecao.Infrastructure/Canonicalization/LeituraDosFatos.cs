namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
/// Leitura dos fatos coletados e dos grupos de coleta.
/// </summary>
internal static class LeituraDosFatos
{
    private static readonly string[] ChavesDoFatoColetado =
    [
        "codigoDoDado", "finalidade", "paginaCodigo", "ordem", "rotulo", "tipoDeCampo", "obrigatoriedade", "ajuda",
        "pedirConfirmacao", "restricoes", "impedimento", "origemValores", "formato", "classificacaoProtecao",
        "hipoteseLegal", "finalidadeTratamento", "descricao", "componenteExibicao", "exibicao", "valoresSelecionaveis",
    ];

    /// <summary>
    /// A política de referência temporal como foi congelada, insumo de <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>.
    /// </summary>
    internal static ReferenciaTemporalFatos? LerReferenciaTemporalFatosPolitica(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonObject? json = leitor.ObjetoOpcional(bloco, "referenciaTemporalDosDados", "documentosExigidos");
        if (leitor.Falhou || json is null)
        {
            return null;
        }

        const string path = "documentosExigidos.referenciaTemporalDosDados";
        leitor.ExigirChaves(json, path, "tipo", "data", "faseId");
        string tipoCodigo = leitor.TextoNaoVazio(json, "tipo", path);
        DateOnly? data = leitor.DataOpcional(json, "data", path);
        Guid? faseId = leitor.IdentificadorOpcional(json, "faseId", path);
        return leitor.Falhou ? null : CriarReferenciaTemporal(leitor, tipoCodigo, data, faseId);
    }

    private static ReferenciaTemporalFatos? CriarReferenciaTemporal(LeitorEnvelope leitor, string tipoCodigo, DateOnly? data, Guid? faseId)
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(tipoCodigo, data, faseId);
        return resultado.IsFailure ? leitor.Propagar<ReferenciaTemporalFatos>(resultado.Error!) : resultado.Value;
    }

    /// <summary>
    /// Os fatos coletados, reconstruídos por <see cref="FatoColetado.Criar"/>
    /// (que revalida a forma: código, ordem, auto-referência), Ids novos (não são congelados no
    /// envelope, diferente de <c>etapa.Id</c>). A pré-condição é a mesma forma DNF do gatilho.
    /// <c>ValoresSelecionaveis</c> (UNI-REQ-0072) é o dicionário reidratado —
    /// completo, uma entrada por fato coletado (array para os de seleção, <see langword="null"/>
    /// para os demais) — que acompanha o envelope para recanonicalizar, sem reconsultar o
    /// catálogo vivo.
    /// </summary>
    internal static (
        IReadOnlyList<FatoColetado> Fatos,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis)
        LerFatosColetados(LeitorEnvelope leitor, JsonObject payload)
    {
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = new(StringComparer.Ordinal);

        JsonArray array = leitor.Array(payload, "dadosDosFormularios", "$");
        return leitor.Falhou ? ([], valoresSelecionaveis) : (LerListaDeFatos(leitor, array, "dadosDosFormularios", valoresSelecionaveis), valoresSelecionaveis);
    }

    /// <summary>Uma lista de fatos coletados — os itens ou os campos de um grupo —, na forma congelada.</summary>
    internal static IReadOnlyList<FatoColetado> LerListaDeFatos(
        LeitorEnvelope leitor,
        JsonArray array,
        string caminho,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis)
    {
        List<FatoColetado> fatos = [];
        for (int i = 0; i < array.Count; i++)
        {
            FatoColetado? fato = LerFatoColetado(
                leitor, leitor.ItemObjeto(array, i, caminho), $"{caminho}[{i}]", valoresSelecionaveis);
            if (fato is null)
            {
                return [];
            }

            fatos.Add(fato);
        }

        return fatos;
    }

    /// <summary>
    /// A unicidade do código do fato é reconferida pela conferência do bloco de fatos mais adiante:
    /// um envelope adulterado com fato duplicado sobrescreve a entrada de <paramref name="valoresSelecionaveis"/>
    /// aqui e é recusado lá.
    /// </summary>
    private static FatoColetado? LerFatoColetado(
        LeitorEnvelope leitor,
        JsonObject item,
        string path,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis)
    {
        if (LerDeclaracaoNoVocabulario(leitor, item, path) is not { } d
            || LerComponentes(leitor, item, path, d.TipoRenderizacao!.Value, d.Finalidade) is not { } c)
        {
            return null;
        }

        Result<FatoColetado> fato = CriarFato(d, c);
        if (fato.IsFailure)
        {
            return leitor.Propagar<FatoColetado?>(fato.Error!);
        }

        valoresSelecionaveis[d.FatoCodigo] = c.ValoresSelecionaveis;
        return fato.Value;
    }

    private static DeclaracaoDoFato? LerDeclaracaoNoVocabulario(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDoFatoColetado);
        DeclaracaoDoFato d = LerDeclaracao(leitor, item, path);
        return leitor.Falhou
            ? null
            : VocabularioForaDoCatalogo(d, path) is { } mensagem ? leitor.Recusar<DeclaracaoDoFato?>(mensagem) : d;
    }

    private static Result<FatoColetado> CriarFato(DeclaracaoDoFato d, ComponentesDoFato c) =>
        FatoColetado.Criar(
            d.FatoCodigo,
            d.Ordem,
            d.EtapaCodigo,
            new ApresentacaoDoCampo(d.Rotulo, d.TipoRenderizacao!.Value, d.Ajuda, d.PedirConfirmacao),
            c.Regras,
            d.Copia,
            d.Finalidade);

    private static ComponentesDoFato? LerComponentes(
        LeitorEnvelope leitor, JsonObject item, string path, TipoRenderizacao tipoRenderizacao, FinalidadeFormulario? finalidade)
    {
        PredicadoDnf? exibicao = LerPrecondicoes(leitor, item, path);
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato = leitor.Falhou
            ? null
            : LeituraDosValoresDeDominio.LerValoresSelecionaveis(leitor, item, path, tipoRenderizacao);
        return leitor.Falhou || LerRegrasDoItem(leitor, item, path, exibicao, finalidade) is not { } regras
            ? null
            : new ComponentesDoFato(regras, valoresDoFato);
    }

    private static RegrasDoCampo? LerRegrasDoItem(
        LeitorEnvelope leitor, JsonObject item, string path, PredicadoDnf? exibicao, FinalidadeFormulario? finalidade)
    {
        if (LeituraDasCondicoes.LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade
            || LeituraDasRestricoes.LerRestricoes(leitor, item, path) is not { } restricoes)
        {
            return null;
        }

        Impedimento? impedimento = LeituraDasCondicoes.LerImpedimento(leitor, item, path);

        // A forma do item não conhece a finalidade; o agregado recusa o impedimento fora da inscrição ao
        // definir os itens, e o decoder tem de recusar o mesmo.
        return leitor.Falhou ? null : Impedimento.ConferirFinalidade(finalidade, [impedimento]) is [{ } foraDaInscricao, ..]
            ? leitor.Recusar<RegrasDoCampo?>($"'{path}.impedimento': {foraDaInscricao.Error.Message}")
            : new RegrasDoCampo(exibicao, obrigatoriedade, restricoes, impedimento);
    }

    private sealed record ComponentesDoFato(RegrasDoCampo Regras, IReadOnlyList<ValorDominioDeclaradoCongelado>? ValoresSelecionaveis);

    private static string? VocabularioForaDoCatalogo(DeclaracaoDoFato d, string path) =>
        !CatalogoDe<HipoteseLegalTratamento>.TentarObter(d.Copia.HipoteseLegal, out _)
            ? $"'{path}.hipoteseLegal' fora do vocabulário de hipóteses legais."
        : d.Copia.ComponenteExibicao is { } componente && (string.IsNullOrWhiteSpace(componente) || componente != componente.Trim())
            ? $"'{path}.componenteExibicao' não está na forma canônica: sem espaço nas pontas e não vazio."
        : d.Finalidade is null
            ? $"'{path}.finalidade' fora do vocabulário de finalidades."
        : d.TipoRenderizacao is null
            ? $"'{path}.tipoRenderizacao' fora dos tipos de campo aceitos: {string.Join(", ", CatalogoDe<TipoRenderizacao>.Todos().Select(static c => c.Codigo))}."
            : null;

    private static PredicadoDnf? LerPrecondicoes(LeitorEnvelope leitor, JsonObject item, string path)
    {
        IReadOnlyList<LinhaDoPredicado> linhas = LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> predicado = LeituraDasCondicoes.PredicadoOpcional(linhas);
        return predicado.IsFailure ? leitor.Propagar<PredicadoDnf?>(predicado.Error!) : predicado.Value;
    }

    private static DeclaracaoDoFato LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string path) => new()
    {
        FatoCodigo = leitor.TextoNaoVazio(item, "codigoDoDado", path, LimitesDoEnvelope.Fato),
        Finalidade = EstruturaFormulario.FinalidadeDoCodigo(leitor.TextoNaoVazio(item, "finalidade", path)),
        EtapaCodigo = leitor.TextoOpcional(item, "paginaCodigo", path, LimitesDoEnvelope.CodigoEtapaFormulario),
        Ordem = leitor.Inteiro(item, "ordem", path),
        Rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.NomeDeCadastro),
        TipoRenderizacao = TipoDoCodigo(leitor.TextoNaoVazio(item, "tipoDeCampo", path)),
        Ajuda = leitor.TextoOpcional(item, "ajuda", path, LimitesDoEnvelope.AjudaDoCampo),
        PedirConfirmacao = leitor.Booleano(item, "pedirConfirmacao", path),
        Copia = LerCopiaDoCatalogo(leitor, item, path),
    };

    private static CopiaDoItemNoCatalogo LerCopiaDoCatalogo(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.CodigoDeContrato<OrigemValoresColeta>(item, "origemValores", path),
        leitor.TextoOpcional(item, "formato", path, LimitesDoEnvelope.Token),
        leitor.TextoNaoVazio(item, "classificacaoProtecao", path, LimitesDoEnvelope.Token),
        leitor.TextoNaoVazio(item, "hipoteseLegal", path, LimitesDoEnvelope.HipoteseLegal),
        leitor.TextoNaoVazio(item, "finalidadeTratamento", path, LimitesDoEnvelope.FinalidadeTratamento),
        leitor.TextoNaoVazio(item, "descricao", path, LimitesDoEnvelope.DescricaoDoFato),
        leitor.TextoOpcional(item, "componenteExibicao", path, LimitesDoEnvelope.ComponenteExibicao));

    private static TipoRenderizacao? TipoDoCodigo(string codigo) =>
        CatalogoDe<TipoRenderizacao>.TentarObter(codigo, out TipoRenderizacao tipo) ? tipo : null;

    private sealed class DeclaracaoDoFato
    {
        public string FatoCodigo { get; init; } = string.Empty;

        public FinalidadeFormulario? Finalidade { get; init; }

        public string? EtapaCodigo { get; init; }

        public int Ordem { get; init; }

        public string Rotulo { get; init; } = string.Empty;

        public TipoRenderizacao? TipoRenderizacao { get; init; }

        public string? Ajuda { get; init; }

        public bool PedirConfirmacao { get; init; }

        public required CopiaDoItemNoCatalogo Copia { get; init; }
    }
}
