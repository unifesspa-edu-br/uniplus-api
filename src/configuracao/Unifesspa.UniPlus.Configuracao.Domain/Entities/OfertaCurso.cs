namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Oferta de Curso — a instância <b>regulatória</b> da oferta acadêmica
/// (story #588, issue #749, ADR-0066): liga um <see cref="Curso"/> (matriz
/// curricular pura) a um <see cref="LocalOferta"/> e à unidade ofertante
/// (instituto/faculdade, snapshot-copy — ADR-0061), carregando os atributos
/// que variam por campus: código da oferta, código e-MEC, código no sistema acadêmico,
/// programa, formato pedagógico, regime de funcionamento, regime de turno e
/// turnos, teto de vagas e-MEC e base legal.
/// O mesmo curso tem ofertas distintas por campus, com códigos e-MEC diferentes.
/// </summary>
/// <remarks>
/// <para><see cref="CursoId"/>, <see cref="LocalOfertaId"/> e
/// <see cref="UnidadeOfertante"/> são <b>imutáveis</b> pós-criação: mudar
/// curso×local×unidade não é editar a oferta, é <i>outra</i> oferta —
/// <see cref="Atualizar"/> não os recebe nem os altera. A unidade ofertante é
/// snapshot-copy (ADR-0061): congelada da Unidade viva no ato da criação, sem FK.</para>
/// <para><see cref="VagasAnuaisAutorizadas"/> é o teto autorizado no e-MEC —
/// <b>não</b> são as vagas de um certame (essas pertencem ao edital, módulo
/// Seleção). A <see cref="BaseLegal"/> é obrigatória quando
/// <see cref="ProgramaDeOferta"/> ≠ <see cref="Enums.ProgramaDeOferta.Regular"/>
/// (guard revalidado na criação E na atualização — ex.: transição
/// Regular→Parfor sem base legal é rejeitada).</para>
/// <para><see cref="RegimeDeTurno"/> e <see cref="Turnos"/> são obrigatórios em
/// <b>todo</b> formato pedagógico, a distância inclusive (UNI-REQ-0137,
/// ADR-0126): <c>REGULAR</c> exige exatamente um turno e <c>INTEGRAL</c>,
/// exatamente dois distintos. O regime é declarado pelo chamador e conferido
/// contra a coleção — dois turnos sob <c>REGULAR</c> é recusa, não promoção
/// silenciosa a <c>INTEGRAL</c>. A leitura devolve os turnos em ordem canônica
/// (matutino, vespertino, noturno), qualquer que tenha sido a ordem de entrada.</para>
/// <para><see cref="RegimeDeFuncionamento"/> é dimensão <b>própria</b> e também
/// obrigatória (UNI-REQ-0138, ADR-0128): <c>INTENSIVO</c>,
/// <c>EXTENSIVO</c> e <c>ALTERNANCIA_PEDAGOGICA</c>,
/// declarado e nunca inferido da quantidade de turnos, do regime de turno, do
/// formato pedagógico ou do programa. A única compatibilidade entre as duas
/// dimensões é que <c>INTENSIVO</c> exige regime de turno <c>INTEGRAL</c>;
/// <c>EXTENSIVO</c> e <c>ALTERNANCIA_PEDAGOGICA</c> aceitam ambos.
/// A combinação incompatível é recusada — nenhuma
/// das duas dimensões é convertida para acomodar a outra.</para>
/// <para>Não há chave natural única entre ofertas vivas — a repetição
/// curso×local×unidade é admitida (ex.: turnos ou programas distintos). A
/// remoção é soft-delete simples e <b>não</b> é bloqueada por snapshots
/// congelados em outros módulos (as cópias de Seleção são desacopladas —
/// ADR-0061). Dado institucional sem PII (LGPD inaplicável).</para>
/// </remarks>
public sealed class OfertaCurso : SoftDeletableEntity, IAuditableEntity
{
    /// <summary>
    /// Código informado pela gestão, com que o edital e o documento de configuração citam a oferta; único entre as ofertas
    /// vivas (a conferência é do handler e o índice do banco decide a corrida) e editável — a identidade é o <c>Id</c>.
    /// </summary>
    public string Codigo { get; private set; } = string.Empty;

    public Guid CursoId { get; private set; }
    public Guid LocalOfertaId { get; private set; }

    /// <summary>Snapshot-copy da unidade ofertante (ADR-0061) — imutável pós-criação.</summary>
    public UnidadeOfertante UnidadeOfertante { get; private set; } = null!;

    private List<TurnoOferta> _turnos = [];

    public ProgramaDeOferta ProgramaDeOferta { get; private set; }
    public FormatoPedagogico FormatoPedagogico { get; private set; }

    /// <summary>
    /// Regime de funcionamento declarado — obrigatório, independente do
    /// <see cref="RegimeDeTurno"/>. <c>INTENSIVO</c> exige regime de turno
    /// <c>INTEGRAL</c>; <c>EXTENSIVO</c> e <c>ALTERNANCIA_PEDAGOGICA</c> aceitam ambos.
    /// </summary>
    public RegimeDeFuncionamento RegimeDeFuncionamento { get; private set; }

