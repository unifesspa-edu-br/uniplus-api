namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// O que a publicação e a retificação do edital recusam quando o pedido não traz o documento do edital, o bloco do ato,
/// o órgão, a série, o ano, a data, o assinante, o tipo ou o documento de um anexo: um item por campo, no campo do pedido, com o código de campo obrigatório.
/// </summary>
public sealed class AusenciasNaDeclaracaoDoEditalTests
{
    private static readonly Guid Documento = Guid.CreateVersion7();

    [Fact(DisplayName = "O pedido completo não tem ausência")]
    public void PedidoCompleto_SemAusencia() =>
        AusenciasNaDeclaracaoDoEdital.Listar(Documento, NovoAto(), [new(Guid.CreateVersion7(), "Quadro")]).Should().BeEmpty();

    [Fact(DisplayName = "O documento do edital ausente é recusado no campo do pedido")]
    public void DocumentoAusente_RecusaODocumento() =>
        Campos(AusenciasNaDeclaracaoDoEdital.Listar(null, NovoAto(), null)).Should().Equal("documentoEditalId");

    [Fact(DisplayName = "O bloco do ato ausente é recusado uma vez, sem cobrar o ano e a data que dependem dele")]
    public void AtoAusente_RecusaSoOBloco() =>
        Campos(AusenciasNaDeclaracaoDoEdital.Listar(Documento, null, null)).Should().Equal("ato");

    [Fact(DisplayName = "O ano e a data ausentes saem juntos, na ordem do bloco")]
    public void AnoEDataAusentes_SaemJuntos() =>
        Campos(AusenciasNaDeclaracaoDoEdital.Listar(Documento, NovoAto() with { Ano = null, DataDoAto = null }, null))
            .Should().Equal("ato.ano", "ato.dataDoAto");

    [Theory(DisplayName = "O órgão, a série, o assinante e o tipo ausentes, nulos ou em branco são recusados cada um no seu campo")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TextoAusenteOuEmBranco_RecusaCadaCampo(string? texto)
    {
        IReadOnlyList<FieldError> ausencias = AusenciasNaDeclaracaoDoEdital.Listar(
            Documento, NovoAto() with { Orgao = texto, Serie = texto, Assinante = texto, TipoAtoCodigo = texto }, null);

        Campos(ausencias).Should().Equal("ato.orgao", "ato.serie", "ato.assinante", "ato.tipoAtoCodigo");
        ausencias.Should().OnlyContain(static e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "O texto ausente acumula com o ano e a data ausentes, na ordem do bloco, e nenhuma mensagem repete o valor")]
    public void TextoEAnoAusentes_AcumulamNaOrdemDoBloco()
    {
        IReadOnlyList<FieldError> ausencias = AusenciasNaDeclaracaoDoEdital.Listar(
            Documento, NovoAto() with { Orgao = null, Ano = null, Assinante = "  " }, null);

        Campos(ausencias).Should().Equal("ato.orgao", "ato.ano", "ato.assinante");
    }

    [Fact(DisplayName = "Cada anexo sem documento é recusado na sua posição, e o que tem documento não")]
    public void AnexoSemDocumento_RecusaNaPosicao() =>
        Campos(AusenciasNaDeclaracaoDoEdital.Listar(
            Documento, NovoAto(), [new(null, "A"), new(Guid.CreateVersion7(), "B"), new(null, "C")]))
            .Should().Equal("anexos[0].documentoId", "anexos[2].documentoId");

    [Fact(DisplayName = "Todos os campos ausentes acumulam na ordem do pedido, todos com o código de campo obrigatório")]
    public void TudoAusente_AcumulaNaOrdemDoPedido()
    {
        IReadOnlyList<FieldError> ausencias = AusenciasNaDeclaracaoDoEdital.Listar(null, null, [new(null, null)]);

        Campos(ausencias).Should().Equal("documentoEditalId", "ato", "anexos[0].documentoId");
        ausencias.Should().OnlyContain(static e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "Os dados do ato ficam sem lacuna quando o ano e a data vieram")]
    public void Completar_ComAnoEData_DevolveOsValores()
    {
        DadosDoAtoCompletos? completos = AusenciasNaDeclaracaoDoEdital.Completar(NovoAto());

        completos.Should().NotBeNull();
        completos!.Ano.Should().Be(2026);
        completos.DataDoAto.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Theory(DisplayName = "Texto ausente ou em branco no bloco do ato não deixa dados completos")]
    [InlineData(null)]
    [InlineData("  ")]
    public void Completar_ComTextoAusente_DevolveNulo(string? texto)
    {
        AusenciasNaDeclaracaoDoEdital.Completar(NovoAto() with { Orgao = texto }).Should().BeNull();
        AusenciasNaDeclaracaoDoEdital.Completar(NovoAto() with { Serie = texto }).Should().BeNull();
        AusenciasNaDeclaracaoDoEdital.Completar(NovoAto() with { Assinante = texto }).Should().BeNull();
        AusenciasNaDeclaracaoDoEdital.Completar(NovoAto() with { TipoAtoCodigo = texto }).Should().BeNull();
    }

    [Theory(DisplayName = "Sem o bloco, o ano ou a data não há dados completos")]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Completar_ComLacuna_DevolveNulo(bool temBloco, bool semAno, bool semData)
    {
        DadosDoAto? ato = temBloco ? NovoAto() with { Ano = semAno ? null : 2026, DataDoAto = semData ? null : new DateOnly(2026, 1, 1) } : null;

        AusenciasNaDeclaracaoDoEdital.Completar(ato).Should().BeNull();
    }

    private static string?[] Campos(IReadOnlyList<FieldError> erros) => [.. erros.Select(static e => e.Field)];
}
