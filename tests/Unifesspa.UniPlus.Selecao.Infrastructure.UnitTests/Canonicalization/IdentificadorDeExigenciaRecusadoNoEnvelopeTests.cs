namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;

public sealed class IdentificadorDeExigenciaRecusadoNoEnvelopeTests
{
    [Fact]
    public void Ler_IdentificadorDeExigenciaRepetido_NaoRepeteValor()
    {
        const string valor = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        JsonObject documento = Documento(valor);
        LeitorEnvelope leitor = new();
        LeituraDoEnvelope.Ler(leitor, documento, false);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be("'documentosExigidos.exigencias': um identificador de exigência aparece mais de uma vez.").And.NotContain(valor);
        causa.Field.Should().BeNull();
    }

    private static JsonObject Documento(string valor)
    {
        EntradaCanonicalizacao entrada = ExemploDeConfiguracao.Entrada();
        entrada.Processo.DefinirTaxaInscricao(ConfiguracaoTaxaInscricao.Criar(false, null, null).Value!).IsSuccess.Should().BeTrue();
        SnapshotCanonico snapshot = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        JsonObject documento = JsonNode.Parse(snapshot.Bytes)!.AsObject();
        JsonObject primeira = Exigencia(valor);
        JsonObject segunda = primeira.DeepClone().AsObject();
        segunda["chave"] = "exigencia_segunda";
        documento["documentosExigidos"]!["exigencias"] = new JsonArray(primeira, segunda);
        return documento;
    }

    private static JsonObject Exigencia(string valor)
    {
        JsonObject exigencia = JsonNode.Parse("""
            {"chave":"exigencia_primeira","exigenciaId":"11111111-1111-4111-8111-111111111111",
             "tipoDocumentoOrigemId":"22222222-2222-4222-8222-222222222222","tipoDocumentoCodigo":"IDENTIDADE",
             "tipoDocumentoNome":"Identidade","tipoDocumentoCategoria":"PESSOAL","classificacaoProtecao":"PESSOAL",
             "finalidadeTratamento":"Comprovação","hipoteseLegal":"Obrigação legal","dadosComprovados":[],
             "exigidoNaFaseId":"33333333-3333-4333-8333-333333333333","exigidoNaEtapaId":null,
             "finalidade":null,"aplicabilidade":"GERAL","obrigatorio":true,"abrePrazoParaReenvio":false,"consequenciaDoDescumprimento":null,
             "condicoes":null,"basesLegais":[],"prazoDeEmissao":null,"validade":null,
             "formatosPermitidos":{"qualquer":true,"lista":null},"tamanhoMaximoBytes":null,"modelo":null}
            """)!.AsObject();
        exigencia["exigenciaId"] = valor;
        return exigencia;
    }
}
