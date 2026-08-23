namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Motivo institucional que fundamenta uma decisão sobre pedido de isenção da
/// taxa de inscrição (UNI-REQ-0120). Cadastro por fundamento de isenção, com
/// código e descrição estáveis, disponível para seleção pelo processo seletivo
/// e utilizável tanto na análise documental inicial quanto no julgamento do
/// recurso do mesmo fundamento.
/// </summary>
/// <remarks>
/// <para>
/// <b>Um resultado por motivo</b> (UNI-REQ-0121). O motivo pertence a
/// exatamente um <see cref="ResultadoPermitido"/> — deferimento ou
/// indeferimento —, nunca aos dois. Um motivo que servisse às duas conclusões
/// não fundamentaria nenhuma: "renda acima do limite" não defere, e a
/// obrigação de escolher no cadastro é o que impede a banca de citá-lo do lado
/// errado.
/// </para>
/// <para>
/// <b>A mesma lista serve às duas etapas</b> (UNI-REQ-0120). Não há campo de
/// etapa nem marcação de "só recurso": a análise documental inicial e o recurso
/// do mesmo fundamento leem o mesmo conjunto. Para doação de medula óssea isso
/// é explícito — os códigos que voltam do SISTAC são evidência externa, e não
/// entram neste catálogo.
/// </para>
/// <para>
/// <b>Desativar não apaga</b> (UNI-REQ-0122). A retirada de um motivo é a
/// desativação, que impede sua entrada em novas publicações e preserva tudo o
/// que já o referencia. Por isso a entidade não deriva de
/// <see cref="SoftDeletableEntity"/>: não há remoção a modelar, e um segundo
/// eixo de "sumiu" ao lado de <see cref="Ativo"/> só criaria a dúvida sobre
/// qual dos dois a publicação deve consultar.
/// </para>
/// <para>
/// <b>Só a descrição se edita.</b> Código, fundamento e resultado permitido são
/// definidos na criação e não mudam mais — ver <see cref="Atualizar"/>. Isso
/// entrega a imutabilidade que UNI-REQ-0121 exige do resultado sem precisar
/// saber se o motivo já foi disponibilizado a processo publicado, pergunta que
/// só a seleção por processo saberá responder.
/// </para>
/// <para>
/// <b>Fora desta fatia.</b> Travar também a descrição depois da
/// disponibilização, e recusar publicação de rascunho que referencie motivo
/// desativado, dependem do vínculo entre motivo e processo, que nasce na Task
/// da seleção por processo.
/// </para>
/// </remarks>
public sealed class MotivoDecisaoIsencao : EntityBase, IAuditableEntity
{
    private const int DescricaoMinLength = 3;
    private const int DescricaoMaxLength = 500;

    public CodigoMotivoDecisao Codigo { get; private set; } = null!;

    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Fundamento de isenção a que o motivo se aplica. Nunca <see cref="FundamentoIsencao.Nenhum"/>.</summary>
    public FundamentoIsencao Fundamento { get; private set; }

    /// <summary>Resultado único que o motivo fundamenta. Nunca <see cref="ResultadoPermitido.Nenhum"/>.</summary>
    public ResultadoPermitido ResultadoPermitido { get; private set; }

    /// <summary>Motivo ativo entra em novas publicações; desativado, não. Nasce ativo.</summary>
    public bool Ativo { get; private set; } = true;

    public string? CreatedBy { get; private set; }

    public string? UpdatedBy { get; private set; }

    // Construtor de materialização do EF Core.
    private MotivoDecisaoIsencao()
    {
    }

