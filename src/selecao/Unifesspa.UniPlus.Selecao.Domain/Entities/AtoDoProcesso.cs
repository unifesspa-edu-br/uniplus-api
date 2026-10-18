namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Registro, na Seleção, de um ato que o processo publicou — o edital de abertura e de retificação,
/// que criam versão de configuração, e o aviso, a errata, o resultado declarado no edital e
/// semelhantes, que não criam (ADR-0108, emenda 1; ADR-0132, emenda 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que existe.</b> <c>AtoNormativoRegistrado</c> leva só o ato, o ano e o tipo: não diz a que
/// processo o ato pertence nem quais arquivos tem. Para o edital a Seleção chega ao processo pela
/// versão que o ato criou; o ato que não cria versão não deixa esse rastro, e este registro é a fonte
/// de que a divulgação precisa. O ato que cria versão também é registrado, para que a consulta dos atos
/// do processo tenha uma fonte única, com tipo, número, data e assinante de todos.
/// </para>
/// <para>
/// <b>Nasce junto do envio.</b> É gravado na mesma transação em que a requisição de registro entra no
/// outbox: ou os dois existem, ou nenhum. A identidade é o <c>AtoId</c> que a Seleção decidiu, e é por
/// ela que a confirmação do Publicações encontra o registro.
/// </para>
/// <para>
/// <b>Imutável depois de gravado</b>, salvo a transição <see cref="SituacaoDoRegistroDoAto.Pendente"/>
/// para <see cref="SituacaoDoRegistroDoAto.Registrado"/>. Não é evidência forense: o ato em si é do
/// Publicações, e aqui fica o que a Seleção precisa para divulgá-lo.
/// </para>
/// <para>
/// A unidade e o identificador legível do processo ficam gravados porque a chave do documento no
/// acervo é composta com eles e não muda depois de publicada: lê-los de novo mais tarde, da versão
/// vigente de então, poderia devolver outro endereço.
/// </para>
/// <para>
/// As invariantes de negócio (processo com página pública, tipo vigente, documentos confirmados e de
/// outro ato) são do comando de publicação, que as recusa com erro nomeado antes de enviar. O par
/// ato corrigido e motivo é regra do agregado e vem do payload: a recusa é <see cref="Result"/> nomeado,
/// sem exceção (ADR-0125). Os demais guards daqui são a última linha e lançam, porque a violação é
/// erro de programação.
/// </para>
/// </remarks>
public sealed class AtoDoProcesso : EntityBase
{
    public const int TipoCodigoMaxLength = 60;
    public const int TipoNomeMaxLength = 200;
    public const int NumeroMaxLength = 60;
    public const int AssinanteMaxLength = 200;
    public const int MotivoDaCorrecaoMaxLength = 1000;
    public const int UnidadeAdministradoraMaxLength = 100;
    public const int IdentificadorLegivelMaxLength = 200;
    public const int SubMaxLength = 255;

    private readonly List<AnexoDoAtoDoProcesso> _anexos = [];

    public Guid ProcessoSeletivoId { get; private set; }
    public string TipoCodigo { get; private set; } = string.Empty;
    public string TipoNome { get; private set; } = string.Empty;
    public string? Numero { get; private set; }
    public DateOnly DataDoAto { get; private set; }
    public string Assinante { get; private set; } = string.Empty;
    public string UnidadeAdministradora { get; private set; } = string.Empty;
    public string IdentificadorLegivelDoProcesso { get; private set; } = string.Empty;

    /// <summary>
    /// Referência ao <c>Id</c> congelado do produto de fase ou de etapa que o ato cumpre. Por valor,
    /// sem chave estrangeira: o produto vive no envelope congelado da versão, não numa tabela.
    /// </summary>
    public Guid? ProdutoDoEditalId { get; private set; }

    public Guid DocumentoPrincipalId { get; private set; }

    /// <summary>O ato que este corrige — por valor, sem chave estrangeira: o ato é do Publicações.</summary>
    public Guid? AtoCorrigidoId { get; private set; }

    public string? MotivoDaCorrecao { get; private set; }

    /// <summary>Instante em que a política de divulgação foi confirmada; nulo quando não foi pedida.</summary>
    public DateTimeOffset? PoliticaDeDivulgacaoConfirmadaEm { get; private set; }

