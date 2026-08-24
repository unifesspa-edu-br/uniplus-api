namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A tradução da leitura do módulo Configuração para o snapshot que a raiz consome e o envelope
/// congela.
/// </summary>
/// <remarks>
/// O ponto sensível é a UF: a view a carrega em dois campos mutuamente exclusivos — um para o
/// município, outro para o estado —, e o snapshot tem um só. Escolher pelo campo errado congelaria
/// um dia estadual sem UF, ou um municipal com a UF vazia, e a recusa só apareceria na
/// decodificação, quando não houvesse mais como corrigir a versão.
/// </remarks>
public sealed class LeituraDoCalendarioVigenteTests
{
    private static readonly Guid OrigemId = Guid.Parse("01930000-0000-7000-8000-000000000001");

    private static CalendarioVigenteView View(params DiaNaoUtilView[] dias) => new(OrigemId, "2026", dias);

    [Fact(DisplayName = "Sem dataset vigente, a tradução devolve ausência — não é falha")]
    public void SemVigente_DevolveNulo()
    {
        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull(
            "quem decide se a ausência impede a operação é o gate da raiz, e só quando há contagem sobre dia útil");
    }

    [Fact(DisplayName = "A UF do município e a do estado colapsam no mesmo campo, escolhidas pela abrangência")]
    public void Uf_EscolhidaPelaAbrangencia()
    {
        CalendarioVigenteView vigente = View(
            new DiaNaoUtilView(new DateOnly(2026, 4, 5), "MUNICIPAL", "1504208", "Marabá", "PA", null),
            new DiaNaoUtilView(new DateOnly(2026, 8, 15), "ESTADUAL", null, null, null, "PA"));

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        IReadOnlyList<DiaNaoUtilCongelado> dias = resultado.Value!.DiasNaoUteis;

        dias.Should().ContainSingle(static d => d.Abrangencia == "MUNICIPAL")
            .Which.Should().Match<DiaNaoUtilCongelado>(d => d.Uf == "PA" && d.MunicipioIbge == "1504208",
                "no dia municipal a UF vem de MunicipioUf");

        dias.Should().ContainSingle(static d => d.Abrangencia == "ESTADUAL")
            .Which.Should().Match<DiaNaoUtilCongelado>(d => d.Uf == "PA" && d.MunicipioIbge == null,
                "no dia estadual a UF vem de Uf, e não há município");
    }

    [Fact(DisplayName = "Dias nacionais e institucionais não carregam recorte territorial")]
    public void SemRecorte_NaoCarregaTerritorio()
    {
        CalendarioVigenteView vigente = View(
            new DiaNaoUtilView(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null, null),
            new DiaNaoUtilView(new DateOnly(2026, 10, 28), "INSTITUCIONAL", null, null, null, null));

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.DiasNaoUteis.Should().OnlyContain(
            d => d.MunicipioIbge == null && d.MunicipioNome == null && d.Uf == null);
    }

    [Fact(DisplayName = "A mesma data com abrangências diferentes são dois fatos, não duplicata")]
    public void MesmaDataAbrangenciasDiferentes_NaoEDuplicata()
    {
        // 20/11 é feriado nacional (Consciência Negra) e, em Marabá, também o do padroeiro.
        // São fundamentos distintos: apagar um porque o outro já torna o dia não útil apagaria
        // a base legal que o sustenta.
        CalendarioVigenteView vigente = View(
            new DiaNaoUtilView(new DateOnly(2026, 11, 20), "NACIONAL", null, null, null, null),
            new DiaNaoUtilView(new DateOnly(2026, 11, 20), "MUNICIPAL", "1504208", "Marabá", "PA", null));

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.DiasNaoUteis.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Dia repetido com a mesma chave é recusado, não deduplicado em silêncio")]
    public void DiaRepetido_Recusado()
    {
        CalendarioVigenteView vigente = View(
            new DiaNaoUtilView(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null, null),
            new DiaNaoUtilView(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null, null));

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.IsFailure.Should().BeTrue(
            "dois registros idênticos indicam cadastro inconsistente; absorvê-los esconderia o defeito num artefato imutável");
        resultado.Error!.Code.Should().Be("CalendarioDiasUteisCongelado.DiaDuplicado");
    }

    [Fact(DisplayName = "Município cujo código IBGE não corresponde à UF é recusado pela verificação do cadastro de cidade")]
    public void IbgeIncoerenteComUf_Recusado()
    {
        // 1504208 é Marabá/PA — o prefixo 15 designa o Pará. Declarar SP é incoerência que a
        // mesma verificação usada por Campus e LocalOferta já nomeia.
        CalendarioVigenteView vigente = View(
            new DiaNaoUtilView(new DateOnly(2026, 4, 5), "MUNICIPAL", "1504208", "Marabá", "SP", null));

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CidadeReferencia.UfIncoerente",
            "a coerência território-UF tem uma única fonte no Kernel, e não uma tabela paralela aqui");
    }

