namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um fato do vocabulário que <b>este</b> processo seletivo coleta do candidato, com a sua
/// posição na ordem de coleta, a pré-condição que decide se o campo produtor é apresentado
/// (Story #926) e a apresentação do campo no formulário de inscrição — rótulo, tipo de
/// renderização e obrigatoriedade (Story #559).
/// </summary>
/// <remarks>
/// <para>
/// A pré-condição é <b>aresta do grafo por processo, não propriedade do catálogo</b>: o mesmo
/// fato pode ser coletado sem gate nenhum em outro processo. Por isso vive aqui, na configuração
/// do certame, e não em <c>FatoCandidato</c> — que é catálogo global e seed-governado (ADR-0111).
/// A apresentação é pelo mesmo motivo: o mesmo fato pode ter rótulo e obrigatoriedade diferentes
/// em processos diferentes.
/// </para>
/// <para>
/// Fato <b>sem</b> pré-condição é coletado sempre — é o caso das autodeclarações de abertura, que
/// não dependem de nada anterior. Fato com pré-condição só é apresentado quando o predicado
/// resolve verdadeiro; quando resolve falso, o fato vinculado passa a não-aplicável, e quando
/// fica indeterminado, o fato o acompanha.
/// </para>
/// </remarks>
public sealed class FatoColetado : EntityBase
{
    /// <summary>Alinhado a <c>FatoColetadoConfiguration</c> (varchar(60)).</summary>
    public const int FatoCodigoMaxLength = 60;

    /// <summary>
    /// Alinhado a <c>FatoColetadoConfiguration</c> (varchar(300)) — mesma grandeza de
    /// <c>LimitesDoEnvelope.NomeDeCadastro</c> (o decoder do envelope aplica o mesmo limite ao
    /// reidratar): um rótulo de campo de formulário é a mesma grandeza de um nome de cadastro
    /// curto.
    /// </summary>
    public const int RotuloMaxLength = 300;

    private readonly List<CondicaoPrecondicaoFato> _precondicoes = [];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Código do fato no vocabulário fechado do candidato.</summary>
    public string FatoCodigo { get; private set; } = string.Empty;

    /// <summary>
    /// Posição na ordem de coleta. Estritamente crescente e única no processo: é ela que dá
    /// sentido a "fato anterior", e todo fato citado numa pré-condição precisa ter ordem menor
    /// que a do fato que o cita.
    /// </summary>
    public int Ordem { get; private set; }

    /// <summary>Rótulo do campo no formulário de inscrição.</summary>
    public string Rotulo { get; private set; } = string.Empty;

    /// <summary>
    /// Como o campo é renderizado. A coerência contra o <c>Dominio</c> do fato no catálogo (ex.:
    /// domínio <c>BOOLEANO</c> só aceita <see cref="TipoRenderizacao.Booleano"/>) é validada na
    /// Application, que tem acesso ao vocabulário cross-módulo — o Domain não alcança o catálogo.
    /// </summary>
    public TipoRenderizacao TipoRenderizacao { get; private set; }

    /// <summary>Se o candidato é obrigado a preencher o campo para prosseguir com a inscrição.</summary>
    public bool Obrigatorio { get; private set; }

    public IReadOnlyCollection<CondicaoPrecondicaoFato> Precondicoes => _precondicoes.AsReadOnly();

    private FatoColetado() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — os
    /// quatro campos e a autorreferência de pré-condição não dependem uns dos outros. A
    /// mensagem de autorreferência não ecoa o código do fato (ADR-0023).
    /// </summary>
    public static Result<FatoColetado> Criar(
        string fatoCodigo,
        int ordem,
        string rotulo,
        TipoRenderizacao tipoRenderizacao,
        bool obrigatorio,
        IReadOnlyList<CondicaoPrecondicaoFato>? precondicoes)
    {
        List<FieldError> erros = ValidarFormaBasica(fatoCodigo, ordem, rotulo, tipoRenderizacao);

        string codigo = fatoCodigo?.Trim() ?? string.Empty;
        IReadOnlyList<CondicaoPrecondicaoFato> condicoes = precondicoes ?? [];

        // Auto-referência é ciclo de comprimento um. Barrada aqui, na criação, porque não depende
        // de conhecer os irmãos — e um erro específico diz mais do que o genérico de ciclo, que
        // reportaria um caminho de um nó só. Toda a validação acontece antes de qualquer mutação:
        // uma condição só é vinculada depois de o fato inteiro ser aceito, para nunca deixar uma
        // instância recebida do chamador apontando para um fato que a factory acabou de recusar.
        if (condicoes.Any(precondicao => string.Equals(precondicao.Fato, codigo, StringComparison.Ordinal)))
        {
            erros.Add(new("precondicao", new DomainError(
                FatoColetadoErrorCodes.PrecondicaoAutorreferente,
                "A pré-condição cita o próprio fato.")));
        }

        if (erros.Count > 0)
        {
            return Result<FatoColetado>.ValidationFailure(erros);
        }

        FatoColetado fato = new()
        {
            FatoCodigo = codigo,
            Ordem = ordem,
            Rotulo = rotulo.Trim(),
            TipoRenderizacao = tipoRenderizacao,
            Obrigatorio = obrigatorio,
        };
        foreach (CondicaoPrecondicaoFato precondicao in condicoes)
        {
            precondicao.VincularFatoColetado(fato.Id);
            fato._precondicoes.Add(precondicao);
        }

        return Result<FatoColetado>.Success(fato);
    }

