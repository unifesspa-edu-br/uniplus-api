namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de <see cref="ProjecaoDoProcessoSeletivo.Projetar"/> para
/// <c>DocumentosExigidos</c> (Story #554, PR #895): a projeção precisa emitir o mesmo
/// token de wire que <c>DefinirDocumentosExigidosCommandValidator</c> aceita — round-trip
/// GET→PUT direto, sem transformação do cliente.
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerDocumentosExigidosTests
{
    private static FaseCronograma FaseQualquer() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar(
            "PS Query", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static (ProcessoSeletivo Processo, FaseCronograma Fase) ProcessoComUmaFase()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        return (processo, fase);
    }

    private static DocumentoExigido Exigencia(
        Guid faseId,
        Aplicabilidade aplicabilidade,
        IReadOnlyList<CondicaoGatilho>? condicoes = null,
        IReadOnlyList<DocumentoExigidoBaseLegal>? basesLegais = null,
        IdadeMaximaEmissao? idade = null,
        FormatosPermitidos? formatos = null,
        int? tamanhoMaximoBytes = null) =>
        DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: condicoes ?? [], basesLegais: basesLegais ?? [], idadeMaximaEmissao: idade,
            formatosPermitidos: formatos ?? FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: tamanhoMaximoBytes).Value!;

    private static void ConferirSomentePdfSemTeto(JsonElement formatos)
    {
        formatos.ValueKind.Should().Be(JsonValueKind.Array);
        formatos.EnumerateArray().Should().ContainSingle(
            e => e.GetProperty("formato").GetString() == "PDF" && e.GetProperty("tamanhoMaximoBytesMax").ValueKind == JsonValueKind.Null);
    }

    private static IdadeMaximaEmissao NoventaDiasNaSubmissao() =>
        IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.DataSubmissao, null, null).Value!;

    private static async Task<ProcessoSeletivoDto> ProjetarAsync(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return (await ObterProcessoSeletivoQueryHandler.Handle(new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None))!;
    }

    private static async Task<DocumentoExigidoDto> ProjetarExigenciaAsync(ProcessoSeletivo processo, DocumentoExigido exigencia)
    {
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();

        return (await ProjetarAsync(processo)).DocumentosExigidos.Should().ContainSingle().Which;
    }

    [Theory(DisplayName = "Projeta aplicabilidade no mesmo token de wire aceito pelo validator (GERAL/CONDICIONAL)")]
    [InlineData(Aplicabilidade.Geral, "GERAL")]
    [InlineData(Aplicabilidade.Condicional, "CONDICIONAL")]
    public async Task Handle_Aplicabilidade_EmiteTokenDeWire(Aplicabilidade aplicabilidade, string tokenEsperado)
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        DocumentoExigido exigencia = Exigencia(fase.Id, aplicabilidade);

        DocumentoExigidoDto projetado = await ProjetarExigenciaAsync(processo, exigencia);

        projetado.Aplicabilidade.Tipo.Should().Be(tokenEsperado);
        projetado.TipoDocumento.Id.Should().Be(exigencia.TipoDocumentoOrigemId);
    }

    [Fact(DisplayName = "Projeta Condicoes do gatilho DNF (issue #892) — round-trip GET→PUT sem perda")]
    public async Task Handle_CondicaoGatilho_EmiteTokenDeWireERoundTripDoValor()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        CondicaoGatilho escalar = CondicaoGatilho.Criar(0, "SEXO", Operador.Igual, JsonSerializer.SerializeToElement("MASCULINO")).Value!;
        CondicaoGatilho multivalorada = CondicaoGatilho.Criar(
            1, "MODALIDADE", Operador.Em, JsonSerializer.SerializeToElement(new[] { "LB_PPI", "AC" })).Value!;

        DocumentoExigidoDto projetado = await ProjetarExigenciaAsync(processo, Exigencia(fase.Id, Aplicabilidade.Condicional, [escalar, multivalorada]));

        projetado.Aplicabilidade.Condicoes.Should().HaveCount(2);
        CondicaoGatilhoDto escalarDto = projetado.Aplicabilidade.Condicoes.Should().ContainSingle(c => c.Fato == "SEXO").Which;
        escalarDto.Operador.Should().Be("IGUAL");
        escalarDto.Valor.Should().Be("\"MASCULINO\"", "GetRawText preserva o JSON canônico — o mesmo PUT reinterpreta como JSON válido");
        CondicaoGatilhoDto emDto = projetado.Aplicabilidade.Condicoes.Should().ContainSingle(c => c.Fato == "MODALIDADE").Which;
        emDto.Operador.Should().Be("EM");
        JsonDocument.Parse(emDto.Valor).RootElement.EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo(["LB_PPI", "AC"]);
    }

    [Fact(DisplayName = "Projeta ReferenciaTemporalFatos no agregado GET (issue #892) — round-trip GET→PUT")]
    public async Task Handle_ReferenciaTemporalFatos_EmiteTokenDeWire()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        processo.DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, fase.Id).Value!).IsSuccess.Should().BeTrue();

        ProcessoSeletivoDto dto = await ProjetarAsync(processo);

        dto.ReferenciaTemporalFatos.Should().NotBeNull();
        dto.ReferenciaTemporalFatos!.Tipo.Should().Be("FIM_FASE");
        dto.ReferenciaTemporalFatos.FaseId.Should().Be(fase.Id);
        dto.ReferenciaTemporalFatos.Data.Should().BeNull();
    }

    [Fact(DisplayName = "ReferenciaTemporalFatos ausente projeta null no agregado GET (contraprova)")]
    public async Task Handle_SemReferenciaTemporalFatos_ProjetaNulo() =>
        (await ProjetarAsync(NovoProcesso())).ReferenciaTemporalFatos.Should().BeNull();

    [Fact(DisplayName = "Story #554/issue #549 (PR #898): projeta BasesLegais — round-trip GET→PUT, inclusive PENDENTE (a projeção 'só RESOLVIDO' é da PR #903, não deste DTO de edição)")]
    public async Task Handle_BasesLegais_EmiteTokenDeWireERoundTrip()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        DocumentoExigidoBaseLegal resolvida = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, "Observação").Value!;
        DocumentoExigidoBaseLegal pendente = DocumentoExigidoBaseLegal.Criar(
            "Cláusula 5.2 do edital", TipoAbrangencia.InternaEdital, StatusBaseLegal.Pendente, null).Value!;

        DocumentoExigidoDto projetado = await ProjetarExigenciaAsync(processo, Exigencia(fase.Id, Aplicabilidade.Geral, basesLegais: [resolvida, pendente]));

        projetado.Indeferimento.BasesLegais.Should().BeEquivalentTo(
            [new BaseLegalDto(Guid.Empty, "Lei 12.711/2012, art. 3º", "FEDERAL", "RESOLVIDO", "Observação"),
             new BaseLegalDto(Guid.Empty, "Cláusula 5.2 do edital", "INTERNA_EDITAL", "PENDENTE", null)],
            opcoes => opcoes.Excluding(b => b.Id));
    }

    // DATA_SUBMISSAO não exige fase âncora nem extremo, diferente de INICIO_FASE/FIM_FASE: a fase do teste não declara
    // fim, e uma âncora de fase reprovaria por IdadeMaximaEmissao.FaseExtremoAusente (coberto em teste próprio).
    [Fact(DisplayName = "Story #554/issue #893 (PR #900): projeta IdadeMaximaEmissao/TamanhoMaximoBytes — round-trip GET→PUT")]
    public async Task Handle_IdadeFormatoTamanho_EmiteTokensDeWire()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        FormatosPermitidos somentePdf = FormatosPermitidos.Criar(qualquer: false, entradas: [("PDF", null)]).Value!;

        DocumentoExigidoDto projetado = await ProjetarExigenciaAsync(
            processo, Exigencia(fase.Id, Aplicabilidade.Geral, idade: NoventaDiasNaSubmissao(), formatos: somentePdf, tamanhoMaximoBytes: 5_000_000));

        ConferirSomentePdfSemTeto(projetado.Requisitos.FormatosPermitidos);
        projetado.Requisitos.TamanhoMaximoBytes.Should().Be(5_000_000);
        projetado.Requisitos.IdadeMaximaEmissao.Should().BeEquivalentTo(new IdadeMaximaEmissaoDto(90, "DIAS", "DATA_SUBMISSAO", null, null));
    }

    [Fact(DisplayName = "Sem idade/tamanho configurados (FormatosPermitidos=QUALQUER), a projeção emite QUALQUER e o resto null (contraprova)")]
    public async Task Handle_SemIdadeFormatoTamanho_ProjetaNulo()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();

        DocumentoExigidoDto projetado = await ProjetarExigenciaAsync(processo, Exigencia(fase.Id, Aplicabilidade.Geral));

        projetado.Requisitos.FormatosPermitidos.GetString().Should().Be("QUALQUER");
        projetado.Requisitos.TamanhoMaximoBytes.Should().BeNull();
        projetado.Requisitos.IdadeMaximaEmissao.Should().BeNull();
        projetado.Requisitos.Modelo.Should().BeNull();
    }
}
