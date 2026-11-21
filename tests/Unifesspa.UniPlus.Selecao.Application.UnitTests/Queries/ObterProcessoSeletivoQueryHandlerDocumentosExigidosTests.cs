namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDaProjecaoDosDocumentosExigidos;

/// <summary>
/// Cobertura de <see cref="ProjecaoDoProcessoSeletivo.Projetar"/> para
/// <c>DocumentosExigidos</c> (Story #554, PR #895): a projeção precisa emitir o mesmo
/// token de wire que <c>DefinirDocumentosExigidosCommandValidator</c> aceita — round-trip
/// GET→PUT direto, sem transformação do cliente.
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerDocumentosExigidosTests
{
    [Theory(DisplayName = "Projeta aplicabilidade no mesmo token de wire aceito pelo validator (GERAL/CONDICIONAL)")]
    [InlineData(Aplicabilidade.Geral, "GERAL")]
    [InlineData(Aplicabilidade.Condicional, "CONDICIONAL")]
    public async Task Handle_Aplicabilidade_EmiteTokenDeWire(Aplicabilidade aplicabilidade, string tokenEsperado)
    {
        Guid tipoDocumentoOrigemId = Guid.CreateVersion7();

        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(
            new ExigenciaParaProjetar { TipoDocumentoOrigemId = tipoDocumentoOrigemId, Aplicabilidade = aplicabilidade });

        projetado.Aplicabilidade.Should().Be(tokenEsperado);
        projetado.TipoDocumentoOrigemId.Should().Be(tipoDocumentoOrigemId);
    }

    [Fact(DisplayName = "Projeta Condicoes do gatilho DNF (issue #892) — round-trip GET→PUT sem perda")]
    public async Task Handle_CondicaoGatilho_EmiteTokenDeWireERoundTripDoValor()
    {
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(ExigenciaCondicionalComCondicaoEscalarEMultivalorada());

        projetado.Condicoes.Should().HaveCount(2);

        CondicaoGatilhoDto escalarDto = projetado.Condicoes.Should().ContainSingle(c => c.Fato == "SEXO").Which;
        escalarDto.Operador.Should().Be("IGUAL");
        escalarDto.Valor.Should().Be("\"MASCULINO\"", "GetRawText preserva o JSON canônico — o mesmo PUT reinterpreta como JSON válido");

        CondicaoGatilhoDto emDto = projetado.Condicoes.Should().ContainSingle(c => c.Fato == "MODALIDADE").Which;
        emDto.Operador.Should().Be("EM");
        JsonDocument.Parse(emDto.Valor).RootElement.EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["LB_PPI", "AC"]);
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
    public async Task Handle_SemReferenciaTemporalFatos_ProjetaNulo()
    {
        ProcessoSeletivoDto dto = await ProjetarAsync(NovoProcesso());

        dto.ReferenciaTemporalFatos.Should().BeNull();
    }

    [Fact(DisplayName = "Story #554/issue #549 (PR #898): projeta BasesLegais — round-trip GET→PUT, inclusive PENDENTE (a projeção 'só RESOLVIDO' é da PR #903, não deste DTO de edição)")]
    public async Task Handle_BasesLegais_EmiteTokenDeWireERoundTrip()
    {
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(ExigenciaComUmaBaseLegalResolvidaEUmaPendente());

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
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(ExigenciaComIdadeFormatoETamanho());

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
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(new ExigenciaParaProjetar());

        projetado.FormatosPermitidos.ValueKind.Should().Be(JsonValueKind.String);
        projetado.FormatosPermitidos.GetString().Should().Be("QUALQUER");
        projetado.TamanhoMaximoBytes.Should().BeNull();
        projetado.IdadeMaximaEmissao.Should().BeNull();
    }
}
