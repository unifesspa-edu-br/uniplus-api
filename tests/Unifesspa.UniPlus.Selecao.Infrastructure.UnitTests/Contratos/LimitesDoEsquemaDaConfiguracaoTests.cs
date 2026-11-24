namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Os limites de tamanho do esquema da configuração batem com os que o codec aplica ao reidratar: o esquema nunca admite
/// o que o codec recusa e, onde o limite vem de coluna do domínio, é o mesmo número.
/// </summary>
public sealed class LimitesDoEsquemaDaConfiguracaoTests
{
    private const string Atendimento = "atendimento.schema.json";
    private const string Bonus = "bonus-regional.schema.json";
    private const string Comuns = "comuns.schema.json";
    private const string Etapas = "etapas.schema.json";
    private const string Distribuicao = "distribuicao-de-vagas.schema.json";
    private const string Formulario = "formulario.schema.json";
    private const string Item = "item-do-formulario.schema.json";
    private const string Exigencia = "exigencias.schema.json#/$defs/exigencia/properties";
    private const string Condicoes = "condicoes.schema.json";
    private const string Pesos = "pesos-por-area.schema.json";

    private static readonly (string Constante, int Limite, string Arquivo, string Caminho, bool Exato)[] Paridades =
    [
        (nameof(LimitesDoEnvelope.EtapaNome), LimitesDoEnvelope.EtapaNome, Etapas, "/properties/nome", true),
        (nameof(LimitesDoEnvelope.ItemDeAvaliacaoRotulo), LimitesDoEnvelope.ItemDeAvaliacaoRotulo, Etapas, "/$defs/itemDeAvaliacao/properties/rotulo", true),
        (nameof(LimitesDoEnvelope.NivelDeAtendimentoRotulo), LimitesDoEnvelope.NivelDeAtendimentoRotulo, Etapas, "/$defs/nivelDeAtendimento/properties/rotulo", true),
        (nameof(LimitesDoEnvelope.TipoBancaCodigo), LimitesDoEnvelope.TipoBancaCodigo, "bancas.schema.json", "/properties/tipoBancaCodigo", true),
        (nameof(LimitesDoEnvelope.CategoriaDocumentoCodigo), LimitesDoEnvelope.CategoriaDocumentoCodigo, "bancas.schema.json", "/properties/categoriasDocumentoCodigos/items", true),
        (nameof(LimitesDoEnvelope.TipoAtoCodigo), LimitesDoEnvelope.TipoAtoCodigo, Comuns, "/$defs/publicacaoPrevista/properties/atoCodigo", true),
        (nameof(LimitesDoEnvelope.RegraCodigo), LimitesDoEnvelope.RegraCodigo, Comuns, "/$defs/referenciaDeRegra/properties/codigo", true),
        (nameof(LimitesDoEnvelope.RegraVersao), LimitesDoEnvelope.RegraVersao, Comuns, "/$defs/referenciaDeRegra/properties/versao", true),
        (nameof(LimitesDoEnvelope.Fato), LimitesDoEnvelope.Fato, Condicoes, "/$defs/condicao/properties/codigoDoDado", true),
        (nameof(LimitesDoEnvelope.ObservacaoBaseLegal), LimitesDoEnvelope.ObservacaoBaseLegal, Condicoes, "/$defs/baseLegal/properties/observacao", true),
        (nameof(LimitesDoEnvelope.TipoDocumentoCodigo), LimitesDoEnvelope.TipoDocumentoCodigo, Exigencia, "/tipoDocumentoCodigo", true),
        (nameof(LimitesDoEnvelope.TipoDocumentoNome), LimitesDoEnvelope.TipoDocumentoNome, Exigencia, "/tipoDocumentoNome", true),
        (nameof(LimitesDoEnvelope.TipoDocumentoCategoria), LimitesDoEnvelope.TipoDocumentoCategoria, Exigencia, "/tipoDocumentoCategoria", true),
        (nameof(LimitesDoEnvelope.FinalidadeTratamento), LimitesDoEnvelope.FinalidadeTratamento, Exigencia, "/finalidadeTratamento", true),
        (nameof(LimitesDoEnvelope.HipoteseLegal), LimitesDoEnvelope.HipoteseLegal, Exigencia, "/hipoteseLegal", true),
        (nameof(LimitesDoEnvelope.NomeArquivoDoModelo), LimitesDoEnvelope.NomeArquivoDoModelo, Exigencia, "/modelo/properties/nomeArquivo", true),
        (nameof(ValidacaoDoMotivo.DescricaoMaxLength), ValidacaoDoMotivo.DescricaoMaxLength, "motivo.schema.json", "/properties/descricao", true),
        (nameof(ConferenciaDosMotivosDaRetificacao.JustificativaMaxLength), ConferenciaDosMotivosDaRetificacao.JustificativaMaxLength, "motivo.schema.json", "/properties/justificativaDaInclusao", true),
        (nameof(LimitesDoEnvelope.Justificativa), LimitesDoEnvelope.Justificativa, "divulgacao.schema.json", "/properties/justificativa", true),
        (nameof(LimitesDoEnvelope.TipoInstrumentoNormativo), LimitesDoEnvelope.TipoInstrumentoNormativo, Bonus, "/properties/tipoInstrumento", true),
        (nameof(LimitesDoEnvelope.FatoElegibilidadeBonusRegional), LimitesDoEnvelope.FatoElegibilidadeBonusRegional, Bonus, "/properties/codigoDoDadoDeElegibilidade", true),
        (nameof(LimitesDoEnvelope.IdentificacaoBaseLegalBonusRegional), LimitesDoEnvelope.IdentificacaoBaseLegalBonusRegional, Bonus, "/properties/identificacao", true),
        (nameof(LimitesDoEnvelope.DescricaoBaseLegalBonusRegional), LimitesDoEnvelope.DescricaoBaseLegalBonusRegional, Bonus, "/properties/descricao", true),
        (nameof(LimitesDoEnvelope.MunicipioBonusRegionalNome), LimitesDoEnvelope.MunicipioBonusRegionalNome, Bonus, "/$defs/municipio/properties/nome", true),
        (nameof(LimitesDoEnvelope.UnidadeAdministradoraSigla), LimitesDoEnvelope.UnidadeAdministradoraSigla, "identificacao.schema.json", "/properties/unidadeAdministradoraSigla", true),
        (nameof(LimitesDoEnvelope.UnidadeAdministradoraNome), LimitesDoEnvelope.UnidadeAdministradoraNome, "identificacao.schema.json", "/properties/unidadeAdministradoraNome", true),
        (nameof(LimitesDoEnvelope.UnidadeAdministradoraCidadeNome), LimitesDoEnvelope.UnidadeAdministradoraCidadeNome, "identificacao.schema.json", "/properties/localidadeNome", true),
        (nameof(LimitesDoEnvelope.ResolucaoPesoAreaEnem), LimitesDoEnvelope.ResolucaoPesoAreaEnem, "classificacao.schema.json", "/properties/resolucaoPesoAreaEnem", true),
        (nameof(LimitesDoEnvelope.GrupoAreaEnemCodigo), LimitesDoEnvelope.GrupoAreaEnemCodigo, Pesos, "/properties/grupoAreaEnemCodigo", true),
        (nameof(LimitesDoEnvelope.GrupoAreaEnemRotulo), LimitesDoEnvelope.GrupoAreaEnemRotulo, Pesos, "/properties/grupoAreaEnemRotulo", true),
        (nameof(LimitesDoEnvelope.BaseLegalPesoAreaEnem), LimitesDoEnvelope.BaseLegalPesoAreaEnem, Pesos, "/properties/baseLegal", true),
        (nameof(LimitesDoEnvelope.AreaPesoAreaEnemCodigo), LimitesDoEnvelope.AreaPesoAreaEnemCodigo, Pesos, "/$defs/areaDePeso/properties/codigo", true),
        (nameof(LimitesDoEnvelope.AreaPesoAreaEnemRotulo), LimitesDoEnvelope.AreaPesoAreaEnemRotulo, Pesos, "/$defs/areaDePeso/properties/rotulo", true),
        (nameof(LimitesDoEnvelope.CodigoDoTermoNoCatalogo), LimitesDoEnvelope.CodigoDoTermoNoCatalogo, Formulario, "/$defs/termoExigido/properties/termoCodigo", true),
        (nameof(LimitesDoEnvelope.CodigoTermoExigido), LimitesDoEnvelope.CodigoTermoExigido, Item, "/$defs/codigoLocal", true),
        (nameof(LimitesDoEnvelope.CodigoEtapaFormulario), LimitesDoEnvelope.CodigoEtapaFormulario, Item, "/$defs/codigoLocal", true),
        (nameof(LimitesDoEnvelope.TituloEtapaFormulario), LimitesDoEnvelope.TituloEtapaFormulario, Formulario, "/$defs/pagina/properties/titulo", true),
        (nameof(LimitesDoEnvelope.TextoEtapaFormulario), LimitesDoEnvelope.TextoEtapaFormulario, Formulario, "/$defs/pagina/properties/descricao", true),
        (nameof(LimitesDoEnvelope.CodigoModeloDeFormulario), LimitesDoEnvelope.CodigoModeloDeFormulario, Formulario, "/properties/modeloOrigemCodigo", true),
        (nameof(LimitesDoEnvelope.AjudaDoCampo), LimitesDoEnvelope.AjudaDoCampo, Item, "/properties/ajuda", true),
        (nameof(LimitesDoEnvelope.MensagemDoImpedimento), LimitesDoEnvelope.MensagemDoImpedimento, Item, "/$defs/impedimento/properties/mensagem", true),
        (nameof(FormaDoItem.FatoCodigoMaxLength), FormaDoItem.FatoCodigoMaxLength, Item, "/properties/codigoDoDado", true),
        (nameof(FormaDoItem.RotuloMaxLength), FormaDoItem.RotuloMaxLength, Item, "/properties/rotulo", true),
        (nameof(FormaDoItem.RotuloMaxLength) + " (opção)", FormaDoItem.RotuloMaxLength, Item, "/$defs/opcao/properties/rotulo", true),
        (nameof(FormaDoCabecalho.TituloMaxLength), FormaDoCabecalho.TituloMaxLength, Formulario, "/properties/titulo", true),
        ($"{nameof(OfertaCursoSnapshot)}.{nameof(OfertaCursoSnapshot.CodigoMaxLength)}", OfertaCursoSnapshot.CodigoMaxLength, Distribuicao, "/properties/ofertaCursoCodigo", true),
        (nameof(OfertaCursoSnapshot.CursoNomeMaxLength), OfertaCursoSnapshot.CursoNomeMaxLength, Distribuicao, "/properties/cursoNome", true),
        (nameof(OfertaCursoSnapshot.CursoGrauMaxLength), OfertaCursoSnapshot.CursoGrauMaxLength, Distribuicao, "/properties/cursoGrau", true),
        (nameof(OfertaCursoSnapshot.UnidadeOfertanteNomeMaxLength), OfertaCursoSnapshot.UnidadeOfertanteNomeMaxLength, Distribuicao, "/properties/unidadeOfertanteNome", true),
        (nameof(OfertaCursoSnapshot.TurnoMaxLength), OfertaCursoSnapshot.TurnoMaxLength, Distribuicao, "/properties/turnos/items", true),
        (nameof(LimitesDoEnvelope.CensoReferencia), LimitesDoEnvelope.CensoReferencia, Distribuicao, "/properties/referenciaDemograficaCodigo", true),
        (nameof(LimitesDoEnvelope.ModalidadeCodigo), LimitesDoEnvelope.ModalidadeCodigo, Distribuicao, "/$defs/modalidade/properties/codigo", true),
        (nameof(LimitesDoEnvelope.ModalidadeDescricao), LimitesDoEnvelope.ModalidadeDescricao, Distribuicao, "/$defs/modalidade/properties/descricao", true),
        ($"{nameof(DestinoRemanejamento)}.{nameof(DestinoRemanejamento.CodigoMaxLength)}", DestinoRemanejamento.CodigoMaxLength, "cascata-de-remanejamento.schema.json", "/properties/destinoFinalCodigo", true),
        // O nome do cadastro vem de colunas de 200 caracteres; o codec guarda a cópia em coluna de 300, folga que o esquema não usa.
        ($"{nameof(LimitesDoEnvelope.NomeDeCadastro)} (condição)", LimitesDoEnvelope.NomeDeCadastro, Atendimento, "/$defs/condicao/properties/condicaoNome", false),
        ($"{nameof(LimitesDoEnvelope.NomeDeCadastro)} (recurso)", LimitesDoEnvelope.NomeDeCadastro, Atendimento, "/$defs/recurso/properties/recursoNome", false),
        ($"{nameof(LimitesDoEnvelope.NomeDeCadastro)} (deficiência)", LimitesDoEnvelope.NomeDeCadastro, Atendimento, "/$defs/tipoDeficiencia/properties/tipoDeficienciaNome", false),
    ];

