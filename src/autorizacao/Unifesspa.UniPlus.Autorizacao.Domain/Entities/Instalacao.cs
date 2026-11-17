namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Instalação dos primeiros gestores de acessos: quem tem o papel de instalação designa dois
/// gestores com o documento de designação, e só a confirmação por código enviado ao e-mail
/// institucional dele os faz valer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Só a identidade do token é gravada</b>: nunca nome, CPF ou e-mail de ninguém, e nunca o
/// código em claro — só o resumo dele, preso a esta instalação.
/// </para>
/// <para>
/// <b>Os estados terminais não voltam.</b> Recusa do documento, cancelamento, expiração e
/// confirmação encerram a instalação; recomeçar é iniciar outra.
/// </para>
/// </remarks>
public sealed class Instalacao : EntityBase
{
    public const int MaximoDeTentativas = 5;
    public static readonly TimeSpan PrazoDoCodigo = TimeSpan.FromHours(2);

    /// <summary>As situações em que a instalação ainda pode avançar: a de um executor só pode estar numa delas por vez.</summary>
    public static readonly IReadOnlyList<SituacaoDaInstalacao> SituacoesPendentes =
    [
        SituacaoDaInstalacao.AguardandoDocumento,
        SituacaoDaInstalacao.DocumentoEmVerificacao,
        SituacaoDaInstalacao.DocumentoVerificado,
        SituacaoDaInstalacao.AguardandoCodigo,
    ];

    private const string PrefixoDasChaves = "autorizacao/instalacao";

    private readonly List<GestorDaInstalacao> _gestores = [];

    public string ExecutorEmissor { get; private set; } = string.Empty;

    public string ExecutorContaId { get; private set; } = string.Empty;

    /// <summary>Nome de exibição de quem iniciou a instalação, gravado no início; nulo quando o token não o trazia.</summary>
    public string? ExecutorNome { get; private set; }

    public string NumeroDoDocumento { get; private set; } = string.Empty;

    /// <summary>Tamanho declarado do PDF, em bytes; é o limite do pedido ao verificador, no máximo <see cref="PdfDeclarado.TamanhoMaximoBytes"/>.</summary>
    public long TamanhoDoPdf { get; private set; }

    public SituacaoDaInstalacao Situacao { get; private set; }

    public IReadOnlyList<GestorDaInstalacao> Gestores => _gestores;

    /// <summary>ETag que o PUT devolveu; fixa a versão do PDF que o verificador confere.</summary>
    public string? DocumentoETag { get; private set; }

    public string? DocumentoSha256 { get; private set; }

    public DateTimeOffset? DocumentoVerificadoEm { get; private set; }

    /// <summary>Instante em que o verificador recusou o documento, ou pediu o reenvio.</summary>
    public DateTimeOffset? DocumentoRecusadoEm { get; private set; }

    /// <summary>HMAC do código preso a esta instalação; nunca o código.</summary>
    public byte[]? CodigoResumo { get; private set; }

    /// <summary>Instante do primeiro envio do código; o reenvio não o altera, e cada um fica na tentativa que o registra.</summary>
    public DateTimeOffset? CodigoEnviadoEm { get; private set; }

    public DateTimeOffset? CodigoExpiraEm { get; private set; }

    /// <summary>Códigos errados de quem iniciou, até <see cref="MaximoDeTentativas"/>.</summary>
    public int Tentativas { get; private set; }

    public DateTimeOffset IniciadaEm { get; private set; }

    public DateTimeOffset? ConfirmadaEm { get; private set; }

    public DateTimeOffset? CanceladaEm { get; private set; }

    // Construtor de materialização do EF Core.
    private Instalacao()
    {
    }