    /// <summary>
    /// Valida e normaliza os campos declarados no cadastro, acumulando toda
    /// violação independente em vez de parar na primeira (ADR-0125) — sem mutar
    /// nada. O <c>errors[]</c> do contrato público precisa de todas as regras
    /// violadas no mesmo lote: código malformado, descrição fora do tamanho,
    /// fundamento ausente e resultado ausente coexistem no mesmo payload.
    /// Existe separada das factories para o handler validar o payload por
    /// inteiro antes de qualquer I/O.
    /// </summary>
    public static Result<CamposValidados> ValidarCampos(
        string? codigo,
        string? descricao,
        FundamentoIsencao fundamento,
        ResultadoPermitido resultadoPermitido)
    {
        List<FieldError> erros = [];

        CodigoMotivoDecisao? codigoValidado = null;
        Result<CodigoMotivoDecisao> codigoResult = CodigoMotivoDecisao.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            erros.Add(new("codigo", codigoResult.Error!));
        }
        else
        {
            codigoValidado = codigoResult.Value;
        }

        string? descricaoNormalizada = null;
        Result<string> descricaoResult = ValidarDescricao(descricao);
        if (descricaoResult.IsFailure)
        {
            erros.AddRange(descricaoResult.Errors);
        }
        else
        {
            descricaoNormalizada = descricaoResult.Value;
        }

        // Fundamento e resultado chegam já convertidos do código do wire, e a
        // sentinela Nenhum é o que a conversão devolve tanto para ausente quanto
        // para valor fora do vocabulário. Os dois casos recusam pela mesma
        // regra: a operação exige um valor conhecido, e nada distingue "não
        // informei" de "informei errado" quando o efeito é o mesmo.
        if (fundamento == FundamentoIsencao.Nenhum)
        {
            erros.Add(new("fundamento", new DomainError(
                MotivoDecisaoIsencaoErrorCodes.FundamentoObrigatorio,
                "Fundamento de isenção do motivo é obrigatório e deve ser um dos códigos aceitos: "
                + $"{string.Join(", ", FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea)}.")));
        }

        if (resultadoPermitido == ResultadoPermitido.Nenhum)
        {
            erros.Add(new("resultadoPermitido", new DomainError(
                MotivoDecisaoIsencaoErrorCodes.ResultadoPermitidoObrigatorio,
                "Motivo de decisão deve declarar exatamente um resultado permitido: "
                + $"{string.Join(" ou ", ResultadoPermitidoCodigo.Todos)}.")));
        }

        if (erros.Count > 0)
        {
            return Result<CamposValidados>.ValidationFailure(erros);
        }