    /// <summary>Regime de turno declarado — obrigatório, define quantos turnos a oferta ocupa.</summary>
    public RegimeDeTurno RegimeDeTurno { get; private set; }

    /// <summary>
    /// Turnos ocupados pela oferta, em ordem canônica (matutino, vespertino,
    /// noturno). Um turno sob <see cref="Enums.RegimeDeTurno.Regular"/>; dois
    /// distintos sob <see cref="Enums.RegimeDeTurno.Integral"/>.
    /// </summary>
    public IReadOnlyList<TurnoOferta> Turnos => _turnos.AsReadOnly();

    /// <summary>Código e-MEC da oferta por campus-sede (opcional).</summary>
    public string? CodigoEmec { get; private set; }

    /// <summary>Código no sistema acadêmico (nome vendor-neutral, opcional).</summary>
    public string? CodigoNoSistemaAcademico { get; private set; }

    /// <summary>Teto de vagas anuais autorizadas no e-MEC (≥ 0) — não são vagas de certame.</summary>
    public int? VagasAnuaisAutorizadas { get; private set; }

    /// <summary>Base legal da oferta — obrigatória quando o programa não é Regular (ADR-0066).</summary>
    public string? BaseLegal { get; private set; }

    public string? AtoAutorizacaoMec { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private OfertaCurso()
    {
    }

    /// <summary>
    /// Cria uma nova Oferta de Curso a partir da <paramref name="declaracao"/> da gestão. Os enums chegam como tokens
    /// textuais (UPPER_SNAKE): o programa é obrigatório; o formato pedagógico vale PRESENCIAL quando ausente; o regime de
    /// funcionamento, o regime de turno e os turnos são obrigatórios e conferidos entre si. A
    /// <paramref name="unidadeOfertante"/> já chega congelada (resolvida pelo handler via <c>IUnidadeReader</c>). A existência
    /// viva do curso e do local de oferta e a unicidade do código são responsabilidade do handler.
    /// </summary>
    public static Result<OfertaCurso> Criar(
        Guid cursoId,
        Guid localOfertaId,
        UnidadeOfertante unidadeOfertante,
        DeclaracaoDaOfertaDeCurso declaracao)
    {
        ArgumentNullException.ThrowIfNull(unidadeOfertante);

        Result<CamposDaOfertaDeCurso> campos = Conferir(declaracao);
        if (campos.IsFailure)
        {
            return Result<OfertaCurso>.ValidationFailure(campos.Errors);
        }

        var oferta = new OfertaCurso { CursoId = cursoId, LocalOfertaId = localOfertaId, UnidadeOfertante = unidadeOfertante };
        oferta.AplicarCampos(campos.Value!);

        return Result<OfertaCurso>.Success(oferta);
    }

    /// <summary>
    /// Atualiza os atributos editáveis da oferta: o código, o programa, o formato pedagógico, os regimes, os turnos, os códigos
    /// (e-MEC / SGA), o teto de vagas, a base legal e o ato de autorização. <c>CursoId</c>, <c>LocalOfertaId</c> e
    /// <c>UnidadeOfertante</c> são <b>imutáveis</b> — mudar curso×local×unidade caracteriza outra oferta, não uma edição.
    /// Revalida o guard condicional da base legal na transição (Regular→Parfor sem base legal é rejeitado).
    /// </summary>
    public Result Atualizar(DeclaracaoDaOfertaDeCurso declaracao)
    {
        Result<CamposDaOfertaDeCurso> campos = Conferir(declaracao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        AplicarCampos(campos.Value!);

        return Result.Success();
    }

    /// <summary>
    /// Confere a declaração sem construir nem mutar nada, para os handlers falharem rápido antes de qualquer busca no banco
    /// ou em módulo cruzado (validação sempre vence I/O). <see cref="Criar"/> e <see cref="Atualizar"/> revalidam por conta
    /// própria: este método só antecipa a mesma checagem.
    /// </summary>
    public static Result ValidarDeclaracao(DeclaracaoDaOfertaDeCurso declaracao)
    {
        Result<CamposDaOfertaDeCurso> campos = Conferir(declaracao);

        return campos.IsFailure ? Result.ValidationFailure(campos.Errors) : Result.Success();
    }

    private static Result<CamposDaOfertaDeCurso> Conferir(DeclaracaoDaOfertaDeCurso declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        return new ValidacaoDaOfertaDeCurso(declaracao).Resolver();
    }

    private void AplicarCampos(CamposDaOfertaDeCurso campos)
    {
        Codigo = campos.Codigo;
        ProgramaDeOferta = campos.ProgramaDeOferta;
        FormatoPedagogico = campos.FormatoPedagogico;
        RegimeDeFuncionamento = campos.RegimeDeFuncionamento;
        RegimeDeTurno = campos.RegimeDeTurno;
        _turnos = [.. campos.Turnos];
        CodigoEmec = campos.CodigoEmec;
        CodigoNoSistemaAcademico = campos.CodigoNoSistemaAcademico;
        VagasAnuaisAutorizadas = campos.VagasAnuaisAutorizadas;
        BaseLegal = campos.BaseLegal;
        AtoAutorizacaoMec = campos.AtoAutorizacaoMec;
    }
}
