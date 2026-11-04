namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Leitura, no envelope congelado, das ofertas de curso, das opções de curso e do quadro de vagas.
/// </summary>
internal static class LeituraDoCertameVagas
{
    internal static bool TentarOfertas(JsonObject envelope, [NotNullWhen(true)] out List<OfertaDeCursoCertameDto>? ofertas)
    {
        ofertas = null;
        if (!PrimitivasDaProjecao.TentarArray(envelope, ProjecaoDoCertamePublicado.BlocoPublico("ofertas"), out JsonArray? array))
        {
            return false;
        }

        List<OfertaDeCursoCertameDto> lidas = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject oferta
                || !PrimitivasDaProjecao.TentarIdentificador(oferta, "ofertaCursoOrigemId", out Guid id)
                || !PrimitivasDaProjecao.TentarTexto(oferta, "cursoNome", out string cursoNome)
                || !PrimitivasDaProjecao.TentarTexto(oferta, "cursoGrau", out string cursoGrau)
                || !PrimitivasDaProjecao.TentarTexto(oferta, "unidadeOfertanteNome", out string unidade)
                || !PrimitivasDaProjecao.TentarTextos(oferta, "turnos", out List<string>? turnos))
            {
                return false;
            }

            lidas.Add(new OfertaDeCursoCertameDto(id, cursoNome, cursoGrau, unidade, turnos));
        }

        ofertas = lidas;
        return true;
    }

    internal static bool TentarOpcoesDeCurso(JsonObject envelope, [NotNullWhen(true)] out OpcoesDeCursoCertameDto? opcoesDeCurso)
    {
        opcoesDeCurso = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("opcoesDeCurso"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarInteiro(bloco, "numero", out int numero)
            || numero is < 1 or > ProcessoSeletivo.MaximoDeOpcoesDeCurso)
        {
            return false;
        }

        opcoesDeCurso = new OpcoesDeCursoCertameDto(numero);
        return true;
    }

    internal static bool TentarVagas(JsonObject envelope, [NotNullWhen(true)] out List<QuadroDeVagasCertameDto>? vagas)
    {
        vagas = null;
        return PrimitivasDaProjecao.TentarArray(envelope, ProjecaoDoCertamePublicado.BlocoPublico("vagas"), out JsonArray? array)
            && PrimitivasDaProjecao.TentarItens<QuadroDeVagasCertameDto>(array, TentarQuadro, out vagas);
    }

    private static bool TentarQuadro(JsonNode? item, [NotNullWhen(true)] out QuadroDeVagasCertameDto? quadro)
    {
        quadro = null;
        if (item is not JsonObject configuracao
            || !PrimitivasDaProjecao.TentarIdentificador(configuracao, "ofertaCursoOrigemId", out Guid ofertaId)
            || !PrimitivasDaProjecao.TentarInteiro(configuracao, "totalPublicado", out int total)
            || !PrimitivasDaProjecao.TentarArray(configuracao, "quadro", out JsonArray? quadroArray)
            || !PrimitivasDaProjecao.TentarItens<VagaPorModalidadeCertameDto>(quadroArray, TentarVagaPorModalidade, out List<VagaPorModalidadeCertameDto>? linhas))
        {
            return false;
        }

        quadro = new QuadroDeVagasCertameDto(ofertaId, linhas, total);
        return true;
    }

    private static bool TentarVagaPorModalidade(JsonNode? linha, [NotNullWhen(true)] out VagaPorModalidadeCertameDto? vaga)
    {
        vaga = null;
        if (linha is not JsonObject objeto
            || !PrimitivasDaProjecao.TentarTexto(objeto, "modalidadeCodigo", out string modalidadeCodigo)
            || !PrimitivasDaProjecao.TentarInteiro(objeto, "quantidade", out int quantidade))
        {
            return false;
        }

        vaga = new VagaPorModalidadeCertameDto(modalidadeCodigo, quantidade);
        return true;
    }
}
