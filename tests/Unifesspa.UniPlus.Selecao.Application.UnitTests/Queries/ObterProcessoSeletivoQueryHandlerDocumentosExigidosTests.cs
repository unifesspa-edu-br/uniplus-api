namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ObterProcessoSeletivoQueryHandler.Project"/> para
/// <c>DocumentosExigidos</c> (Story #554, PR #895): a projeção precisa emitir o mesmo
/// token de wire que <c>DefinirDocumentosExigidosCommandValidator</c> aceita — round-trip
/// GET→PUT direto, sem transformação do cliente.
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerDocumentosExigidosTests
{
    private static FaseCronograma FaseQualquer() => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false, produzResultado: false,
        resultadoDefinitivo: false, coletaInscricao: false, inicio: null, fim: null,
        atoProduzidoCodigo: null, atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    [Theory(DisplayName = "Projeta aplicabilidade no mesmo token de wire aceito pelo validator (GERAL/CONDICIONAL)")]
    [InlineData(Aplicabilidade.Geral, "GERAL")]
    [InlineData(Aplicabilidade.Condicional, "CONDICIONAL")]
    public async Task Handle_Aplicabilidade_EmiteTokenDeWire(Aplicabilidade aplicabilidade, string tokenEsperado)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Guid tipoDocumentoOrigemId = Guid.CreateVersion7();
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, tipoDocumentoOrigemId, "IDENTIDADE", "Documento de identidade", "PESSOAL",
            aplicabilidade, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        DocumentoExigidoDto projetado = dto!.DocumentosExigidos.Should().ContainSingle().Which;
        projetado.Aplicabilidade.Should().Be(tokenEsperado);
        projetado.TipoDocumentoOrigemId.Should().Be(tipoDocumentoOrigemId);
    }

    [Fact(DisplayName = "Achado Codex P2 (PR #896, issue #892): projeta Condicoes do gatilho DNF — round-trip GET→PUT sem perda")]
    public async Task Handle_CondicaoGatilho_EmiteTokenDeWireERoundTripDoValor()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        CondicaoGatilho condicaoEscalar = CondicaoGatilho.Criar(
            0, "SEXO", Operador.Igual, JsonSerializer.SerializeToElement("MASCULINO")).Value!;
        CondicaoGatilho condicaoMultivalorada = CondicaoGatilho.Criar(
            1, "MODALIDADE", Operador.Em, JsonSerializer.SerializeToElement(new[] { "LB_PPI", "AC" })).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "CERTIDAO_RESERVISTA", "Certidão de reservista", "MILITAR",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [condicaoEscalar, condicaoMultivalorada], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        DocumentoExigidoDto projetado = dto!.DocumentosExigidos.Should().ContainSingle().Which;
        projetado.Condicoes.Should().HaveCount(2);

        CondicaoGatilhoDto escalarDto = projetado.Condicoes.Should().ContainSingle(c => c.Fato == "SEXO").Which;
        escalarDto.Operador.Should().Be("IGUAL");
        escalarDto.Valor.Should().Be("\"MASCULINO\"", "GetRawText preserva o JSON canônico — o mesmo PUT reinterpreta como JSON válido");

        CondicaoGatilhoDto emDto = projetado.Condicoes.Should().ContainSingle(c => c.Fato == "MODALIDADE").Which;
        emDto.Operador.Should().Be("EM");
        JsonDocument.Parse(emDto.Valor).RootElement.EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["LB_PPI", "AC"]);
    }

    [Fact(DisplayName = "Achado Codex P2 (PR #896, issue #892, 3ª rodada): projeta ReferenciaTemporalFatos no agregado GET — round-trip GET→PUT")]
    public async Task Handle_ReferenciaTemporalFatos_EmiteTokenDeWire()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, fase.Id).Value!;
        processo.DefinirReferenciaTemporalFatos(referencia, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        dto!.ReferenciaTemporalFatos.Should().NotBeNull();
        dto.ReferenciaTemporalFatos!.Tipo.Should().Be("FIM_FASE");
        dto.ReferenciaTemporalFatos.FaseId.Should().Be(fase.Id);
        dto.ReferenciaTemporalFatos.Data.Should().BeNull();
    }

    [Fact(DisplayName = "ReferenciaTemporalFatos ausente projeta null no agregado GET (contraprova)")]
    public async Task Handle_SemReferenciaTemporalFatos_ProjetaNulo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        dto!.ReferenciaTemporalFatos.Should().BeNull();
    }

    [Fact(DisplayName = "Story #554/issue #549 (PR #898): projeta BasesLegais — round-trip GET→PUT, inclusive PENDENTE (a projeção 'só RESOLVIDO' é da PR #903, não deste DTO de edição)")]
    public async Task Handle_BasesLegais_EmiteTokenDeWireERoundTrip()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigidoBaseLegal baseResolvida = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, "Observação").Value!;
        DocumentoExigidoBaseLegal basePendente = DocumentoExigidoBaseLegal.Criar(
            "Cláusula 5.2 do edital", TipoAbrangencia.InternaEdital, StatusBaseLegal.Pendente, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [baseResolvida, basePendente], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        DocumentoExigidoDto projetado = dto!.DocumentosExigidos.Should().ContainSingle().Which;
        projetado.BasesLegais.Should().HaveCount(2);

        BaseLegalDto resolvidaDto = projetado.BasesLegais.Should().ContainSingle(b => b.Status == "RESOLVIDO").Which;
        resolvidaDto.Referencia.Should().Be("Lei 12.711/2012, art. 3º");
        resolvidaDto.Abrangencia.Should().Be("FEDERAL");
        resolvidaDto.Observacao.Should().Be("Observação");

        BaseLegalDto pendenteDto = projetado.BasesLegais.Should().ContainSingle(b => b.Status == "PENDENTE").Which;
        pendenteDto.Abrangencia.Should().Be("INTERNA_EDITAL");
        pendenteDto.Observacao.Should().BeNull();
    }

    [Fact(DisplayName = "Story #554/issue #893 (PR #900): projeta IdadeMaximaEmissao/TamanhoMaximoBytes — round-trip GET→PUT")]
    public async Task Handle_IdadeFormatoTamanho_EmiteTokensDeWire()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // DATA_SUBMISSAO — não exige fase âncora (nem extremo), diferente de INICIO_FASE/
        // FIM_FASE; FaseQualquer() não declara Fim, então uma âncora de fase aqui
        // reprovaria por IdadeMaximaEmissao.FaseExtremoAusente (coberto em teste próprio).
        IdadeMaximaEmissao idade = IdadeMaximaEmissao.Criar(
            90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.DataSubmissao, null, null).Value!;
        FormatosPermitidos formatosPermitidos = FormatosPermitidos.Criar(
            qualquer: false, entradas: [("PDF", null)]).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "COMPROVANTE_RESIDENCIA", "Comprovante de residência", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [],
            idadeMaximaEmissao: idade, formatosPermitidos: formatosPermitidos, tamanhoMaximoBytes: 5_000_000).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        DocumentoExigidoDto projetado = dto!.DocumentosExigidos.Should().ContainSingle().Which;
        projetado.FormatosPermitidos.ValueKind.Should().Be(JsonValueKind.Array);
        projetado.FormatosPermitidos.EnumerateArray().Should().ContainSingle(
            e => e.GetProperty("formato").GetString() == "PDF" && e.GetProperty("tamanhoMaximoBytesMax").ValueKind == JsonValueKind.Null);
        projetado.TamanhoMaximoBytes.Should().Be(5_000_000);
        projetado.IdadeMaximaEmissao.Should().NotBeNull();
        projetado.IdadeMaximaEmissao!.Valor.Should().Be(90);
        projetado.IdadeMaximaEmissao.Unidade.Should().Be("DIAS");
        projetado.IdadeMaximaEmissao.ReferenciaTipo.Should().Be("DATA_SUBMISSAO");
        projetado.IdadeMaximaEmissao.ReferenciaFaseId.Should().BeNull();
    }

    [Fact(DisplayName = "Sem idade/tamanho configurados (FormatosPermitidos=QUALQUER), a projeção emite QUALQUER e o resto null (contraprova)")]
    public async Task Handle_SemIdadeFormatoTamanho_ProjetaNulo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        DocumentoExigidoDto projetado = dto!.DocumentosExigidos.Should().ContainSingle().Which;
        projetado.FormatosPermitidos.ValueKind.Should().Be(JsonValueKind.String);
        projetado.FormatosPermitidos.GetString().Should().Be("QUALQUER");
        projetado.TamanhoMaximoBytes.Should().BeNull();
        projetado.IdadeMaximaEmissao.Should().BeNull();
    }
}