    /// <summary>
    /// Os quatro campos que não dependem do vocabulário cross-módulo nem de a pré-condição já
    /// ter sido resolvida — ao contrário da autorreferência, checada só dentro de
    /// <see cref="Criar"/>. Existe separada para o handler poder confirmar a forma de TODOS os
    /// fatos do payload numa primeira passada, antes de resolver o catálogo de cada um (achado
    /// de revisão): sem essa ordem, um <c>fatoCodigo</c> vazio caía direto no
    /// <c>FatoDesconhecido</c> da busca no catálogo — um erro menos específico — e uma violação
    /// de forma de um fato podia ser mascarada pelo erro semântico de outro fato da mesma lista.
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(string? fatoCodigo, int ordem, string? rotulo, TipoRenderizacao tipoRenderizacao)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(fatoCodigo))
        {
            erros.Add(new("fatoCodigo", new DomainError(
                FatoColetadoErrorCodes.FatoCodigoObrigatorio,
                "O código do fato coletado é obrigatório.")));
        }
        else if (fatoCodigo.Trim().Length > FatoCodigoMaxLength)
        {
            erros.Add(new("fatoCodigo", new DomainError(
                FatoColetadoErrorCodes.FatoCodigoTamanho,
                $"O código do fato coletado deve ter no máximo {FatoCodigoMaxLength} caracteres.")));
        }

        if (ordem < 0)
        {
            erros.Add(new("ordem", new DomainError(
                FatoColetadoErrorCodes.OrdemInvalida,
                "A ordem de coleta não pode ser negativa.")));
        }

        if (string.IsNullOrWhiteSpace(rotulo))
        {
            erros.Add(new("rotulo", new DomainError(
                FatoColetadoErrorCodes.RotuloObrigatorio,
                "O rótulo do fato coletado é obrigatório.")));
        }
        else if (rotulo.Trim().Length > RotuloMaxLength)
        {
            erros.Add(new("rotulo", new DomainError(
                FatoColetadoErrorCodes.RotuloTamanho,
                $"O rótulo do fato coletado deve ter no máximo {RotuloMaxLength} caracteres.")));
        }

        if (tipoRenderizacao == TipoRenderizacao.Nenhuma)
        {
            erros.Add(new("tipoRenderizacao", new DomainError(
                FatoColetadoErrorCodes.TipoRenderizacaoObrigatorio,
                "O tipo de renderização do fato coletado é obrigatório.")));
        }

        return erros;
    }

    /// <summary>Indica se o fato é coletado incondicionalmente.</summary>
    public bool SemPrecondicao => _precondicoes.Count == 0;

    /// <summary>Códigos dos fatos citados na pré-condição, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. _precondicoes.Select(static c => c.Fato).Distinct(StringComparer.Ordinal)];

    internal void VincularProcessoSeletivo(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// O predicado de pré-condição na forma avaliável, ou <see langword="null"/> quando o fato é
    /// coletado incondicionalmente. Um predicado sem cláusula nenhuma avaliaria falso — que é o
    /// oposto de "sem pré-condição" —, então a ausência é representada pelo nulo, nunca por um
    /// predicado vazio.
    /// </summary>
    internal ValueObjects.PredicadoDnf? ParaPredicado() =>
        _precondicoes.Count == 0
            ? null
            : ValueObjects.PredicadoDnf.CriarDeCondicoesAgrupadas(
                [.. _precondicoes.Select(static c => (c.Clausula, c.ParaCondicaoDnf()))]).Value!;
}

/// <summary>Códigos de erro de <see cref="FatoColetado"/>.</summary>
public static class FatoColetadoErrorCodes
{
    public const string FatoCodigoObrigatorio = "FatoColetado.FatoCodigoObrigatorio";
    public const string FatoCodigoTamanho = "FatoColetado.FatoCodigoTamanho";
    public const string OrdemInvalida = "FatoColetado.OrdemInvalida";
    public const string RotuloObrigatorio = "FatoColetado.RotuloObrigatorio";
    public const string RotuloTamanho = "FatoColetado.RotuloTamanho";
    public const string TipoRenderizacaoObrigatorio = "FatoColetado.TipoRenderizacaoObrigatorio";
    public const string PrecondicaoAutorreferente = "FatoColetado.PrecondicaoAutorreferente";
    public const string FatoDuplicado = "FatoColetado.FatoDuplicado";
    public const string OrdemDuplicada = "FatoColetado.OrdemDuplicada";
    public const string PrecondicaoCitaFatoNaoColetado = "FatoColetado.PrecondicaoCitaFatoNaoColetado";
    public const string PrecondicaoCitaFatoPosterior = "FatoColetado.PrecondicaoCitaFatoPosterior";
    public const string GrafoComCiclo = "FatoColetado.GrafoComCiclo";
}
