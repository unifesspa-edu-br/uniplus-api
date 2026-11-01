namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Modelo de persistência de <see cref="Domain.Entities.Inscricao"/> — não é a entidade de
/// domínio. O EF Core mapeia exclusivamente este tipo; o domínio nunca é materializado
/// diretamente pelo <c>ChangeTracker</c> (ADR-0121, Opção C, mesmo desenho de
/// <c>VinculoDiscenteRecord</c> em Discentes).
/// </summary>
/// <remarks>
/// O CPF não é coluna da inscrição: é a resposta do fato <c>CPF</c>, gravada cifrada como as demais,
/// de modo que o dado tem uma fonte só e a proteção congelada no processo o alcança.
/// <see cref="CpfIndice"/> é o índice cego (HMAC) de <c>IUniPlusBlindIndexService</c>, que sustenta o índice único
/// <c>(ProcessoSeletivoId, CpfIndice)</c> e o cruzamento com o INEP; não é dado exibido. As respostas ficam em <see cref="RespostaDaInscricaoRecord"/>,
/// <see cref="EnderecoDaInscricaoRecord"/>, <see cref="OcorrenciaDeGrupoDaInscricaoRecord"/> e
/// <see cref="AceiteDaInscricaoRecord"/> (ADR-0141). A tradução para/de
/// <see cref="Domain.Entities.Inscricao"/> é responsabilidade exclusiva do
/// <c>InscricaoRepository</c>, nunca de um <c>ValueConverter</c>.
/// </remarks>
public sealed class InscricaoRecord : IIdentificavel
{
    public Guid Id { get; set; }

    public Guid ProcessoSeletivoId { get; set; }

    public byte[] CpfIndice { get; set; } = null!;

    public string Sub { get; set; } = null!;

    public string EmissorToken { get; set; } = null!;

    public SituacaoInscricao Situacao { get; set; }

    public Guid VersaoFormularioId { get; set; }

    public Guid? OfertaCursoId { get; set; }

    public Guid? OfertaCursoSegundaOpcaoId { get; set; }

    public string? NumeroInscricao { get; set; }

    public DateTimeOffset? CanceladaEm { get; set; }

    public string? CanceladaPor { get; set; }

    public string? MotivoDoCancelamento { get; set; }

    /// <summary>Token de concorrência (ADR-0119) — espelha <see cref="Domain.Entities.Inscricao.Revisao"/>.</summary>
    public int Revisao { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
