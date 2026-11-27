namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

internal static class ApoioDoEnvelopeCodecTipos
{
    internal static void Definir(JsonObject raiz, string caminho, string valor)
    {
        string[] partes = caminho.Split('.');
        JsonNode atual = raiz;

        for (int i = 0; i < partes.Length - 1; i++)
        {
            atual = int.TryParse(partes[i], System.Globalization.CultureInfo.InvariantCulture, out int indice)
                ? atual.AsArray()[indice]!
                : atual.AsObject()[partes[i]]!;
        }

        atual.AsObject()[partes[^1]] = valor;
    }

    internal static Result<EnvelopeReidratado> Reidratar(Action<JsonObject> adulterar)
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico(comBancasExigidasNaPublicacao: true);
        byte[] originais = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo)).Bytes;
        ExemploDeProcessoSeletivoEntrada.Publicar(processo);

        JsonObject envelope = JsonNode.Parse(Encoding.UTF8.GetString(originais))!.AsObject();
        adulterar(envelope);

        PerfilCanonicoV1.Instancia.Serializar(envelope).Should().NotEqual(originais, "pré-condição: a adulteração tem de mudar os bytes");
        // Os rótulos acompanham o código escrito; a adulteração troca só o código, e a leitura decide por ele.
        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(ContextoDosRotulos.SemContexto(envelope).AsObject());

        VersaoConfiguracao versao = ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(processo, adulterados);
        return ExemploDeProcessoSeletivo.Registro.Reidratar(versao);
    }
}
