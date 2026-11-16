namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;

/// <summary>
/// As conferências dos valores de domínio de um fato categórico do administrador (ADR-0116): o estado
/// do fato que os comporta, o valor novo, os textos e a ordem. Só confere e devolve as recusas, cada
/// uma no seu campo (ADR-0125); quem grava é o agregado.
/// </summary>
internal static class ConferenciaDosValoresDoFato
{
    /// <summary>
    /// As recusas de estado comuns a tudo que mexe nos valores: só o fato do administrador, categórico,
    /// de fonte global e que não seja agregado tem valores próprios.
    /// </summary>
    internal static DomainError? RecusaDeEstado(
        DomainError? recusaDoFatoDeSistema, DominioFato dominio, string? membroAgregado, FonteValoresFato? fonteValores) =>
        recusaDoFatoDeSistema
        ?? (dominio != DominioFato.Categorico ? RecusaForaDeCategorico : null)
        ?? (membroAgregado is { } membro ? RecusaDeAgregado(membro) : null)
        ?? (fonteValores != FonteValoresFato.Global ? RecusaForaDeFonteGlobal : null);

    internal static Result<NovoValorDoFato> ConferirNovo(
        string codigo, string? descricao, string? orientacao, int ordem, OrigemFato origem, IReadOnlyCollection<string> existentes)
    {
        string codigoAparado = codigo?.Trim() ?? string.Empty;
        Result<TextosDoValor> textos = ConferirTextos(origem, descricao, orientacao);
        List<FieldError> erros = [.. Presentes(RecusaDoCodigo(codigoAparado, existentes)), .. textos.Errors, .. Presentes(RecusaDaOrdem(ordem))];
        return erros.Count > 0
            ? Result<NovoValorDoFato>.ValidationFailure(erros)
            : Result<NovoValorDoFato>.Success(new NovoValorDoFato(codigoAparado, textos.Value, ordem));
    }

    /// <summary>A descrição e a orientação aparadas; as recusas acumulam, cada uma no seu campo (ADR-0125).</summary>
    internal static Result<TextosDoValor> ConferirTextos(OrigemFato origem, string? descricao, string? orientacao)
    {
        string? descricaoAparada = AparadoOuNulo(descricao);
        string? orientacaoAparada = AparadoOuNulo(orientacao);
        List<FieldError> erros = [.. Presentes(RecusaDaDescricao(descricaoAparada, origem), RecusaDaOrientacao(orientacaoAparada))];
        return erros.Count > 0
            ? Result<TextosDoValor>.ValidationFailure(erros)
            : Result<TextosDoValor>.Success(new TextosDoValor(descricaoAparada, orientacaoAparada));
    }