    [Fact(DisplayName = "A ordem canônica é do snapshot, não da ordem em que o reader devolveu")]
    public void OrdemCanonica_NaoDependeDaOrdemDoReader()
    {
        CalendarioVigenteView vigente = View(
            new DiaNaoUtilView(new DateOnly(2026, 10, 28), "INSTITUCIONAL", null, null, null, null),
            new DiaNaoUtilView(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null, null),
            new DiaNaoUtilView(new DateOnly(2026, 8, 15), "ESTADUAL", null, null, null, "PA"));

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.Value!.DiasNaoUteis.Select(static d => d.Data).Should().BeInAscendingOrder(
            "o envelope é comparado byte a byte — uma ordem que variasse com o plano de execução do " +
            "Postgres faria a mesma configuração produzir bytes diferentes");
    }

    [Fact(DisplayName = "Dia com data omitida é recusado antes de ser congelado")]
    public void DataDefault_Recusada()
    {
        // O decoder do envelope recusa o default de DateOnly. Sem esta guarda, uma data omitida
        // no cadastro seria congelada numa versão publicada que ninguém conseguiria reidratar —
        // e com ela morreriam a restauração e a retificação daquele certame, para sempre.
        CalendarioVigenteView vigente = View(
            new DiaNaoUtilView(default, "NACIONAL", null, null, null, null));

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.IsFailure.Should().BeTrue(
            "congelar o default produziria uma versão publicada impossível de reidratar");
        resultado.Error!.Code.Should().Be("DiaNaoUtilCongelado.DataAusente");
    }

    [Fact(DisplayName = "Dataset vigente sem nenhum dia não útil é recusado — um calendário vazio não conta nada")]
    public void DatasetVazio_Recusado()
    {
        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(View());

        resultado.IsFailure.Should().BeTrue(
            "congelar a lista vazia faria a versão publicada afirmar que todo dia é útil, de forma imutável");
        resultado.Error!.Code.Should().Be("CalendarioDiasUteisCongelado.SemDiaNaoUtil",
            "é a mesma cardinalidade que o cadastro de origem exige na criação do dataset");
    }

    [Theory(DisplayName = "A versão do dataset respeita o limite do cadastro de origem")]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void VersaoDataset_RespeitaLimiteDoCadastro(int comprimento, bool aceita)
    {
        var vigente = new CalendarioVigenteView(
            OrigemId,
            new string('A', comprimento),
            [new DiaNaoUtilView(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null, null)]);

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.IsSuccess.Should().Be(aceita,
            "aceitar mais que o cadastro grava deixaria o decoder reidratar um calendário que não pode ter vindo dele");
    }

    [Theory(DisplayName = "Campo territorial que a abrangência proíbe é recusado, não descartado em silêncio")]
    // Municipal com UF estadual concorrente: escolher MunicipioUf e ignorar Uf apagaria a contradição.
    [InlineData("MUNICIPAL", "1504208", "Marabá", "PA", "SP")]
    // Estadual com município: o dia não tem recorte municipal nenhum.
    [InlineData("ESTADUAL", "1504208", "Marabá", "PA", "PA")]
    // Nacional e institucional incidem em todo lugar.
    [InlineData("NACIONAL", null, null, null, "PA")]
    [InlineData("INSTITUCIONAL", "1504208", "Marabá", "PA", null)]
    public void FormaTerritorialIncoerente_Recusada(
        string abrangencia, string? ibge, string? nome, string? municipioUf, string? uf)
    {
        CalendarioVigenteView vigente = View(
            new DiaNaoUtilView(new DateOnly(2026, 1, 1), abrangencia, ibge, nome, municipioUf, uf));

        Result<CalendarioDiasUteisCongelado?> resultado = LeituraDoCalendarioVigente.Traduzir(vigente);

        resultado.IsFailure.Should().BeTrue(
            "o campo proibido sumiria sem registro, e a versão publicada afirmaria um território que o cadastro não declarou");
        resultado.Error!.Code.Should().Be("CalendarioVigente.FormaTerritorialInvalida");
    }
}
