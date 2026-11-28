namespace Unifesspa.UniPlus.Selecao.Application.Validators.AtosDoProcesso;

using Commands.AtosDoProcesso;

using Domain.Entities;

using FluentValidation;

/// <summary>
/// Valida o formato do comando de publicação do ato avulso antes de qualquer conferência.
/// </summary>
public sealed class PublicarAtoAvulsoDoProcessoCommandValidator : DadosDoAtoPublicadoValidator<PublicarAtoAvulsoDoProcessoCommand>
{
    public PublicarAtoAvulsoDoProcessoCommandValidator()
    {
        // O tipo ausente ou em branco é recusado pela forma do pedido, com o código de campo obrigatório; o limite só vale para texto com conteúdo.
        RuleFor(x => x.TipoAtoCodigo)
            .MaximumLength(AtoDoProcesso.TipoCodigoMaxLength)
            .When(static x => !string.IsNullOrWhiteSpace(x.TipoAtoCodigo))
            .WithMessage($"Tipo do ato deve ter no máximo {AtoDoProcesso.TipoCodigoMaxLength} caracteres.");
    }
}
