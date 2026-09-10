namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Garante o contrato do vocabulário <see cref="TipoInstrumentoNormativoCodigo"/>:
/// seis tipos canônicos, sentinela excluída, round-trip ToCodigo/FromCodigo estável,
/// Descritos derivado do enum (não lista escrita à mão) e Descricao não-vazia em todos.
/// </summary>
public sealed class TipoInstrumentoNormativoCodigoTests
{
    [Theory(DisplayName = "ToCodigo converte cada membro canônico para o código UPPER_SNAKE esperado")]
    [InlineData(TipoInstrumentoNormativo.Lei, "LEI")]
    [InlineData(TipoInstrumentoNormativo.Decreto, "DECRETO")]
    [InlineData(TipoInstrumentoNormativo.Portaria, "PORTARIA")]
    [InlineData(TipoInstrumentoNormativo.Resolucao, "RESOLUCAO")]
    [InlineData(TipoInstrumentoNormativo.InstrucaoNormativa, "INSTRUCAO_NORMATIVA")]
    [InlineData(TipoInstrumentoNormativo.Parecer, "PARECER")]
    public void ToCodigo_MembroCanonico_DevolveCodigoCorreto(TipoInstrumentoNormativo tipo, string codigoEsperado)
    {
        tipo.ToCodigo().Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "ToCodigo lança ArgumentOutOfRangeException para o sentinela Nenhum")]
    public void ToCodigo_Nenhum_Lanca()
    {
        Action ato = static () => TipoInstrumentoNormativo.Nenhum.ToCodigo();
        ato.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory(DisplayName = "FromCodigo round-trip: ToCodigo -> FromCodigo devolve o mesmo membro")]
    [InlineData(TipoInstrumentoNormativo.Lei)]
    [InlineData(TipoInstrumentoNormativo.Decreto)]
    [InlineData(TipoInstrumentoNormativo.Portaria)]
    [InlineData(TipoInstrumentoNormativo.Resolucao)]
    [InlineData(TipoInstrumentoNormativo.InstrucaoNormativa)]
    [InlineData(TipoInstrumentoNormativo.Parecer)]
    public void FromCodigo_RoundTrip_Estavel(TipoInstrumentoNormativo tipo)
    {
        TipoInstrumentoNormativoCodigo.FromCodigo(tipo.ToCodigo()).Should().Be(tipo);
    }

    [Theory(DisplayName = "FromCodigo devolve Nenhum para código desconhecido, vazio ou nulo")]
    [InlineData("MEDIDA_PROVISORIA")]
    [InlineData("lei")]
    [InlineData("")]
    [InlineData(null)]
    public void FromCodigo_CodigoDesconhecido_DevolveNenhum(string? codigo)
    {
        TipoInstrumentoNormativoCodigo.FromCodigo(codigo).Should().Be(TipoInstrumentoNormativo.Nenhum);
    }

    [Fact(DisplayName = "Descritos expõe exatamente os seis tipos canônicos, na ordem de declaração do enum, sem o sentinela Nenhum")]
    public void Descritos_ExpoeSeisTipos_SemNenhum()
    {
        TipoInstrumentoNormativoCodigo.Descritos.Select(d => d.Codigo).Should().Equal(
            "LEI",
            "DECRETO",
            "PORTARIA",
            "RESOLUCAO",
            "INSTRUCAO_NORMATIVA",
            "PARECER");
    }

    [Fact(DisplayName = "Descritos: todos os itens têm Nome e Descricao não-vazios")]
    public void Descritos_TodosItens_NomeEDescricaoNaoVazios()
    {
        TipoInstrumentoNormativoCodigo.Descritos.Should().AllSatisfy(d =>
        {
            d.Nome.Should().NotBeNullOrWhiteSpace();
            d.Descricao.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact(DisplayName = "Codigos é derivado de Descritos — sem diferença por item adicional ou faltante")]
    public void Codigos_DerivadoDeDescritos()
    {
        TipoInstrumentoNormativoCodigo.Codigos.Should()
            .Equal(TipoInstrumentoNormativoCodigo.Descritos.Select(static d => d.Codigo));
    }

    [Fact(DisplayName = "CodigosEmTexto contém todos os seis códigos separados por vírgula")]
    public void CodigosEmTexto_ContemTodosOsCodigos()
    {
        string[] codigos = ["LEI", "DECRETO", "PORTARIA", "RESOLUCAO", "INSTRUCAO_NORMATIVA", "PARECER"];

        foreach (string codigo in codigos)
        {
            TipoInstrumentoNormativoCodigo.CodigosEmTexto.Should().Contain(codigo);
        }
    }
}
