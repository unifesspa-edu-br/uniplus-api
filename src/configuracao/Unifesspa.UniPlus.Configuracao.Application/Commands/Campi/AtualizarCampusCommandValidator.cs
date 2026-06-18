namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Cidades;

public sealed class AtualizarCampusCommandValidator : AbstractValidator<AtualizarCampusCommand>
{
    public AtualizarCampusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id do Campus é obrigatório.");

        RuleFor(x => x.Sigla)
            .NotEmpty().WithMessage("Sigla do Campus é obrigatória.")
            .MaximumLength(20).WithMessage("Sigla do Campus deve ter no máximo 20 caracteres.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do Campus é obrigatório.")
            .MinimumLength(2).WithMessage("Nome do Campus deve ter ao menos 2 caracteres.")
            .MaximumLength(200).WithMessage("Nome do Campus deve ter no máximo 200 caracteres.");

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
            .MaximumLength(500).WithMessage("Endereço do Campus deve ter no máximo 500 caracteres.")
            .When(x => x.Endereco is not null);

        RuleFor(x => x.Cep)
            .Must(CampusCampoRegras.CepValidoOuAusente)
            .WithMessage("CEP do Campus deve ter exatamente 8 dígitos numéricos.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(CampusCampoRegras.LatitudeMin, CampusCampoRegras.LatitudeMax)
            .WithMessage("Latitude deve estar entre -90 e 90.")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(CampusCampoRegras.LongitudeMin, CampusCampoRegras.LongitudeMax)
            .WithMessage("Longitude deve estar entre -180 e 180.")
            .When(x => x.Longitude.HasValue);

        RuleFor(x => x.CodigoEmec)
            .MaximumLength(20).WithMessage("Código e-MEC do Campus deve ter no máximo 20 caracteres.")
            .When(x => x.CodigoEmec is not null);
    }
}