    /// <summary>Sub do usuário que confirmou a política de divulgação; nulo quando não foi pedida.</summary>
    public string? PoliticaDeDivulgacaoConfirmadaPorSub { get; private set; }

    public SituacaoDoRegistroDoAto Situacao { get; private set; }

    /// <summary>Instante em que o Publicações confirmou o ato; nulo enquanto pendente.</summary>
    public DateTimeOffset? RegistradoEm { get; private set; }

    public IReadOnlyList<AnexoDoAtoDoProcesso> Anexos => _anexos;

    private AtoDoProcesso() { }

    /// <summary>
    /// Confere o par ato corrigido e motivo da correção, que vem do payload: quem corrige diz o que
    /// corrige e por quê, e quem não corrige não tem motivo (ADR-0103). Acumula as violações, com o
    /// campo no casing do payload (ADR-0125).
    /// </summary>
    public static Result ValidarCorrecao(Guid? atoCorrigidoId, string? motivoDaCorrecao)
    {
        bool temMotivo = !string.IsNullOrWhiteSpace(motivoDaCorrecao);
        List<FieldError> erros = [];

        if (atoCorrigidoId == Guid.Empty)
        {
            erros.Add(new FieldError("atoCorrigidoId", new DomainError(
                AtoDoProcessoErrorCodes.AtoCorrigidoInvalido,
                "O ato corrigido informado é inválido.")));
        }
        else if (atoCorrigidoId is null && temMotivo)
        {
            erros.Add(new FieldError("atoCorrigidoId", new DomainError(
                AtoDoProcessoErrorCodes.AtoCorrigidoObrigatorio,
                "Informe o ato corrigido: o motivo da correção só existe junto dele.")));
        }

        if (atoCorrigidoId is not null && !temMotivo)
        {
            erros.Add(new FieldError("motivoDaCorrecao", new DomainError(
                AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio,
                "O motivo da correção é obrigatório quando o ato corrige outro.")));
        }

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    /// <summary>
    /// Cria o registro pendente que acompanha o envio da requisição de registro do ato. Devolve falha
    /// nomeada quando o par ato corrigido e motivo é incoerente.
    /// </summary>
    public static Result<AtoDoProcesso> Registrar(NovoAtoDoProcesso novo, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(novo);
        ArgumentNullException.ThrowIfNull(clock);

        Result correcao = ValidarCorrecao(novo.AtoCorrigidoId, novo.MotivoDaCorrecao);
        if (correcao.IsFailure)
        {
            return Result<AtoDoProcesso>.ValidationFailure(correcao.Errors);
        }

        GuardarIdentidade(novo);
        GuardarTexto(novo.TipoCodigo, TipoCodigoMaxLength, nameof(novo.TipoCodigo));
        GuardarTexto(novo.TipoNome, TipoNomeMaxLength, nameof(novo.TipoNome));
        GuardarTexto(novo.Assinante, AssinanteMaxLength, nameof(novo.Assinante));
        GuardarTexto(novo.UnidadeAdministradora, UnidadeAdministradoraMaxLength, nameof(novo.UnidadeAdministradora));
        GuardarTexto(novo.IdentificadorLegivelDoProcesso, IdentificadorLegivelMaxLength, nameof(novo.IdentificadorLegivelDoProcesso));

        // Texto opcional em branco é ausência, não erro: quem declara pode mandar o campo vazio.
        novo = novo with
        {
            Numero = novo.Numero.EmBrancoViraNulo(),
            PoliticaDeDivulgacaoConfirmadaPorSub = novo.PoliticaDeDivulgacaoConfirmadaPorSub.EmBrancoViraNulo(),
            MotivoDaCorrecao = novo.MotivoDaCorrecao.EmBrancoViraNulo(),
        };
        GuardarTextoOpcional(novo.Numero, NumeroMaxLength, nameof(novo.Numero));
        GuardarTextoOpcional(novo.PoliticaDeDivulgacaoConfirmadaPorSub, SubMaxLength, nameof(novo.PoliticaDeDivulgacaoConfirmadaPorSub));
        GuardarAutocorrecao(novo);
        GuardarDocumentos(novo);

        AtoDoProcesso ato = new()
        {
            Id = novo.AtoId,
            ProcessoSeletivoId = novo.ProcessoSeletivoId,
            TipoCodigo = novo.TipoCodigo,
            TipoNome = novo.TipoNome,
            Numero = novo.Numero,
            DataDoAto = novo.DataDoAto,
            Assinante = novo.Assinante,
            UnidadeAdministradora = novo.UnidadeAdministradora,
            IdentificadorLegivelDoProcesso = novo.IdentificadorLegivelDoProcesso,
            ProdutoDoEditalId = novo.ProdutoDoEditalId,
            DocumentoPrincipalId = novo.DocumentoPrincipalId,
            AtoCorrigidoId = novo.AtoCorrigidoId,
            MotivoDaCorrecao = novo.MotivoDaCorrecao,
            PoliticaDeDivulgacaoConfirmadaEm = novo.PoliticaDeDivulgacaoConfirmadaPorSub is null ? null : clock.GetUtcNow(),
            PoliticaDeDivulgacaoConfirmadaPorSub = novo.PoliticaDeDivulgacaoConfirmadaPorSub,
            Situacao = SituacaoDoRegistroDoAto.Pendente,
        };

        for (int i = 0; i < novo.Anexos.Count; i++)
        {
            ato._anexos.Add(AnexoDoAtoDoProcesso.Criar(ato.Id, i, novo.Anexos[i].DocumentoAPublicarId, novo.Anexos[i].NomeDeExibicao));
        }

        return Result<AtoDoProcesso>.Success(ato);
    }

    /// <summary>
    /// Passa o registro a <see cref="SituacaoDoRegistroDoAto.Registrado"/>. Idempotente: a mensagem de
    /// confirmação é entregue pelo menos uma vez, e a repetição não muda nada.
    /// </summary>
    /// <returns><see langword="true"/> quando a situação mudou.</returns>
    public bool ConfirmarRegistro(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Situacao == SituacaoDoRegistroDoAto.Registrado)
        {
            return false;
        }

        Situacao = SituacaoDoRegistroDoAto.Registrado;
        RegistradoEm = clock.GetUtcNow();
        return true;
    }

