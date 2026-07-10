namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using FluentValidation;

public sealed class RemoverTipoAtoPublicadoCommandValidator
    : AbstractValidator<RemoverTipoAtoPublicadoCommand>
{
    public RemoverTipoAtoPublicadoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(TipoAtoPublicadoRegras.IdObrigatorio);
    }
}
