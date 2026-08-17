namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Entities;

using FluentValidation;

/// <summary>
/// Duas checagens sem equivalente no agregado (ADR-0125): <c>ProcessoSeletivoId</c> é
/// identificador de rota que <c>ProcessoSeletivo.DefinirTaxaInscricao</c> não recebe; a
/// precisão/escala decimal é limite de forma de wire/coluna (<c>numeric(12,2)</c>), não regra
/// de negócio — sem ela, um valor mais preciso passa a validação e só falha em
/// <c>SaveChanges</c> com erro de banco em vez de 422. <c>GreaterThan(0)</c> foi removido: já
/// tem equivalente em <c>ConfiguracaoTaxaInscricao.Criar</c> (ValorObrigatorioQuandoCobra/
/// ValorNaoPermitidoQuandoNaoCobra cobrem qualquer valor não-positivo nos dois estados de
/// <c>Cobra</c>).
/// </summary>
public sealed class DefinirTaxaInscricaoCommandValidator : AbstractValidator<DefinirTaxaInscricaoCommand>
{
    public DefinirTaxaInscricaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.Valor)
            .PrecisionScale(ConfiguracaoTaxaInscricao.ValorPrecisao, ConfiguracaoTaxaInscricao.ValorEscala, ignoreTrailingZeros: false)
            .When(x => x.Valor.HasValue)
            .WithMessage(
                $"Valor da taxa, quando informado, deve ter no máximo {ConfiguracaoTaxaInscricao.ValorEscala} casas decimais.");
    }
}
