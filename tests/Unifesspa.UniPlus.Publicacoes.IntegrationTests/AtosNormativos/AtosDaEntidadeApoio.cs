namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static AtoNormativoApoio;

/// <summary>
/// Tipos de ato, atos registrados pelo domínio, payloads e a leitura da consulta por entidade compartilhados
/// pelas classes de teste de <c>GET /entidades/{entidadeTipo}/{entidadeId}/atos</c> e do registro com vínculos.
/// </summary>
internal sealed class AtosDaEntidadeApoio(PublicacoesEndpointFixture fixture)
{
    internal const string ObjetoA = "OBJETO_A";

    private static readonly AtoNormativoPublicado AtoDoDominio = new()
    {
        AtoId = Guid.Empty,
        Orgao = "CEPS",
        Serie = "EDITAL",
        Ano = 2026,
        Numero = "13",
        TipoCodigo = "EDITAL_ABERTURA",
        AtributosDoTipo = new AtributosDoTipoAto(true, false, false, "Tipo de teste"),
        DataDoAto = new DateOnly(2026, 3, 13),
        PublicadoEm = new DateTimeOffset(2026, 3, 13, 15, 0, 0, TimeSpan.Zero),
        DocumentoHash = HashValido,
        Assinante = PersonasDeTeste.NomeKevinCarvalho,
        Vinculos = [],
    };

    internal static object Vinculo(Guid entidadeId, string entidadeTipo = ObjetoA) =>
        new { entidadeTipo, entidadeId };

    internal static object PayloadAto(
        string tipoCodigo, string numero, string dataDoAto, IReadOnlyList<object> vinculos) =>
        new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            numero,
            tipoCodigo,
            dataDoAto,
            documentoHash = HashValido,
            assinante = PersonasDeTeste.NomeKevinCarvalho,
            vinculos,
        };

    internal static object PayloadRetificacao(
        string tipoCodigo,
        string numero,
        string dataDoAto,
        Guid atoRetificadoId,
        string motivo,
        IReadOnlyList<object> vinculos) =>
        new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            numero,
            tipoCodigo,
            dataDoAto,
            documentoHash = HashValido,
            assinante = PersonasDeTeste.NomeKevinCarvalho,
            atoRetificadoId,
            motivoRetificacao = motivo,
            vinculos,
        };

    internal static async Task<JsonElement[]> ListarDaEntidadeAsync(HttpClient client, string tipo, Guid id)
    {
        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/publicacoes/entidades/{tipo}/{id}/atos", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    internal static async Task<Guid[]> ListarIdsDaEntidadeAsync(HttpClient client, string tipo, Guid id) =>
        [.. (await ListarDaEntidadeAsync(client, tipo, id)).Select(i => i.GetProperty("id").GetGuid())];

    internal static string ExtrairCursorDoLinkNext(HttpResponseMessage resposta)
    {
        string proxima = LinkDe(resposta, "next")
            ?? throw new InvalidOperationException("A resposta não trouxe link 'next'.");

        string query = new Uri(proxima, UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(proxima).Query
            : proxima[proxima.IndexOf('?', StringComparison.Ordinal)..];

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query)["cursor"]!;
    }

    internal static async Task AtualizarTipoParaUnicoPorObjetoAsync(HttpClient client, Guid id, string codigo)
    {
        CorpoDoTipoDeAto corpo = new(codigo) { Id = id, Nome = "Tipo de teste", UnicoPorObjeto = true };

        HttpResponseMessage resposta = await TipoAtoPublicadoApoio.AtualizarAsync(client, id, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    internal async Task<string> CriarTipoAsync(bool unicoPorObjeto) =>
        (await CriarTipoComIdAsync(unicoPorObjeto)).Codigo;

    /// <remarks>O sufixo mapeia os hexadígitos para letras, mantendo a unicidade e o formato válido entre os testes.</remarks>
    internal async Task<(string Codigo, Guid Id)> CriarTipoComIdAsync(bool unicoPorObjeto)
    {
        string codigo = "TIPO_" + Sufixo();
        using HttpClient client = fixture.Factory.CreateClient();

        Guid id = await TipoAtoPublicadoApoio.CadastrarAsync(
            client, new CorpoDoTipoDeAto(codigo) { Nome = "Tipo de teste", UnicoPorObjeto = unicoPorObjeto });

        return (codigo, id);
    }

    /// <summary>
    /// Registra o ato como o domínio dono do objeto faz: pela porta de escrita, que é o único caminho que admite o
    /// vínculo com o processo seletivo, e com a gravação única que o domínio faz depois dela.
    /// </summary>
    internal Task<Guid> RegistrarPeloDominioAsync(string tipoCodigo, string numero, Guid processo) =>
        RegistrarPeloDominioAsync(tipoCodigo, numero, processo, Guid.CreateVersion7(), AtoDoDominio.PublicadoEm);

    /// <summary>O mesmo registro, com o identificador do ato e o instante da divulgação escolhidos pelo teste.</summary>
    internal async Task<Guid> RegistrarPeloDominioAsync(
        string tipoCodigo, string numero, Guid processo, Guid atoId, DateTimeOffset publicadoEm)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();

        Result registrado = await escopo.ServiceProvider.GetRequiredService<IRegistroDeAtoNormativo>()
            .RegistrarAsync(AtoDoProcessoSeletivo(atoId, tipoCodigo, numero, processo, publicadoEm), CancellationToken.None);
        registrado.IsSuccess.Should().BeTrue(registrado.Error?.Message);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync(CancellationToken.None);

        return atoId;
    }

    private static AtoNormativoPublicado AtoDoProcessoSeletivo(
        Guid atoId, string tipoCodigo, string numero, Guid processo, DateTimeOffset publicadoEm) =>
        AtoDoDominio with
        {
            AtoId = atoId,
            PublicadoEm = publicadoEm,
            Numero = numero,
            TipoCodigo = tipoCodigo,
            Vinculos = [new VinculoDoAtoPublicado(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, processo)],
        };
}
