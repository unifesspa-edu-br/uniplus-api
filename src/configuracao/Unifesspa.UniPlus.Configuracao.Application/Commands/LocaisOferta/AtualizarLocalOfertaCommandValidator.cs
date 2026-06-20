namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using FluentValidation;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class AtualizarLocalOfertaCommandValidator : AbstractValidator<AtualizarLocalOfertaCommand>
{
    public AtualizarLocalOfertaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id do Local de Oferta é obrigatório.");

        RuleFor(x => x.Tipo)
            .Must(t => Enum.IsDefined(t) && t != TipoLocalOferta.Nenhum)
            .WithMessage("Tipo de Local de Oferta inválido.");

        RuleFor(x => x.CidadeCodigoIbge)
            .NotEmpty().WithMessage("Código IBGE da cidade é obrigatório.");

        RuleFor(x => x.CidadeNome)
            .NotEmpty().WithMessage("Nome da cidade é obrigatório.");

        RuleFor(x => x.CidadeUf)
            .NotEmpty().WithMessage("UF da cidade é obrigatória.");

        RuleFor(x => x)
            .Must(x => ReferenciaCidadeGeo.EhValida(x.CidadeCodigoIbge, x.CidadeNome, x.CidadeUf))
            .WithMessage("Referência de cidade inválida: o código IBGE deve ter 7 dígitos e o prefixo de UF deve ser coerente com a UF informada.")
            .When(x => !string.IsNullOrWhiteSpace(x.CidadeCodigoIbge)
                && !string.IsNullOrWhiteSpace(x.CidadeNome)
                && !string.IsNullOrWhiteSpace(x.CidadeUf));

        RuleFor(x => x.Endereco)
            .MaximumLength(500).WithMessage("Endereço do Local de Oferta deve ter no máximo 500 caracteres.")
            .When(x => x.Endereco is not null);

        RuleFor(x => x.CodigoEmec)
            .MaximumLength(20).WithMessage("Código e-MEC do Local de Oferta deve ter no máximo 20 caracteres.")
            .When(x => x.CodigoEmec is not null);
    }
}
