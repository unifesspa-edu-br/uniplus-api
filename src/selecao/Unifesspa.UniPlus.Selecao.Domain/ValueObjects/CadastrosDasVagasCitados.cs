namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// A oferta de curso que o documento cita pelo código: o id dela no cadastro, o nome do curso e da unidade ofertante, os turnos, o teto
/// de vagas anuais autorizadas e o grupo de área do ENEM dela, que ficam nulos quando o cadastro não os tem.
/// </summary>
public sealed record OfertaDeCursoCitada(
    Guid OrigemId,
    string Codigo,
    string CursoNome,
    string CursoGrau,
    string UnidadeOfertanteNome,
    IReadOnlyList<string> Turnos,
    int? VagasAnuaisAutorizadas,
    string? GrupoAreaEnemCodigo,
    string? GrupoAreaEnemRotulo);

/// <summary>A condição de atendimento, o recurso de acessibilidade ou o tipo de deficiência que o documento cita pelo código: o id dele no cadastro e o nome.</summary>
public sealed record OpcaoDeAtendimentoCitada(Guid OrigemId, string Codigo, string Nome);

/// <summary>
/// Os cadastros que a distribuição de vagas, o atendimento especializado, o FADA e as exigências de documentos citam, cada um pelo
/// código: só o que o cadastro encontrou vivo, de modo que o código ausente é o que o cadastro não tem. Cada dicionário vem vazio e
/// sai congelado.
/// </summary>
public sealed record CadastrosDasVagasEDasExigenciasCitados
{
    private readonly FrozenDictionary<string, OfertaDeCursoCitada> _ofertasDeCurso = FrozenDictionary<string, OfertaDeCursoCitada>.Empty;
    private readonly FrozenDictionary<string, ModalidadeNoCadastro> _modalidades = FrozenDictionary<string, ModalidadeNoCadastro>.Empty;
    private readonly FrozenDictionary<string, ReferenciaReservaDemograficaSnapshot> _referenciasDemograficas = FrozenDictionary<string, ReferenciaReservaDemograficaSnapshot>.Empty;
    private readonly FrozenDictionary<string, OpcaoDeAtendimentoCitada> _condicoesDeAtendimento = FrozenDictionary<string, OpcaoDeAtendimentoCitada>.Empty;
    private readonly FrozenDictionary<string, OpcaoDeAtendimentoCitada> _recursosDeAcessibilidade = FrozenDictionary<string, OpcaoDeAtendimentoCitada>.Empty;
    private readonly FrozenDictionary<string, OpcaoDeAtendimentoCitada> _tiposDeDeficiencia = FrozenDictionary<string, OpcaoDeAtendimentoCitada>.Empty;
    private readonly FrozenDictionary<string, CopiaDoTipoDeDocumento> _tiposDeDocumento = FrozenDictionary<string, CopiaDoTipoDeDocumento>.Empty;
    private readonly FrozenDictionary<string, BaseLegalCongelada> _basesLegaisDoFada = FrozenDictionary<string, BaseLegalCongelada>.Empty;

    /// <summary>As ofertas de curso vivas, pelo código da oferta.</summary>
    public IReadOnlyDictionary<string, OfertaDeCursoCitada> OfertasDeCurso
    {
        get => _ofertasDeCurso;
        init => _ofertasDeCurso = Congelado(value);
    }

    /// <summary>As modalidades vivas, pelo código, como o cadastro as dá à distribuição.</summary>
    public IReadOnlyDictionary<string, ModalidadeNoCadastro> Modalidades
    {
        get => _modalidades;
        init => _modalidades = Congelado(value);
    }

    /// <summary>As referências de reserva demográfica vivas, pelo código (o censo de referência), já como a cópia que a distribuição guarda; a que o domínio recusa copiar fica de fora.</summary>
    public IReadOnlyDictionary<string, ReferenciaReservaDemograficaSnapshot> ReferenciasDemograficas
    {
        get => _referenciasDemograficas;
        init => _referenciasDemograficas = Congelado(value);
    }

    /// <summary>As condições de atendimento vivas, pelo código.</summary>
    public IReadOnlyDictionary<string, OpcaoDeAtendimentoCitada> CondicoesDeAtendimento
    {
        get => _condicoesDeAtendimento;
        init => _condicoesDeAtendimento = Congelado(value);
    }

    /// <summary>Os recursos de acessibilidade vivos, pelo código.</summary>
    public IReadOnlyDictionary<string, OpcaoDeAtendimentoCitada> RecursosDeAcessibilidade
    {
        get => _recursosDeAcessibilidade;
        init => _recursosDeAcessibilidade = Congelado(value);
    }

    /// <summary>Os tipos de deficiência vivos, pelo código.</summary>
    public IReadOnlyDictionary<string, OpcaoDeAtendimentoCitada> TiposDeDeficiencia
    {
        get => _tiposDeDeficiencia;
        init => _tiposDeDeficiencia = Congelado(value);
    }

    /// <summary>Os tipos de documento vivos, pelo código, já como a cópia que a exigência guarda.</summary>
    public IReadOnlyDictionary<string, CopiaDoTipoDeDocumento> TiposDeDocumento
    {
        get => _tiposDeDocumento;
        init => _tiposDeDocumento = Congelado(value);
    }

    /// <summary>As bases legais do FADA, pelo código, já como o processo as congela.</summary>
    public IReadOnlyDictionary<string, BaseLegalCongelada> BasesLegaisDoFada
    {
        get => _basesLegaisDoFada;
        init => _basesLegaisDoFada = Congelado(value);
    }

    private static FrozenDictionary<string, T> Congelado<T>(IReadOnlyDictionary<string, T>? cadastro) =>
        (cadastro ?? throw new ArgumentNullException(nameof(cadastro))).ToFrozenDictionary(StringComparer.Ordinal);
}
