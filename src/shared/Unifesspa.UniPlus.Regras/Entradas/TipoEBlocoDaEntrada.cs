namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O tipo e o bloco de sistema que o pedido informou para uma etapa do formulário: a única leitura deles, no processo e
/// no modelo. A mensagem do código desconhecido lista os códigos aceitos sem repetir o valor recebido.
/// </summary>
public static class TipoEBlocoDaEntrada
{
    /// <summary>O tipo do código informado, ou a recusa de domínio quando ele falta ou não é um dos aceitos.</summary>
    public static Result<TipoEtapaFormulario> LerTipo(string? codigo) =>
        CatalogoDe<TipoEtapaFormulario>.TentarObter(codigo, out TipoEtapaFormulario tipo)
            ? Result<TipoEtapaFormulario>.Success(tipo)
            : Result<TipoEtapaFormulario>.Failure(new DomainError(
                EstruturaFormularioErrorCodes.TipoDeEtapaObrigatorio,
                $"A etapa é uma seção ou um bloco de sistema. Os tipos aceitos são {CodigosAceitos<TipoEtapaFormulario>()}."));

    /// <summary>O bloco do código informado; o código ausente é a etapa sem bloco, e o desconhecido é recusado.</summary>
    public static Result<BlocoSistema?> LerBloco(string? codigo)
    {
        if (codigo is null)
        {
            return Result<BlocoSistema?>.Success(null);
        }

        return CatalogoDe<BlocoSistema>.TentarObter(codigo, out BlocoSistema bloco)
            ? Result<BlocoSistema?>.Success(bloco)
            : Result<BlocoSistema?>.Failure(new DomainError(
                EstruturaFormularioErrorCodes.BlocoNaoAdmitido,
                $"O bloco de sistema não é reconhecido. Os blocos aceitos são {CodigosAceitos<BlocoSistema>()}."));
    }

    private static string CodigosAceitos<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", CatalogoDe<TEnum>.Todos().Select(static d => d.Codigo));
}