    private Instalacao(Identidade executor, DocumentoDeDesignacao documento, IEnumerable<GestorDaInstalacao> gestores, DateTimeOffset instante)
    {
        ExecutorEmissor = executor.Emissor;
        ExecutorContaId = executor.ContaId;
        ExecutorNome = executor.Nome;
        NumeroDoDocumento = documento.Numero;
        TamanhoDoPdf = documento.TamanhoDoPdf;
        Situacao = SituacaoDaInstalacao.AguardandoDocumento;
        IniciadaEm = instante;
        _gestores.AddRange(gestores);
    }

    /// <summary>
    /// Inicia a instalação, aguardando o PDF. Acumula toda violação independente (ADR-0125). Que
    /// não haja gestor ativo e que não haja outra pendência do executor são do handler.
    /// </summary>
    /// <param name="emissorAceito">O único emissor de token que a API aceita; o gestor de outro emissor nunca poderia entrar.</param>
    public static Result<Instalacao> Iniciar(
        Identidade executor,
        IReadOnlyList<IdentidadeInformada> gestores,
        string emissorAceito,
        string? numeroDoDocumento,
        long tamanhoDoPdf,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(executor);

        Result<IReadOnlyList<GestorDaInstalacao>> designados = GestoresDesignados.Conferir(executor, gestores, emissorAceito);
        Result<DocumentoDeDesignacao> documento = DocumentoDeDesignacao.Conferir(numeroDoDocumento, tamanhoDoPdf);

        return designados.IsFailure || documento.IsFailure
            ? Result<Instalacao>.ValidationFailure([.. designados.Errors, .. documento.Errors])
            : Result<Instalacao>.Success(new Instalacao(executor, documento.Value!, designados.Value!, instante));
    }

    /// <summary>Chave do PDF no bucket de envio, onde o cliente o sobe pela URL assinada.</summary>
    public string ChaveDeEnvio => $"{PrefixoDasChaves}/{Id:D}/envio";

    /// <summary>Chave em que o verificador promove o PDF limpo.</summary>
    public string ChaveDefinitiva => $"{PrefixoDasChaves}/{Id:D}/definitivo";

    public Identidade Executor => new(ExecutorEmissor, ExecutorContaId, ExecutorNome);

    public bool Pendente => SituacoesPendentes.Contains(Situacao);

    /// <summary>Põe o PDF em verificação, com o ETag que o PUT devolveu.</summary>
    public Result ConfirmarDocumento(string? etag)
    {
        if (Situacao != SituacaoDaInstalacao.AguardandoDocumento)
        {
            return Result.Failure(SituacaoInvalida());
        }

        if (ETagDoArmazenamento.Normalizar(etag) is not { } normalizado)
        {
            return ETagAusente();
        }

        DocumentoETag = normalizado;
        Situacao = SituacaoDaInstalacao.DocumentoEmVerificacao;
        return Result.Success();
    }

    /// <summary>
    /// Registra o veredicto "verificado". Só vale com o documento em verificação; o resultado que
    /// chega repetido ou depois do fim da instalação não muda nada e devolve falso.
    /// </summary>
    public bool RegistrarDocumentoVerificado(string sha256, DateTimeOffset instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        if (Situacao != SituacaoDaInstalacao.DocumentoEmVerificacao)
        {
            return false;
        }

        DocumentoSha256 = sha256;
        DocumentoVerificadoEm = instante;
        Situacao = SituacaoDaInstalacao.DocumentoVerificado;
        return true;
    }

    /// <summary>Registra a recusa, ou o pedido de reenvio, do verificador; só vale com o documento em verificação.</summary>
    public bool RegistrarDocumentoRecusado(DateTimeOffset instante)
    {
        if (Situacao != SituacaoDaInstalacao.DocumentoEmVerificacao)
        {
            return false;
        }

        DocumentoRecusadoEm = instante;
        Situacao = SituacaoDaInstalacao.DocumentoRecusado;
        return true;
    }

    /// <summary>Indica se a instalação aceita o envio do código: com o documento verificado, o primeiro, e enquanto se aguarda o código, o reenvio.</summary>
    public bool AceitaEnvioDoCodigo => Situacao is SituacaoDaInstalacao.DocumentoVerificado or SituacaoDaInstalacao.AguardandoCodigo;

