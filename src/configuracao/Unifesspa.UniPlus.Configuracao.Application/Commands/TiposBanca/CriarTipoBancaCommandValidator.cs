namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Antecipa (422) o formato do código, a pertença ao conjunto canônico das quatro
/// bancas e os tamanhos. A fase típica é orientativa (não validada contra o
/// cadastro de fases), apenas limitada em tamanho.
/// </summary>
public sealed class CriarTipoBancaCommandValidator : AbstractValidator<CriarTipoBancaCommand>
{
    public CriarTipoBancaCommandValidator()
    {
        // Formato do código (sempre avaliado — NotEmpty + regex).
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("Código do tipo de banca é obrigatório.")
            .Must(CodigoBanca.EhValido)
            .WithMessage("Código do tipo de banca deve conter apenas letras maiúsculas e sublinhado "
                + "(sem hífen e sem dígito), com no máximo 60 caracteres.");

        // Pertença ao conjunto canônico — só quando o formato já é válido. Em RuleFor
        // separado para o When não vazar ao NotEmpty.
        RuleFor(x => x.Codigo)
            .Must(TipoBancaCatalogo.EhCanonico)
            .WithMessage($"Código do tipo de banca deve ser um dos canônicos: {string.Join(", ", TipoBancaCatalogo.Codigos)}.")
            .When(x => CodigoBanca.EhValido(x.Codigo));

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do tipo de banca é obrigatório.")
            .MaximumLength(200).WithMessage("Nome do tipo de banca deve ter no máximo 200 caracteres.");

        RuleFor(x => x.FaseTipica)
            .MaximumLength(60).WithMessage("Fase típica do tipo de banca deve ter no máximo 60 caracteres.")
            .When(x => x.FaseTipica is not null);

        RuleFor(x => x.Descricao)
            .MaximumLength(300).WithMessage("Descrição do tipo de banca deve ter no máximo 300 caracteres.")
            .When(x => x.Descricao is not null);
    }
}
