namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As regras de um dia não útil declarado, exercitadas pela inclusão pontual, que rotula as recusas com o campo "item".
/// </summary>
public sealed class ConferenciaDoDiaNaoUtilTests
{
    private static readonly DateOnly Data = new(2027, 5, 1);

    private static IReadOnlyList<FieldError> Recusas(DiaNaoUtilCriacao? item)
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar(
            "2027.1", [new DiaNaoUtilCriacao("NACIONAL", null, null, null, Data.AddDays(30), "Dia base")]).ValorEsperado();

        return calendario.IncluirDiaNaoUtil(item).Errors;
    }

    private static void DeveRecusarSomente(IReadOnlyList<FieldError> recusas, string campo, string codigo)
    {
        recusas.Should().ContainSingle();
        recusas[0].Field.Should().Be(campo);
        recusas[0].Error.Code.Should().Be(codigo);
    }

    [Fact(DisplayName = "Data omitida (valor default) é recusada no campo item.data")]
    public void Conferir_DataVazia_RecusaNoCampoData()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("NACIONAL", null, null, null, default, "Dia sem data"));

        DeveRecusarSomente(recusas, "item.data", CalendarioDiasUteisErrorCodes.DataAusente);
    }

    [Fact(DisplayName = "Abrangência municipal sem município é recusada no campo item.municipioIbge")]
    public void Conferir_MunicipalSemMunicipio_RecusaNoCampoDoMunicipio()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("MUNICIPAL", null, "Belém", "PA", Data, "Aniversário do município"));

        DeveRecusarSomente(recusas, "item.municipioIbge", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio);
    }

    [Fact(DisplayName = "Nome do município ausente é recusado no campo item.municipioNome")]
    public void Conferir_MunicipalSemNomeDoMunicipio_RecusaNoCampoDoNome()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("MUNICIPAL", "1501402", null, "PA", Data, "Aniversário do município"));

        DeveRecusarSomente(recusas, "item.municipioNome", CidadeReferenciaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "UF do município incoerente com o código IBGE é recusada no campo item.municipioUf")]
    public void Conferir_MunicipalComUfIncoerente_RecusaNoCampoDaUfDoMunicipio()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("MUNICIPAL", "1501402", "Belém", "SP", Data, "Aniversário do município"));

        DeveRecusarSomente(recusas, "item.municipioUf", CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Município informado fora da abrangência municipal é recusado no próprio item")]
    public void Conferir_MunicipioEmAbrangenciaNacional_RecusaNoItem()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("NACIONAL", "1501402", null, null, Data, "Feriado nacional"));

        DeveRecusarSomente(recusas, "item", CalendarioDiasUteisErrorCodes.SnapshotMunicipalApenasParaMunicipal);
    }

    [Fact(DisplayName = "Abrangência estadual sem UF é recusada no campo item.uf")]
    public void Conferir_EstadualSemUf_RecusaNoCampoUf()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("ESTADUAL", null, null, null, Data, "Data magna do estado"));

        DeveRecusarSomente(recusas, "item.uf", CalendarioDiasUteisErrorCodes.UfObrigatoriaParaEstadual);
    }

    [Fact(DisplayName = "UF fora das 27 siglas é recusada no campo item.uf")]
    public void Conferir_EstadualComUfDesconhecida_RecusaNoCampoUf()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("ESTADUAL", null, null, null, Data, "Data magna do estado", "ZZ"));

        DeveRecusarSomente(recusas, "item.uf", CalendarioDiasUteisErrorCodes.UfFormatoInvalido);
    }

    [Fact(DisplayName = "UF informada fora da abrangência estadual é recusada no campo item.uf")]
    public void Conferir_UfEmAbrangenciaInstitucional_RecusaNoCampoUf()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("INSTITUCIONAL", null, null, null, Data, "Recesso", "PA"));

        DeveRecusarSomente(recusas, "item.uf", CalendarioDiasUteisErrorCodes.UfApenasParaEstadual);
    }

    [Fact(DisplayName = "Descrição só com espaços é recusada no campo item.descricao")]
    public void Conferir_DescricaoEmBranco_RecusaNoCampoDescricao()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("NACIONAL", null, null, null, Data, "   "));

        DeveRecusarSomente(recusas, "item.descricao", CalendarioDiasUteisErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "Descrição com 200 caracteres é aceita e com 201 é recusada")]
    public void Conferir_DescricaoNoLimite_AceitaAte200()
    {
        Recusas(new("NACIONAL", null, null, null, Data, new string('A', 200))).Should().BeEmpty();

        DeveRecusarSomente(
            Recusas(new("NACIONAL", null, null, null, Data, new string('A', 201))),
            "item.descricao",
            CalendarioDiasUteisErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Abrangência inválida não gera recusa de município nem de UF derivada")]
    public void Conferir_AbrangenciaInvalidaComMunicipioEUf_RecusaSoAAbrangencia()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("INVALIDA", "1501402", "Belém", "PA", Data, "Dia qualquer", "PA"));

        DeveRecusarSomente(recusas, "item.abrangencia", CalendarioDiasUteisErrorCodes.AbrangenciaInvalida);
    }

    [Fact(DisplayName = "Data vazia, estadual sem UF e descrição em branco acumulam as três recusas, nessa ordem")]
    public void Conferir_VariasRecusas_AcumulaNaOrdemDasRegras()
    {
        IReadOnlyList<FieldError> recusas = Recusas(new("ESTADUAL", null, null, null, default, " "));

        recusas.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("item.data", CalendarioDiasUteisErrorCodes.DataAusente),
            ("item.uf", CalendarioDiasUteisErrorCodes.UfObrigatoriaParaEstadual),
            ("item.descricao", CalendarioDiasUteisErrorCodes.DescricaoObrigatoria));
    }
}
