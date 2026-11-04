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
/// Leitura dos fatos coletados e dos grupos de coleta.
/// </summary>
internal static class LeituraDosFatos
{
    private static readonly string[] ChavesDoFatoColetado =
    [
        "fatoCodigo", "finalidade", "etapaCodigo", "ordem", "rotulo", "tipoRenderizacao", "obrigatoriedade", "ajuda",
        "pedirConfirmacao", "restricoes", "impedimento", "origemValores", "formato", "classificacaoProtecao",
        "hipoteseLegal", "finalidadeTratamento", "descricao", "componenteExibicao", "precondicao", "valoresSelecionaveis",
    ];

    /// <summary>
    /// A política de referência temporal como foi congelada, insumo de <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>.
    /// </summary>
    internal static ReferenciaTemporalFatos? LerReferenciaTemporalFatosPolitica(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonObject? json = leitor.ObjetoOpcional(bloco, "referenciaTemporalFatos", "documentosExigidos");
        if (leitor.Falhou || json is null)
        {
            return null;
        }

        const string path = "documentosExigidos.referenciaTemporalFatos";
        leitor.ExigirChaves(json, path, "tipo", "data", "faseId");

        string tipoCodigo = leitor.TextoNaoVazio(json, "tipo", path);
        DateOnly? data = leitor.DataOpcional(json, "data", path);
        Guid? faseId = leitor.IdentificadorOpcional(json, "faseId", path);
        if (leitor.Falhou)
        {
            return null;
        }

        // FromCodigo mapeia um token não reconhecido para o sentinela Nenhuma — e Criar já
        // o rejeita com um DomainError nomeado (ReferenciaTemporalFatos.TipoObrigatorio),
        // sem precisar de uma checagem de "código desconhecido" própria aqui.
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(
            ReferenciaTipoCodigo.FromCodigo(tipoCodigo), data, faseId);
        return resultado.IsFailure ? leitor.Propagar<ReferenciaTemporalFatos>(resultado.Error!) : resultado.Value;
    }

