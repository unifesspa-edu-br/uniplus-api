namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Validação de <b>forma</b> do <see cref="DefinirDocumentosExigidosCommand"/> — o que
/// não depende de leitura externa. As invariantes de negócio da árvore (grupo não vazio,
/// sem ciclo, mesma fase, cardinalidade por tipo de nó — Story #920) são do domínio
/// (<c>NoExigencia.CriarGrupo</c>, ADR-0102); aqui só a FORMA de cada nó e um teto
/// OPERACIONAL de profundidade (segurança/DoS, não regra de satisfação — a árvore em si
/// não tem limite semântico de profundidade).
/// </summary>
public sealed class DefinirDocumentosExigidosCommandValidator : AbstractValidator<DefinirDocumentosExigidosCommand>
{
    /// <summary>Teto operacional — não é regra de satisfação (essa é ilimitada, ver <see cref="Domain.Entities.NoExigencia"/>).</summary>
    private const int MaxProfundidadeArvore = 20;

    public DefinirDocumentosExigidosCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.Raizes)
            .NotNull()
            .WithMessage("A lista de raízes da árvore de satisfação não pode ser nula.");

        RuleForEach(x => x.Raizes)
            .NotNull()
            .WithMessage("Nó raiz não pode ser nulo.")
            .SetValidator(new NoExigenciaInputValidator());

        RuleFor(x => x.Raizes)
            .Must(static raizes => ProfundidadeMaxima(raizes, 1) <= MaxProfundidadeArvore)
            .When(static x => x.Raizes is { Count: > 0 })
            .WithMessage($"A árvore de satisfação excede a profundidade operacional máxima ({MaxProfundidadeArvore}).");
    }

    private static int ProfundidadeMaxima(IReadOnlyList<NoExigenciaInput>? nos, int profundidadeAtual)
    {
        if (nos is not { Count: > 0 })
        {
            return profundidadeAtual - 1;
        }

        return nos.Max(no => ProfundidadeMaxima(no.Filhos, profundidadeAtual + 1));
    }
}

/// <summary>
/// Validação recursiva de UM nó da árvore (Story #920) — auto-referenciada via
/// <c>RuleForEach(x =&gt; x.Filhos).SetValidator(this)</c> (padrão documentado do
/// FluentValidation para estruturas em árvore): coerência de campos por
/// <see cref="NoExigenciaInput.Tipo"/> e delegação ao validador de folha.
/// </summary>
public sealed class NoExigenciaInputValidator : AbstractValidator<NoExigenciaInput>
{
    private static readonly string[] TiposValidos = ["FOLHA", "E", "OU"];

    private static readonly string[] ConsequenciasValidas =
    [
        "ELIMINA",
        "RECLASSIFICA_AC",
        "REMOVE_VANTAGEM",
        "PENDENCIA_REENVIO",
    ];

    // Story #921 — cardinalidade qualificada, catálogo fechado (Domain.Enums.ChaveDistincaoCodigo).
    private static readonly string[] ChavesDistincaoValidas = ["COMPETENCIA_MENSAL", "EXERCICIO_ANUAL", "OCORRENCIA"];

    // Story #922 — repetição por entidade, catálogo fechado (Domain.Enums.TipoEntidadeCodigo).
    private static readonly string[] TiposEntidadeValidos = ["MEMBRO_NUCLEO_FAMILIAR", "PESSOA_JURIDICA_VINCULADA"];

