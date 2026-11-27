namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static LeituraDoJsonDoDocumento;
using static PartesComunsDoDocumentoLidas;

/// <summary>
/// Copia o documento que passou pela forma para as declarações do domínio: a identificação, as fases, o cadastro de reserva, a
/// manifestação de interesse, a prova presencial, as etapas e a classificação, cada parte de lista com a posição que tinha na lista enviada e cada código de catálogo como foi escrito. O esquema garante o tipo de cada valor presente,
/// então aqui só se converte; o que falta fica ausente ou no valor de partida do tipo, e quem o confere é o domínio. Não decide regra.
/// </summary>
internal static class DeclaracoesDoDocumentoLidas
{
    internal static DeclaracoesDoDocumento De(JsonObject documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return new(
            Identificacao(Objeto(documento, "identificacao")),
            [.. Itens(Objeto(documento, "cronograma"), "fases").Select(static item => Fase(item.Posicao, item.Item))])
        {
            CadastroDeReserva = [.. Itens(documento, "cadastroDeReserva").Select(static item => CadastroDeReserva(item.Item))],
            ManifestacaoDeInteresse = Texto(documento, "manifestacaoDeInteresse"),
            ProvaPresencial = ProvaPresencial(Objeto(documento, "provaPresencial")),
            Etapas = EtapasDoDocumentoLidas.De(documento["etapas"]),
            Classificacao = ClassificacaoDoDocumentoLida.De(documento["classificacao"]),
        };
    }

    // O código da participação segue como texto até o domínio: o desconhecido não vira ausente, porque ausente é a chamada sem opção.
    private static CadastroDeReservaInformado CadastroDeReserva(JsonObject chamada) => new(
        Valor<int>(chamada, "numeroDaChamada"),
        Valor<int>(chamada, "pessoasDaReservaPorVaga"),
        Texto(chamada, "participacaoDaReserva"));

    // O nome do município é só de leitura e fica de fora; o código e a UF seguem como foram escritos, e o domínio os confere.
    private static ProvaPresencialNoDocumento? ProvaPresencial(JsonObject? bloco) => bloco is null
        ? null
        : new(
            Valor<bool>(bloco, "temProvaPresencial") ?? false,
            [.. Itens(bloco, "municipios").Select(static item => new MunicipioDeProvaInformado(Texto(item.Item, "codigoIbge"), Texto(item.Item, "uf")))]);

    private static IdentificacaoNoDocumento Identificacao(JsonObject? bloco) => new(
        Texto(bloco, "nome"),
        Texto(bloco, "tipoProcessoCodigo"),
        Texto(bloco, "unidadeAdministradoraCodigo"),
        CodigoDoCatalogo<OrigemCandidatos>(Texto(bloco, "origemCandidatos")),
        Texto(bloco, "localidadeCodigoIbge"),
        Texto(bloco, "identificadorLegivel"),
        Referencia(Objeto(bloco, "algoritmoContagemPrazo")));

    private static FaseNoDocumento Fase(int posicao, JsonObject fase) => new(
        posicao,
        Texto(fase, "chave")!,
        new(Texto(fase, "tipoFaseCodigo")!, Texto(fase, "unidadeResponsavelCodigo")),
        new(Valor<DateTimeOffset>(fase, "inicio"), Valor<DateTimeOffset>(fase, "fim")),
        Publicacoes(fase),
        [.. Itens(fase, "bancasRequeridas").Select(static item => Banca(item.Posicao, item.Item))],
        Objeto(fase, "regraRecurso") is { } regraDeRecurso ? RegraDeRecurso(regraDeRecurso) : null)
    {
        NoMesmoMomentoDaAnterior = Valor<bool>(fase, "noMesmoMomentoDaAnterior") ?? false,
    };

    private static PublicacoesNoDocumento Publicacoes(JsonObject fase) => new(
        [.. Itens(fase, "publicacoesPrevistas").Select(static item => Produto(item.Posicao, item.Item))],
        Valor<bool>(fase, "emiteParecerIndividual") ?? false,
        TiposDeDecisao(fase),
        RegraDeHomologacao(Objeto(fase, "regraDeHomologacao")));

    private static RegraDeHomologacaoNoDocumento? RegraDeHomologacao(JsonObject? regra) => regra is null
        ? null
        : new(
            Texto(regra, "resultadoAtoCodigo"),
            [.. Itens(regra, "alternativas").Select(static item => Alternativa(item.Item))]);

    private static AlternativaNoDocumento Alternativa(JsonObject alternativa) =>
        new([.. Itens(alternativa, "condicoes").Select(static item => Condicao(item.Item))]);

    private static CondicaoNoDocumento Condicao(JsonObject condicao) => new(
        TipoDaCondicao(Texto(condicao, "tipo")),
        Valor<int>(condicao, "edicaoDoExame"),
        Texto(condicao, "motivoDesfavoravelCodigo"),
        condicao["enquadramento"] is { } enquadramento ? JsonSerializer.SerializeToElement(enquadramento) : null);

    // O esquema fecha o tipo em uma enumeração; o código que o domínio não conhece fica ausente, e o domínio o recusa.
    private static TipoDeCondicaoDeHomologacao? TipoDaCondicao(string? codigo) =>
        CatalogoDe<TipoDeCondicaoDeHomologacao>.TentarObter(codigo, out TipoDeCondicaoDeHomologacao tipo) ? tipo : null;
}
