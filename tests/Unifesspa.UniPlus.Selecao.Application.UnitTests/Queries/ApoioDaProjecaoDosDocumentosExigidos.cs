namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Monta o processo com uma fase e uma exigência, e o projeta pela consulta de leitura do processo,
/// para os testes do que a projeção emite dos documentos exigidos.
/// </summary>
internal static class ApoioDaProjecaoDosDocumentosExigidos
{
    /// <summary>
    /// Os dados da exigência que mudam de um teste para outro; o que não é dito é uma exigência geral
    /// e obrigatória do documento de identidade, sem condições, bases legais, prazo de emissão, validade nem tamanho, com qualquer formato.
    /// </summary>
    internal sealed record ExigenciaParaProjetar
    {
        internal Guid TipoDocumentoOrigemId { get; init; } = Guid.CreateVersion7();

        internal string? Consequencia { get; init; }

        internal Aplicabilidade Aplicabilidade { get; init; } = Aplicabilidade.Geral;

        internal IReadOnlyList<CondicaoGatilho> Condicoes { get; init; } = [];

        internal IReadOnlyList<DocumentoExigidoBaseLegal> BasesLegais { get; init; } = [];

        internal PrazoDeEmissao? PrazoDeEmissao { get; init; }

        internal ValidadeDoDocumento? Validade { get; init; }

        internal FormatosPermitidos FormatosPermitidos { get; init; } = FormatosPermitidos.Criar(true, null).Value!;

        internal int? TamanhoMaximoBytes { get; init; }

        internal DocumentoExigido NaFase(Guid faseId) =>
            DocumentoExigidoDeTeste.Novo()
                .NaFase(faseId)
                .ComTipoDeOrigem(TipoDocumentoOrigemId)
                .ComTipo("IDENTIDADE", "Documento de identidade", "PESSOAL")
                .ComAplicabilidade(Aplicabilidade)
                .ComCondicoes(Condicoes)
                .ComConsequencia(Consequencia)
                .ComBasesLegais(BasesLegais)
                .ComPrazoDeEmissao(PrazoDeEmissao)
                .ComValidade(Validade)
                .ComFormatos(FormatosPermitidos)
                .ComTamanhoMaximo(TamanhoMaximoBytes)
                .Criar().Value!;
    }

    internal static ProcessoSeletivo NovoProcesso() => ApoioDosDocumentosExigidos.NovoProcesso("PS Query");

    internal static (ProcessoSeletivo Processo, FaseCronograma Fase) ProcessoComUmaFase()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = ApoioDosDocumentosExigidos.FaseQualquer();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        return (processo, fase);
    }

    internal static async Task<ProcessoSeletivoDto> ProjetarAsync(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);
        dto.Should().NotBeNull();

        return dto!;
    }

    internal static async Task<DocumentoExigidoDto> ProjetarAUnicaExigenciaAsync(ExigenciaParaProjetar exigencia)
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        processo.DefinirDocumentosExigidosNosFormulariosMinimos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia.NaFase(fase.Id), 0).Value!]).IsSuccess.Should().BeTrue();

        ProcessoSeletivoDto dto = await ProjetarAsync(processo);

        return dto.DocumentosExigidos.Should().ContainSingle().Which;
    }

    internal static ExigenciaParaProjetar ExigenciaCondicionalComCondicaoEscalarEMultivalorada() =>
        new()
        {
            Aplicabilidade = Aplicabilidade.Condicional,
            Condicoes =
            [
                CondicaoGatilho.Criar(0, "SEXO", Operador.Igual, JsonSerializer.SerializeToElement("MASCULINO")).Value!,
                CondicaoGatilho.Criar(1, "MODALIDADE", Operador.AlgumDe, JsonSerializer.SerializeToElement(new[] { "LB_PPI", "AC" })).Value!,
            ],
        };

    internal static ExigenciaParaProjetar ExigenciaComUmaBaseLegalResolvidaEUmaPendente() =>
        new()
        {
            BasesLegais =
            [
                DocumentoExigidoBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, "Observação").Value!,
                DocumentoExigidoBaseLegal.Criar("Cláusula 5.2 do edital", TipoAbrangencia.InternaEdital, StatusBaseLegal.Pendente, null).Value!,
            ],
        };

    /// <summary>
    /// Prazo de 90 dias contado da data da apresentação e validade exigida no fim das inscrições: a referência na
    /// apresentação não exige fase nem data no cronograma, que a fase de teste não declara.
    /// </summary>
    internal static ExigenciaParaProjetar ExigenciaComPrazoValidadeFormatoETamanho() =>
        new()
        {
            PrazoDeEmissao = new PrazoDeEmissaoBuilder().Referenciado(new DataDeReferenciaBuilder().NaApresentacao().Construir()).Construir(),
            Validade = new ValidadeDoDocumentoBuilder().ComEfeito(EfeitoNaAnaliseDoDocumento.AceitarComAviso).Construir(),
            FormatosPermitidos = FormatosPermitidos.Criar(qualquer: false, entradas: [("PDF", null)]).Value!,
            TamanhoMaximoBytes = 5_000_000,
        };
}
