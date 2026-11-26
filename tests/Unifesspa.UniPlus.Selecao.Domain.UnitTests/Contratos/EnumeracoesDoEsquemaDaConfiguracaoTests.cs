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
    private const string RegrasDeComprovacao = "regras-de-comprovacao.schema.json";
    private const string Bancas = "bancas.schema.json";
    private const string Comuns = "comuns.schema.json";
    private const string Deliberacao = "/$defs/regraDeDeliberacao/properties";
    private const string Exigencias = "exigencias.schema.json";
    private const string Exigencia = "/$defs/exigencia/properties";
    private const string Condicoes = "condicoes.schema.json";
    private const string Recurso = "/$defs/propriedadesDoRecurso/properties";

    private static readonly (string Arquivo, string Caminho, Func<IEnumerable<string?>> Dominio)[] Paridades =
    [
        (RegrasDeComprovacao, "/properties/forma", () => Codigos<FormaDeComprovacao>(static v => v.Codigo())),
        (RegrasDeComprovacao, "/properties/consequenciaDoDescumprimento", () => CatalogoDe<ConsequenciaDoDescumprimento>.Valores().Select(static valor => valor.Codigo()).ToArray()),
        (RegrasDeComprovacao, "/properties/criterioDeDistincao", () => Codigos<CriterioDeDistincaoDasApresentacoes>(static v => v.Codigo())),
        (Bancas, $"{Deliberacao}/modo", () => Codigos<ModoDeDeliberacao>(static v => v.Codigo())),
        (Bancas, $"{Deliberacao}/criterio", () => Codigos<CriterioDeVotacao>(static v => v.Codigo())),
        (Bancas, $"{Deliberacao}/desempate", () => Codigos<DesempateDaVotacao>(static v => v.Codigo())),
        (Bancas, $"{Deliberacao}/encerramento", () => Codigos<EncerramentoDaVotacao>(static v => v.Codigo())),
        (Bancas, $"{Deliberacao}/semUnanimidade", () => Codigos<ResultadoSemUnanimidade>(static v => v.Codigo())),
        ("coleta.schema.json", "/properties/referenciaTemporalDosDados/properties/tipo", () => Codigos<ReferenciaTipo>(static v => v.Codigo())),
        (Comuns, "/$defs/publicacaoPrevista/properties/papel", () => Codigos<PapelDaPublicacaoPrevista>(static v => v.Codigo())),
        (Comuns, $"{Recurso}/prazoUnidade", () => Codigos<UnidadePrazo>(static v => v.Codigo())),
        (Comuns, $"{Recurso}/prazoSegundaInstanciaUnidade", () => Codigos<UnidadePrazo>(static v => v.Codigo())),
        (Comuns, $"{Recurso}/efeitoSuspensivo", () => Codigos<EfeitoSuspensivo>(static v => v.Codigo())),
        (Comuns, $"{Recurso}/guardaDaVaga", () => Codigos<GuardaDaVaga>(static v => v.Codigo())),
        (Condicoes, "/$defs/condicao/properties/operador", () => Codigos<Operador>(static v => v.Codigo())),
        (Condicoes, "/$defs/obrigatoriedade/properties/tipo", () => Codigos<TipoObrigatoriedade>(static v => v.Codigo())),
        (Condicoes, "/$defs/baseLegal/properties/abrangencia", () => Codigos<TipoAbrangencia>(static v => v.Codigo())),
        (Condicoes, "/$defs/baseLegal/properties/situacao", () => Codigos<StatusBaseLegal>(static v => v.Codigo())),
        ("divulgacao.schema.json", "/properties/camposPublicos/items", () => ConfiguracaoDivulgacao.CamposPermitidos.Select(static c => c.Codigo)),
        (Comuns, "/$defs/tipoDeDecisao", () => CatalogoDe<TipoMotivoDeDecisao>.Todos().Select(static c => c.Codigo)),
        ("motivo.schema.json", "/properties/sentido", () => CatalogoDe<SentidoDoMotivo>.Todos().Select(static item => item.Codigo).ToArray()),
        ("etapas.schema.json", "/properties/carater", () => Codigos<CaraterEtapa>(static v => v.Codigo())),
        ("etapas.schema.json", "/properties/formaDeAvaliacao", () => Codigos<FormaDeAvaliacao>(static v => v.Codigo())),
        ("etapas.schema.json", "/$defs/recursoDaEtapa/properties/eventoInicialDoPrazo", () => Codigos<EventoInicialDoPrazo>(static v => v.Codigo())),
        (Exigencias, $"{Exigencia}/classificacaoProtecao", () => ClassificacoesDeProtecao.TokensCanonicos),
        (Exigencias, $"{Exigencia}/aplicabilidade", () => Codigos<Aplicabilidade>(static v => v.Codigo())),
        (Exigencias, $"{Exigencia}/consequenciaDoDescumprimento", () => CatalogoDe<ConsequenciaDoDescumprimento>.Valores().Select(static valor => valor.Codigo()).ToArray()),
        (Exigencias, "/$defs/prazoDeEmissao/properties/escolha", () => CatalogoDe<EscolhaDoPrazoDeEmissao>.Todos().Select(static c => c.Codigo)),
        (Exigencias, "/$defs/prazoDeEmissao/properties/unidade", () => CatalogoDe<UnidadeDoPrazoDeEmissao>.Todos().Select(static c => c.Codigo)),
        (Exigencias, "/$defs/prazoDeEmissao/properties/efeitoForaDoPrazo", () => CatalogoDe<EfeitoNaAnaliseDoDocumento>.Todos().Select(static c => c.Codigo)),
        (Exigencias, "/$defs/validade/properties/escolha", () => CatalogoDe<EscolhaDaValidade>.Todos().Select(static c => c.Codigo)),
        (Exigencias, "/$defs/validade/properties/efeitoDoVencimento", () => CatalogoDe<EfeitoNaAnaliseDoDocumento>.Todos().Select(static c => c.Codigo)),
        (Exigencias, "/$defs/dataDeReferenciaDoDocumento/properties/tipo", () => CatalogoDe<TipoDaDataDeReferencia>.Todos().Select(static c => c.Codigo)),
        (Exigencias, "/$defs/formatosPermitidos/properties/lista/items/properties/formato", () => CatalogoDe<FormatoPermitido>.Todos().Select(static c => c.Codigo)),
        ("identificacao.schema.json", "/properties/origemCandidatos", () => Codigos<OrigemCandidatos>(static v => v.Codigo())),
        ("taxa-de-inscricao.schema.json", "/properties/fundamentos/items", () => CatalogoDe<FundamentoIsencao>.Todos().Select(static v => v.Codigo).ToArray()),
        ("formulario.schema.json", "/properties/finalidade", () => CatalogoDe<FinalidadeFormulario>.Todos().Select(static c => c.Codigo)),
        ("formulario.schema.json", "/$defs/pagina/properties/tipo", () => Codigos<TipoPaginaFormulario>(static v => v.Codigo())),
        ("formulario.schema.json", "/$defs/pagina/properties/bloco", () => Codigos<BlocoSistema>(static v => v.Codigo())),
        (Comuns, "/$defs/alvoDoProcedimentoDeRecurso/properties/areaDoEnem", () => AreaDoEnem.Todas),
        (Comuns, "/$defs/procedimentoDeRecurso/properties/regra/properties/codigo", () => CatalogoDe<RegraDeProcedimentoDoRecurso>.Todos().Select(static item => item.Codigo)),
        ("restricao-de-valor.schema.json", "/$defs/limiteDeData/properties/referenciaDaData", () => [RestricaoValorJson.AncoraDataFixa, RestricaoValorJson.AncoraEnvio]),
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
            CatalogoDe<TipoRestricaoValor>.Valores()
                .Where(static t => RestricoesDeValor.TipoDeclarado(t.Codigo()) is not null)
                .Select(static t => t.Codigo()));

    private static IEnumerable<string> Codigos<T>(Func<T, string> codigo)
        where T : struct, Enum =>
        Enum.GetValues<T>().Where(static v => v.ToString() is not ("Nenhum" or "Nenhuma")).Select(codigo);

    private static IEnumerable<string?> ValoresDoEsquema(string arquivo, string caminho)
    {
        string texto = File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1", arquivo));
        JsonNode no = caminho.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(JsonNode.Parse(texto)!, static (atual, passo) => atual[passo]!);
        if (no["$ref"]?.GetValue<string>() is { } referencia)
        {
            string[] partes = referencia.Split('#', 2);
            return ValoresDoEsquema(partes[0], partes[1]);
        }
        return no["enum"]!.AsArray().Select(static valor => valor?.GetValue<string>());
    }
}
