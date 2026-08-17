namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Trimado pela ADR-0125: Nome (obrigatoriedade + tamanho), Carater (obrigatoriedade +
/// enum válido), Peso (positividade), NotaMinima (não negatividade) e Ordem (positividade)
/// saíram daqui — <see cref="Unifesspa.UniPlus.Selecao.Domain.Entities.EtapaProcesso.ValidarFormaBasica"/>
/// os cobre, acumulando, na primeira passada do <c>DefinirEtapasCommandHandler</c>. O que
/// permanece é checagem de forma/rota sem equivalente de domínio: <c>PrecisionScale</c> (limite
/// de escala do wire/coluna <c>numeric(18,4)</c>, sem helper de domínio ainda — achado B2 do
/// mapeamento da rolagem) e <see cref="EtapaProcessoInput.TipoEtapaOrigemId"/> (Guid de rota,
/// nunca chega bruto em <see cref="Unifesspa.UniPlus.Selecao.Domain.Entities.EtapaProcesso.Criar"/> —
/// só o <c>TipoEtapaSnapshot</c> já resolvido é passado).
/// </summary>
public sealed class DefinirEtapasCommandValidator : AbstractValidator<DefinirEtapasCommand>
{
    public DefinirEtapasCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Story #851 §3.5: lista vazia passa a ser um payload válido (processo sem
        // prova, ex. SiSU) — só a nulidade do array em si é recusada aqui.
        RuleFor(x => x.Etapas)
            .NotNull()
            .WithMessage("O campo Etapas é obrigatório (pode ser uma lista vazia).");

        // Rejeita item nulo no array (ex.: `[null]`) antes das regras de campo —
        // sem isso o ChildRules não gera falha para o elemento nulo e o handler
        // desreferenciaria a etapa, estourando como 500.
        RuleForEach(x => x.Etapas)
            .NotNull()
            .WithMessage("Item de etapa não pode ser nulo.");

        RuleForEach(x => x.Etapas).ChildRules(etapa =>
        {
            // Issue #1071: toda etapa declara o tipo de origem — sem produção em
            // nenhum ambiente, não há transição com tipo opcional.
            etapa.RuleFor(e => e.TipoEtapaOrigemId)
                .NotEmpty()
                .WithMessage("TipoEtapaOrigemId é obrigatório.");

            // O peso é persistido como numeric(18,4). Sem o limite de escala,
            // um valor positivo com mais de 4 casas (ex.: 0.00001) passa aqui e
            // no guard de divisor-zero do domínio, mas o banco arredonda para
            // 0.0000 — depois do reload a etapa continua "compondo nota"
            // (Peso.HasValue) enquanto CalcularDivisorMedia() soma zero.
            etapa.RuleFor(e => e.Peso)
                .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                .When(e => e.Peso.HasValue)
                .WithMessage("Peso da etapa deve ter no máximo 4 casas decimais.");

            // NotaMinima também é persistida como numeric(18,4) e controla o
            // corte de eliminação — mesma proteção de escala do peso, para o
            // banco não arredondar o limiar silenciosamente.
            etapa.RuleFor(e => e.NotaMinima)
                .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                .When(e => e.NotaMinima.HasValue)
                .WithMessage("Nota mínima deve ter no máximo 4 casas decimais.");
        });
    }
}
