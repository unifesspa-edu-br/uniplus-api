namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A consequência da ausência pertence às bancas que avaliam o mérito.</summary>
public static class ConsequenciaDaAusenciaNaBanca
{
    public static Result<ConsequenciaDoDescumprimento?> Ler(string? codigo, string campo)
    {
        if (codigo is null)
        {
            return Result<ConsequenciaDoDescumprimento?>.Success(null);
        }

        return CatalogoDe<ConsequenciaDoDescumprimento>.TentarObter(codigo, out ConsequenciaDoDescumprimento valor)
            ? Result<ConsequenciaDoDescumprimento?>.Success(valor)
            : Result<ConsequenciaDoDescumprimento?>.ValidationFailure([new(campo, new DomainError(
                BancaErrorCodes.ConsequenciaDaAusenciaInvalida, "A consequência da ausência não é uma opção conhecida."))]);
    }

    public static IReadOnlyList<FieldError> Violacoes(
        IEnumerable<(TipoDeBancaCopiado Tipo, ConsequenciaDoDescumprimento? Consequencia)> bancas, string lista) =>
        [.. bancas.SelectMany((banca, posicao) => Violacao(banca.Tipo, banca.Consequencia, $"{lista}[{posicao}].consequenciaDaAusencia"))];

    public static IReadOnlyList<FieldError> Violacao(TipoDeBancaCopiado tipo, ConsequenciaDoDescumprimento? consequencia, string campo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return consequencia is not null && !tipo.AvaliaOMerito
            ? [new FieldError(campo, new DomainError(BancaErrorCodes.ConsequenciaDaAusenciaSoEmBancaQueAvaliaOMerito,
                "A consequência da ausência só pode ser declarada em banca que avalia o mérito."))]
            : [];
    }
}
