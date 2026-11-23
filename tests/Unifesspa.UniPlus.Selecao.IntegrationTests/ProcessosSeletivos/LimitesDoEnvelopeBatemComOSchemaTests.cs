namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Os limites que o decoder impõe <b>são os das colunas</b> — e continuam sendo (Story #859).
/// </summary>
/// <remarks>
/// <para>
/// O decoder recusa um <c>etapas[].nome</c> de 301 caracteres porque a coluna comporta 300.
/// Mas o valor <c>300</c> está escrito em <b>dois</b> lugares — na
/// <c>EtapaProcessoConfiguration</c> e em <c>LimitesDoEnvelope</c> — e duas listas manuais
/// divergem. No dia em que alguém alargar a coluna para 500 e esquecer do codec, o decoder
/// passa a recusar configuração <b>legítima</b>; no dia em que alguém a <b>estreitar</b>
/// para 100, o decoder passa a aceitar o que o banco recusa, e o descarte volta a morrer
/// com <c>DbUpdateException</c> — <b>500 não tratado</b>, que é exatamente o que os limites
/// existem para evitar.
/// </para>
/// <para>
/// Este teste lê o <b>modelo do EF Core</b> — a fonte de verdade do schema — e compara
/// coluna a coluna. Não é um teste de banco: <c>DbContext.Model</c> é construído em memória,
/// sem conexão. Mexer num <c>HasMaxLength</c> ou num <c>HasPrecision</c> sem mexer no codec
/// <b>quebra o build</b>.
/// </para>
/// </remarks>
public sealed class LimitesDoEnvelopeBatemComOSchemaTests
{
    /// <summary>
    /// Os limites que o decoder aplica, e a coluna de onde cada um tem de vir. Os nomes são
    /// os das constantes de <c>LimitesDoEnvelope</c> — referenciadas aqui, nunca recopiadas.
    /// </summary>
    internal static readonly (string Nome, int ValorNoCodec, Type Entidade, string Propriedade)[] Comprimentos =
    [
        ("NomeDoProcesso", LimitesDoEnvelope.NomeDoProcesso, typeof(ProcessoSeletivo), nameof(ProcessoSeletivo.Nome)),
        ("EtapaNome", LimitesDoEnvelope.EtapaNome, typeof(EtapaProcesso), nameof(EtapaProcesso.Nome)),
        ("ModalidadeCodigo", LimitesDoEnvelope.ModalidadeCodigo, typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.Codigo)),
        ("ModalidadeDescricao", LimitesDoEnvelope.ModalidadeDescricao, typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.Descricao)),
        ("Token", LimitesDoEnvelope.Token, typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.AcaoQuandoIndeferido)),
        ("BaseLegal", LimitesDoEnvelope.BaseLegal, typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.BaseLegal)),
        ("CondicaoCodigo", LimitesDoEnvelope.CondicaoCodigo, typeof(CondicaoOfertada), nameof(CondicaoOfertada.CondicaoCodigo)),
        ("TipoDeficienciaCodigo", LimitesDoEnvelope.TipoDeficienciaCodigo, typeof(DeficienciaAtendida), nameof(DeficienciaAtendida.TipoDeficienciaCodigo)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(CondicaoOfertada), nameof(CondicaoOfertada.CondicaoNome)),
        ("RecursoCodigo", LimitesDoEnvelope.RecursoCodigo, typeof(RecursoOfertado), nameof(RecursoOfertado.RecursoCodigo)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(RecursoOfertado), nameof(RecursoOfertado.RecursoNome)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(DeficienciaAtendida), nameof(DeficienciaAtendida.TipoDeficienciaNome)),
        // Story #1466 — bônus regional referencia Base Legal tipada (snapshot congelado).
        ("TipoInstrumentoNormativo", LimitesDoEnvelope.TipoInstrumentoNormativo, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.TipoInstrumento)),
        ("FatoElegibilidadeBonusRegional", LimitesDoEnvelope.FatoElegibilidadeBonusRegional, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.FatoElegibilidadeCodigo)),
        ("CodigoBaseLegalBonusRegional", LimitesDoEnvelope.CodigoBaseLegalBonusRegional, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.BaseLegalBonusRegionalCodigo)),
        ("IdentificacaoBaseLegalBonusRegional", LimitesDoEnvelope.IdentificacaoBaseLegalBonusRegional, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Identificacao)),
        ("DescricaoBaseLegalBonusRegional", LimitesDoEnvelope.DescricaoBaseLegalBonusRegional, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Descricao)),
        ("MunicipioBonusRegionalCodigoIbge", LimitesDoEnvelope.MunicipioBonusRegionalCodigoIbge, typeof(ConfiguracaoBonusRegionalMunicipio), nameof(ConfiguracaoBonusRegionalMunicipio.CodigoIbge)),
        ("MunicipioBonusRegionalNome", LimitesDoEnvelope.MunicipioBonusRegionalNome, typeof(ConfiguracaoBonusRegionalMunicipio), nameof(ConfiguracaoBonusRegionalMunicipio.Nome)),
        ("MunicipioBonusRegionalUf", LimitesDoEnvelope.MunicipioBonusRegionalUf, typeof(ConfiguracaoBonusRegionalMunicipio), nameof(ConfiguracaoBonusRegionalMunicipio.Uf)),

        // Story #851 — cronograma de fases.
        ("FaseCodigo", LimitesDoEnvelope.FaseCodigo, typeof(FaseCronograma), nameof(FaseCronograma.Codigo)),
        ("TipoAtoCodigo", LimitesDoEnvelope.TipoAtoCodigo, typeof(ProdutoDaFase), nameof(ProdutoDaFase.AtoCodigo)),
        ("TipoBancaCodigo", LimitesDoEnvelope.TipoBancaCodigo, typeof(BancaRequerida), CaminhoDaCopiaDoTipo(nameof(TipoDeBancaCopiado.Codigo))),
        ("TipoBancaNome", LimitesDoEnvelope.TipoBancaNome, typeof(BancaRequerida), CaminhoDaCopiaDoTipo(nameof(TipoDeBancaCopiado.Nome))),
        ("CategoriaDocumentoCodigo", LimitesDoEnvelope.CategoriaDocumentoCodigo, typeof(CategoriaJulgada), nameof(CategoriaJulgada.Codigo)),

        // As mesmas grandezas, um nível abaixo: a etapa repete fase, ato e banca, e as colunas
        // dela têm a mesma largura. Sem estas linhas a tabela cobria só o lado da fase, e
        // estreitar uma coluna da etapa não acusaria nada.
        //
        // O que estas linhas NÃO alcançam, e convém não confiar que alcancem: o decodificador
        // media faseCodigo contra o teto do NOME da etapa, e isso passou porque aquele teto
        // batia com a coluna do nome — a tabela confere constante contra coluna, nunca que o
        // call site pegou a constante da coluna que vai receber o valor. Quem prende esse
        // segundo erro é o teste de recusa por campo, em EnvelopeCodecRecusaEtapasTests.
        ("FaseCodigo", LimitesDoEnvelope.FaseCodigo, typeof(EtapaProcesso), nameof(EtapaProcesso.FaseCodigo)),
        ("TipoAtoCodigo", LimitesDoEnvelope.TipoAtoCodigo, typeof(ProdutoDaEtapa), nameof(ProdutoDaEtapa.AtoCodigo)),
        ("TipoBancaCodigo", LimitesDoEnvelope.TipoBancaCodigo, typeof(BancaDaEtapa), CaminhoDaCopiaDoTipo(nameof(TipoDeBancaCopiado.Codigo))),
        ("TipoBancaNome", LimitesDoEnvelope.TipoBancaNome, typeof(BancaDaEtapa), CaminhoDaCopiaDoTipo(nameof(TipoDeBancaCopiado.Nome))),
        ("ItemDeAvaliacaoRotulo", LimitesDoEnvelope.ItemDeAvaliacaoRotulo, typeof(ItemDeAvaliacao), nameof(ItemDeAvaliacao.Rotulo)),

        // Story #554 (PR #903) — exigencias[] real.
        ("TipoDocumentoCodigo", LimitesDoEnvelope.TipoDocumentoCodigo, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoCodigo)),
        ("TipoDocumentoNome", LimitesDoEnvelope.TipoDocumentoNome, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoNome)),
        ("TipoDocumentoCategoria", LimitesDoEnvelope.TipoDocumentoCategoria, typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoCategoria)),
        ("FinalidadeTratamento", LimitesDoEnvelope.FinalidadeTratamento, typeof(DocumentoExigido), nameof(DocumentoExigido.FinalidadeTratamento)),
        ("HipoteseLegal", LimitesDoEnvelope.HipoteseLegal, typeof(DocumentoExigido), nameof(DocumentoExigido.HipoteseLegal)),
        ("Token", LimitesDoEnvelope.Token, typeof(DocumentoExigido), nameof(DocumentoExigido.ConsequenciaIndeferimento)),
        ("BaseLegal", LimitesDoEnvelope.BaseLegal, typeof(DocumentoExigidoBaseLegal), nameof(DocumentoExigidoBaseLegal.Referencia)),
        ("ObservacaoBaseLegal", LimitesDoEnvelope.ObservacaoBaseLegal, typeof(DocumentoExigidoBaseLegal), nameof(DocumentoExigidoBaseLegal.Observacao)),
        ("Fato", LimitesDoEnvelope.Fato, typeof(CondicaoGatilho), nameof(CondicaoGatilho.Fato)),

        // Story #575 — cascata de remanejamento. Códigos de modalidade — mesmo limite de
        // ModalidadeSelecionada.Codigo, reusado porque são a mesma grandeza.
        ("ModalidadeCodigo", LimitesDoEnvelope.ModalidadeCodigo, typeof(ConfiguracaoCascataRemanejamento), nameof(ConfiguracaoCascataRemanejamento.FallbackCodigo)),
        ("ModalidadeCodigo", LimitesDoEnvelope.ModalidadeCodigo, typeof(DestinoRemanejamento), nameof(DestinoRemanejamento.ModalidadeOrigemCodigo)),
        ("ModalidadeCodigo", LimitesDoEnvelope.ModalidadeCodigo, typeof(DestinoRemanejamento), nameof(DestinoRemanejamento.ModalidadeDestinoCodigo)),

        // Story #559 — formulário de inscrição. Rotulo/FormularioTitulo reusam
        // NomeDeCadastro (mesma grandeza: rótulo curto legível).
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(FatoColetado), nameof(FatoColetado.Rotulo)),
        ("NomeDeCadastro", LimitesDoEnvelope.NomeDeCadastro, typeof(FormularioProcesso), nameof(FormularioProcesso.Titulo)),
        ("CodigoEtapaFormulario", LimitesDoEnvelope.CodigoEtapaFormulario, typeof(EtapaFormulario), nameof(EtapaFormulario.Codigo)),
        ("CodigoEtapaFormulario", LimitesDoEnvelope.CodigoEtapaFormulario, typeof(FatoColetado), nameof(FatoColetado.EtapaCodigo)),
        ("Token", LimitesDoEnvelope.Token, typeof(FatoColetado), nameof(FatoColetado.Formato)),
        ("AjudaDoCampo", LimitesDoEnvelope.AjudaDoCampo, typeof(FatoColetado), nameof(FatoColetado.Ajuda)),
        ("DescricaoDoFato", LimitesDoEnvelope.DescricaoDoFato, typeof(FatoColetado), nameof(FatoColetado.Descricao)),
        ("ComponenteExibicao", LimitesDoEnvelope.ComponenteExibicao, typeof(FatoColetado), nameof(FatoColetado.ComponenteExibicao)),
        ("NomeArquivoDoModelo", LimitesDoEnvelope.NomeArquivoDoModelo, typeof(ModeloDaExigencia), nameof(ModeloDaExigencia.NomeArquivo)),
        ("TituloEtapaFormulario", LimitesDoEnvelope.TituloEtapaFormulario, typeof(EtapaFormulario), nameof(EtapaFormulario.Titulo)),
        ("TextoEtapaFormulario", LimitesDoEnvelope.TextoEtapaFormulario, typeof(EtapaFormulario), nameof(EtapaFormulario.Descricao)),
        ("TextoEtapaFormulario", LimitesDoEnvelope.TextoEtapaFormulario, typeof(EtapaFormulario), nameof(EtapaFormulario.Aviso)),
        ("CodigoModeloDeFormulario", LimitesDoEnvelope.CodigoModeloDeFormulario, typeof(FormularioProcesso), nameof(FormularioProcesso.ModeloOrigemCodigo)),
        ("CodigoTermoExigido", LimitesDoEnvelope.CodigoTermoExigido, typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.Codigo)),
        ("CodigoDoTermoNoCatalogo", LimitesDoEnvelope.CodigoDoTermoNoCatalogo, typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.TermoCodigo)),
        ("NomeDoTermo", LimitesDoEnvelope.NomeDoTermo, typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.Nome)),
        ("TextoDoTermo", LimitesDoEnvelope.TextoDoTermo, typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.Texto)),
        ("BaseLegalDoTermo", LimitesDoEnvelope.BaseLegalDoTermo, typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.BaseLegal)),
        ("HashDaVersaoDoTermo", LimitesDoEnvelope.HashDaVersaoDoTermo, typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.HashVersao)),

        // Issue #563 — divulgação pública.
        ("Justificativa", LimitesDoEnvelope.Justificativa, typeof(ConfiguracaoDivulgacao), nameof(ConfiguracaoDivulgacao.Justificativa)),

        // Resolução de Pesos por Área e quadro congelado na classificação.
        ("ResolucaoPesoAreaEnem", LimitesDoEnvelope.ResolucaoPesoAreaEnem, typeof(ConfiguracaoClassificacao), nameof(ConfiguracaoClassificacao.ResolucaoPesoAreaEnem)),
        ("BaseLegalPesoAreaEnem", LimitesDoEnvelope.BaseLegalPesoAreaEnem, typeof(GrupoPesoAreaEnemCongelado), nameof(GrupoPesoAreaEnemCongelado.BaseLegal)),
        ("AreaPesoAreaEnemCodigo", LimitesDoEnvelope.AreaPesoAreaEnemCodigo, typeof(AreaPesoAreaEnemCongelada), nameof(AreaPesoAreaEnemCongelada.Codigo)),
        ("AreaPesoAreaEnemRotulo", LimitesDoEnvelope.AreaPesoAreaEnemRotulo, typeof(AreaPesoAreaEnemCongelada), nameof(AreaPesoAreaEnemCongelada.Rotulo)),
    ];

    internal static readonly (string Nome, int PrecisaoNoCodec, int EscalaNoCodec, Type Entidade, string Propriedade)[] Precisoes =
    [
        ("PrecisaoEtapa", LimitesDoEnvelope.PrecisaoEtapa, 4, typeof(EtapaProcesso), nameof(EtapaProcesso.Peso)),
        ("PrecisaoEtapa", LimitesDoEnvelope.PrecisaoEtapa, 4, typeof(EtapaProcesso), nameof(EtapaProcesso.NotaMinima)),
        ("PrecisaoEtapa", LimitesDoEnvelope.PrecisaoEtapa, 4, typeof(ItemDeAvaliacao), nameof(ItemDeAvaliacao.PontuacaoMaxima)),
        ("PrecisaoBonus", LimitesDoEnvelope.PrecisaoBonus, 4, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Fator)),
        ("PrecisaoBonus", LimitesDoEnvelope.PrecisaoBonus, 4, typeof(ConfiguracaoBonusRegional), nameof(ConfiguracaoBonusRegional.Teto)),
        ("PrecisaoPr", LimitesDoEnvelope.PrecisaoPr, 4, typeof(ConfiguracaoDistribuicaoVagas), nameof(ConfiguracaoDistribuicaoVagas.PercentualDaReservaLegal)),
        ("PrecisaoTaxaInscricao", LimitesDoEnvelope.PrecisaoTaxaInscricao, ConfiguracaoTaxaInscricao.ValorEscala, typeof(ConfiguracaoTaxaInscricao), nameof(ConfiguracaoTaxaInscricao.Valor)),
        ("PrecisaoPesoAreaEnem", LimitesDoEnvelope.PrecisaoPesoAreaEnem, 4, typeof(AreaPesoAreaEnemCongelada), nameof(AreaPesoAreaEnemCongelada.Peso)),
        ("PrecisaoCorteAreaEnem", LimitesDoEnvelope.PrecisaoCorteAreaEnem, 4, typeof(AreaPesoAreaEnemCongelada), nameof(AreaPesoAreaEnemCongelada.Corte)),
    ];

    [Fact(DisplayName = "Todo limite de COMPRIMENTO do decoder é o da coluna que vai receber o valor")]
    public void Comprimentos_BatemComOSchema()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();

        foreach ((string nome, int valorNoCodec, Type entidade, string propriedade) in Comprimentos)
        {
            ConferirOComprimento(contexto, nome, valorNoCodec, entidade, propriedade);
        }
    }

    // As duas bancas guardam a cópia do tipo como tipo de propriedade própria: o caminho vai da navegação até a coluna.
    private static string CaminhoDaCopiaDoTipo(string propriedade) => $"{nameof(BancaRequerida.Tipo)}.{propriedade}";

    private static IProperty Coluna(UniPlusDbContext contexto, Type entidade, string caminho)
    {
        IEntityType tipo = contexto.Model.FindEntityType(entidade)!;
        string[] partes = caminho.Split('.');
        foreach (string navegacao in partes[..^1])
        {
            tipo = tipo.FindNavigation(navegacao)!.TargetEntityType;
        }

        return tipo.FindProperty(partes[^1])!;
    }

    private static void ConferirOComprimento(UniPlusDbContext contexto, string nome, int valorNoCodec, Type entidade, string propriedade) =>
        Coluna(contexto, entidade, propriedade).GetMaxLength()
            .Should().Be(valorNoCodec,
                $"LimitesDoEnvelope.{nome} tem de ser o HasMaxLength de {entidade.Name}.{propriedade}. " +
                "Se a coluna encolheu, o decoder passou a aceitar o que o banco recusa — e o descarte volta a morrer " +
                "com DbUpdateException (500) em vez de recusa nomeada. Se a coluna cresceu, o decoder passou a " +
                "recusar configuração legítima.");

    [Fact(DisplayName = "Todo limite de PRECISÃO do decoder é o da coluna numeric(p,s)")]
    public void Precisoes_BatemComOSchema()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();

        foreach ((string nome, int precisaoNoCodec, int escalaNoCodec, Type entidade, string propriedade) in Precisoes)
        {
            IProperty coluna = contexto.Model.FindEntityType(entidade)!.FindProperty(propriedade)!;

            coluna.GetPrecision().Should().Be(precisaoNoCodec,
                $"LimitesDoEnvelope.{nome} tem de ser a precisão de {entidade.Name}.{propriedade} — sem ela, um " +
                "decimal com escala impecável mas dígitos demais recanonicaliza nos MESMOS bytes, a prova de " +
                "a conversão é aceita, mas o numeric(p,s) excede o limite no SaveChanges (22003).");
            coluna.GetScale().Should().Be(escalaNoCodec);
        }
    }
}