    /// <summary>
    /// Os fatos coletados, reconstruídos por <see cref="FatoColetado.Criar"/>
    /// (que revalida a forma: código, ordem, auto-referência), Ids novos (não são congelados no
    /// envelope, diferente de <c>etapa.Id</c>). A pré-condição é a mesma forma DNF do gatilho.
    /// <c>ValoresSelecionaveis</c> (UNI-REQ-0072) é o dicionário reidratado —
    /// completo, uma entrada por fato coletado (array para os de seleção, <see langword="null"/>
    /// para os demais) — que <see cref="Application.Services.RestauradorDeConfiguracao"/> repassa
    /// intacto para recanonicalizar, sem reconsultar o catálogo vivo.
    /// </summary>
    internal static (
        IReadOnlyList<FatoColetado> Fatos,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis)
        LerFatosColetados(LeitorEnvelope leitor, JsonObject payload)
    {
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = new(StringComparer.Ordinal);

        JsonArray array = leitor.Array(payload, "fatosColetados", "$");
        return leitor.Falhou ? ([], valoresSelecionaveis) : (LerListaDeFatos(leitor, array, "fatosColetados", valoresSelecionaveis), valoresSelecionaveis);
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

    private static FatoColetado? LerFatoColetado(
        LeitorEnvelope leitor,
        JsonObject item,
        string path,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis)
    {
        leitor.ExigirChaves(item, path, ChavesDoFatoColetado);

        DeclaracaoDoFato d = LerDeclaracao(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (VocabularioForaDoCatalogo(d, path) is { } mensagem)
        {
            return leitor.Recusar<FatoColetado?>(mensagem);
        }

        TipoRenderizacao tipoRenderizacao = TipoRenderizacaoCodigo.FromCodigo(d.TipoRenderizacaoCodigo);
        ComponentesDoFato? c = LerComponentes(leitor, item, path, tipoRenderizacao, d.Finalidade);
        if (c is null)
        {
            return null;
        }

        Result<FatoColetado> fato = CriarFato(d, tipoRenderizacao, c);
        if (fato.IsFailure)
        {
            return leitor.Propagar<FatoColetado?>(fato.Error!);
        }

        // A unicidade do código do fato é reconferida pela conferência do bloco de fatos mais adiante:
        // um envelope adulterado com fato duplicado sobrescreve a entrada aqui e é recusado lá.
        valoresSelecionaveis[d.FatoCodigo] = c.ValoresSelecionaveis;
        return fato.Value;
    }

    private static Result<FatoColetado> CriarFato(DeclaracaoDoFato d, TipoRenderizacao tipoRenderizacao, ComponentesDoFato c) =>
        FatoColetado.Criar(
            d.FatoCodigo, d.Ordem, d.Rotulo, tipoRenderizacao, c.Obrigatoriedade, c.Precondicoes, d.ClassificacaoProtecao, d.HipoteseLegal, d.FinalidadeTratamento, d.Descricao, d.OrigemValores, d.EtapaCodigo,
            d.Finalidade, d.Formato, d.Ajuda, d.PedirConfirmacao, c.Restricoes, c.Impedimento, d.ComponenteExibicao);

    private static ComponentesDoFato? LerComponentes(
        LeitorEnvelope leitor, JsonObject item, string path, TipoRenderizacao tipoRenderizacao, FinalidadeFormulario finalidade)
    {
        List<CondicaoPrecondicaoFato>? precondicoes = LerPrecondicoes(leitor, item, path);
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDoFato = precondicoes is null
            ? null
            : LeituraDosValoresDeDominio.LerValoresSelecionaveis(leitor, item, path, tipoRenderizacao);
        if (leitor.Falhou || precondicoes is null)
        {
            return null;
        }

        if (LeituraDasCondicoes.LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade
            || LeituraDasRestricoes.LerRestricoes(leitor, item, path) is not { } restricoes)
        {
            return null;
        }

        Impedimento? impedimento = LeituraDasCondicoes.LerImpedimento(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        // A forma do item não conhece a finalidade; o agregado recusa o impedimento fora da inscrição ao
        // definir os itens, e o decoder tem de recusar o mesmo.
        return Impedimento.ConferirFinalidade(finalidade, [impedimento]) is [{ } foraDaInscricao, ..]
            ? leitor.Recusar<ComponentesDoFato?>($"'{path}.impedimento': {foraDaInscricao.Error.Message}")
            : new ComponentesDoFato(precondicoes, valoresDoFato, obrigatoriedade, restricoes, impedimento);
    }

    private sealed record ComponentesDoFato(
        List<CondicaoPrecondicaoFato> Precondicoes,
        IReadOnlyList<ValorDominioDeclaradoCongelado>? ValoresSelecionaveis,
        Obrigatoriedade Obrigatoriedade,
        List<RestricaoValor> Restricoes,
        Impedimento? Impedimento);

    private static string? VocabularioForaDoCatalogo(DeclaracaoDoFato d, string path)
    {
        if (!DominioDoCatalogo.HipotesesLegais.Contains(d.HipoteseLegal))
        {
            return $"'{path}.hipoteseLegal' fora do vocabulário de hipóteses legais.";
        }

        if (d.ComponenteExibicao is not null && !DominioDoCatalogo.ComponentesDeExibicao.Contains(d.ComponenteExibicao))
        {
            return $"'{path}.componenteExibicao' fora do vocabulário de componentes de exibição.";
        }

        if (d.Finalidade == FinalidadeFormulario.Nenhuma)
        {
            return $"'{path}.finalidade' fora do vocabulário de finalidades.";
        }

        return TipoRenderizacaoCodigo.FromCodigo(d.TipoRenderizacaoCodigo) is TipoRenderizacao.Nenhuma
            ? $"'{path}.tipoRenderizacao' fora do vocabulário de tipos de renderização."
            : null;
    }

    private static List<CondicaoPrecondicaoFato>? LerPrecondicoes(LeitorEnvelope leitor, JsonObject item, string path)
    {
        IReadOnlyList<LinhaDoPredicado> condicoes = LeituraDasCondicoes.LerDnf(leitor, item, "precondicao", path);
        if (leitor.Falhou)
        {
            return null;
        }

        List<CondicaoPrecondicaoFato> precondicoes = [];
        foreach ((int clausula, string fato, Operador operador, JsonElement valor) in condicoes)
        {
            Result<CondicaoPrecondicaoFato> condicao = CondicaoPrecondicaoFato.Criar(clausula, fato, operador, valor);
            if (condicao.IsFailure)
            {
                leitor.Propagar<CondicaoPrecondicaoFato>(condicao.Error!);
                return null;
            }

            precondicoes.Add(condicao.Value!);
        }

        return precondicoes;
    }

    private static DeclaracaoDoFato LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string path) => new()
    {
        FatoCodigo = leitor.TextoNaoVazio(item, "fatoCodigo", path, LimitesDoEnvelope.Fato),
        Finalidade = EstruturaFormulario.FinalidadeDoToken(leitor.TextoNaoVazio(item, "finalidade", path)),
        EtapaCodigo = leitor.TextoOpcional(item, "etapaCodigo", path, LimitesDoEnvelope.CodigoEtapaFormulario),
        Ordem = leitor.Inteiro(item, "ordem", path),
        Rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.NomeDeCadastro),
        TipoRenderizacaoCodigo = leitor.TextoNaoVazio(item, "tipoRenderizacao", path),
        Ajuda = leitor.TextoOpcional(item, "ajuda", path, LimitesDoEnvelope.AjudaDoCampo),
        PedirConfirmacao = leitor.Booleano(item, "pedirConfirmacao", path),
        OrigemValores = leitor.Enumeracao<OrigemValoresColeta>(item, "origemValores", path),
        Formato = leitor.TextoOpcional(item, "formato", path, LimitesDoEnvelope.Token),
        ClassificacaoProtecao = leitor.TextoNaoVazio(item, "classificacaoProtecao", path, LimitesDoEnvelope.Token),
        HipoteseLegal = leitor.TextoNaoVazio(item, "hipoteseLegal", path, LimitesDoEnvelope.HipoteseLegal),
        FinalidadeTratamento = leitor.TextoNaoVazio(item, "finalidadeTratamento", path, LimitesDoEnvelope.FinalidadeTratamento),
        Descricao = leitor.TextoNaoVazio(item, "descricao", path, LimitesDoEnvelope.DescricaoDoFato),
        ComponenteExibicao = leitor.TextoOpcional(item, "componenteExibicao", path, LimitesDoEnvelope.ComponenteExibicao),
    };

    private sealed class DeclaracaoDoFato
    {
        public string FatoCodigo { get; init; } = string.Empty;

        public FinalidadeFormulario Finalidade { get; init; }

        public string? EtapaCodigo { get; init; }

        public int Ordem { get; init; }

        public string Rotulo { get; init; } = string.Empty;

        public string TipoRenderizacaoCodigo { get; init; } = string.Empty;

        public string? Ajuda { get; init; }

        public bool PedirConfirmacao { get; init; }

        public OrigemValoresColeta OrigemValores { get; init; }

        public string? Formato { get; init; }

        public string ClassificacaoProtecao { get; init; } = string.Empty;

        public string HipoteseLegal { get; init; } = string.Empty;

        public string FinalidadeTratamento { get; init; } = string.Empty;

        public string Descricao { get; init; } = string.Empty;

        public string? ComponenteExibicao { get; init; }
    }

}
