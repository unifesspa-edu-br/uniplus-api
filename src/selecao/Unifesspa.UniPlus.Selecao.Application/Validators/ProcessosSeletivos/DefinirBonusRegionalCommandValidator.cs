namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Três checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota; <c>RegraVersao</c>/<c>Fator</c> obrigatórios quando
/// <c>RegraCodigo</c> é informado são coerência de wire anterior à resolução da regra —
/// <c>ConfiguracaoBonusRegional.Criar</c> só é chamado depois que o handler já resolveu a
/// regra no <c>rol_de_regras</c>, então nunca recebe esses campos crus; a precisão/escala
/// decimal de <c>Fator</c>/<c>Teto</c> é limite de forma de wire/coluna
/// (<c>numeric(6,4)</c>), não regra de negócio. <c>GreaterThan(0)</c> e os limites de
/// tamanho de <c>MunicipioConvenio</c>/<c>BaseLegal</c> foram removidos: já têm equivalente
/// em <c>ConfiguracaoBonusRegional.Criar</c>.
/// </summary>
public sealed class DefinirBonusRegionalCommandValidator : AbstractValidator<DefinirBonusRegionalCommand>
{
    public DefinirBonusRegionalCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // RegraCodigo nulo = remover o bônus (toggle por ausência, INV-B5) —
        // as demais regras só se aplicam quando um bônus está sendo definido.
        RuleFor(x => x.RegraVersao)
            .NotEmpty()
            .When(x => x.RegraCodigo is not null)
            .WithMessage("Versão da regra de bônus é obrigatória quando RegraCodigo é informado.");

        RuleFor(x => x.Fator)
            .NotNull()
            .PrecisionScale(6, 4, ignoreTrailingZeros: false)
            .When(x => x.RegraCodigo is not null)
            .WithMessage("Fator do bônus, quando RegraCodigo é informado, é obrigatório e deve ter no máximo 4 casas decimais.");

        RuleFor(x => x.Teto)
            .PrecisionScale(6, 4, ignoreTrailingZeros: false)
            .When(x => x.Teto.HasValue)
            .WithMessage("Teto do bônus, quando informado, deve ter no máximo 4 casas decimais.");
    }
}
