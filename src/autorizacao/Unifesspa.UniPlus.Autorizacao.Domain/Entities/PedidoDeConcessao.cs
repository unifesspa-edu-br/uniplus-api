namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Pedido de concessão de um perfil de acesso a uma pessoa, num escopo. Toda concessão nasce de um
/// pedido aprovado: o gestor de acessos aprova a adequação funcional e, quando o perfil reúne
/// permissão pessoal ou sensível, a encarregada de dados aprova depois, a adequação à proteção de dados.
/// </summary>
/// <remarks>
/// <para>
/// <b>Só a identidade do token é gravada.</b> Nunca CPF; o nome e o e-mail do beneficiário existem para
/// que o aprovador saiba quem é a pessoa (aprovar exige julgar a pessoa), e o nome vem do token dele, no
/// aceite. A sensibilidade do perfil também não se grava: deriva do catálogo.
/// </para>
/// <para>
/// <b>Nenhuma decisão acontece sem a identidade completa do beneficiário.</b> O pedido para outra
/// pessoa espera o aceite dela, que grava emissor, <c>sub</c> e nome do token.
/// </para>
/// <para>
/// <b>As decisões só se acrescentam</b> e guardam o retrato do que o aprovador avaliou. Os estados
/// terminais não voltam.
/// </para>
/// </remarks>
public sealed class PedidoDeConcessao : EntityBase
{
    public const int JustificativaMinLength = 10;
    public const int JustificativaMaxLength = 2000;
    public const int MotivoMaxLength = 500;
    public const int EmailMaxLength = 254;

    /// <summary>As situações em que o pedido ainda pode avançar: o mesmo pedido não se repete enquanto numa delas.</summary>
    public static readonly IReadOnlyList<SituacaoDoPedido> SituacoesAtivas =
    [
        SituacaoDoPedido.AguardandoBeneficiario,
        SituacaoDoPedido.AguardandoGestor,
        SituacaoDoPedido.AguardandoEncarregada,
    ];

    private readonly List<DecisaoDoPedido> _decisoes = [];
    private readonly List<AnexoDoPedido> _anexos = [];

    public string SolicitanteEmissor { get; private set; } = string.Empty;

    public string SolicitanteSub { get; private set; } = string.Empty;

    public string? SolicitanteNome { get; private set; }

    /// <summary>E-mail institucional do beneficiário, em minúsculas; é como o pedido o encontra até ele aceitar.</summary>
    public string BeneficiarioEmail { get; private set; } = string.Empty;

    public string? BeneficiarioEmissor { get; private set; }

    public string? BeneficiarioSub { get; private set; }

    public string? BeneficiarioNome { get; private set; }

    /// <summary>Instante do aceite do beneficiário; nulo no pedido para si mesmo, que não tem aceite.</summary>
    public DateTimeOffset? BeneficiarioAceitouEm { get; private set; }

    public Guid PerfilId { get; private set; }

    /// <summary>Unidade do escopo; nula só no perfil de governança, de escopo institucional.</summary>
    public Guid? UnidadeId { get; private set; }

    public string Justificativa { get; private set; } = string.Empty;

