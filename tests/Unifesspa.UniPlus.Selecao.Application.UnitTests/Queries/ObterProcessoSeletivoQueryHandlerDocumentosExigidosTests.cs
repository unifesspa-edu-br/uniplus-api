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
/// <c>DocumentosExigidos</c> (Story #554, PR-a): a projeção precisa emitir o mesmo
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
            condicoes: []).Value!;
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
            condicoes: [condicaoEscalar, condicaoMultivalorada]).Value!;
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
}
