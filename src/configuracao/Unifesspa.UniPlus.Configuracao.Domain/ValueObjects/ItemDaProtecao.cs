namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A identidade de um item sob o circuito de aprovação da proteção de dados: o cadastro (tipo de documento ou fato) e
/// o identificador do item nele. Liga ao item a manifestação e o ato, que não guardam chave estrangeira para ele.
/// </summary>
/// <param name="Cadastro">O cadastro do item.</param>
/// <param name="ItemId">O identificador do tipo de documento ou do fato.</param>
public readonly record struct ItemDaProtecao(CadastroDoCatalogo Cadastro, Guid ItemId)
{
    /// <summary>
    /// O item que o pedido aponta. O cadastro ausente ou fora do vocabulário (<c>TIPO_DOCUMENTO</c> ou <c>FATO</c>,
    /// comparado sem os espaços das pontas) é recusado no campo <c>cadastro</c>, junto das demais recusas do lote
    /// (ADR-0125), e devolve <see langword="null"/>.
    /// </summary>
    public static ItemDaProtecao? Ler(string? cadastro, Guid itemId, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);

        if (CadastrosDoCatalogo.TryAnalisar(cadastro, out CadastroDoCatalogo lido))
        {
            return new ItemDaProtecao(lido, itemId);
        }

        erros.Add(new("cadastro", new DomainError(
            ProtecaoDoCatalogoErrorCodes.CadastroInvalido,
            $"O cadastro do item deve ser {string.Join(" ou ", CadastrosDoCatalogo.TokensCanonicos)}.")));
        return null;
    }
}
