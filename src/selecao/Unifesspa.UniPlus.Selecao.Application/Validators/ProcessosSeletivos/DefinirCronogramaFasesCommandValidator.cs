namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Validação de <b>forma</b> do <see cref="DefinirCronogramaFasesCommand"/> — o que não
/// depende de leitura externa. As invariantes de negócio (piso mínimo, precedência,
/// bicondicional fase×etapa, resolução da regra/ato âncora) são do domínio e do handler
/// (ADR-0102).
/// </summary>
/// <remarks>
/// A lista de fases vazia também não está aqui: <c>ProcessoSeletivo.CronogramaFasesVazio</c>
/// a recusa com erro nomeado, e a regra de borda tornava essa causa inalcançável por um
/// cliente HTTP.
/// <para>A checagem de janela (Fim ≥ Início) NÃO está aqui, de propósito — desde a ADR-0125,
/// <c>FaseCronograma.JanelaInvertida</c> acumula no domínio junto das demais violações da
/// mesma fase (ex.: parecer individual prometido sem publicação de resultado). Mantê-la
/// também aqui faria o FluentValidation
/// (middleware, sempre roda primeiro) bloquear sozinho um payload com janela invertida +
/// outra violação, entregando ao cliente só o erro de janela — a acumulação do domínio
/// nunca chegaria a rodar. Ordem (só <c>throw</c> no domínio, nunca acumulada) não tem
/// esse conflito e continua validada aqui.</para>
/// </remarks>
public sealed class DefinirCronogramaFasesCommandValidator : AbstractValidator<DefinirCronogramaFasesCommand>
{
    public DefinirCronogramaFasesCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleForEach(x => x.Fases)
            .NotNull()
            .WithMessage("Item de fase não pode ser nulo.");

