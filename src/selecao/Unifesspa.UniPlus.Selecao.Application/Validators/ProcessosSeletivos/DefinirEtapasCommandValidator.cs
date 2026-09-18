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

            // A COLEÇÃO em si não pode ser nula. `[JsonRequired]` recusa a carga que omite a
            // chave, mas `"produtos": null` atravessa a desserialização e chega até aqui — o
            // tipo não-anulável não a barra, e a exigência implícita do `[ApiController]` não
            // alcança propriedade de item de coleção no corpo. Como a gravação substitui a
            // coleção inteira, um nulo apagaria em silêncio o que a etapa declara.
            // A lista vazia continua válida: é a declaração explícita de que não há nenhum.
            etapa.RuleFor(e => e.Produtos)
                .NotNull()
                .WithMessage("Declare os produtos da etapa: lista vazia se ela não publica nenhum.");

            etapa.RuleFor(e => e.Bancas)
                .NotNull()
                .WithMessage("Declare as bancas da etapa: lista vazia se ela não requer nenhuma.");

            etapa.RuleFor(e => e.Recursos)
                .NotNull()
                .WithMessage("Declare as janelas recursais da etapa: lista vazia se ela não abre nenhuma.");

            // Mesma proteção do array de etapas, um nível abaixo: item nulo nas coleções
            // aninhadas passa incólume pelo ChildRules, e o handler o desreferencia — o papel
            // do produto, o tipo da banca, a âncora do recurso — estourando como 500.
            etapa.RuleForEach(e => e.Produtos)
                .NotNull()
                .WithMessage("Item de produto da etapa não pode ser nulo.");

            etapa.RuleForEach(e => e.Bancas)
                .NotNull()
                .WithMessage("Item de banca da etapa não pode ser nulo.");

            etapa.RuleForEach(e => e.Recursos)
                .NotNull()
                .WithMessage("Item de janela recursal da etapa não pode ser nulo.");

            // O prazo de interposição e os dois pares de suspensividade são persistidos como
            // numeric(18,4), e o que sobra de escala o banco ARREDONDA em silêncio: um prazo
            // de 0,00001 hora é estritamente positivo para ValidacaoDeArgsDeRecurso, vira
            // 0,0000 na coluna, e a janela recursal passa a fechar no mesmo instante em que
            // abre — exatamente o que aquela validação existe para impedir. Acima de catorze
            // dígitos inteiros o banco nem arredonda: estoura 22003 como 500 no meio do PUT.
            // Mesma proteção que Peso e NotaMinima já recebem um nível acima.
            etapa.RuleForEach(e => e.Recursos).ChildRules(recurso =>
            {
                recurso.RuleFor(r => r.PrazoValor)
                    .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                    .WithMessage("O prazo de interposição deve caber em numeric(18,4) — no máximo 4 casas decimais.");

                recurso.RuleFor(r => r.SuspensividadePrimeiraInstanciaValor)
                    .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                    .When(r => r.SuspensividadePrimeiraInstanciaValor.HasValue)
                    .WithMessage("A suspensividade da 1ª instância deve caber em numeric(18,4) — no máximo 4 casas decimais.");

                recurso.RuleFor(r => r.SuspensividadeSegundaInstanciaValor)
                    .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                    .When(r => r.SuspensividadeSegundaInstanciaValor.HasValue)
                    .WithMessage("A suspensividade da 2ª instância deve caber em numeric(18,4) — no máximo 4 casas decimais.");
            });

            // Forma do item, e só ela — mesma regra que os produtos da fase já tinham. Sem
            // ela, o código vazio chega a ProdutoDaEtapa.Criar, cuja guarda de argumento
            // LANÇA: um corpo malformado viraria 500 em vez da recusa de validação que
            // nomeia o campo. Que o papel seja declarável e que o ato exista no catálogo
            // continuam sendo resolução do handler.
            etapa.RuleForEach(e => e.Produtos).ChildRules(produto =>
            {
                produto.RuleFor(p => p.AtoCodigo)
                    .NotEmpty()
                    .WithMessage("O código do ato do produto é obrigatório.");
            });
        });
    }
}
