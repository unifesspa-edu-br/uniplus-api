namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDaIdentidadeNoEnvelopeTests
{
    [Fact]
    public void LerIdentificadorLegivel_FormatoInvalido_ApontaIdentificador()
    {
        const string valor = "VALOR_FICTICIO_X";
        JsonObject documento = new() { ["identificadorLegivel"] = valor };
        LeitorEnvelope leitor = new();
        LeituraDoEdital.LerIdentificadorLegivel(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Single();
        DomainError erro = IdentificadorLegivel.Criar(valor).Error!;
        ConferirCausaNoDocumento.Conferir(documento, causa, "identificadorLegivel", erro.Code, erro.Message);
        causa.Field.Should().NotContain(valor);
        causa.Error.Message.Should().NotContain(valor);
    }

    [Fact]
    public void LerIdentidadesUnidade_CodigoENomeInvalidos_PropagaAmbasNoItem()
    {
        JsonObject documento = JsonNode.Parse("""
            {"identidadesUnidade":{"administradora":{"origemId":"11111111-1111-1111-1111-111111111111",
            "sigla":"UNI","codigo":"UNI","identificadorNoEnderecoWeb":"uni","nome":"Unidade","tipo":"UNIDADE",
            "cidadeCodigoIbge":"123","cidadeNome":" ","cidadeUf":"PA"}}}
            """)!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDoEdital.LerIdentidadesUnidade(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "identidadesUnidade.administradora", "CidadeReferencia.NomeObrigatorio", "Nome da cidade é obrigatório.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "identidadesUnidade.administradora", "CidadeReferencia.CodigoIbgeFormatoInvalido", "Código IBGE da cidade deve ter exatamente 7 dígitos numéricos.");
    }
    [Fact]
    public void LerIdentidadesUnidade_CodigoInvalido_ApontaAdministradora()
    {
        JsonObject documento = JsonNode.Parse("""{"identidadesUnidade":{"administradora":{"origemId":"11111111-1111-1111-1111-111111111111","sigla":"UNI","codigo":"UNI","identificadorNoEnderecoWeb":"uni","nome":"Unidade","tipo":"UNIDADE","cidadeCodigoIbge":"123","cidadeNome":"Cidade","cidadeUf":"PA"}}}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDoEdital.LerIdentidadesUnidade(leitor, documento);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "identidadesUnidade.administradora", "CidadeReferencia.CodigoIbgeFormatoInvalido", "Código IBGE da cidade deve ter exatamente 7 dígitos numéricos.");
    }

}