        return Result<CamposValidados>.Success(new CamposValidados(
            codigoValidado!,
            descricaoNormalizada!,
            fundamento,
            resultadoPermitido));
    }

    /// <summary>
    /// Cria um motivo, já ativo. A unicidade do código entre os motivos
    /// existentes é responsabilidade do handler, com proteção de corrida no
    /// índice único.
    /// </summary>
    public static Result<MotivoDecisaoIsencao> Criar(
        string? codigo,
        string? descricao,
        FundamentoIsencao fundamento,
        ResultadoPermitido resultadoPermitido)
    {
        Result<CamposValidados> campos = ValidarCampos(codigo, descricao, fundamento, resultadoPermitido);
        if (campos.IsFailure)
        {
            return Result<MotivoDecisaoIsencao>.ValidationFailure(campos.Errors);
        }

        MotivoDecisaoIsencao motivo = new();
        motivo.AplicarCampos(campos.Value!);

        return Result<MotivoDecisaoIsencao>.Success(motivo);
    }

    /// <summary>
    /// Atualiza a descrição, único campo editável do motivo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Código, fundamento e resultado permitido não se editam.</b> O código é
    /// a chave natural citada na decisão que o usa, e vale aqui o mesmo
    /// raciocínio dos cadastros de tipo do módulo Configuração, cujo código
    /// também não se edita: quem precisa de outro código cria outro motivo. O fundamento define a que
    /// lista o motivo pertence, e trocá-lo o moveria de catálogo sem que
    /// nenhuma decisão já proferida soubesse. O resultado permitido é o que
    /// UNI-REQ-0121 declara imutável depois da disponibilização — travá-lo desde
    /// a criação entrega essa garantia sem depender de saber se o motivo já foi
    /// disponibilizado, o que só a seleção por processo saberá responder.
    /// </para>
    /// <para>
    /// Também não altera <see cref="Ativo"/>: ativar e desativar são operações
    /// próprias, com sua própria autorização e sua própria trilha, e
    /// escondê-las dentro da edição faria uma retirada do catálogo passar por
    /// correção de texto.
    /// </para>
    /// </remarks>
    public Result Atualizar(string? descricao)
    {
        Result<string> descricaoValidada = ValidarDescricao(descricao);
        if (descricaoValidada.IsFailure)
        {
            return Result.ValidationFailure(descricaoValidada.Errors);
        }

        Descricao = descricaoValidada.Value!;

        return Result.Success();
    }

    /// <summary>
    /// Reativa o motivo, devolvendo-o às novas publicações. Reativar o que já
    /// está ativo é recusado em vez de aceito em silêncio: a operação não é
    /// idempotente por natureza — ela registra ator e instante —, e aceitar o
    /// nada gravaria uma reativação que não mudou estado algum.
    /// </summary>
    public Result Ativar()
    {
        if (Ativo)
        {
            return Result.Failure(new DomainError(
                MotivoDecisaoIsencaoErrorCodes.JaAtivo,
                "O motivo de decisão de isenção já está ativo."));
        }

        Ativo = true;

        return Result.Success();
    }

    /// <summary>
    /// Desativa o motivo. O efeito é prospectivo: ele deixa de entrar em novas
    /// publicações e permanece onde já foi disponibilizado, inclusive nas
    /// decisões proferidas (UNI-REQ-0122).
    /// </summary>
    public Result Desativar()
    {
        if (!Ativo)
        {
            return Result.Failure(new DomainError(
                MotivoDecisaoIsencaoErrorCodes.JaInativo,
                "O motivo de decisão de isenção já está inativo."));
        }

        Ativo = false;

        return Result.Success();
    }

    /// <summary>
    /// Valida e normaliza a descrição isoladamente — é o único campo que a
    /// edição alcança, e a criação a reaproveita para que as duas operações
    /// recusem exatamente o mesmo conjunto de textos.
    /// </summary>
    private static Result<string> ValidarDescricao(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            return Result<string>.ValidationFailure([new("descricao", new DomainError(
                MotivoDecisaoIsencaoErrorCodes.DescricaoObrigatoria,
                "Descrição do motivo de decisão de isenção é obrigatória."))]);
        }

        string normalizada = descricao.Trim();

        // O caractere nulo não é espaço em branco, então atravessa a checagem
        // de obrigatoriedade, e cabe no tamanho — mas a coluna textual do banco
        // não o armazena. Sem esta recusa o valor só falharia ao gravar, e um
        // payload inválido responderia 500 em vez da validação de domínio.
        if (normalizada.Contains('\0', StringComparison.Ordinal))
        {
            return Result<string>.ValidationFailure([new("descricao", new DomainError(
                MotivoDecisaoIsencaoErrorCodes.DescricaoCaractereInvalido,
                "Descrição do motivo não pode conter o caractere nulo."))]);
        }

        if (normalizada.Length is < DescricaoMinLength or > DescricaoMaxLength)
        {
            return Result<string>.ValidationFailure([new("descricao", new DomainError(
                MotivoDecisaoIsencaoErrorCodes.DescricaoTamanho,
                $"Descrição do motivo deve ter entre {DescricaoMinLength} e {DescricaoMaxLength} caracteres."))]);
        }

        return Result<string>.Success(normalizada);
    }

    private void AplicarCampos(CamposValidados campos)
    {
        Codigo = campos.Codigo;
        Descricao = campos.Descricao;
        Fundamento = campos.Fundamento;
        ResultadoPermitido = campos.ResultadoPermitido;
    }

    /// <summary>Campos já validados e normalizados, prontos para aplicar ao agregado.</summary>
    public sealed record CamposValidados(
        CodigoMotivoDecisao Codigo,
        string Descricao,
        FundamentoIsencao Fundamento,
        ResultadoPermitido ResultadoPermitido);
}