    public NoExigenciaInputValidator()
    {
        RuleFor(x => x.Tipo)
            .Must(static valor => TiposValidos.Contains(valor, StringComparer.Ordinal))
            .WithMessage($"Tipo do nó deve ser um de: {string.Join(", ", TiposValidos)}.");

        RuleFor(x => x.Documento)
            .NotNull()
            .When(static x => x.Tipo == "FOLHA")
            .WithMessage("Um nó FOLHA precisa declarar 'documento'.");

        RuleFor(x => x.Documento)
            .Null()
            .When(static x => x.Tipo is "E" or "OU")
            .WithMessage("Um nó E/OU não pode declarar 'documento' — só folha declara.");

        RuleFor(x => x.Filhos)
            .Must(static filhos => filhos is { Count: > 0 })
            .When(static x => x.Tipo is "E" or "OU")
            .WithMessage("Um nó E/OU precisa de ao menos um filho.");

        RuleFor(x => x.Filhos)
            .Must(static filhos => filhos is null or { Count: 0 })
            .When(static x => x.Tipo == "FOLHA")
            .WithMessage("Um nó FOLHA não pode ter filhos.");

        // Story #921: quantidadeMinima passa a ser aceita também em FOLHA (cardinalidade de
        // apresentações) — só grupo E permanece sem cardinalidade própria (transparente).
        RuleFor(x => x.QuantidadeMinima)
            .Null()
            .When(static x => x.Tipo == "E")
            .WithMessage("quantidadeMinima só é permitida em nó FOLHA ou OU/N-de.");

        RuleFor(x => x.QuantidadeMinima)
            .GreaterThanOrEqualTo(1)
            .When(static x => x.Tipo is "OU" or "FOLHA" && x.QuantidadeMinima is not null)
            .WithMessage("quantidadeMinima, quando presente, deve ser maior ou igual a 1.");

        // Story #921 — cardinalidade qualificada, exclusiva de FOLHA. A coerência
        // chave×dataReferencia×ocorrenciasEsperadas por valor de chaveDistincao é do domínio
        // (NoExigencia.CriarFolha) — aqui só a forma (catálogo fechado + campo restrito a FOLHA).
        RuleFor(x => x.ChaveDistincao)
            .Null()
            .When(static x => x.Tipo is "E" or "OU")
            .WithMessage("chaveDistincao só é permitida em nó FOLHA.");

        // `x.ChaveDistincao is not null` (não `!string.IsNullOrWhiteSpace`) — string vazia/em
        // branco também precisa passar por este Must e ser recusada: senão o handler a repassa
        // ao domínio, que a mapeia silenciosamente para "sem qualificação" (Nenhuma) via
        // ChaveDistincaoCodigo.FromCodigo, mascarando um payload malformado como 204 aceito.
        RuleFor(x => x.ChaveDistincao)
            .Must(static valor => ChavesDistincaoValidas.Contains(valor, StringComparer.Ordinal))
            .When(static x => x.Tipo == "FOLHA" && x.ChaveDistincao is not null)
            .WithMessage($"chaveDistincao deve ser um de: {string.Join(", ", ChavesDistincaoValidas)}.");

        RuleFor(x => x.DataReferencia)
            .Null()
            .When(static x => x.Tipo is "E" or "OU")
            .WithMessage("dataReferencia só é permitida em nó FOLHA.");

        RuleFor(x => x.OcorrenciasEsperadas)
            .Must(static ocorrencias => ocorrencias is null or { Count: 0 })
            .When(static x => x.Tipo is "E" or "OU")
            .WithMessage("ocorrenciasEsperadas só é permitida em nó FOLHA.");

        RuleFor(x => x.Consequencia)
            .Null()
            .When(static x => x.Tipo is "FOLHA" or "E")
            .WithMessage("consequencia de nó só é permitida em grupo OU/N-de.");

        RuleFor(x => x.Consequencia)
            .Must(static valor => ConsequenciasValidas.Contains(valor, StringComparer.Ordinal))
            .When(static x => x.Tipo == "OU" && !string.IsNullOrWhiteSpace(x.Consequencia))
            .WithMessage($"Consequência do grupo deve ser um de: {string.Join(", ", ConsequenciasValidas)}.");

        RuleFor(x => x.BasesLegais)
            .Must(static basesLegais => basesLegais is null or { Count: 0 })
            .When(static x => x.Tipo is "FOLHA" or "E" || string.IsNullOrWhiteSpace(x.Consequencia))
            .WithMessage("basesLegais de nó só é permitida em grupo OU/N-de com consequência declarada.");

        // Story #922 — repetição por entidade: permitida em QUALQUER tipo de nó (folha ou
        // grupo, ao contrário de chaveDistincao/dataReferencia/ocorrenciasEsperadas, exclusivas
        // de folha) — só o catálogo fechado é validado aqui; aninhamento é invariante de
        // ÁRVORE (não dá para checar localmente por nó) e fica com o domínio
        // (NoExigencia.CriarGrupo). Mesmo cuidado de `is not null` (não IsNullOrWhiteSpace) do
        // chaveDistincao: string vazia/em branco também precisa cair no Must.
        RuleFor(x => x.RepetePorEntidade)
            .Must(static valor => TiposEntidadeValidos.Contains(valor, StringComparer.Ordinal))
            .When(static x => x.RepetePorEntidade is not null)
            .WithMessage($"repetePorEntidade deve ser um de: {string.Join(", ", TiposEntidadeValidos)}.");

        RuleFor(x => x.Documento!)
            .SetValidator(new ItemDocumentoExigidoInputValidator())
            .When(static x => x.Tipo == "FOLHA" && x.Documento is not null);

        RuleForEach(x => x.Filhos)
            .SetValidator(this)
            .When(static x => x.Filhos is { Count: > 0 });
    }
}

