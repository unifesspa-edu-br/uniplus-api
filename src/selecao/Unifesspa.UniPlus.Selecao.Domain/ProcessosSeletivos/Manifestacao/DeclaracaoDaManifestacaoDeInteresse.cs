namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere o código que o processo declara para a manifestação de interesse. Sem código, a manifestação não é prevista e a
/// declaração é válida: o resultado é nulo. O código que o catálogo não conhece é recusado no campo da declaração, sem repeti-lo.
/// </summary>
public static class DeclaracaoDaManifestacaoDeInteresse
{
    /// <summary>O campo da declaração no documento da configuração e no envelope.</summary>
    public const string Campo = "manifestacaoDeInteresse";

    public static Result<ManifestacaoDeInteresse?> Declarar(string? codigo)
    {
        if (codigo is null)
        {
            return Result<ManifestacaoDeInteresse?>.Success(null);
        }

        return CatalogoDe<ManifestacaoDeInteresse>.TentarObter(codigo, out ManifestacaoDeInteresse valor)
            ? Result<ManifestacaoDeInteresse?>.Success(valor)
            : Result<ManifestacaoDeInteresse?>.ValidationFailure([Desconhecida()]);
    }

    private static FieldError Desconhecida() => new(Campo, new DomainError(
        ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido,
        $"A manifestação de interesse informada não existe; escolha {Opcoes()}, ou deixe sem valor quando o edital não a prevê."));

    private static string Opcoes() =>
        string.Join(" ou ", CatalogoDe<ManifestacaoDeInteresse>.Todos().Select(static opcao => $"\"{opcao.Rotulo}\""));
}
