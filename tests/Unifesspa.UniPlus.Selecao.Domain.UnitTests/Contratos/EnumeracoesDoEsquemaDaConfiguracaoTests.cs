namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Cada enumeração do esquema da configuração tem os mesmos valores do vocabulário que o domínio aceita: um valor novo num
/// lado sem o outro faz o contrato recusar o que o domínio entende, ou o domínio receber o que ele não conhece.
/// </summary>
public sealed class EnumeracoesDoEsquemaDaConfiguracaoTests
{
    private const string Arvore = "arvore-de-satisfacao.schema.json";
    private const string Bancas = "bancas.schema.json";
    private const string Comuns = "comuns.schema.json";
    private const string Deliberacao = "/$defs/regraDeDeliberacao/properties";
    private const string Exigencias = "exigencias.schema.json";
    private const string Exigencia = "/$defs/exigencia/properties";
    private const string Predicado = "predicado.schema.json";
    private const string Recurso = "/$defs/propriedadesDoRecurso/properties";

    private static readonly (string Arquivo, string Caminho, Func<IEnumerable<string?>> Dominio)[] Paridades =
    [
        (Arvore, "/properties/tipo", () => Codigos<TipoNo>(static v => v.ToCodigo())),
        (Arvore, "/properties/consequencia", () => ConsequenciaIndeferimentoCodigo.Validas),
        (Arvore, "/properties/chaveDistincao", () => Codigos<ChaveDistincao>(static v => v.ToCodigo())),
        (Bancas, $"{Deliberacao}/modo", Nomes<ModoDeDeliberacao>),
        (Bancas, $"{Deliberacao}/criterio", Nomes<CriterioDeVotacao>),
        (Bancas, $"{Deliberacao}/desempate", Nomes<DesempateDaVotacao>),
        (Bancas, $"{Deliberacao}/encerramento", Nomes<EncerramentoDaVotacao>),
        (Bancas, $"{Deliberacao}/semUnanimidade", Nomes<ResultadoSemUnanimidade>),
        ("coleta.schema.json", "/properties/referenciaTemporalFatos/properties/tipo", () => Codigos<ReferenciaTipo>(static v => v.ToCodigo())),
        (Comuns, "/$defs/produto/properties/papel", () => Codigos<PapelProdutoFase>(static v => ((PapelProdutoFase?)v).ToCodigo()!)),
        (Comuns, $"{Recurso}/prazoUnidade", () => Codigos<UnidadePrazo>(static v => v.Codigo())),
        (Comuns, $"{Recurso}/prazoSegundaInstanciaUnidade", () => Codigos<UnidadePrazo>(static v => v.Codigo())),
        (Comuns, $"{Recurso}/efeitoSuspensivo", () => Codigos<EfeitoSuspensivo>(static v => v.Codigo())),
        (Comuns, $"{Recurso}/guardaDaVaga", () => Codigos<GuardaDaVaga>(static v => v.Codigo())),
        (Predicado, "/$defs/condicao/properties/operador", () => Codigos<Operador>(static v => v.ToCodigo())),
        (Predicado, "/$defs/obrigatoriedade/properties/tipo", () => [PredicadoDnfJson.ObrigatoriedadeSempre, PredicadoDnfJson.ObrigatoriedadeNunca, PredicadoDnfJson.ObrigatoriedadeQuando]),
        (Predicado, "/$defs/baseLegal/properties/abrangencia", () => Codigos<TipoAbrangencia>(static v => v.ToCodigo())),
        (Predicado, "/$defs/baseLegal/properties/status", () => Codigos<StatusBaseLegal>(static v => v.ToCodigo())),
        ("divulgacao.schema.json", "/properties/camposPublicos/items", () => ConfiguracaoDivulgacao.CamposPermitidos.Select(static c => c.Codigo)),
        (Comuns, "/$defs/tipoDeDecisao", () => CatalogoDe<TipoMotivoDeDecisao>.Todos().Select(static c => c.Codigo)),
        ("motivo.schema.json", "/properties/sentido", () => SentidoDoMotivoCodigo.Todos),
        ("etapas.schema.json", "/properties/carater", Nomes<CaraterEtapa>),
        ("etapas.schema.json", "/properties/formaDeAvaliacao", () => Codigos<FormaDeAvaliacao>(static v => ((FormaDeAvaliacao?)v).ToCodigo()!)),
        ("etapas.schema.json", "/$defs/recursoDaEtapa/properties/ancora", Nomes<AncoraDoRecurso>),
        (Exigencias, $"{Exigencia}/classificacaoProtecao", () => ClassificacoesDeProtecao.TokensCanonicos),
        (Exigencias, $"{Exigencia}/aplicabilidade", () => Enum.GetNames<Aplicabilidade>().Where(static n => n != nameof(Aplicabilidade.Nenhuma))),
        (Exigencias, $"{Exigencia}/consequenciaIndeferimento", () => ConsequenciaIndeferimentoCodigo.Validas),
        (Exigencias, "/$defs/idadeMaximaEmissao/properties/unidade", () => Codigos<UnidadeIdade>(static v => v.ToCodigo())),
        (Exigencias, "/$defs/idadeMaximaEmissao/properties/referenciaTipo", () => Codigos<ReferenciaTipoIdadeEmissao>(static v => v.ToCodigo())),
        (Exigencias, "/$defs/formatosPermitidos/properties/lista/items/properties/formato", () => Codigos<FormatoPermitido>(static v => v.ToCodigo())),
        ("identificacao.schema.json", "/properties/origemCandidatos", Nomes<OrigemCandidatos>),
        ("taxa-de-inscricao.schema.json", "/properties/fundamentos/items", () => FundamentoIsencaoCodigo.Codigos),
        ("formulario.schema.json", "/properties/finalidade", () => [EstruturaFormulario.FinalidadeInscricao, EstruturaFormulario.FinalidadeIsencaoTaxa, EstruturaFormulario.FinalidadeHabilitacao]),
        ("formulario.schema.json", "/$defs/etapa/properties/tipo", () => [EstruturaFormulario.TipoSecao, EstruturaFormulario.TipoBloco]),
        ("formulario.schema.json", "/$defs/etapa/properties/bloco", () => [EstruturaFormulario.BlocoComprovacaoDocumental, EstruturaFormulario.BlocoModalidadesCalculadas, EstruturaFormulario.BlocoRevisaoEAceite]),
        (Comuns, "/$defs/alvoDoProcedimentoDeRecurso/properties/areaDoEnem", () => AreaDoEnem.Todas),
        (Comuns, "/$defs/procedimentoDeRecurso/properties/regra/properties/codigo", () => RegraProcedimentoRecursoCodigo.Rol),
        ("restricao-de-valor.schema.json", "/$defs/limiteDeData/properties/ancora", () => [RestricaoValorJson.AncoraDataFixa, RestricaoValorJson.AncoraEnvio]),
    ];

