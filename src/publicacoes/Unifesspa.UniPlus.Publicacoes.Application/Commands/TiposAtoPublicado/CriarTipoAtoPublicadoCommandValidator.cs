namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using FluentValidation;

/// <summary>
/// Antecipa a recusa que o agregado faria, devolvendo 400 antes de o handler tocar
/// o repositório.
/// </summary>
/// <remarks>
/// As regras avaliam o valor <b>normalizado</b>, como o agregado faz. Validar o texto
/// cru divergiria nos dois sentidos: <c>"  EDITAL_ABERTURA  "</c> seria recusado aqui
/// embora o domínio o aceite, e <c>" E"</c> passaria o comprimento mínimo aqui para
/// ser recusado adiante, com outro status. Como a expressão não é um acesso a membro,
/// o nome da propriedade no <c>ProblemDetails</c> vem de <c>OverridePropertyName</c>.
/// </remarks>
public sealed class CriarTipoAtoPublicadoCommandValidator
    : AbstractValidator<CriarTipoAtoPublicadoCommand>
{
    public CriarTipoAtoPublicadoCommandValidator()
    {
        RuleFor(x => TipoAtoPublicadoRegras.Normalizar(x.Codigo))
            .NotEmpty().WithMessage(TipoAtoPublicadoRegras.CodigoObrigatorio)
            .MaximumLength(TipoAtoPublicadoRegras.CodigoMaxLength).WithMessage(TipoAtoPublicadoRegras.CodigoTamanho)
            .Matches(TipoAtoPublicadoRegras.CodigoPattern).WithMessage(TipoAtoPublicadoRegras.CodigoFormato)
            .OverridePropertyName(nameof(CriarTipoAtoPublicadoCommand.Codigo));

        RuleFor(x => TipoAtoPublicadoRegras.Normalizar(x.Nome))
            .NotEmpty().WithMessage(TipoAtoPublicadoRegras.NomeObrigatorio)
            .Length(TipoAtoPublicadoRegras.NomeMinLength, TipoAtoPublicadoRegras.NomeMaxLength)
            .WithMessage(TipoAtoPublicadoRegras.NomeTamanho)
            .OverridePropertyName(nameof(CriarTipoAtoPublicadoCommand.Nome));

        RuleFor(x => TipoAtoPublicadoRegras.Normalizar(x.BaseLegal))
            .MaximumLength(TipoAtoPublicadoRegras.BaseLegalMaxLength).WithMessage(TipoAtoPublicadoRegras.BaseLegalTamanho)
            .OverridePropertyName(nameof(CriarTipoAtoPublicadoCommand.BaseLegal))
            .When(x => x.BaseLegal is not null);

        RuleFor(x => x.VigenciaFim)
            .GreaterThan(x => x.VigenciaInicio).WithMessage(TipoAtoPublicadoRegras.VigenciaFim)
            .When(x => x.VigenciaFim.HasValue);
    }
}