    public static TheoryData<string> Constantes => [.. Paridades.Select(static p => p.Constante)];

    [Fact]
    public void UnidadeResponsavelCodigo_LimiteDoCadastro_EstaContidoNoLimiteDoEsquema()
    {
        int limiteDoEsquema = MaximoDoEsquema(Comuns, "/$defs/codigoDeCatalogo");

        LimitesDoEnvelope.UnidadeAdministradoraCodigo.Should().Be(50);
        limiteDoEsquema.Should().BeGreaterThanOrEqualTo(LimitesDoEnvelope.UnidadeAdministradoraCodigo);
    }

    [Theory(DisplayName = "O limite de tamanho do esquema bate com o que o codec aplica")]
    [MemberData(nameof(Constantes))]
    public void LimiteDoEsquema_BateComOCodec(string constante)
    {
        (_, int limite, string arquivo, string caminho, bool exato) = Paridades.Single(p => p.Constante == constante);

        int noEsquema = MaximoDoEsquema(arquivo, caminho);

        if (exato)
        {
            noEsquema.Should().Be(limite);
            return;
        }

        noEsquema.Should().BeLessThanOrEqualTo(limite);
    }

    // O caminho pode vir com o arquivo ("arquivo#/ponteiro"), como nas exigências, que repetem o prefixo.
    private static int MaximoDoEsquema(string arquivo, string caminho)
    {
        string[] partes = arquivo.Split('#', 2);
        string ponteiro = (partes.Length == 2 ? partes[1] : string.Empty) + caminho;
        string texto = File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1", partes[0]));
        JsonNode no = ponteiro.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(JsonNode.Parse(texto)!, static (atual, passo) => atual[passo]!);
        return no["maxLength"]!.GetValue<int>();
    }
}
