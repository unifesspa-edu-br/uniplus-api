namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Monta as restrições de valor a partir do que o administrador declarou, devolvendo a recusa em
/// vez da exceção dos construtores: as mesmas invariantes, vistas como erro de configuração.
/// </summary>
public static class RestricoesDeValor
{
    /// <summary>
    /// O tipo de restrição que o administrador declara pelo código; nulo quando o código é desconhecido e para o
    /// formato de texto, que vem do fato no catálogo e não se declara no item.
    /// </summary>
    public static TipoRestricaoValor? TipoDeclarado(string? codigo) =>
        CatalogoDe<TipoRestricaoValor>.TentarObter(codigo, out TipoRestricaoValor tipo) && tipo != TipoRestricaoValor.FormatoTexto
            ? tipo
            : null;

    public static Result<RestricaoValor> Faixa(decimal? minimo, decimal? maximo) =>
        FaixaNumerica.Violacao(minimo, maximo) is { } violacao
            ? Recusa(RestricaoValorErrorCodes.LimitesIncoerentes, violacao)
            : Result<RestricaoValor>.Success(new FaixaNumerica(minimo, maximo));

    public static Result<RestricaoValor> Tamanho(int? minimo, int? maximo) =>
        TamanhoTexto.Violacao(minimo, maximo) is { } violacao
            ? Recusa(RestricaoValorErrorCodes.LimitesIncoerentes, violacao)
            : Result<RestricaoValor>.Success(new TamanhoTexto(minimo, maximo));

    public static Result<RestricaoValor> Opcoes(IReadOnlyList<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        if (entradas.Count == 0)
        {
            return Recusa(RestricaoValorErrorCodes.OpcoesVazias, "As opções permitidas precisam de ao menos um grupo de valores.");
        }

        List<FieldError> recusas = RecusasDosGrupos(entradas);

        return recusas.Count > 0
            ? Result<RestricaoValor>.ValidationFailure(recusas)
            : Result<RestricaoValor>.Success(new OpcoesPermitidas([.. entradas.Select(static e => new OpcoesCondicionadas(e.Quando, e.Valores))]));
    }

    private static List<FieldError> RecusasDosGrupos(IReadOnlyList<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> entradas) =>
        [.. entradas
            .Select(static (grupo, posicao) => (Violacao: OpcoesCondicionadas.Violacao(grupo.Valores), Posicao: posicao))
            .Where(static grupo => grupo.Violacao is not null)
            .Select(static grupo => new FieldError(
                $"entradas[{grupo.Posicao}].valores", new DomainError(RestricaoValorErrorCodes.OpcoesVazias, grupo.Violacao!)))];

    public static Result<RestricaoValor> DasRespostas(IReadOnlyCollection<string> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        return OpcoesDasRespostas.Violacao(fatos) is { } violacao
            ? Recusa(RestricaoValorErrorCodes.FatosVazios, violacao)
            : Result<RestricaoValor>.Success(new OpcoesDasRespostas(fatos));
    }

    public static Result<RestricaoValor> DaUf(IReadOnlyCollection<string> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        return MunicipiosDaUf.Violacao(fatos) is { } violacao
            ? Recusa(RestricaoValorErrorCodes.FatosVazios, violacao)
            : Result<RestricaoValor>.Success(new MunicipiosDaUf(fatos.Single()));
    }

    public static Result<RestricaoValor> FaixaDeData(LimiteDeData? minima, LimiteDeData? maxima) =>
        Formularios.FaixaDeData.Violacao(minima, maxima) is { } violacao
            ? Recusa(RestricaoValorErrorCodes.LimitesIncoerentes, violacao)
            : Result<RestricaoValor>.Success(new FaixaDeData(minima, maxima));

    /// <summary>
    /// A faixa de data a partir dos limites já lidos; limite nulo é limite ausente. Cada limite recusado volta no seu
    /// campo, o mínimo antes do máximo, e a coerência entre os dois só é conferida com os dois lidos.
    /// </summary>
    public static Result<RestricaoValor> FaixaDeDataLida(Result<LimiteDeData>? minima, Result<LimiteDeData>? maxima)
    {
        RecusasDoElemento recusas = new();
        LimiteDeData? limiteMinimo = minima is null ? null : recusas.Parte(minima, "dataMinima");
        LimiteDeData? limiteMaximo = maxima is null ? null : recusas.Parte(maxima, "dataMaxima");

        return recusas.ConferidoPor(() => FaixaDeData(limiteMinimo, limiteMaximo));
    }

    /// <summary>
    /// O limite da faixa de data pelo token da âncora: <c>DATA_FIXA</c> com a data e sem deslocamento,
    /// ou <c>ENVIO</c> sem data, deslocado em anos e dias — o deslocamento que falta é zero.
    /// </summary>
    public static Result<LimiteDeData> Limite(string? ancora, DateOnly? data, int? anos, int? dias) =>
        RestricaoValorJson.AncoraDoToken(ancora) switch
        {
            AncoraDoLimiteDeData.DataFixa when data is { } fixa && anos is null && dias is null =>
                LimiteOuRecusa(LimiteDeData.ViolacaoDaDataFixa(fixa), () => LimiteDeData.Fixo(fixa)),
            AncoraDoLimiteDeData.Envio when data is null =>
                LimiteOuRecusa(LimiteDeData.ViolacaoDoDeslocamento(anos ?? 0, dias ?? 0), () => LimiteDeData.NoEnvio(anos ?? 0, dias ?? 0)),
            _ => Result<LimiteDeData>.Failure(new DomainError(
                RestricaoValorErrorCodes.LimiteDeDataInvalido,
                "O limite da faixa de data é DATA_FIXA, com a data, ou ENVIO, com o deslocamento em anos e dias.")),
        };

    private static Result<LimiteDeData> LimiteOuRecusa(string? violacao, Func<LimiteDeData> limite) =>
        violacao is not null
            ? Result<LimiteDeData>.Failure(new DomainError(RestricaoValorErrorCodes.LimiteDeDataInvalido, violacao))
            : Result<LimiteDeData>.Success(limite());

    /// <summary>A recusa quando o item declara mais de uma restrição do mesmo tipo.</summary>
    public static DomainError? TipoRepetido(IEnumerable<RestricaoValor> restricoes)
    {
        ArgumentNullException.ThrowIfNull(restricoes);
        return restricoes.GroupBy(static r => r.Tipo).Any(static g => g.Count() > 1)
            ? new DomainError(RestricaoValorErrorCodes.TipoRepetido, "O item declara no máximo uma restrição de cada tipo.")
            : null;
    }

    private static Result<RestricaoValor> Recusa(string codigo, string mensagem) =>
        Result<RestricaoValor>.Failure(new DomainError(codigo, mensagem));
}
