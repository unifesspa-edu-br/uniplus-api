namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Collections.Frozen;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>
/// Escreve, ao lado de cada código de enum do documento, o rótulo que o esquema declara só para leitura, com o texto atual do
/// catálogo do sistema. O código que o catálogo não conhece fica sem rótulo, e uma lista só recebe os rótulos quando todos os
/// seus códigos têm um.
/// </summary>
public static class RotulosDosCodigosDeContrato
{
    private const string Bancas = "bancas.schema.json#/$defs/regraDeDeliberacao/properties/";
    private const string Comuns = "comuns.schema.json#/$defs/";
    private const string Recurso = Comuns + "propriedadesDoRecurso/properties/";
    private const string Condicoes = "condicoes.schema.json#/$defs/";
    private const string Etapas = "etapas.schema.json#/";
    private const string Exigencias = "exigencias.schema.json#/$defs/";
    private const string Formulario = "formulario.schema.json#/";
    private const string RegrasDeComprovacao = "regras-de-comprovacao.schema.json#/properties/";

    /// <summary>Os campos de código do esquema que têm rótulo, um por trecho do esquema que declara o campo.</summary>
    public static IReadOnlyList<CodigoDeContratoNoEsquema> Campos { get; } =
    [
        CodigoDeContratoNoEsquema.De<ModoDeDeliberacao>(Bancas + "modo"),
        CodigoDeContratoNoEsquema.De<CriterioDeVotacao>(Bancas + "criterio"),
        CodigoDeContratoNoEsquema.De<DesempateDaVotacao>(Bancas + "desempate"),
        CodigoDeContratoNoEsquema.De<EncerramentoDaVotacao>(Bancas + "encerramento"),
        CodigoDeContratoNoEsquema.De<ResultadoSemUnanimidade>(Bancas + "semUnanimidade"),
        CodigoDeContratoNoEsquema.De<ReferenciaTipo>("coleta.schema.json#/properties/referenciaTemporalDosDados/properties/tipo"),
        CodigoDeContratoNoEsquema.De<PapelDaPublicacaoPrevista>(Comuns + "publicacaoPrevista/properties/papel"),
        CodigoDeContratoNoEsquema.De<UnidadePrazo>(Recurso + "prazoUnidade"),
        CodigoDeContratoNoEsquema.De<UnidadePrazo>(Recurso + "prazoSegundaInstanciaUnidade"),
        CodigoDeContratoNoEsquema.De<EfeitoSuspensivo>(Recurso + "efeitoSuspensivo"),
        CodigoDeContratoNoEsquema.De<GuardaDaVaga>(Recurso + "guardaDaVaga"),
        CodigoDeContratoNoEsquema.De<TipoMotivoDeDecisao>(Comuns + "tipoDeDecisao"),
        CodigoDeContratoNoEsquema.De<Operador>(Condicoes + "condicao/properties/operador"),
        CodigoDeContratoNoEsquema.De<TipoObrigatoriedade>(Condicoes + "obrigatoriedade/properties/tipo"),
        CodigoDeContratoNoEsquema.De<TipoAbrangencia>(Condicoes + "baseLegal/properties/abrangencia"),
        CodigoDeContratoNoEsquema.De<StatusBaseLegal>(Condicoes + "baseLegal/properties/situacao"),
        CodigoDeContratoNoEsquema.De<CaraterEtapa>(Etapas + "properties/carater"),
        CodigoDeContratoNoEsquema.De<FormaDeAvaliacao>(Etapas + "properties/formaDeAvaliacao"),
        CodigoDeContratoNoEsquema.De<EventoInicialDoPrazo>(Etapas + "$defs/recursoDaEtapa/properties/eventoInicialDoPrazo"),
        CodigoDeContratoNoEsquema.De<FinalidadeFormulario>(Exigencias + "exigencia/properties/finalidade"),
        CodigoDeContratoNoEsquema.De<Aplicabilidade>(Exigencias + "exigencia/properties/aplicabilidade"),
        CodigoDeContratoNoEsquema.De<ConsequenciaDoDescumprimento>(Exigencias + "exigencia/properties/consequenciaDoDescumprimento"),
        CodigoDeContratoNoEsquema.De<ConsequenciaDoDescumprimento>(RegrasDeComprovacao + "consequenciaDoDescumprimento"),
        CodigoDeContratoNoEsquema.De<ConsequenciaDoDescumprimento>("bancas.schema.json#/properties/consequenciaDaAusencia"),
        CodigoDeContratoNoEsquema.De<ConsequenciaDoDescumprimento>(Formulario + "properties/consequenciaDaFaltaDeEnvio"),
        CodigoDeContratoNoEsquema.De<FormatoDeDocumento>(Exigencias + "exigencia/properties/modelo/properties/formato"),
        CodigoDeContratoNoEsquema.De<FormatoPermitido>(Exigencias + "formatosPermitidos/properties/lista/items/properties/formato"),
        CodigoDeContratoNoEsquema.De<EscolhaDoPrazoDeEmissao>(Exigencias + "prazoDeEmissao/properties/escolha"),
        CodigoDeContratoNoEsquema.De<UnidadeDoPrazoDeEmissao>(Exigencias + "prazoDeEmissao/properties/unidade"),
        CodigoDeContratoNoEsquema.De<EfeitoNaAnaliseDoDocumento>(Exigencias + "prazoDeEmissao/properties/efeitoForaDoPrazo"),
        CodigoDeContratoNoEsquema.De<EscolhaDaValidade>(Exigencias + "validade/properties/escolha"),
        CodigoDeContratoNoEsquema.De<EfeitoNaAnaliseDoDocumento>(Exigencias + "validade/properties/efeitoDoVencimento"),
        CodigoDeContratoNoEsquema.De<TipoDaDataDeReferencia>(Exigencias + "dataDeReferenciaDoDocumento/properties/tipo"),
        CodigoDeContratoNoEsquema.De<FinalidadeFormulario>(Formulario + "properties/finalidade"),
        CodigoDeContratoNoEsquema.De<TipoPaginaFormulario>(Formulario + "$defs/pagina/properties/tipo"),
        CodigoDeContratoNoEsquema.De<BlocoSistema>(Formulario + "$defs/pagina/properties/bloco"),
        CodigoDeContratoNoEsquema.De<TipoDeCondicaoDeHomologacao>("homologacao.schema.json#/$defs/condicao/properties/tipo"),
        CodigoDeContratoNoEsquema.De<OrigemCandidatos>("identificacao.schema.json#/properties/origemCandidatos"),
        CodigoDeContratoNoEsquema.De<FormaDeComprovacao>(RegrasDeComprovacao + "forma"),
        CodigoDeContratoNoEsquema.De<CriterioDeDistincaoDasApresentacoes>(RegrasDeComprovacao + "criterioDeDistincao"),
        CodigoDeContratoNoEsquema.De<TipoRestricaoValor>("restricao-de-valor.schema.json#/properties/tipo"),
        CodigoDeContratoNoEsquema.De<FundamentoIsencao>("taxa-de-inscricao.schema.json#/properties/fundamentos/items"),
    ];