    public SituacaoDoPedido Situacao { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? EncerradoEm { get; private set; }

    /// <summary>A concessão que a última aprovação gerou; nula até lá.</summary>
    public Guid? ConcessaoId { get; private set; }

    public IReadOnlyList<DecisaoDoPedido> Decisoes => _decisoes;

    public IReadOnlyList<AnexoDoPedido> Anexos => _anexos;

    public Identidade Solicitante => new(SolicitanteEmissor, SolicitanteSub, SolicitanteNome);

    /// <summary>A identidade do beneficiário; nula enquanto ele não aceitou.</summary>
    public Identidade? Beneficiario => BeneficiarioEmissor is { } emissor && BeneficiarioSub is { } sub
        ? new Identidade(emissor, sub, BeneficiarioNome)
        : null;

    public EscopoDaConcessao Escopo => UnidadeId is { } unidade
        ? EscopoDaConcessao.DaUnidade(unidade)
        : EscopoDaConcessao.DaInstituicao();

    public bool Ativo => SituacoesAtivas.Contains(Situacao);

    /// <summary>A etapa que o pedido aguarda; nula quando ele não espera aprovador.</summary>
    public EtapaDoPedido? EtapaAtual => Situacao switch
    {
        SituacaoDoPedido.AguardandoGestor => EtapaDoPedido.Gestor,
        SituacaoDoPedido.AguardandoEncarregada => EtapaDoPedido.Encarregada,
        _ => null,
    };

    public bool TemAnexoVerificado => _anexos.Exists(a => a.Situacao == SituacaoDoAnexo.Verificado);

    // Construtor de materialização do EF Core.
    private PedidoDeConcessao()
    {
    }

    /// <summary>
    /// Cria o pedido. Acumula toda violação independente (ADR-0125). Que o pedido ativo igual não exista é do
    /// índice único, traduzido por quem grava; que a unidade exista é do handler.
    /// </summary>
    /// <param name="solicitante">Quem pede, como o token o traz.</param>
    /// <param name="emailDoBeneficiario">E-mail institucional do beneficiário; ausente, ou igual ao e-mail verificado do solicitante, o pedido é para si mesmo.</param>
    public static Result<PedidoDeConcessao> Criar(
        PessoaNoToken solicitante,
        string? emailDoBeneficiario,
        Perfil perfil,
        EscopoDaConcessao escopo,
        string? justificativa,
        IDominiosInstitucionais dominios,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(solicitante);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(escopo);
        ArgumentNullException.ThrowIfNull(dominios);

        if (!ParaSiMesmo(solicitante, emailDoBeneficiario) && !SolicitanteInstitucional(solicitante, dominios))
        {
            return Result<PedidoDeConcessao>.Failure(new DomainError(
                PedidoErrorCodes.SolicitanteNaoInstitucional,
                "Para pedir acesso a outra pessoa, o e-mail da sessão deve ser institucional e verificado."));
        }

        List<FieldError> erros = [];

        if (!perfil.Ativo)
        {
            erros.Add(new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita pedido novo.")));
        }

        string? incoerencia = Concessao.ConferirEscopo(perfil, escopo);
        if (incoerencia is not null)
        {
            erros.Add(new("escopo", new DomainError(PedidoErrorCodes.EscopoInvalido, incoerencia)));
        }

        string? justificativaNormalizada = NormalizarTexto(justificativa, JustificativaMinLength, JustificativaMaxLength);
        if (justificativaNormalizada is null)
        {
            erros.Add(new("justificativa", new DomainError(
                PedidoErrorCodes.JustificativaInvalida,
                $"A justificativa é obrigatória e deve ter entre {JustificativaMinLength} e {JustificativaMaxLength} caracteres.")));
        }

        (string? email, bool paraSiMesmo) = ResolverBeneficiario(solicitante, emailDoBeneficiario, dominios, erros);

        if (erros.Count > 0)
        {
            return Result<PedidoDeConcessao>.ValidationFailure(erros);
        }

        PedidoDeConcessao pedido = new()
        {
            SolicitanteEmissor = solicitante.Identidade.Emissor,
            SolicitanteSub = solicitante.Identidade.Sub,
            SolicitanteNome = NomeDeExibicao.Normalizar(solicitante.Nome),
            BeneficiarioEmail = email!,
            PerfilId = perfil.Id,
            UnidadeId = escopo.UnidadeId,
            Justificativa = justificativaNormalizada!,
            Situacao = paraSiMesmo ? SituacaoDoPedido.AguardandoGestor : SituacaoDoPedido.AguardandoBeneficiario,
            CriadoEm = instante,
        };

        if (paraSiMesmo)
        {
            pedido.BeneficiarioEmissor = solicitante.Identidade.Emissor;
            pedido.BeneficiarioSub = solicitante.Identidade.Sub;
            pedido.BeneficiarioNome = NomeDeExibicao.Normalizar(solicitante.Nome);
        }

        return Result<PedidoDeConcessao>.Success(pedido);
    }

    /// <summary>
    /// O beneficiário aceita o pedido que o nomeou: o e-mail verificado do token tem de ser o do pedido, e o
    /// aceite grava a identidade e o nome do token dele.
    /// </summary>
    public Result Aceitar(PessoaNoToken pessoa, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        if (Situacao != SituacaoDoPedido.AguardandoBeneficiario)
        {
            return Result.Failure(SituacaoInvalida("O pedido não aguarda o aceite do beneficiário."));
        }

        if (!EhOBeneficiarioPeloEmail(pessoa))
        {
            return Result.Failure(new DomainError(
                PedidoErrorCodes.NaoEOBeneficiario,
                "Só a pessoa a quem o pedido se destina, com o e-mail institucional verificado, o aceita."));
        }

        // O aprovador julga a pessoa pelo nome: sem ele no token, o pedido não avança à aprovação.
        if (NomeDeExibicao.Normalizar(pessoa.Nome) is not { } nome)
        {
            return Result.Failure(SemNome());
        }

        BeneficiarioEmissor = pessoa.Identidade.Emissor;
        BeneficiarioSub = pessoa.Identidade.Sub;
        BeneficiarioNome = nome;
        BeneficiarioAceitouEm = instante;
        Situacao = SituacaoDoPedido.AguardandoGestor;

        return Result.Success();
    }

    /// <summary>A pessoa é uma das partes do pedido: o solicitante ou o beneficiário.</summary>
    public bool EhParte(PessoaNoToken pessoa)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return pessoa.Identidade == Solicitante
            || (Beneficiario is { } beneficiario && pessoa.Identidade == beneficiario)
            || (Beneficiario is null && EhOBeneficiarioPeloEmail(pessoa));
    }

    /// <summary>Cancela o pedido, só antes da primeira decisão.</summary>
    public Result Cancelar(DateTimeOffset instante)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result.Failure(SituacaoInvalida("O pedido só se cancela antes da primeira decisão."));
        }