    public static TheoryData<string> ChavesDeParidade => [.. Paridades.Select(static p => $"{p.Arquivo}{p.Caminho}")];

    [Theory(DisplayName = "A enumeração do esquema tem os valores do vocabulário do domínio")]
    [MemberData(nameof(ChavesDeParidade))]
    public void EnumeracaoDoEsquema_TemOsValoresDoDominio(string chave)
    {
        (string arquivo, string caminho, Func<IEnumerable<string?>> dominio) = Paridades.Single(p => $"{p.Arquivo}{p.Caminho}" == chave);

        string?[] noEsquema = [.. ValoresDoEsquema(arquivo, caminho)];

        noEsquema.Should().BeEquivalentTo(dominio());
    }

    // O formato do texto é derivado do catálogo do fato, não declarado: fica fora do tipo da restrição do esquema.
    [Fact(DisplayName = "O tipo de restrição do esquema são os tipos declaráveis do domínio")]
    public void TipoDeRestricao_SaoOsTiposDeclaraveis() =>
        ValoresDoEsquema("restricao-de-valor.schema.json", "/properties/tipo").Should().BeEquivalentTo(
            Enum.GetValues<TipoRestricaoValor>()
                .Where(static t => t is not (TipoRestricaoValor.Nenhuma or TipoRestricaoValor.FormatoTexto))
                .Select(RestricaoValorJson.ParaToken));

    private static IEnumerable<string> Codigos<T>(Func<T, string> codigo)
        where T : struct, Enum =>
        Enum.GetValues<T>().Where(static v => v.ToString() is not ("Nenhum" or "Nenhuma")).Select(codigo);

    private static IEnumerable<string> Nomes<T>()
        where T : struct, Enum =>
        Enum.GetNames<T>().Where(static n => n is not ("Nenhum" or "Nenhuma")).Select(static n => JsonNamingPolicy.CamelCase.ConvertName(n));

    private static IEnumerable<string?> ValoresDoEsquema(string arquivo, string caminho)
    {
        string texto = File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1", arquivo));
        JsonNode no = caminho.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(JsonNode.Parse(texto)!, static (atual, passo) => atual[passo]!);
        return no["enum"]!.AsArray().Select(static valor => valor?.GetValue<string>());
    }
}
