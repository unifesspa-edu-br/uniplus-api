namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDaProjecaoDosDocumentosExigidos;

/// <summary>
/// Cobertura de <see cref="ProjecaoDoProcessoSeletivo.Projetar"/> para
/// <c>DocumentosExigidos</c>: a projeção precisa emitir o mesmo
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

        projetado.Aplicabilidade.Tipo.Codigo.Should().Be(tokenEsperado);
        projetado.TipoDocumento.Id.Should().Be(tipoDocumentoOrigemId);
    }

    [Fact(DisplayName = "Projeta Condicoes do gatilho DNF — round-trip GET→PUT sem perda")]
    public async Task Handle_CondicaoGatilho_EmiteTokenDeWireERoundTripDoValor()
    {
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(ExigenciaCondicionalComCondicaoEscalarEMultivalorada());

        projetado.Aplicabilidade.Condicoes.Should().HaveCount(2);

        CondicaoGatilhoDto escalarDto = projetado.Aplicabilidade.Condicoes.Should().ContainSingle(c => c.Fato == "SEXO").Which;
        escalarDto.Operador.Should().Be("IGUAL");
        escalarDto.Valor.Should().Be("\"MASCULINO\"", "GetRawText preserva o JSON canônico — o mesmo PUT reinterpreta como JSON válido");

        CondicaoGatilhoDto emDto = projetado.Aplicabilidade.Condicoes.Should().ContainSingle(c => c.Fato == "MODALIDADE").Which;
        emDto.Operador.Should().Be("ALGUM_DE");
        JsonDocument.Parse(emDto.Valor).RootElement.EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["LB_PPI", "AC"]);
    }

    [Fact(DisplayName = "Projeta ReferenciaTemporalFatos no agregado GET — round-trip GET→PUT")]
    public async Task Handle_ReferenciaTemporalFatos_EmiteTokenDeWire()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        processo.DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, fase.Id).Value!).IsSuccess.Should().BeTrue();

        ProcessoSeletivoDto dto = await ProjetarAsync(processo);

        dto.ReferenciaTemporalFatos.Should().NotBeNull();
        dto.ReferenciaTemporalFatos!.Tipo.Codigo.Should().Be("FIM_FASE");
        dto.ReferenciaTemporalFatos.FaseId.Should().Be(fase.Id);
        dto.ReferenciaTemporalFatos.Data.Should().BeNull();
    }

    [Fact(DisplayName = "ReferenciaTemporalFatos ausente projeta null no agregado GET (contraprova)")]
    public async Task Handle_SemReferenciaTemporalFatos_ProjetaNulo()
    {
        ProcessoSeletivoDto dto = await ProjetarAsync(NovoProcesso());

        dto.ReferenciaTemporalFatos.Should().BeNull();
    }

    [Fact(DisplayName = "Projeta BasesLegais — round-trip GET→PUT, inclusive PENDENTE (a projeção 'só RESOLVIDO' fica em outra consulta, não neste DTO de edição)")]
    public async Task Handle_BasesLegais_EmiteTokenDeWireERoundTrip()
    {
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(ExigenciaComUmaBaseLegalResolvidaEUmaPendente());

        projetado.Indeferimento.BasesLegais.Should().HaveCount(2);

        BaseLegalDto resolvidaDto = projetado.Indeferimento.BasesLegais.Should().ContainSingle(b => b.Situacao.Codigo == "RESOLVIDO").Which;
        resolvidaDto.Referencia.Should().Be("Lei 12.711/2012, art. 3º");
        resolvidaDto.Abrangencia.Codigo.Should().Be("FEDERAL");
        resolvidaDto.Observacao.Should().Be("Observação");

        BaseLegalDto pendenteDto = projetado.Indeferimento.BasesLegais.Should().ContainSingle(b => b.Situacao.Codigo == "PENDENTE").Which;
        pendenteDto.Abrangencia.Codigo.Should().Be("INTERNA_EDITAL");
        pendenteDto.Observacao.Should().BeNull();
    }

    [Fact(DisplayName = "Projeta os formatos permitidos e o tamanho máximo no mesmo valor que o PUT aceita de volta")]
    public async Task Handle_FormatoTamanho_EmiteValorDeWire()
    {
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(ExigenciaComPrazoValidadeFormatoETamanho());

        projetado.Requisitos.FormatosPermitidos.ValueKind.Should().Be(JsonValueKind.Array);
        projetado.Requisitos.FormatosPermitidos.EnumerateArray().Should().ContainSingle(
            e => e.GetProperty("formato").GetProperty("codigo").GetString() == "PDF" && e.GetProperty("tamanhoMaximoBytesMax").ValueKind == JsonValueKind.Null);
        projetado.Requisitos.TamanhoMaximoBytes.Should().Be(5_000_000);
    }

    [Fact(DisplayName = "Projeta o prazo de emissão com código e rótulo de cada enum")]
    public async Task Handle_PrazoDeEmissao_EmiteCodigoERotulo()
    {
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(ExigenciaComPrazoValidadeFormatoETamanho());

        PrazoDeEmissaoDto prazo = projetado.Requisitos.PrazoDeEmissao!;
        prazo.Escolha.Should().Match<CodigoComRotulo<EscolhaDoPrazoDeEmissao>>(e => e.Codigo == "COM_PRAZO" && e.Rotulo == "Com prazo");
        prazo.Valor.Should().Be(90);
        prazo.Unidade!.Codigo.Should().Be("DIAS");
        prazo.Referencia!.Tipo.Codigo.Should().Be("DATA_DA_APRESENTACAO");
        prazo.Referencia.FaseId.Should().BeNull();
        prazo.EfeitoForaDoPrazo!.Codigo.Should().Be("RECUSAR_DOCUMENTO");
    }

    [Fact(DisplayName = "Projeta a validade com código e rótulo de cada enum")]
    public async Task Handle_Validade_EmiteCodigoERotulo()
    {
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(ExigenciaComPrazoValidadeFormatoETamanho());

        ValidadeDoDocumentoDto validade = projetado.Requisitos.Validade!;
        validade.Escolha.Codigo.Should().Be("EXIGE_DOCUMENTO_VALIDO");
        validade.Referencia!.Tipo.Codigo.Should().Be("FIM_DAS_INSCRICOES");
        validade.EfeitoDoVencimento!.Codigo.Should().Be("ACEITAR_COM_AVISO");
    }

    [Fact(DisplayName = "Sem prazo e sem exigência de validade, a projeção traz só a escolha e deixa os detalhes nulos")]
    public async Task Handle_SemPrazoNaoExigeValidade_ProjetaApenasAEscolha()
    {
        ExigenciaParaProjetar exigencia = new()
        {
            PrazoDeEmissao = new PrazoDeEmissaoBuilder().SemPrazo().Construir(),
            Validade = new ValidadeDoDocumentoBuilder().NaoExige().Construir(),
        };

        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(exigencia);

        projetado.Requisitos.PrazoDeEmissao!.Escolha.Codigo.Should().Be("SEM_PRAZO");
        projetado.Requisitos.PrazoDeEmissao.Valor.Should().BeNull();
        projetado.Requisitos.PrazoDeEmissao.Referencia.Should().BeNull();
        projetado.Requisitos.Validade!.Escolha.Codigo.Should().Be("NAO_EXIGE_DOCUMENTO_VALIDO");
        projetado.Requisitos.Validade.Referencia.Should().BeNull();
    }

    [Fact(DisplayName = "Sem a escolha do prazo e da validade (formatos QUALQUER), a projeção emite QUALQUER e o resto nulo")]
    public async Task Handle_SemEscolhaNemTamanho_ProjetaNulo()
    {
        DocumentoExigidoDto projetado = await ProjetarAUnicaExigenciaAsync(new ExigenciaParaProjetar());

        projetado.Requisitos.FormatosPermitidos.ValueKind.Should().Be(JsonValueKind.String);
        projetado.Requisitos.FormatosPermitidos.GetString().Should().Be("QUALQUER");
        projetado.Requisitos.TamanhoMaximoBytes.Should().BeNull();
        projetado.Requisitos.PrazoDeEmissao.Should().BeNull();
        projetado.Requisitos.Validade.Should().BeNull();
    }
}
