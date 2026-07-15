namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Antecipa (422) o formato do código, a pertença ao conjunto canônico das
/// quatorze fases, o domínio fechado do dono típico e os tamanhos. A coerência
/// <c>agrupa_etapas</c>/<c>permite_complementacao</c> (que depende do código) fica
/// no agregado — inclusive na atualização, onde o comando não carrega o código.
/// </summary>
public sealed class CriarFaseCanonicaCommandValidator : AbstractValidator<CriarFaseCanonicaCommand>
{
    public CriarFaseCanonicaCommandValidator()
    {
        // Formato do código (sempre avaliado — NotEmpty + regex).
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("Código da fase é obrigatório.")
            .Must(CodigoFase.EhValido)
            .WithMessage("Código da fase deve conter apenas letras maiúsculas e sublinhado "
                + "(sem hífen e sem dígito), com no máximo 60 caracteres.");

        // Pertença ao conjunto canônico — só quando o formato já é válido (evita
        // mensagem redundante). Em RuleFor separado para o When não vazar ao NotEmpty.
        // Trim antes do Contains: espelha a normalização de FaseCanonica.Criar (via
        // CodigoFase.Criar), que também compara o código já trimado contra o catálogo.
        RuleFor(x => x.Codigo)
            .Must(codigo => FaseCanonicaCatalogo.EhCanonico(codigo.Trim()))
            .WithMessage($"Código da fase deve ser um dos canônicos: {string.Join(", ", FaseCanonicaCatalogo.Codigos)}.")
            .When(x => CodigoFase.EhValido(x.Codigo));

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome da fase é obrigatório.")
            .MaximumLength(200).WithMessage("Nome da fase deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Descricao)
            .MaximumLength(300).WithMessage("Descrição da fase deve ter no máximo 300 caracteres.")
            .When(x => x.Descricao is not null);

        // Dono típico obrigatório (sempre) + domínio fechado (só quando informado).
        RuleFor(x => x.DonoTipico)
            .NotEmpty().WithMessage("Dono típico da fase é obrigatório.");

        RuleFor(x => x.DonoTipico)
            .Must(DonosTipicos.EhValido)
            .WithMessage($"Dono típico deve ser um de: {string.Join(", ", DonosTipicos.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.DonoTipico));

        RuleFor(x => x.BaseLegal)
            .MaximumLength(500).WithMessage("Base legal da fase deve ter no máximo 500 caracteres.")
            .When(x => x.BaseLegal is not null);

        // Origem da data obrigatória (sempre) + domínio fechado (só quando informada).
        RuleFor(x => x.OrigemData)
            .NotEmpty().WithMessage("Origem da data da fase é obrigatória.");

        RuleFor(x => x.OrigemData)
            .Must(OrigensDataFase.EhValido)
            .WithMessage($"Origem da data deve ser uma de: {string.Join(", ", OrigensDataFase.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrigemData));

        // CA-04: resultado definitivo verdadeiro implica produzir resultado.
        RuleFor(x => x)
            .Must(x => !x.ResultadoDefinitivo || x.ProduzResultado)
            .WithMessage("Uma fase só pode ter resultado definitivo se também produzir resultado.")
            .WithName(nameof(CriarFaseCanonicaCommand.ResultadoDefinitivo));
    }
}
