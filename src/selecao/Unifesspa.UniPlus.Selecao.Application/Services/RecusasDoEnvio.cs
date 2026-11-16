namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;
using DTOs.Comprovantes;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As recusas do envio, acumuladas numa resposta só: com recusa, 422 com todas; sem recusa e com o Geo fora do ar em alguma
/// conferência, indisponível. Com o Geo fora do ar na conferência do endereço, a recusa que só existe porque um derivado de
/// residência ficou indeterminado não conta, porque com o Geo de volta ela pode deixar de valer: a modalidade que falta
/// responder e deriva desses derivados é uma delas, e a que não concorre a nenhuma modalidade está decidida e conta.
/// </summary>
public static class RecusasDoEnvio
{
    public static Result Conferir(PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(avaliado);

        List<FieldError> recusas = [.. Reunir(pedido, avaliado).Distinct()];
        DomainError? geoIndisponivel = avaliado.DoEndereco.GeoIndisponivel ?? avaliado.DosMunicipios.GeoIndisponivel;
        return recusas.Count > 0
            ? Result.ValidationFailure(recusas)
            : geoIndisponivel is null ? Result.Success() : Result.Failure(geoIndisponivel);
    }

    private static IEnumerable<FieldError> Reunir(PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado) =>
    [
        .. pedido.Respostas.Recusas,
        .. avaliado.DoEndereco.Recusas,
        .. DoAgregado(pedido, avaliado),
        .. DasOpcoes(pedido, avaliado),
        .. DoVeredito(pedido, avaliado),
        .. avaliado.DosMunicipios.Recusas,
    ];

    // As recusas do agregado são do envio e não dependem do Geo.
    private static IEnumerable<FieldError> DoAgregado(PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado) =>
        pedido.Inscricao.RecusasDoEnvio(
            pedido.Corpo.Respostas, pedido.Corpo.Grupos, pedido.Corpo.Aceites, pedido.AdmitidasDoCandidato,
            pedido.Request.OfertaCursoId, pedido.Request.OfertaCursoSegundaOpcaoId, avaliado.Derivados.Valores,
            DerivadosDoSistemaInscricao.DefinicoesDasRespostas, avaliado.Avaliacao, avaliado.Arquivos.EmVerificacao());

    // O avaliador descarta em silêncio a resposta cujo código não pertence à oferta vigente: sem a checagem sobre as respostas
    // BRUTAS, uma opção fora da oferta num campo opcional passaria sem recusa.
    private static IEnumerable<FieldError> DasOpcoes(PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado) =>
    [
        .. Inscricao.RecusasDasModalidades(
            DependenciaDoGeo(pedido, avaliado)?.SemAsModalidadesDoGeo(
                avaliado.Avaliacao, pedido.Contexto.ConfiguracaoDaPrimeiraOpcao, pedido.Contexto.ConfiguracaoDaSegundaOpcao) ?? avaliado.Avaliacao,
            pedido.Request.OfertaCursoId, pedido.Request.OfertaCursoSegundaOpcaoId, pedido.AdmitidasDoCandidato),
        .. VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(RespostasFinais(pedido, avaliado), pedido.Respostas.GruposMesclados, pedido.Contexto.Ofertas),
    ];

    private static IEnumerable<FieldError> DoVeredito(PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado)
    {
        bool comRecusaDoNomeSocial = Inscricao.RecusasDoNomeSocial(pedido.Respostas.Mescladas, pedido.AdmitidasDoCandidato, aoEnviar: true).Any();
        HashSet<string> camposComRecusaDoGeo = [.. avaliado.DosMunicipios.Recusas.Select(static r => r.Field).OfType<string>()];
        return VeredictoDeSubmissao.AvaliarDistinguindoOGeo(
                AvaliacaoDaInscricao.DaInscricao(avaliado.Avaliacao.Formulario), [.. pedido.Contexto.TermosExigidos], pedido.Corpo.Aceites,
                avaliado.Envio.Dia, DependenciaDoGeo(pedido, avaliado))
            .Where(static r => !r.DependeDoGeo)
            .Select(static r => r.Recusa)
            .Where(e => !comRecusaDoNomeSocial || !string.Equals(e.Field, ConjuntoBasicoDaInscricao.NomeSocial, StringComparison.Ordinal))
            .Where(e => e.Field is null || !camposComRecusaDoGeo.Contains(e.Field));
    }

    private static DependenciaDoGeo? DependenciaDoGeo(PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado) =>
        avaliado.DoEndereco.GeoIndisponivel is null
            ? null
            : new DependenciaDoGeo(
                pedido.Contexto.Configuracao.Definicao, avaliado.Derivados.Indeterminados,
                CaminhosRespondidos(pedido.Respostas.Mescladas, pedido.Respostas.GruposMesclados));

    private static Dictionary<string, JsonElement> RespostasFinais(PedidoDoEnvio pedido, AvaliadoDoEnvio avaliado)
    {
        Dictionary<string, JsonElement> finais = new(pedido.Respostas.Mescladas, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in avaliado.Derivados.Valores)
        {
            finais[codigo] = valor;
        }

        return finais;
    }

    /// <summary>Os caminhos dos campos que o candidato respondeu: <c>FATO</c> no nível superior e <c>GRUPO.ocorrencia.FATO</c> nos grupos.</summary>
    private static HashSet<string> CaminhosRespondidos(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) =>
    [
        .. respostas.Where(static r => !RespostaDeCampo.EstaVazia(r.Value)).Select(static r => r.Key),
        .. grupos.SelectMany(static g => g.Value.SelectMany(o => o.Respostas
            .Where(static r => !RespostaDeCampo.EstaVazia(r.Value))
            .Select(r => $"{g.Key}.{o.Id}.{r.Key}"))),
    ];
}