    /// <summary>
    /// A ordem pela lista completa de códigos — ativos e desativados. A lista que deixa um valor de
    /// fora, repete um código ou cita um código que o fato não tem é recusada inteira, e as faltas
    /// acumulam. Devolve os códigos aparados, na ordem informada.
    /// </summary>
    internal static Result<string[]> ConferirOrdem(IReadOnlyList<string> codigosEmOrdem, IReadOnlyCollection<string> existentes)
    {
        string[] informados = [.. codigosEmOrdem.Select(static c => c?.Trim() ?? string.Empty)];
        List<FieldError> erros =
        [
            .. Presentes(
                RecusaDeCodigoEmBranco(informados),
                RecusaDeCodigosDesconhecidos(informados, existentes),
                RecusaDeCodigosRepetidos(informados),
                RecusaDeValoresAusentes(informados, existentes)),
        ];

        return erros.Count > 0 ? Result<string[]>.ValidationFailure(erros) : Result<string[]>.Success(informados);
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));

    private static FieldError? RecusaDaOrdem(int ordem) =>
        ordem < 0 ? Recusa("ordem", FatoValorDominioErrorCodes.OrdemInvalida, "Ordem do valor de domínio não pode ser negativa.") : null;

    private static string? AparadoOuNulo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static DomainError RecusaForaDeCategorico { get; } = new(
        FatoValorDominioErrorCodes.NaoPermitidoForaDeCategorico,
        "Só fato categórico tem valores de domínio próprios.");

    private static DomainError RecusaDeAgregado(string membro) => new(
        FatoCandidatoErrorCodes.AgregadoNaoTemValoresProprios,
        $"Os valores do agregado são os do fato de membro '{membro}'; mantenha-os nele.");

    private static DomainError RecusaForaDeFonteGlobal { get; } = new(
        FatoValorDominioErrorCodes.NaoPermitidoForaDeFonteGlobal,
        "Valores de domínio só são declarados no catálogo quando a fonte dos valores é global; "
        + "nas demais fontes, os valores vêm do processo.");

    private static FieldError? RecusaDoCodigo(string codigo, IReadOnlyCollection<string> existentes) => codigo switch
    {
        { Length: 0 } => Recusa("codigo", FatoValorDominioErrorCodes.CodigoObrigatorio, "Código do valor de domínio é obrigatório."),
        { Length: > FatoValorDominio.CodigoMaxLength } => Recusa("codigo", FatoValorDominioErrorCodes.CodigoTamanho,
            $"Código do valor de domínio deve ter no máximo {FatoValorDominio.CodigoMaxLength} caracteres."),
        _ when existentes.Contains(codigo, StringComparer.Ordinal) => Recusa("codigo", FatoValorDominioErrorCodes.CodigoDuplicado,
            $"Já existe um valor de domínio com o código '{codigo}' neste fato."),
        _ => null,
    };

    private static FieldError? RecusaDaDescricao(string? descricao, OrigemFato origem) => descricao switch
    {
        { Length: > FatoValorDominio.DescricaoMaxLength } => Recusa("descricao", FatoValorDominioErrorCodes.DescricaoTamanho,
            $"Descrição do valor de domínio deve ter no máximo {FatoValorDominio.DescricaoMaxLength} caracteres."),
        null when origem == OrigemFato.Declarado => Recusa("descricao", FatoValorDominioErrorCodes.DescricaoObrigatoria,
            "Descrição do valor de domínio é obrigatória quando a origem do fato é DECLARADO."),
        _ => null,
    };

    private static FieldError? RecusaDaOrientacao(string? orientacao) =>
        orientacao is { Length: > FatoValorDominio.OrientacaoMaxLength }
            ? Recusa("orientacao", FatoValorDominioErrorCodes.OrientacaoTamanho,
                $"Orientação do valor de domínio deve ter no máximo {FatoValorDominio.OrientacaoMaxLength} caracteres.")
            : null;

    private static FieldError? RecusaDeCodigoEmBranco(string[] informados) =>
        informados.Any(static c => c.Length == 0)
            ? Recusa("codigos", FatoValorDominioErrorCodes.OrdemComCodigoDesconhecido,
                "A ordem tem um código em branco; informe o código de cada valor do fato.")
            : null;

    private static FieldError? RecusaDeCodigosDesconhecidos(string[] informados, IReadOnlyCollection<string> existentes)
    {
        string[] estranhos = [.. informados.Where(c => c.Length > 0 && !existentes.Contains(c, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal)];
        return estranhos.Length > 0
            ? Recusa("codigos", FatoValorDominioErrorCodes.OrdemComCodigoDesconhecido, $"O fato não tem os valores: {string.Join(", ", estranhos)}.")
            : null;
    }

    private static FieldError? RecusaDeCodigosRepetidos(string[] informados)
    {
        string[] repetidos = [.. informados.Where(static c => c.Length > 0).GroupBy(static c => c, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1).Select(static g => g.Key)];
        return repetidos.Length > 0
            ? Recusa("codigos", FatoValorDominioErrorCodes.OrdemComCodigoRepetido, $"Valores repetidos na ordem: {string.Join(", ", repetidos)}.")
            : null;
    }

    private static FieldError? RecusaDeValoresAusentes(string[] informados, IReadOnlyCollection<string> existentes)
    {
        HashSet<string> informadosSet = new(informados, StringComparer.Ordinal);
        string[] ausentes = [.. existentes.Where(c => !informadosSet.Contains(c)).Order(StringComparer.Ordinal)];
        return ausentes.Length > 0
            ? Recusa("codigos", FatoValorDominioErrorCodes.OrdemSemTodosOsValores,
                $"A ordem deve trazer todos os valores do fato, ativos e desativados; faltam: {string.Join(", ", ausentes)}.")
            : null;
    }
}
