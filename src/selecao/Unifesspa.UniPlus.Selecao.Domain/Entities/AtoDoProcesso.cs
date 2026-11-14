namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Registro, na Seleção, de um ato que o processo publicou — o edital de abertura e de retificação,
/// que criam versão de configuração, e o aviso, a errata, o resultado declarado no edital e
/// semelhantes, que não criam (ADR-0105, emenda 1; ADR-0132, emenda 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que existe.</b> O ato registrado em Publicações não diz a que processo pertence nem quais arquivos tem.
/// Para o edital a Seleção chega ao processo pela versão que o ato criou; o ato que não cria versão não deixa esse
/// rastro, e este registro é a fonte de que a divulgação precisa. O ato que cria versão também é registrado, para que a
/// consulta dos atos do processo tenha uma fonte única, com tipo, número, data e assinante de todos.
/// </para>
/// <para>
/// <b>Nasce junto da publicação.</b> É gravado na mesma transação que grava o ato em Publicações, a divulgação do
/// certame e, quando há, a versão: ou tudo existe, ou nada. A identidade é o <c>AtoId</c> que a Seleção decidiu. Imutável
/// depois de gravado; não é evidência forense, porque o ato em si é de Publicações, e aqui fica o que a Seleção precisa
/// para divulgá-lo.
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
    public const int SlugDaUnidadeAdministradoraMaxLength = 100;
    public const int IdentificadorLegivelMaxLength = 200;
    public const int SubMaxLength = 255;

    private readonly List<AnexoDoAtoDoProcesso> _anexos = [];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>
    /// A ordem em que os atos foram registrados, dada pelo banco: desempata dois atos do mesmo processo gravados no
    /// mesmo microssegundo, que o registro sob a trava do processo serializa.
    /// </summary>
    public long OrdemDeRegistro { get; private set; }

    public string TipoCodigo { get; private set; } = string.Empty;
    public string TipoNome { get; private set; } = string.Empty;
    public string? Numero { get; private set; }
    /// <summary>A data que o documento declara; não conta para prazo, vigência nem escolha do tipo de ato.</summary>
    public DateOnly DataDoAto { get; private set; }

    /// <summary>
    /// O instante da divulgação, lido uma só vez do relógio pelo comando que publica: o mesmo que a versão
    /// de configuração grava como vigente a partir dele e que o registro do ato leva a Publicações.
    /// </summary>
    public DateTimeOffset PublicadoEm { get; private set; }

    public string Assinante { get; private set; } = string.Empty;
    public string SlugDaUnidadeAdministradora { get; private set; } = string.Empty;
    public string IdentificadorLegivelDoProcesso { get; private set; } = string.Empty;

    /// <summary>
    /// Chave da identidade do item de fase ou de etapa que o ato cumpre (<see cref="IdentidadeDoItemDoEdital"/>):
    /// o lugar, o tipo de ato e o papel, que valem em qualquer versão do edital. Sem chave estrangeira: o
    /// item vive no envelope congelado da versão, não numa tabela, e o <c>Id</c> do produto muda entre versões.
    /// </summary>
    public string? ItemDoEdital { get; private set; }

    public Guid DocumentoPrincipalId { get; private set; }

    /// <summary>O ato que este corrige — por valor, sem chave estrangeira: o ato é do Publicações.</summary>
    public Guid? AtoCorrigidoId { get; private set; }

    public string? MotivoDaCorrecao { get; private set; }

    /// <summary>Instante em que a política de divulgação foi confirmada; nulo quando não foi pedida.</summary>
    public DateTimeOffset? PoliticaDeDivulgacaoConfirmadaEm { get; private set; }

    /// <summary>Sub do usuário que confirmou a política de divulgação; nulo quando não foi pedida.</summary>
    public string? PoliticaDeDivulgacaoConfirmadaPorSub { get; private set; }

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
        FieldError[] erros =
        [
            .. new[] { ErroDoAtoCorrigido(atoCorrigidoId, temMotivo), ErroDoMotivoDaCorrecao(atoCorrigidoId, temMotivo) }
                .OfType<FieldError>(),
        ];

        return erros.Length > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    /// <summary>
    /// Cria o registro do ato que o processo publica em <paramref name="publicadoEm"/>. Devolve falha nomeada quando o par ato corrigido e motivo é incoerente.
    /// </summary>
    public static Result<AtoDoProcesso> Registrar(NovoAtoDoProcesso novo, DateTimeOffset publicadoEm)
    {
        ArgumentNullException.ThrowIfNull(novo);
        ArgumentOutOfRangeException.ThrowIfEqual(publicadoEm, default);

        Result correcao = ValidarCorrecao(novo.AtoCorrigidoId, novo.MotivoDaCorrecao);
        if (correcao.IsFailure)
        {
            return Result<AtoDoProcesso>.ValidationFailure(correcao.Errors);
        }

        return Result<AtoDoProcesso>.Success(AtoDe(ConferirDeclaracao(novo), publicadoEm));
    }

    private static FieldError? ErroDoAtoCorrigido(Guid? atoCorrigidoId, bool temMotivo) => (atoCorrigidoId, temMotivo) switch
    {
        (Guid id, _) when id == Guid.Empty => Violacao(
            "atoCorrigidoId", AtoDoProcessoErrorCodes.AtoCorrigidoInvalido, "O ato corrigido informado é inválido."),
        (null, true) => Violacao(
            "atoCorrigidoId",
            AtoDoProcessoErrorCodes.AtoCorrigidoObrigatorio,
            "Informe o ato corrigido: o motivo da correção só existe junto dele."),
        _ => null,
    };

    private static FieldError? ErroDoMotivoDaCorrecao(Guid? atoCorrigidoId, bool temMotivo) =>
        atoCorrigidoId is not null && !temMotivo
            ? Violacao(
                "motivoDaCorrecao",
                AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio,
                "O motivo da correção é obrigatório quando o ato corrige outro.")
            : null;

    private static FieldError Violacao(string campo, string codigo, string mensagem) =>
        new(campo, new DomainError(codigo, mensagem));

    private static NovoAtoDoProcesso ConferirDeclaracao(NovoAtoDoProcesso novo)
    {
        GuardarIdentidade(novo);
        GuardarTextosObrigatorios(novo);

        NovoAtoDoProcesso declarado = SemTextoOpcionalEmBranco(novo);
        GuardarTextosOpcionais(declarado);
        GuardarAutocorrecao(declarado);
        GuardarDocumentos(declarado);

        return declarado;
    }

    // Texto opcional em branco é ausência, não erro: quem declara pode mandar o campo vazio.
    private static NovoAtoDoProcesso SemTextoOpcionalEmBranco(NovoAtoDoProcesso novo) => novo with
    {
        Numero = novo.Numero.EmBrancoViraNulo(),
        PoliticaDeDivulgacaoConfirmadaPorSub = novo.PoliticaDeDivulgacaoConfirmadaPorSub.EmBrancoViraNulo(),
        MotivoDaCorrecao = novo.MotivoDaCorrecao.EmBrancoViraNulo(),
    };

    private static AtoDoProcesso AtoDe(NovoAtoDoProcesso declarado, DateTimeOffset publicadoEm)
    {
        AtoDoProcesso ato = Declarar(declarado, publicadoEm);
        ato.VincularDocumentos(declarado);
        ato.VincularCorrecao(declarado);
        ato.ConfirmarPoliticaDeDivulgacao(declarado.PoliticaDeDivulgacaoConfirmadaPorSub, publicadoEm);

        return ato;
    }

    private static AtoDoProcesso Declarar(NovoAtoDoProcesso declarado, DateTimeOffset publicadoEm) => new()
    {
        Id = declarado.AtoId,
        ProcessoSeletivoId = declarado.ProcessoSeletivoId,
        TipoCodigo = declarado.TipoCodigo,
        TipoNome = declarado.TipoNome,
        Numero = declarado.Numero,
        DataDoAto = declarado.DataDoAto,
        PublicadoEm = publicadoEm,
        Assinante = declarado.Assinante,
        SlugDaUnidadeAdministradora = declarado.SlugDaUnidadeAdministradora,
        IdentificadorLegivelDoProcesso = declarado.IdentificadorLegivelDoProcesso,
        ItemDoEdital = declarado.ItemDoEdital?.Chave,
    };

    private void VincularDocumentos(NovoAtoDoProcesso declarado)
    {
        DocumentoPrincipalId = declarado.DocumentoPrincipalId;
        for (int i = 0; i < declarado.Anexos.Count; i++)
        {
            _anexos.Add(AnexoDoAtoDoProcesso.Criar(Id, i, declarado.Anexos[i].DocumentoAPublicarId, declarado.Anexos[i].NomeDeExibicao));
        }
    }

    private void VincularCorrecao(NovoAtoDoProcesso declarado)
    {
        AtoCorrigidoId = declarado.AtoCorrigidoId;
        MotivoDaCorrecao = declarado.MotivoDaCorrecao;
    }

    private void ConfirmarPoliticaDeDivulgacao(string? confirmadaPorSub, DateTimeOffset publicadoEm)
    {
        PoliticaDeDivulgacaoConfirmadaPorSub = confirmadaPorSub;
        PoliticaDeDivulgacaoConfirmadaEm = confirmadaPorSub is null ? null : publicadoEm;
    }

    private static void GuardarTextosObrigatorios(NovoAtoDoProcesso novo)
    {
        GuardarTexto(novo.TipoCodigo, TipoCodigoMaxLength, nameof(novo.TipoCodigo));
        GuardarTexto(novo.TipoNome, TipoNomeMaxLength, nameof(novo.TipoNome));
        GuardarTexto(novo.Assinante, AssinanteMaxLength, nameof(novo.Assinante));
        GuardarTexto(novo.SlugDaUnidadeAdministradora, SlugDaUnidadeAdministradoraMaxLength, nameof(novo.SlugDaUnidadeAdministradora));
        GuardarTexto(novo.IdentificadorLegivelDoProcesso, IdentificadorLegivelMaxLength, nameof(novo.IdentificadorLegivelDoProcesso));
    }

    private static void GuardarTextosOpcionais(NovoAtoDoProcesso novo)
    {
        GuardarTextoOpcional(novo.Numero, NumeroMaxLength, nameof(novo.Numero));
        GuardarTextoOpcional(novo.PoliticaDeDivulgacaoConfirmadaPorSub, SubMaxLength, nameof(novo.PoliticaDeDivulgacaoConfirmadaPorSub));
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
