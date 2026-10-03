namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Monta as restrições de valor a partir do que o administrador declarou, devolvendo a recusa em
/// vez da exceção dos construtores: as mesmas invariantes, vistas como erro de configuração.
/// </summary>
public static class RestricoesDeValor
{
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

        if (entradas.Select(static e => OpcoesCondicionadas.Violacao(e.Valores)).FirstOrDefault(static v => v is not null) is { } violacao)
        {
            return Recusa(RestricaoValorErrorCodes.OpcoesVazias, violacao);
        }

        return Result<RestricaoValor>.Success(new OpcoesPermitidas([.. entradas.Select(static e => new OpcoesCondicionadas(e.Quando, e.Valores))]));
    }

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