        RuleForEach(x => x.Fases).ChildRules(RegrasDaFase);
    }

    private static void RegrasDaFase(InlineValidator<FaseCronogramaInput> fase)
    {
        fase.RuleFor(f => f.Ordem)
            .GreaterThan(0)
            .WithMessage("A ordem da fase deve ser maior que zero.");

        fase.RuleFor(f => f.FaseCanonicaId)
            .NotEmpty()
            .WithMessage("O id da fase canônica é obrigatório.");

        ColecoesNaoNulasDaFase(fase);
        ItensNaoNulosDaFase(fase);
        BancasDaFase(fase);
        ProdutosDaFase(fase);
        RegraDeRecursoDaFase(fase);
    }

    // A COLEÇÃO em si não pode ser nula. O host não habilita RespectRequiredConstructorParameters, então a chave omitida no
    // corpo não faz o System.Text.Json recusar nada: o parâmetro de construtor recebe null, e o handler o percorre por índice
    // (BancasRequeridas.Count, Produtos.Count), o que estoura como 500. A lista vazia continua válida — é a declaração
    // explícita de que a fase não requer banca alguma e não publica nada. Item nulo dentro das coleções aninhadas passa
    // incólume pelo ChildRules — a regra de forma do filho nem chega a rodar —, e o handler o desreferencia: o tipo da banca,
    // o papel do produto.
    private static void ColecoesNaoNulasDaFase(InlineValidator<FaseCronogramaInput> fase)
    {
        fase.RuleFor(f => f.BancasRequeridas)
            .NotNull()
            .WithMessage("Declare as bancas requeridas pela fase: lista vazia se ela não requer nenhuma.");

        fase.RuleFor(f => f.Produtos)
            .NotNull()
            .WithMessage("Declare os produtos da fase: lista vazia se ela não publica nenhum.");
    }

    private static void ItensNaoNulosDaFase(InlineValidator<FaseCronogramaInput> fase)
    {
        fase.RuleForEach(f => f.BancasRequeridas)
            .NotNull()
            .WithMessage("Item de banca requerida não pode ser nulo.");

        fase.RuleForEach(f => f.Produtos)
            .NotNull()
            .WithMessage("Item de produto da fase não pode ser nulo.");
    }

    // Forma do item, e só ela. Quando os candidatos atendidos pela banca precisam ser declarados e quando duas bancas atendem os
    // mesmos candidatos são invariantes da fase (ADR-0125), e a existência da categoria no cadastro é resolução do handler.
    private static void BancasDaFase(InlineValidator<FaseCronogramaInput> fase) =>
        fase.RuleForEach(f => f.BancasRequeridas).ChildRules(RegrasDaBanca);

    private static void RegrasDaBanca(InlineValidator<BancaRequeridaInput> banca)
    {
        banca.RuleFor(b => b.TipoBancaId)
            .NotEmpty()
            .WithMessage("O id do tipo de banca não pode ser vazio.");

        // A COLEÇÃO em si, pela mesma razão das de cima: os candidatos atendidos pela banca são declarados como coleção
        // não-anulável, nada exige a chave no corpo, e o handler percorre a lista para congelar cada categoria julgada.
        // Coleção nula é percorrida como vazia pelo RuleForEach, que por isso não cobre este caso.
        banca.RuleFor(b => b.CategoriasDocumentoIds)
            .NotNull()
            .WithMessage("Declare as categorias de documento julgadas pela banca: lista vazia quando o tipo já a identifica sozinho na fase.");

        banca.RuleForEach(b => b.CategoriasDocumentoIds)
            .NotEmpty()
            .WithMessage("O id da categoria de documento não pode ser vazio.");
    }

    // Forma do item, e só ela. Que o papel seja declarável, que o tipo de ato exista e que ele seja resultado no catálogo são
    // resoluções do handler, e que o mesmo ato não se repita na fase é invariante do agregado (ADR-0125).
    private static void ProdutosDaFase(InlineValidator<FaseCronogramaInput> fase) =>
        fase.RuleForEach(f => f.Produtos).ChildRules(produto =>
            produto.RuleFor(p => p.AtoCodigo)
                .NotEmpty()
                .WithMessage("O código do ato do produto é obrigatório."));

    private static void RegraDeRecursoDaFase(InlineValidator<FaseCronogramaInput> fase) =>
        fase.When(f => f.RegraRecurso is not null, () =>
        {
            IdentificacaoDaRegraDeRecurso(fase);
            PrazosDaRegraDeRecurso(fase);
        });

    private static void IdentificacaoDaRegraDeRecurso(InlineValidator<FaseCronogramaInput> fase)
    {
        fase.RuleFor(f => f.RegraRecurso!.RegraCodigo)
            .NotEmpty()
            .WithMessage("O código da regra de recurso é obrigatório.");

        fase.RuleFor(f => f.RegraRecurso!.RegraVersao)
            .NotEmpty()
            .WithMessage("A versão da regra de recurso é obrigatória.");

        fase.RuleFor(f => f.RegraRecurso!.AtoAncoraCodigo)
            .NotEmpty()
            .WithMessage("O código do ato cuja publicação inicia o prazo é obrigatório.");
    }

    // Os dois prazos são persistidos como numeric(18,4): o que sobra de escala o banco arredonda em silêncio, e o excesso de
    // dígitos inteiros estoura como 500 no meio do PUT. Magnitude, unidade, efeito, guarda e prazo da 2ª instância não
    // aparecem aqui: são recusas do domínio, que é a fonte única de validação (ADR-0125); repeti-las devolveria a resposta
    // genérica do validator no lugar do erro nomeado.
    private static void PrazosDaRegraDeRecurso(InlineValidator<FaseCronogramaInput> fase)
    {
        fase.RuleFor(f => f.RegraRecurso!.PrazoValor)
            .PrecisionScale(18, 4, ignoreTrailingZeros: false)
            .When(f => f.RegraRecurso!.PrazoValor.HasValue)
            .WithMessage("O prazo de interposição deve caber em numeric(18,4) — no máximo 4 casas decimais.");

        fase.RuleFor(f => f.RegraRecurso!.PrazoSegundaInstanciaValor)
            .PrecisionScale(18, 4, ignoreTrailingZeros: false)
            .When(f => f.RegraRecurso!.PrazoSegundaInstanciaValor.HasValue)
            .WithMessage("O prazo da 2ª instância deve caber em numeric(18,4) — no máximo 4 casas decimais.");
    }
}
