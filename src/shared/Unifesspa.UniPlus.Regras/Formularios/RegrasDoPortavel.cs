namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A leitura do predicado e da obrigatoriedade que chegam pelo formulário portável. Os campos das recusas são
/// relativos ao que foi lido: quem chama junta o caminho do elemento.
/// </summary>
internal static class RegrasDoPortavel
{
    /// <summary>O predicado do fio: nulo é sem condição, e a lista vazia é o predicado sem cláusula.</summary>
    internal static Result<PredicadoDnf?> Predicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? clausulas)
    {
        if (clausulas is { Count: 0 })
        {
            Result<PredicadoDnf> semClausula = PredicadoDnf.CriarDeCondicoesAgrupadas([]);
            return Result<PredicadoDnf?>.Success(semClausula.Value);
        }

        return EntradaDeRegras.Predicado(LeituraDeRegras.ParaEntrada(clausulas));
    }

    /// <summary>
    /// A obrigatoriedade lida; a condição recusada fica em <c>predicado</c> e impede a conferência da forma, que
    /// depende dela.
    /// </summary>
    internal static Result<Obrigatoriedade> Obrigatoriedade(ObrigatoriedadeDto? obrigatoriedade)
    {
        RecusasDoElemento recusas = new();
        PredicadoDnf? quando = recusas.Parte(Predicado(obrigatoriedade?.Predicado), "predicado");

        return recusas.ConferidoPor(() => DaForma(obrigatoriedade?.Tipo, quando));
    }

    private static Result<Obrigatoriedade> DaForma(string? tipo, PredicadoDnf? quando) =>
        EntradaDeRegras.Obrigatoriedade(tipo, quando) is { } valida
            ? Result<Obrigatoriedade>.Success(valida)
            : Result<Obrigatoriedade>.Failure(new DomainError(
                FormularioPortavelErrorCodes.ObrigatoriedadeInvalida,
                "A obrigatoriedade é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele."));
}