    /// <summary>Indica se o próximo envio de código é o reenvio de um código já enviado e ainda não confirmado.</summary>
    public bool CodigoJaEnviado => Situacao == SituacaoDaInstalacao.AguardandoCodigo;

    /// <summary>
    /// Guarda o resumo do código enviado e abre o prazo da confirmação. Vale com o documento verificado e, para o
    /// reenvio, enquanto se aguarda o código: o resumo novo substitui o anterior, que deixa de valer, e o prazo
    /// recomeça. Os códigos errados já informados continuam contando, para que reenviar não renove as tentativas.
    /// </summary>
    public Result EnviarCodigo(byte[] resumo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(resumo);

        if (!AceitaEnvioDoCodigo)
        {
            return Result.Failure(SituacaoInvalida());
        }

        CodigoEnviadoEm ??= instante;
        CodigoResumo = resumo;
        CodigoExpiraEm = instante + PrazoDoCodigo;
        Situacao = SituacaoDaInstalacao.AguardandoCodigo;
        return Result.Success();
    }

    /// <summary>
    /// Registra a tentativa de quem iniciou. Prazo vencido expira a instalação; código errado
    /// consome uma tentativa, e a última cancela a instalação. O código certo não encerra nada
    /// aqui: <see cref="Confirmar"/> o faz, depois de conferida a regra dos gestores.
    /// </summary>
    public ResultadoDaTentativaDeInstalacao RegistrarTentativa(bool codigoConfere, DateTimeOffset instante)
    {
        if (Situacao != SituacaoDaInstalacao.AguardandoCodigo)
        {
            throw new InvalidOperationException("Só a instalação aguardando código aceita tentativa.");
        }

        if (instante >= CodigoExpiraEm)
        {
            return Expirar();
        }

        return codigoConfere ? ResultadoDaTentativaDeInstalacao.Aceita : RecusarCodigoErrado(instante);
    }

    /// <summary>Confirma a instalação, depois do código aceito. O código vale uma vez: o resumo é descartado.</summary>
    public void Confirmar(DateTimeOffset instante)
    {
        if (Situacao != SituacaoDaInstalacao.AguardandoCodigo)
        {
            throw new InvalidOperationException("Só a instalação aguardando código se confirma.");
        }

        Situacao = SituacaoDaInstalacao.Confirmada;
        ConfirmadaEm = instante;
        CodigoResumo = null;
    }

    /// <summary>Cancela a instalação pendente: iniciar de novo, tentativas esgotadas ou gestor já instalado.</summary>
    public void Cancelar(DateTimeOffset instante)
    {
        if (!Pendente)
        {
            throw new InvalidOperationException("Só a instalação pendente se cancela.");
        }

        Situacao = SituacaoDaInstalacao.Cancelada;
        CanceladaEm = instante;
        CodigoResumo = null;
    }

    private ResultadoDaTentativaDeInstalacao Expirar()
    {
        Situacao = SituacaoDaInstalacao.Expirada;
        CodigoResumo = null;
        return ResultadoDaTentativaDeInstalacao.Expirada;
    }

    private ResultadoDaTentativaDeInstalacao RecusarCodigoErrado(DateTimeOffset instante)
    {
        Tentativas++;
        if (Tentativas >= MaximoDeTentativas)
        {
            Cancelar(instante);
        }

        return ResultadoDaTentativaDeInstalacao.CodigoErrado;
    }

    private static Result ETagAusente() => Result.ValidationFailure([new FieldError("etag", new DomainError(
        InstalacaoErrorCodes.DocumentoInvalido,
        "O ETag que o armazenamento devolveu ao enviar o PDF é obrigatório."))]);

    private static DomainError SituacaoInvalida() => new(
        InstalacaoErrorCodes.SituacaoInvalida,
        "A instalação não está na situação que este passo exige.");
}
