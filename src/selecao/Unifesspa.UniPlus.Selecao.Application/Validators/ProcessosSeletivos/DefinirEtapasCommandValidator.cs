namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Trimado pela ADR-0125: Nome (obrigatoriedade + tamanho), Carater (obrigatoriedade +
/// enum válido), Peso (positividade), NotaMinima (não negatividade), Ordem (positividade) e a
/// coerência da escala (nota máxima, passo) saíram daqui — <see cref="Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas.ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica"/>
/// os cobre, acumulando, na primeira passada do <c>DefinirEtapasCommandHandler</c>. O que
/// permanece é checagem de forma/rota sem equivalente de domínio: <c>PrecisionScale</c> de Peso,
/// NotaMinima, NotaMaxima e PassoDoLancamento (limite de escala do wire/coluna
/// <c>numeric(18,4)</c>, que o domínio não conhece) e <see cref="EtapaProcessoInput.TipoEtapaOrigemId"/> (Guid de rota,
/// nunca chega bruto em <see cref="Unifesspa.UniPlus.Selecao.Domain.Entities.EtapaProcesso.Criar"/> —
/// só o <c>TipoEtapaSnapshot</c> já resolvido é passado).
/// </summary>
public sealed class DefinirEtapasCommandValidator : AbstractValidator<DefinirEtapasCommand>
{
    public DefinirEtapasCommandValidator()
    {
        RegrasDoCorpo();

        // Rejeita item nulo no array (ex.: `[null]`) antes das regras de campo — sem isso o ChildRules não gera falha para o
        // elemento nulo e o handler desreferenciaria a etapa, estourando como 500.
        RuleForEach(x => x.Etapas)
            .NotNull()
            .WithMessage("Item de etapa não pode ser nulo.");

        RuleForEach(x => x.Etapas).ChildRules(RegrasDaEtapa);
    }

    private void RegrasDoCorpo()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Lista vazia é um payload válido (processo sem prova, ex. SiSU) — só a nulidade do array em si é recusada aqui.
        RuleFor(x => x.Etapas)
            .NotNull()
            .WithMessage("O campo Etapas é obrigatório (pode ser uma lista vazia).");
    }

    private static void RegrasDaEtapa(InlineValidator<EtapaProcessoInput> etapa)
    {
        // Toda etapa declara o tipo de origem — sem produção em nenhum ambiente, não há transição com tipo opcional.
        etapa.RuleFor(e => e.TipoEtapaOrigemId)
            .NotEmpty()
            .WithMessage("TipoEtapaOrigemId é obrigatório.");

        PesoENotaMinima(etapa);
        NotaMaximaEPasso(etapa);
        ItensDeAvaliacao(etapa);
        EscalaDeAtendimento(etapa);
        ColecoesNaoNulasDaEtapa(etapa);
        ItensNaoNulosDaEtapa(etapa);
        PrazosDosRecursos(etapa);
        ProdutosDaEtapa(etapa);
    }

    // Peso, nota mínima, nota máxima e passo do lançamento são persistidos como numeric(18,4). Sem o limite de escala, um valor
    // positivo com mais de 4 casas (ex.: 0.00001) passa aqui e na positividade do domínio, mas o banco o arredonda para 0.0000:
    // o peso deixaria a etapa "compondo nota" com divisor zero, a nota mínima mudaria o corte de eliminação, e o passo
    // do lançamento deixaria de ser positivo.
    private static void PesoENotaMinima(InlineValidator<EtapaProcessoInput> etapa)
    {
        etapa.RuleFor(e => e.Peso)
            .PrecisionScale(18, 4, ignoreTrailingZeros: false)
            .When(e => e.Peso.HasValue)
            .WithMessage("Peso da etapa deve ter no máximo 4 casas decimais.");

        etapa.RuleFor(e => e.NotaMinima)
            .PrecisionScale(18, 4, ignoreTrailingZeros: false)
            .When(e => e.NotaMinima.HasValue)
            .WithMessage("Nota mínima deve ter no máximo 4 casas decimais.");
    }

    private static void NotaMaximaEPasso(InlineValidator<EtapaProcessoInput> etapa)
    {
        etapa.RuleFor(e => e.NotaMaxima)
            .PrecisionScale(18, 4, ignoreTrailingZeros: false)
            .When(e => e.NotaMaxima.HasValue)
            .WithMessage("Nota máxima deve ter no máximo 4 casas decimais.");

        etapa.RuleFor(e => e.PassoDoLancamento)
            .PrecisionScale(18, 4, ignoreTrailingZeros: false)
            .When(e => e.PassoDoLancamento.HasValue)
            .WithMessage("Passo do lançamento deve ter no máximo 4 casas decimais.");
    }

    // A pontuação do item é numeric(18,4) como a nota máxima que ela compõe, e a fração do nível vive em json: sem o limite, o
    // que passa de 4 casas seria arredondado ou truncado em silêncio, e a soma dos itens deixaria de bater com o que o edital
    // declarou. Item e nível nulos no array passam incólumes pelo ChildRules e o handler os desreferenciaria.
    private static void ItensDeAvaliacao(InlineValidator<EtapaProcessoInput> etapa)
    {
        etapa.RuleForEach(e => e.ItensDeAvaliacao)
            .NotNull()
            .WithMessage("Item de avaliação da etapa não pode ser nulo.");

        etapa.RuleForEach(e => e.ItensDeAvaliacao).ChildRules(item =>
            item.RuleFor(i => i.PontuacaoMaxima)
                .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                .WithMessage("A pontuação máxima do item deve ter no máximo 4 casas decimais."));
    }

    private static void EscalaDeAtendimento(InlineValidator<EtapaProcessoInput> etapa)
    {
        etapa.RuleForEach(e => e.EscalaDeAtendimento)
            .NotNull()
            .WithMessage("Nível da escala de atendimento não pode ser nulo.");

        etapa.RuleForEach(e => e.EscalaDeAtendimento).ChildRules(nivel =>
            nivel.RuleFor(n => n.Fracao)
                .PrecisionScale(5, 4, ignoreTrailingZeros: false)
                .WithMessage("A fração do nível deve ter no máximo 4 casas decimais e ficar entre 0 e 1."));
    }

    // A COLEÇÃO em si não pode ser nula. `[JsonRequired]` recusa a carga que omite a chave, mas `"produtos": null` atravessa a
    // desserialização e chega até aqui — o tipo não-anulável não a barra, e a exigência implícita do `[ApiController]` não
    // alcança propriedade de item de coleção no corpo. Como a gravação substitui a coleção inteira, um nulo apagaria em silêncio
    // o que a etapa declara. A lista vazia continua válida: é a declaração explícita de que não há nenhum. Item nulo nas
    // coleções aninhadas passa incólume pelo ChildRules, e o handler o desreferencia — o papel do produto, o tipo da banca, a
    // evento que inicia o prazo do recurso — estourando como 500.
    private static void ColecoesNaoNulasDaEtapa(InlineValidator<EtapaProcessoInput> etapa)
    {
        etapa.RuleFor(e => e.Produtos)
            .NotNull()
            .WithMessage("Declare os produtos da etapa: lista vazia se ela não publica nenhum.");

        etapa.RuleFor(e => e.Bancas)
            .NotNull()
            .WithMessage("Declare as bancas da etapa: lista vazia se ela não requer nenhuma.");

        etapa.RuleFor(e => e.Recursos)
            .NotNull()
            .WithMessage("Declare a configuração do recurso da etapa: lista vazia se ela não admite recurso.");
    }

    private static void ItensNaoNulosDaEtapa(InlineValidator<EtapaProcessoInput> etapa)
    {
        etapa.RuleForEach(e => e.Produtos)
            .NotNull()
            .WithMessage("Item de produto da etapa não pode ser nulo.");

        etapa.RuleForEach(e => e.Bancas)
            .NotNull()
            .WithMessage("Item de banca da etapa não pode ser nulo.");

        etapa.RuleForEach(e => e.Recursos)
            .NotNull()
            .WithMessage("Item da configuração do recurso da etapa não pode ser nulo.");
    }

    // Os dois prazos do recurso são persistidos como numeric(18,4), e o que sobra de escala o banco ARREDONDA em silêncio: um
    // prazo de 0,00001 hora é estritamente positivo para o domínio, vira 0,0000 na coluna, e o prazo do recurso passa a fechar
    // no mesmo instante em que abre. Acima de catorze dígitos inteiros o banco nem arredonda: estoura 22003 como 500 no meio do
    // PUT. Mesma proteção que Peso e NotaMinima já recebem.
    private static void PrazosDosRecursos(InlineValidator<EtapaProcessoInput> etapa) =>
        etapa.RuleForEach(e => e.Recursos).ChildRules(recurso =>
        {
            recurso.RuleFor(r => r.PrazoValor)
                .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                .When(r => r.PrazoValor.HasValue)
                .WithMessage("O prazo de interposição deve caber em numeric(18,4) — no máximo 4 casas decimais.");

            recurso.RuleFor(r => r.PrazoSegundaInstanciaValor)
                .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                .When(r => r.PrazoSegundaInstanciaValor.HasValue)
                .WithMessage("O prazo da 2ª instância deve caber em numeric(18,4) — no máximo 4 casas decimais.");
        });

    // Forma do item, e só ela — mesma regra que os produtos da fase já tinham. Sem ela, o código vazio chega a
    // ProdutoDaEtapa.Criar, cuja guarda de argumento LANÇA: um corpo malformado viraria 500 em vez da recusa de validação que
    // nomeia o campo. Que o papel seja declarável e que o ato exista no catálogo continuam sendo resolução do handler.
    private static void ProdutosDaEtapa(InlineValidator<EtapaProcessoInput> etapa) =>
        etapa.RuleForEach(e => e.Produtos).ChildRules(produto =>
            produto.RuleFor(p => p.AtoCodigo)
                .NotEmpty()
                .WithMessage("O código do ato do produto é obrigatório."));
}
