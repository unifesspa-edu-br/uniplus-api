namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using FluentValidation;

/// <summary>
/// Checa só a forma do <c>Id</c> — um identificador de rota sem equivalente no
/// agregado (<c>TipoAtoPublicado.Atualizar</c> nem recebe Id). Todos os demais
/// campos têm equivalente de domínio e ficam fora daqui (ADR-0125): o agregado é
/// a autoridade sobre eles, via <c>ValidarCampos</c>.
/// </summary>
public sealed class AtualizarTipoAtoPublicadoCommandValidator
    : AbstractValidator<AtualizarTipoAtoPublicadoCommand>
{
    public AtualizarTipoAtoPublicadoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Identificador do tipo de ato é obrigatório.");
    }
}