/// <summary>Validação de forma de UMA folha (<see cref="ItemDocumentoExigidoInput"/>) — mesmas regras do modelo plano anterior, menos <c>GrupoSatisfacaoId</c> (a posição na árvore o substitui, Story #920).</summary>
public sealed class ItemDocumentoExigidoInputValidator : AbstractValidator<ItemDocumentoExigidoInput>
{
    private static readonly string[] AplicabilidadesValidas = ["GERAL", "CONDICIONAL"];

    private static readonly string[] ConsequenciasValidas =
    [
        "ELIMINA",
        "RECLASSIFICA_AC",
        "REMOVE_VANTAGEM",
        "PENDENCIA_REENVIO",
    ];

    // Story #916: DIFERENTE/NAO_EM (operadores de exclusão) somam-se aos 4 originais.
    private static readonly string[] OperadoresValidos = ["IGUAL", "EM", "MAIOR_IGUAL", "MENOR_IGUAL", "DIFERENTE", "NAO_EM"];

    private static readonly string[] AbrangenciasValidas = ["FEDERAL", "ESTADUAL", "MUNICIPAL", "INTERNA_NORMA", "INTERNA_EDITAL"];

    private static readonly string[] StatusBaseLegalValidos = ["PENDENTE", "RESOLVIDO"];

    // Espelham DocumentoExigidoBaseLegalConfiguration (HasMaxLength) — sem este teto aqui,
    // um PUT com referência/observação longa demais passa pela validação de forma e só
    // falha no SaveChanges (DbUpdateException/500), em vez de 400 acionável.
    private const int ReferenciaBaseLegalMaxLength = 500;
    private const int ObservacaoBaseLegalMaxLength = 1000;

    // Story #554/issue #893 (PR #900) — idade máxima de emissão, formato e tamanho.
    private static readonly string[] UnidadesIdadeValidas = ["DIAS", "MESES", "ANOS"];

    private static readonly string[] ReferenciaTiposIdadeEmissaoValidos =
        ["FIM_INSCRICAO", "INICIO_FASE", "FIM_FASE", "DATA_ESPECIFICA", "DATA_SUBMISSAO"];