    private static void GuardarIdentidade(NovoAtoDoProcesso novo)
    {
        if (novo.AtoId == Guid.Empty)
        {
            throw new ArgumentException("O ato do processo deve ter o identificador que a Seleção decidiu.", nameof(novo));
        }

        if (novo.ProcessoSeletivoId == Guid.Empty)
        {
            throw new ArgumentException("O ato do processo deve estar vinculado a um Processo Seletivo.", nameof(novo));
        }
    }

    private static void GuardarAutocorrecao(NovoAtoDoProcesso novo)
    {
        // O identificador do ato é gerado no servidor: nunca vem do payload, logo é erro de programação.
        if (novo.AtoCorrigidoId == novo.AtoId)
        {
            throw new ArgumentException("Um ato não corrige a si mesmo.", nameof(novo));
        }

        GuardarTextoOpcional(novo.MotivoDaCorrecao, MotivoDaCorrecaoMaxLength, nameof(novo.MotivoDaCorrecao));
    }

    private static void GuardarDocumentos(NovoAtoDoProcesso novo)
    {
        ArgumentNullException.ThrowIfNull(novo.Anexos);

        if (novo.DocumentoPrincipalId == Guid.Empty)
        {
            throw new ArgumentException("O ato do processo deve ter um documento principal.", nameof(novo));
        }

        HashSet<Guid> documentos = [novo.DocumentoPrincipalId];
        if (novo.Anexos.Any(a => !documentos.Add(a.DocumentoAPublicarId)))
        {
            throw new ArgumentException("Um documento não pode ser o principal e anexo, nem anexo duas vezes.", nameof(novo));
        }
    }

    private static void GuardarTexto(string valor, int tamanhoMaximo, string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, nome);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(valor.Length, tamanhoMaximo, nome);
    }

    private static void GuardarTextoOpcional(string? valor, int tamanhoMaximo, string nome)
    {
        if (valor is null)
        {
            return;
        }

        GuardarTexto(valor, tamanhoMaximo, nome);
    }
}
