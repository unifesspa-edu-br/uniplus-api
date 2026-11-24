namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.ValueObjects;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Projeção de leitura de uma modalidade selecionada.</summary>
public sealed record ModalidadeSelecionadaDto(
    Guid Id,
    Guid ModalidadeOrigemId,
    string Codigo,
    string? Descricao,
    CodigoComRotulo<NaturezaLegalModalidade> NaturezaLegal,
    CodigoComRotulo<ComposicaoVagasModalidade> ComposicaoVagas,
    string? ComposicaoOrigemCodigo,
    CodigoComRotulo<RegraRemanejamentoModalidade>? RegraRemanejamento,
    string? RemanejamentoDestino,
    string? RemanejamentoPar,
    string? RemanejamentoFallback,
    // Quem concorre à modalidade, como o cadastro a declarou quando a distribuição foi
    // gravada: sem cláusula, a modalidade é incondicional.
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> RegraDeAplicabilidade,
    string? AcaoQuandoIndeferido,
    string BaseLegal,
    int? QuantidadeDeclarada);

/// <summary>Projeção de leitura de uma referência a regra do catálogo de regras.</summary>
public sealed record ReferenciaRegraDto(string Codigo, string Versao, string Hash);

/// <summary>Projeção de leitura do snapshot de referência de reserva demográfica.</summary>
public sealed record ReferenciaReservaDemograficaSnapshotDto(
    Guid OrigemId,
    string CensoReferencia,
    decimal PpiPercentual,
    decimal QuilombolaPercentual,
    decimal PcdPercentual,
    string BaseLegal);

/// <summary>Grupo de área do ENEM congelado na distribuição de vagas, com código e rótulo.</summary>
public sealed record GrupoAreaEnemSnapshotDto(string Codigo, string Rotulo);

/// <summary>Projeção de leitura de uma linha do quadro de vagas.</summary>
public sealed record VagaOfertadaDto(Guid Id, Guid ModalidadeOrigemId, string ModalidadeCodigo, int Quantidade);

/// <summary>As vagas que o quadro da Lei de Cotas deu à reserva legal frente ao que a lei nomina (ADR-0115).</summary>
public sealed record ConferenciaDaReservaLegalDto(
    int ReservaLegalNominal,
    int ReservaLegalEfetiva,
    int ExcedenteSobreAReservaLegal,
    int VagasCortadasPorEscassez);

/// <summary>As vagas que o ajuste retirou de uma modalidade do quadro fixado no edital.</summary>
public sealed record VagasRetiradasDaModalidadeDto(string ModalidadeCodigo, int Quantidade);

/// <summary>O que o ajuste retirou do quadro fixado no edital, por modalidade e no total (ADR-0115).</summary>
public sealed record AjusteDoQuadroFixadoDto(
    IReadOnlyList<VagasRetiradasDaModalidadeDto> VagasRetiradas,
    int TotalDeVagasRetiradas);

/// <summary>
/// Projeção de leitura de <c>ConfiguracaoDistribuicaoVagas</c>.
/// O quadro de vagas é output derivado, sempre materializado junto da
/// configuração: <see cref="Quadro"/> e o que o acompanha refletem exatamente o que foi congelado. Cada
/// ramo descreve só o que é dele: <see cref="ConferenciaDaReservaLegal"/> na Lei de Cotas (e
/// <see cref="PercentualDaReservaLegal"/>), <see cref="AjusteDoQuadroFixado"/> no quadro fixado no
/// edital. <see cref="GrupoAreaEnem"/> é o grupo de área do ENEM do curso da oferta, com código e
/// rótulo, congelado na definição; <see langword="null"/> quando o curso não o declara.
/// </summary>
/// <param name="RamoDaDistribuicao">
/// O ramo em que a distribuição foi montada (<c>LEI_DE_COTAS</c> ou <c>QUADRO_FIXADO_NO_EDITAL</c>), calculado
/// do código da <paramref name="RegraDistribuicao"/> congelado na configuração, nunca do catálogo vivo;
/// <see langword="null"/> para um código que não é de nenhum dos dois ramos.
/// </param>
public sealed record ConfiguracaoDistribuicaoVagasDto(
    Guid Id,
    Guid OfertaCursoOrigemId,
    int VagasOfertadasBase,
    decimal? PercentualDaReservaLegal,
    ReferenciaRegraDto RegraDistribuicao,
    string? RamoDaDistribuicao,
    ReferenciaRegraDto? RegraAjuste,
    ParametrosRegraAjusteDistribuicao? AjusteDoQuadro,
    ReferenciaReservaDemograficaSnapshotDto? ReferenciaDemografica,
    GrupoAreaEnemSnapshotDto? GrupoAreaEnem,
    IReadOnlyList<ModalidadeSelecionadaDto> Modalidades,
    IReadOnlyList<VagaOfertadaDto> Quadro,
    int TotalDeVagasDoEdital,
    ConferenciaDaReservaLegalDto? ConferenciaDaReservaLegal,
    AjusteDoQuadroFixadoDto? AjusteDoQuadroFixado);