    public ItemDocumentoExigidoInputValidator()
    {
        RuleFor(i => i.ExigidoNaFaseId)
            .NotEmpty()
            .WithMessage("A fase em que o documento é exigido é obrigatória.");

        RuleFor(i => i.TipoDocumentoId)
            .NotEmpty()
            .WithMessage("O id do tipo de documento é obrigatório.");

        RuleFor(i => i.Aplicabilidade)
            .Must(valor => AplicabilidadesValidas.Contains(valor, StringComparer.Ordinal))
            .WithMessage($"Aplicabilidade deve ser um de: {string.Join(", ", AplicabilidadesValidas)}.");

        RuleFor(i => i.ConsequenciaIndeferimento)
            .Must(valor => ConsequenciasValidas.Contains(valor, StringComparer.Ordinal))
            .When(i => !string.IsNullOrWhiteSpace(i.ConsequenciaIndeferimento))
            .WithMessage($"Consequência de indeferimento deve ser um de: {string.Join(", ", ConsequenciasValidas)}.");

        RuleFor(i => i.Condicoes)
            .NotNull()
            .WithMessage("A lista de condições de gatilho não pode ser nula.");

        RuleForEach(i => i.Condicoes).ChildRules(condicao =>
        {
            condicao.RuleFor(c => c.Clausula)
                .GreaterThanOrEqualTo(0)
                .WithMessage("O ordinal da cláusula não pode ser negativo.");

            condicao.RuleFor(c => c.Fato)
                .NotEmpty()
                .WithMessage("O fato da condição é obrigatório.");

            condicao.RuleFor(c => c.Operador)
                .Must(valor => OperadoresValidos.Contains(valor, StringComparer.Ordinal))
                .WithMessage($"Operador deve ser um de: {string.Join(", ", OperadoresValidos)}.");

            condicao.RuleFor(c => c.Valor)
                .NotEmpty()
                .WithMessage("O valor da condição é obrigatório.");
        });

        RuleFor(i => i.BasesLegais)
            .NotNull()
            .WithMessage("A lista de bases legais não pode ser nula.");

        RuleForEach(i => i.BasesLegais)
            .NotNull()
            .WithMessage("Item de base legal não pode ser nulo.");

        RuleForEach(i => i.BasesLegais).ChildRules(baseLegal =>
        {
            baseLegal.RuleFor(b => b.Referencia)
                .NotEmpty()
                .WithMessage("A referência da base legal é obrigatória.")
                .MaximumLength(ReferenciaBaseLegalMaxLength)
                .WithMessage($"A referência da base legal deve ter no máximo {ReferenciaBaseLegalMaxLength} caracteres.");

            baseLegal.RuleFor(b => b.Abrangencia)
                .Must(valor => AbrangenciasValidas.Contains(valor, StringComparer.Ordinal))
                .WithMessage($"Abrangência da base legal deve ser um de: {string.Join(", ", AbrangenciasValidas)}.");

            baseLegal.RuleFor(b => b.Status)
                .Must(valor => StatusBaseLegalValidos.Contains(valor, StringComparer.Ordinal))
                .WithMessage($"Status da base legal deve ser um de: {string.Join(", ", StatusBaseLegalValidos)}.");

            baseLegal.RuleFor(b => b.Observacao)
                .MaximumLength(ObservacaoBaseLegalMaxLength)
                .When(b => b.Observacao is not null)
                .WithMessage($"A observação da base legal deve ter no máximo {ObservacaoBaseLegalMaxLength} caracteres.");
        });

        // A coerência tudo-nulo OU completo (Valor/Unidade/ReferenciaTipo) é do
        // domínio (IdadeMaximaEmissao.Criar) — aqui só a forma de cada campo NÃO
        // NULO, mesmo espírito de DefinirReferenciaTemporalFatosCommandValidator (PR #896).
        RuleFor(i => i.IdadeMaximaEmissao!.Valor)
            .GreaterThan(0)
            .When(i => i.IdadeMaximaEmissao?.Valor is not null)
            .WithMessage("O valor da idade máxima de emissão deve ser maior que zero.");

        RuleFor(i => i.IdadeMaximaEmissao!.Unidade)
            .Must(valor => UnidadesIdadeValidas.Contains(valor, StringComparer.Ordinal))
            .When(i => i.IdadeMaximaEmissao?.Unidade is not null)
            .WithMessage($"Unidade da idade máxima de emissão deve ser um de: {string.Join(", ", UnidadesIdadeValidas)}.");

        RuleFor(i => i.IdadeMaximaEmissao!.ReferenciaTipo)
            .Must(valor => ReferenciaTiposIdadeEmissaoValidos.Contains(valor, StringComparer.Ordinal))
            .When(i => i.IdadeMaximaEmissao?.ReferenciaTipo is not null)
            .WithMessage($"ReferenciaTipo da idade máxima de emissão deve ser um de: {string.Join(", ", ReferenciaTiposIdadeEmissaoValidos)}.");

        // FormatosPermitidos (Story #918) não tem regra aqui: é um valor JSON
        // polimórfico (JsonElement?, mesmo tratamento de CondicaoGatilhoInput.Valor) que
        // o HANDLER interpreta por ValueKind e o DOMÍNIO (FormatosPermitidos.Criar)
        // valida — os erros nomeados (Obrigatorio/FormaInvalida/FormatoInvalido/
        // FormatoDuplicado/QualquerComFormatosEspecificos/TamanhoMaximoBytesMaxInvalido)
        // são DomainError (422), não FluentValidation (400): a interpretação do
        // ValueKind já é, em si, uma leitura semântica do dado, não só forma.

        RuleFor(i => i.TamanhoMaximoBytes)
            .GreaterThan(0)
            .When(i => i.TamanhoMaximoBytes is not null)
            .WithMessage("O tamanho máximo em bytes, quando presente, deve ser maior que zero.");
    }
}
