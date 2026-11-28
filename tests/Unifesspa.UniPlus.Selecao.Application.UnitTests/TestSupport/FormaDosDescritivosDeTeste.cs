namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

/// <summary>
/// A forma do documento nos pontos em que a exportação do rascunho acrescenta descritivos dos cadastros: cada código com o
/// descritivo ao lado, só de leitura, que o esquema declara. Com <c>somenteLeitura</c> falso, o esquema não os declara só de leitura.
/// </summary>
internal static class FormaDosDescritivosDeTeste
{
    public static FormaDoNo Documento(bool somenteLeitura = true) => FormaDeTeste.Objeto(new()
    {
        ["cronograma"] = FormaDeTeste.Objeto(new() { ["fases"] = FormaDeTeste.Lista(Fase(somenteLeitura)) }),
        ["etapas"] = FormaDeTeste.Lista(Etapa(somenteLeitura)),
        ["distribuicaoVagas"] = FormaDeTeste.Lista(Distribuicao(somenteLeitura)),
        ["atendimento"] = Atendimento(somenteLeitura),
        ["fada"] = Fada(somenteLeitura),
    });

    private static FormaDoNo Fase(bool somenteLeitura) => FormaDeTeste.Objeto(new()
    {
        ["chave"] = FormaDeTeste.Valor(),
        ["tipoFaseCodigo"] = FormaDeTeste.Valor(),
        ["tipoFaseNome"] = FormaDeTeste.Valor(somenteLeitura),
        ["publicacoesPrevistas"] = FormaDeTeste.Lista(Publicacao(somenteLeitura)),
        ["bancasRequeridas"] = FormaDeTeste.Lista(BancaDaFase(somenteLeitura)),
    });

    private static FormaDoNo Etapa(bool somenteLeitura) => FormaDeTeste.Objeto(new()
    {
        ["chave"] = FormaDeTeste.Valor(),
        ["tipoEtapaCodigo"] = FormaDeTeste.Valor(),
        ["tipoEtapaNome"] = FormaDeTeste.Valor(somenteLeitura),
        ["publicacoesPrevistas"] = FormaDeTeste.Lista(Publicacao(somenteLeitura)),
        ["bancas"] = FormaDeTeste.Lista(FormaDeTeste.Objeto(PropriedadesDaBanca(somenteLeitura))),
    });

    private static FormaDoNo Publicacao(bool somenteLeitura) => FormaDeTeste.Objeto(new()
    {
        ["atoCodigo"] = FormaDeTeste.Valor(),
        ["atoNome"] = FormaDeTeste.Valor(somenteLeitura),
    });

    private static Dictionary<string, FormaDoNo> PropriedadesDaBanca(bool somenteLeitura) => new()
    {
        ["chave"] = FormaDeTeste.Valor(),
        ["tipoBancaCodigo"] = FormaDeTeste.Valor(),
        ["tipoBancaNome"] = FormaDeTeste.Valor(somenteLeitura),
    };

    private static FormaDoNo BancaDaFase(bool somenteLeitura)
    {
        Dictionary<string, FormaDoNo> propriedades = PropriedadesDaBanca(somenteLeitura);
        propriedades["categoriasDocumentoCodigos"] = FormaDeTeste.Lista(FormaDeTeste.Valor());
        propriedades["categoriasDocumentoNomes"] = FormaDeTeste.Lista(FormaDeTeste.Valor(), somenteLeitura);
        return FormaDeTeste.Objeto(propriedades);
    }

    private static FormaDoNo Distribuicao(bool somenteLeitura) => FormaDeTeste.Objeto(new()
    {
        ["ofertaCursoCodigo"] = FormaDeTeste.Valor(),
        ["cursoNome"] = FormaDeTeste.Valor(somenteLeitura),
        ["cursoGrau"] = FormaDeTeste.Valor(somenteLeitura),
        ["unidadeOfertanteNome"] = FormaDeTeste.Valor(somenteLeitura),
        ["turnos"] = FormaDeTeste.Lista(FormaDeTeste.Valor(), somenteLeitura),
        ["modalidades"] = FormaDeTeste.Lista(FormaDeTeste.Objeto(new()
        {
            ["codigo"] = FormaDeTeste.Valor(),
            ["nome"] = FormaDeTeste.Valor(somenteLeitura),
            ["descricao"] = FormaDeTeste.Valor(somenteLeitura),
        })),
        ["quadro"] = Quadro(somenteLeitura),
        ["totalDeVagasDoEdital"] = FormaDeTeste.Valor(somenteLeitura, "integer"),
    });

    private static FormaDoNo Quadro(bool somenteLeitura) => FormaDeTeste.Lista(FormaDeTeste.Objeto(new()
    {
        ["modalidadeCodigo"] = FormaDeTeste.Valor(),
        ["quantidade"] = FormaDeTeste.Valor(false, "integer"),
    }), somenteLeitura);

    private static FormaDoNo Atendimento(bool somenteLeitura) => FormaDeTeste.Objeto(new()
    {
        ["condicoes"] = FormaDeTeste.Lista(Item("condicaoCodigo", "condicaoNome", somenteLeitura)),
        ["recursos"] = FormaDeTeste.Lista(Item("recursoCodigo", "recursoNome", somenteLeitura)),
        ["tiposDeficiencia"] = FormaDeTeste.Lista(Item("tipoDeficienciaCodigo", "tipoDeficienciaNome", somenteLeitura)),
    });

    private static FormaDoNo Item(string codigo, string nome, bool somenteLeitura) => FormaDeTeste.Objeto(new()
    {
        [codigo] = FormaDeTeste.Valor(),
        [nome] = FormaDeTeste.Valor(somenteLeitura),
    });

    private static FormaDoNo Fada(bool somenteLeitura) => FormaDeTeste.Objeto(new()
    {
        ["baseLegalCodigo"] = FormaDeTeste.Valor(),
        ["tipoInstrumento"] = FormaDeTeste.Valor(somenteLeitura),
        ["identificacao"] = FormaDeTeste.Valor(somenteLeitura),
        ["descricao"] = FormaDeTeste.Valor(somenteLeitura),
        ["municipios"] = FormaDeTeste.Lista(FormaDeTeste.Objeto(new()
        {
            ["codigoIbge"] = FormaDeTeste.Valor(),
            ["nome"] = FormaDeTeste.Valor(),
            ["uf"] = FormaDeTeste.Valor(),
        }), somenteLeitura),
    });
}