    private static readonly FrozenDictionary<string, CodigoDeContratoNoEsquema> PorLocalizacao =
        Campos.ToFrozenDictionary(static campo => campo.Localizacao, StringComparer.Ordinal);

    /// <summary>
    /// Percorre os pontos do documento que o esquema declara e escreve o rótulo logo depois de cada código, quando o esquema o
    /// declara só de leitura ao lado dele.
    /// </summary>
    public static void Descrever(JsonObject documento, FormaDoNo raiz)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(raiz);

        foreach (NoDoDocumento no in NosDeclaradosNoEsquema.Listar(documento, raiz).Where(static no => no.Valor is JsonObject))
        {
            DescreverObjeto(no.Valor.AsObject(), no.Forma);
        }
    }

    private static void DescreverObjeto(JsonObject objeto, FormaDoNo forma)
    {
        foreach (string campo in objeto.Select(static propriedade => propriedade.Key).Where(forma.Propriedades.ContainsKey).ToArray())
        {
            DescreverCampo(objeto, forma, campo, forma.Propriedades[campo]);
        }
    }

    private static void DescreverCampo(JsonObject objeto, FormaDoNo forma, string campo, FormaDoNo declarada)
    {
        switch (objeto[campo])
        {
            case JsonArray lista when EntradaDe(declarada.Itens) is { } entradaDaLista:
                EscreverRotulos(objeto, forma, campo, lista, entradaDaLista);
                break;
            case JsonValue valor when EntradaDe(declarada) is { } entrada:
                EscreverRotulo(objeto, forma, campo, valor, entrada);
                break;
        }
    }

    private static CodigoDeContratoNoEsquema? EntradaDe(FormaDoNo? forma) =>
        forma?.Localizacoes.Select(localizacao => PorLocalizacao.GetValueOrDefault(localizacao)).OfType<CodigoDeContratoNoEsquema>().FirstOrDefault();

    private static void EscreverRotulo(JsonObject objeto, FormaDoNo forma, string campo, JsonValue valor, CodigoDeContratoNoEsquema entrada)
    {
        if (valor.TryGetValue(out string? codigo) && entrada.RotuloDe(codigo) is { } rotulo)
        {
            Inserir(objeto, forma, campo, $"{campo}Rotulo", JsonValue.Create(rotulo));
        }
    }

    // A lista de rótulos só sai inteira: o código sem rótulo tiraria a correspondência de posição com a lista de códigos.
    private static void EscreverRotulos(JsonObject objeto, FormaDoNo forma, string campo, JsonArray lista, CodigoDeContratoNoEsquema entrada)
    {
        string?[] rotulos = [.. lista.Select(item => item is JsonValue valor && valor.TryGetValue(out string? codigo) ? entrada.RotuloDe(codigo) : null)];
        if (rotulos.All(static rotulo => rotulo is not null))
        {
            Inserir(objeto, forma, campo, $"{campo}Rotulos", new JsonArray([.. rotulos.Select(static rotulo => (JsonNode?)JsonValue.Create(rotulo))]));
        }
    }

    private static void Inserir(JsonObject objeto, FormaDoNo forma, string campo, string nomeDoRotulo, JsonNode rotulo)
    {
        if (forma.DeclaraSomenteLeitura(nomeDoRotulo))
        {
            objeto.Remove(nomeDoRotulo);
            objeto.Insert(objeto.IndexOf(campo) + 1, nomeDoRotulo, rotulo);
        }
    }
}