        Encerrar(SituacaoDoPedido.Cancelado, instante);
        return Result.Success();
    }

    /// <summary>Cria o anexo e devolve-o para o handler emitir a URL de envio. Só antes da primeira decisão.</summary>
    public Result<AnexoDoPedido> NovoAnexo(long tamanhoDoPdf)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result<AnexoDoPedido>.Failure(SituacaoInvalida("O anexo só se envia antes da primeira decisão."));
        }

        Result<AnexoDoPedido> anexo = AnexoDoPedido.Criar(Id, tamanhoDoPdf);
        if (anexo.IsSuccess)
        {
            _anexos.Add(anexo.Value!);
        }

        return anexo;
    }

    /// <summary>Põe o anexo em verificação, com o ETag que o PUT devolveu. Só antes da primeira decisão.</summary>
    public Result<AnexoDoPedido> ConfirmarAnexo(Guid anexoId, string? etag, DateTimeOffset instante)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result<AnexoDoPedido>.Failure(SituacaoInvalida("O anexo só se confirma antes da primeira decisão."));
        }

        AnexoDoPedido? anexo = _anexos.Find(a => a.Id == anexoId);
        if (anexo is null)
        {
            return Result<AnexoDoPedido>.Failure(new DomainError(
                PedidoErrorCodes.NaoEncontrado, "Anexo do pedido não encontrado."));
        }

        Result confirmado = anexo.ConfirmarEnvio(etag, instante);

        return confirmado.IsSuccess
            ? Result<AnexoDoPedido>.Success(anexo)
            : Result<AnexoDoPedido>.ValidationFailure(confirmado.Errors);
    }

    /// <summary>
    /// Aprova a etapa corrente. A primeira etapa é sempre a do gestor: se o perfil, naquele instante, reúne permissão
    /// pessoal ou sensível o pedido segue para a encarregada, senão a aprovação conclui. A segunda aprovação é
    /// dispensada para o perfil da própria encarregada, que ninguém poderia aprovar, e para o beneficiário que já
    /// é a encarregada (ADR-0152); a decisão do gestor registra a dispensa e o motivo.
    /// </summary>
    /// <remarks>
    /// O perfil que mudou entre as etapas devolve o pedido ao gestor e responde <see cref="PedidoErrorCodes.PerfilAlterado"/>:
    /// o pedido muda de estado mesmo na falha, e quem chama precisa gravá-lo.
    /// </remarks>
    public Result<DesfechoDaAprovacao> Aprovar(
        Identidade decisor,
        PerfilAvaliado perfil,
        ContextoDaAprovacao contexto,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(decisor);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(contexto);

        if (EtapaAtual is not { } etapa)
        {
            return Result<DesfechoDaAprovacao>.Failure(SituacaoInvalida("O pedido não aguarda aprovação."));
        }

        List<FieldError> erros = [];
        ConferirPessoasDistintas(decisor, etapa, erros);

        if (!perfil.Ativo)
        {
            erros.Add(new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita aprovação.")));
        }

        if (etapa == EtapaDoPedido.Gestor && perfil.Governanca is not null && !TemAnexoVerificado)
        {
            erros.Add(new("anexo", new DomainError(
                PedidoErrorCodes.DocumentoObrigatorio,
                "O pedido de perfil de governança exige o documento de designação anexado e verificado.")));
        }

        if (erros.Count > 0)
        {
            return Result<DesfechoDaAprovacao>.ValidationFailure(erros);
        }

        if (etapa == EtapaDoPedido.Encarregada && !MesmoPerfilQueOGestorAvaliou(perfil))
        {
            // Nenhuma etapa concede o que a anterior não avaliou: o gestor decide de novo sobre o perfil de agora, e a
            // tentativa fica registrada com quem a fez e o perfil que ele viu.
            _decisoes.Add(DecisaoDoPedido.Registrar(
                Id, etapa, decisor, ResultadoDaDecisao.PerfilAlterado, null, perfil, null, instante));
            Situacao = SituacaoDoPedido.AguardandoGestor;
            return Result<DesfechoDaAprovacao>.Failure(new DomainError(
                PedidoErrorCodes.PerfilAlterado,
                "O perfil mudou depois da aprovação do gestor; o pedido voltou a aguardar o gestor."));
        }

        string? dispensa = etapa == EtapaDoPedido.Gestor ? MotivoDaDispensa(perfil, contexto) : null;
        bool concluido = etapa == EtapaDoPedido.Encarregada || dispensa is not null || !perfil.ExigeEncarregada;

        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, etapa, decisor, ResultadoDaDecisao.Aprovado, null, perfil, dispensa, instante));

        if (concluido)
        {
            Encerrar(SituacaoDoPedido.Aprovado, instante);
            return Result<DesfechoDaAprovacao>.Success(DesfechoDaAprovacao.Concluido);
        }

        Situacao = SituacaoDoPedido.AguardandoEncarregada;
        return Result<DesfechoDaAprovacao>.Success(DesfechoDaAprovacao.AguardaEncarregada);
    }

    /// <summary>Recusa o pedido na etapa corrente, com motivo. Encerra o pedido, sem concessão.</summary>
    public Result Recusar(Identidade decisor, string? motivo, PerfilAvaliado perfil, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(decisor);
        ArgumentNullException.ThrowIfNull(perfil);

        if (EtapaAtual is not { } etapa)
        {
            return Result.Failure(SituacaoInvalida("O pedido não aguarda decisão."));
        }

        List<FieldError> erros = [];
        ConferirPessoasDistintas(decisor, etapa, erros);

        string? motivoNormalizado = NormalizarTexto(motivo, 1, MotivoMaxLength);
        if (motivoNormalizado is null)
        {
            erros.Add(new("motivo", new DomainError(
                PedidoErrorCodes.MotivoInvalido,
                $"O motivo da recusa é obrigatório e deve ter até {MotivoMaxLength} caracteres.")));
        }

        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, etapa, decisor, ResultadoDaDecisao.Recusado, motivoNormalizado, perfil, null, instante));
        Encerrar(SituacaoDoPedido.Recusado, instante);

        return Result.Success();
    }

    /// <summary>Registra a concessão que a última aprovação gerou.</summary>
    public void RegistrarConcessao(Guid concessaoId)
    {
        if (Situacao != SituacaoDoPedido.Aprovado)
        {
            throw new InvalidOperationException("Só o pedido aprovado tem concessão.");
        }

        ConcessaoId = concessaoId;
    }

    private bool PodeSerAlteradoPeloSolicitante =>
        Situacao is SituacaoDoPedido.AguardandoBeneficiario or SituacaoDoPedido.AguardandoGestor && _decisoes.Count == 0;

    private DecisaoDoPedido? UltimaAprovacaoDoGestor => _decisoes
        .Where(d => d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado)
        .MaxBy(d => d.Instante);

    private bool EhOBeneficiarioPeloEmail(PessoaNoToken pessoa) =>
        pessoa.EmailVerificado
        && EmailNormalizado(pessoa.Email) is { } email
        && string.Equals(email, BeneficiarioEmail, StringComparison.Ordinal);

    private void ConferirPessoasDistintas(Identidade decisor, EtapaDoPedido etapa, List<FieldError> erros)
    {
        if (decisor == Solicitante || (Beneficiario is { } beneficiario && decisor == beneficiario))
        {
            erros.Add(new("decisor", new DomainError(
                PedidoErrorCodes.DecisorEParte,
                "Ninguém decide pedido em que é solicitante ou beneficiário.")));
        }

        if (etapa == EtapaDoPedido.Encarregada && _decisoes.Exists(d =>
                d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado && d.Decisor == decisor))
        {
            erros.Add(new("decisor", new DomainError(
                PedidoErrorCodes.MesmoDecisorNasDuasEtapas,
                "Quem decidiu como gestor não decide como encarregada de dados.")));
        }
    }

    private bool MesmoPerfilQueOGestorAvaliou(PerfilAvaliado perfil) =>
        UltimaAprovacaoDoGestor is { } gestor
        && gestor.PermissoesAvaliadas.SequenceEqual(perfil.Permissoes)
        && gestor.SensibilidadeAvaliada == perfil.Sensibilidade;

    private static string? MotivoDaDispensa(PerfilAvaliado perfil, ContextoDaAprovacao contexto) => perfil switch
    {
        { Governanca: TipoDeGovernanca.EncarregadaDeDados } => MotivosDeDispensa.DesignacaoDaEncarregada,
        { ExigeEncarregada: true } when contexto.BeneficiarioJaEEncarregada => MotivosDeDispensa.BeneficiariaJaEEncarregada,
        _ => null,
    };

    private static DomainError SemNome() => new(
        PedidoErrorCodes.BeneficiarioSemNome,
        "A sessão do beneficiário não traz o nome completo, que o aprovador precisa para julgar o pedido.");

    private void Encerrar(SituacaoDoPedido situacao, DateTimeOffset instante)
    {
        Situacao = situacao;
        EncerradoEm = instante;
    }

    // O beneficiário é o próprio solicitante quando o e-mail vem em branco ou é o do token dele, verificado:
    // quem usa o próprio e-mail já provou que é ele, e não há ninguém a quem esperar.
    private static (string? Email, bool ParaSiMesmo) ResolverBeneficiario(
        PessoaNoToken solicitante,
        string? emailInformado,
        IDominiosInstitucionais dominios,
        List<FieldError> erros)
    {
        string? informado = EmailNormalizado(emailInformado);
        string? doToken = solicitante.EmailVerificado ? EmailNormalizado(solicitante.Email) : null;
        bool paraSiMesmo = ParaSiMesmo(solicitante, emailInformado);

        if (paraSiMesmo)
        {
            if (doToken is null || !DominioInstitucional(doToken, dominios))
            {
                erros.Add(new("beneficiarioEmail", new DomainError(
                    PedidoErrorCodes.BeneficiarioInvalido,
                    "Para pedir para si, o e-mail da sessão deve ser institucional e verificado; para outra pessoa, informe o e-mail institucional dela.")));
            }

            if (NomeDeExibicao.Normalizar(solicitante.Nome) is null)
            {
                erros.Add(new("beneficiario", SemNome()));
            }

            return (doToken, true);
        }

        if (informado is null || !DominioInstitucional(informado, dominios))
        {
            erros.Add(new("beneficiarioEmail", new DomainError(
                PedidoErrorCodes.BeneficiarioInvalido,
                "O beneficiário é identificado pelo e-mail institucional dele.")));
        }

        return (informado, false);
    }

    private static bool ParaSiMesmo(PessoaNoToken solicitante, string? emailInformado)
    {
        string? informado = EmailNormalizado(emailInformado);
        string? doToken = solicitante.EmailVerificado ? EmailNormalizado(solicitante.Email) : null;

        return string.IsNullOrWhiteSpace(emailInformado) || (informado is not null && informado == doToken);
    }

    /// <summary>O e-mail da sessão é verificado e de domínio institucional: quem pede por outra pessoa responde por ela.</summary>
    public static bool SolicitanteInstitucional(PessoaNoToken pessoa, IDominiosInstitucionais dominios)
    {
        ArgumentNullException.ThrowIfNull(pessoa);
        ArgumentNullException.ThrowIfNull(dominios);

        return pessoa.EmailVerificado
            && EmailNormalizado(pessoa.Email) is { } email
            && DominioInstitucional(email, dominios);
    }

    private static bool DominioInstitucional(string email, IDominiosInstitucionais dominios) =>
        dominios.Contem(email[(email.LastIndexOf('@') + 1)..]);

    /// <summary>O e-mail como o pedido o guarda e compara (minúsculas, formato válido); nulo se inválido.</summary>
    public static string? EmailNormalizado(string? email)
    {
        Result<Email> resultado = Email.Criar(email);

        return resultado.IsSuccess && resultado.Value!.Valor.Length <= EmailMaxLength && !resultado.Value.Valor.Contains('\0', StringComparison.Ordinal)
            ? resultado.Value.Valor
            : null;
    }

    // O caractere nulo cabe no tamanho, mas a coluna textual do banco não o armazena.
    private static string? NormalizarTexto(string? texto, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        string normalizado = texto.Trim();

        return normalizado.Contains('\0', StringComparison.Ordinal) || normalizado.Length < min || normalizado.Length > max
            ? null
            : normalizado;
    }

    private static DomainError SituacaoInvalida(string mensagem) => new(PedidoErrorCodes.SituacaoInvalida, mensagem);
}

/// <summary>O que a aprovação de uma etapa deixou: o pedido concluído, a conceder, ou a aguardar a encarregada.</summary>
public enum DesfechoDaAprovacao
{
    /// <summary>A última aprovação exigida foi dada: quem chama concede o acesso.</summary>
    Concluido = 1,

    /// <summary>A aprovação do gestor foi dada, e o perfil exige a da encarregada de dados.</summary>
    AguardaEncarregada = 2,
}
