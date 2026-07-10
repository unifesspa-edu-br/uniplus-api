namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using FluentValidation;

/// <summary>
/// Antecipa a recusa de shape que o agregado faria, devolvendo 400 antes de o
/// handler tocar o repositório. As regras de texto avaliam o valor
/// <b>normalizado</b> (trim), como a factory; por isso o nome da propriedade no
/// <c>ProblemDetails</c> vem de <c>OverridePropertyName</c>.
/// </summary>
public sealed class RegistrarAtoNormativoCommandValidator
    : AbstractValidator<RegistrarAtoNormativoCommand>
{
    public RegistrarAtoNormativoCommandValidator()
    {
        RuleFor(x => AtoNormativoRegras.Normalizar(x.Orgao))
            .NotEmpty().WithMessage(AtoNormativoRegras.OrgaoObrigatorio)
            .MaximumLength(AtoNormativoRegras.OrgaoMaxLength).WithMessage(AtoNormativoRegras.OrgaoTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.Orgao));

        RuleFor(x => AtoNormativoRegras.Normalizar(x.Serie))
            .NotEmpty().WithMessage(AtoNormativoRegras.SerieObrigatoria)
            .MaximumLength(AtoNormativoRegras.SerieMaxLength).WithMessage(AtoNormativoRegras.SerieTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.Serie));

        RuleFor(x => x.Ano)
            .GreaterThan(0).WithMessage(AtoNormativoRegras.AnoInvalido);

        RuleFor(x => AtoNormativoRegras.Normalizar(x.Numero))
            .MaximumLength(AtoNormativoRegras.NumeroMaxLength).WithMessage(AtoNormativoRegras.NumeroTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.Numero))
            .When(x => x.Numero is not null);

        RuleFor(x => AtoNormativoRegras.Normalizar(x.TipoCodigo))
            .NotEmpty().WithMessage(AtoNormativoRegras.TipoCodigoObrigatorio)
            .MaximumLength(AtoNormativoRegras.TipoCodigoMaxLength).WithMessage(AtoNormativoRegras.TipoCodigoTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.TipoCodigo));

        RuleFor(x => x.DocumentoHash)
            .NotEmpty().WithMessage(AtoNormativoRegras.DocumentoHashObrigatorio)
            .Matches(AtoNormativoRegras.HashPattern).WithMessage(AtoNormativoRegras.DocumentoHashFormato);

        RuleFor(x => AtoNormativoRegras.Normalizar(x.Assinante))
            .NotEmpty().WithMessage(AtoNormativoRegras.AssinanteObrigatorio)
            .MaximumLength(AtoNormativoRegras.AssinanteMaxLength).WithMessage(AtoNormativoRegras.AssinanteTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.Assinante));

        // O par {id, hash} é completo ou ausente (AC7).
        RuleFor(x => x)
            .Must(x => x.VersaoInvocadaId.HasValue == (x.VersaoInvocadaHash is not null))
            .WithMessage(AtoNormativoRegras.VersaoInvocadaIncompleta)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.VersaoInvocadaHash));

        // Id zerado não é referência: um Guid.Empty não aponta versão alguma.
        RuleFor(x => x.VersaoInvocadaId)
            .Must(id => id != Guid.Empty).WithMessage(AtoNormativoRegras.VersaoInvocadaIdObrigatorio)
            .When(x => x.VersaoInvocadaId.HasValue);

        RuleFor(x => x.VersaoInvocadaHash)
            .Matches(AtoNormativoRegras.HashPattern).WithMessage(AtoNormativoRegras.VersaoInvocadaHashFormato)
            .When(x => x.VersaoInvocadaHash is not null);
    }
}
