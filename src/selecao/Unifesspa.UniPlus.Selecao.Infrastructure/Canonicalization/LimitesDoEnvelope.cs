namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os limites das <b>colunas</b> que vão receber a configuração reidratada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que o decoder precisa deles.</b> Um valor que não cabe na coluna atravessa a
/// leitura inteira, satisfaz o domínio e <b>recanonicaliza nos mesmos bytes</b> — o
/// encoder o reemite tal qual —, de modo que a prova de round-trip <b>aprova</b>. A
/// recusa só chega no <c>SaveChanges</c>, como <c>DbUpdateException</c> do Postgres
/// (<c>22001</c> <i>value too long</i>, <c>22003</c> <i>numeric field overflow</i>) — no
/// meio do descarte, como <b>500 não tratado</b> em vez de recusa nomeada. O caminho de
/// comando já barra isso no <c>FluentValidation</c>; o de reidratação barra aqui.
/// </para>
/// <para>
/// Estes valores <b>espelham o schema</b>, e não podem divergir dele: o fitness
/// <c>LimitesDoEnvelopeBatemComOSchemaTests</c> lê o modelo do EF Core e compara coluna a
/// coluna. Mudar um <c>HasMaxLength</c> ou um <c>HasPrecision</c> sem mudar aqui
/// <b>quebra o build</b> — que é o único jeito de esta lista não envelhecer em silêncio.
/// </para>
/// <para>
/// Os campos que vivem dentro de um <c>json</c>/<c>jsonb</c> (os <c>parametros</c> dos critérios
/// e das regras de eliminação, a regra de aplicabilidade da modalidade) <b>não</b> têm limite
/// de coluna — e por isso não aparecem aqui.
/// </para>
/// </remarks>
public static class LimitesDoEnvelope
{
    // Texto — comprimento máximo (varchar).
    public const int NomeDoProcesso = DocumentoDoRascunho.NomeMaxLength;
    public const int EtapaNome = 300;
    public const int TipoEtapaCodigo = FormatoDoCodigo.ComprimentoMaximo;
    public const int TipoEtapaNome = 200;
    public const int ModalidadeCodigo = 60;
    public const int ModalidadeDescricao = 300;
    public const int Token = 30;
    public const int BaseLegal = 500;
    public const int CondicaoCodigo = 50;

    /// <summary>
    /// Código do tipo de deficiência congelado no atendimento especializado — mesmo teto
    /// do value object <c>CodigoTipoDeficiencia</c> no cadastro de origem.
    /// </summary>
    public const int TipoDeficienciaCodigo = 50;

    /// <summary>
    /// Código do recurso de acessibilidade congelado no atendimento especializado — mesmo teto
    /// do value object <c>CodigoRecursoAcessibilidade</c> no cadastro de origem.
    /// </summary>
    public const int RecursoCodigo = 50;
    public const int NomeDeCadastro = 300;
    public const int CensoReferencia = 20;

    /// <summary>
    /// Código e rótulo do grupo de área do ENEM congelado na distribuição de vagas — mesmo
    /// teto das colunas de origem no cadastro de cursos.
    /// </summary>
    public const int GrupoAreaEnemCodigo = GrupoAreaEnemSnapshot.CodigoMaxLength;
    public const int GrupoAreaEnemRotulo = GrupoAreaEnemSnapshot.RotuloMaxLength;

    /// <summary>
    /// Resolução de Pesos por Área declarada na classificação — mesmo teto da coluna de origem
    /// no cadastro de Pesos por Área.
    /// </summary>
    public const int ResolucaoPesoAreaEnem = ConfiguracaoClassificacao.ResolucaoPesoAreaEnemMaxLength;

    /// <summary>Base legal de cada grupo do quadro de pesos por área congelado na classificação.</summary>
    public const int BaseLegalPesoAreaEnem = GrupoPesoAreaEnemCongelado.BaseLegalMaxLength;

    /// <summary>Código de cada área do quadro de pesos por área congelado na classificação.</summary>
    public const int AreaPesoAreaEnemCodigo = GrupoPesoAreaEnemCongelado.AreaCodigoMaxLength;

    /// <summary>Rótulo de cada área do quadro de pesos por área congelado na classificação.</summary>
    public const int AreaPesoAreaEnemRotulo = GrupoPesoAreaEnemCongelado.AreaRotuloMaxLength;

    public const int RegraCodigo = 128;
    public const int RegraVersao = 16;

    /// <summary>
    /// O número do ato. Não é coluna do agregado (os <c>DadosEdital</c> são do ato, não da
    /// configuração), mas os validators de publicar e de retificar o limitam a 60 — e o
    /// decoder tem de ser tão estrito quanto eles.
    /// </summary>
    public const int NumeroDoAto = 60;

    // Cronograma de fases.
    /// <summary>
    /// <c>FaseCronograma.Codigo</c> — cópia do código canônico de <c>FaseCanonica</c>.
    /// Governa também <c>EtapaProcesso.FaseCodigo</c>, o vínculo que a etapa declara com a
    /// fase: é a mesma grandeza um nível abaixo, e a mesma coluna de 60.
    /// </summary>
    public const int FaseCodigo = 60;

    /// <summary><c>FaseCronograma.TipoFaseNome</c>.</summary>
    public const int TipoFaseNome = FaseCronograma.TipoFaseNomeMaxLength;

    /// <summary>
    /// Código de tipo de ato de Publicações — <c>ProdutoDaFase.AtoCodigo</c> e, um nível
    /// abaixo, <c>ProdutoDaEtapa.AtoCodigo</c>: a etapa publica por conta própria, na mesma
    /// coluna de 60.
    /// </summary>
    public const int TipoAtoCodigo = 60;

    /// <summary>Nome do tipo de ato no catálogo de Publicações.</summary>
    public const int TipoAtoNome = 200;

    /// <summary>
    /// <c>BancaRequerida.Codigo</c> — cópia do código classificatório de
    /// <c>TipoBanca</c>. Governa também <c>BancaDaEtapa.Codigo</c>, a mesma cópia no nível
    /// da etapa, na mesma coluna de 60.
    /// </summary>
    public const int TipoBancaCodigo = 60;

    /// <summary>
    /// <c>BancaRequerida.Nome</c> e <c>BancaDaEtapa.Nome</c> — cópia do nome do
    /// <c>TipoBanca</c>, na mesma coluna de 200.
    /// </summary>
    public const int TipoBancaNome = 200;

    /// <summary><c>ItemDeAvaliacao.Rotulo</c>.</summary>
    public const int ItemDeAvaliacaoRotulo = Domain.Entities.ItemDeAvaliacao.RotuloMaxLength;

    /// <summary><c>NivelDeAtendimento.Rotulo</c> — vive em coluna jsonb, então o teto é o do domínio.</summary>
    public const int NivelDeAtendimentoRotulo = Domain.ValueObjects.NivelDeAtendimento.RotuloMaxLength;

    /// <summary><c>CategoriaJulgada.Codigo</c> — cópia do código classificatório de <c>CategoriaDocumento</c>.</summary>
    public const int CategoriaDocumentoCodigo = 50;

    /// <summary>Nome da categoria de documento julgada.</summary>
    public const int CategoriaDocumentoNome = 200;

    // Exigências.
    /// <summary><c>DocumentoExigido.TipoDocumentoCodigo</c> — cópia do código classificatório de <c>TipoDocumento</c>.</summary>
    public const int TipoDocumentoCodigo = 60;

    /// <summary><c>DocumentoExigido.TipoDocumentoNome</c> — cópia dos dados do rótulo legível de <c>TipoDocumento</c>.</summary>
    public const int TipoDocumentoNome = 200;

    /// <summary><c>DocumentoExigido.TipoDocumentoCategoria</c> — cópia da categoria classificatória de <c>TipoDocumento</c>.</summary>
    public const int TipoDocumentoCategoria = 60;

    /// <summary><c>DocumentoExigido.FinalidadeTratamento</c> — cópia da finalidade do tratamento de <c>TipoDocumento</c>.</summary>
    public const int FinalidadeTratamento = FormaDoItem.FinalidadeTratamentoMaxLength;

    /// <summary><c>FatoColetado.Descricao</c> e a descrição do fato derivado — cópia da descrição do fato no catálogo.</summary>
    public const int DescricaoDoFato = FormaDoItem.DescricaoDoFatoMaxLength;

    /// <summary><c>FatoColetado.ComponenteExibicao</c> — token do componente de exibição do fato no catálogo.</summary>
    public const int ComponenteExibicao = FormaDoItem.ComponenteExibicaoMaxLength;

    /// <summary><c>DocumentoExigido.HipoteseLegal</c> — cópia do código da hipótese legal de <c>TipoDocumento</c>.</summary>
    public const int HipoteseLegal = FormaDoItem.HipoteseLegalMaxLength;

    /// <summary><c>DocumentoExigidoBaseLegal.Observacao</c>.</summary>
    public const int ObservacaoBaseLegal = 1000;

    /// <summary><c>CondicaoGatilho.Fato</c> — nome do fato do gatilho DNF.</summary>
    public const int Fato = FatoDoCatalogo.CodigoMaxLength;

    // Decimais — precisão total (o `p` de numeric(p,s)).
    public const int PrecisaoEtapa = 18;
    public const int PrecisaoDoFada = 6;
    public const int PrecisaoPr = 5;

    /// <summary>Fração de um nível de atendimento: entre 0 e 1, com 4 casas — cinco dígitos no total.</summary>
    public const int PrecisaoFracaoDeAtendimento = 5;
    public const int PrecisaoPercentual = 5;

    /// <summary>
    /// Peso de uma área no quadro de pesos por área congelado — <c>numeric(6,4)</c>: cabe o
    /// teto do cadastro de origem (99,99) na escala com que o registro publicado grava todo decimal.
    /// </summary>
    public const int PrecisaoPesoAreaEnem = 6;

    /// <summary>
    /// Corte de uma área no quadro de pesos por área congelado — <c>numeric(8,4)</c>: cabe a
    /// nota máxima de uma área do ENEM (1000) na escala com que o registro publicado grava todo decimal.
    /// </summary>
    public const int PrecisaoCorteAreaEnem = 8;

    /// <summary>Precisão do valor do prazo de interposição e do prazo da 2ª instância do recurso — <c>numeric(18,4)</c>.</summary>
    public const int PrecisaoPrazo = 18;

    /// <summary>Precisão de <c>ConfiguracaoTaxaInscricao.Valor</c> — <c>numeric(12,2)</c>, monetário.</summary>
    public const int PrecisaoTaxaInscricao = ConfiguracaoTaxaInscricao.ValorPrecisao;

    /// <summary>Precisão da escala da nota final da classificação — <c>numeric(9,4)</c>, a da <c>NotaFinal</c>.</summary>
    public const int PrecisaoEscalaNotaFinal = ConfiguracaoClassificacao.EscalaNotaFinalPrecisao;

    // Unidade administradora.
    /// <summary><c>UnidadeAdministradoraSnapshot.Sigla</c> — espelha <c>UnidadeConfiguration.Sigla</c>.</summary>
    public const int UnidadeAdministradoraSigla = 50;

    /// <summary><c>UnidadeAdministradoraSnapshot.Codigo</c> — espelha <c>UnidadeConfiguration.Codigo</c>.</summary>
    public const int UnidadeAdministradoraCodigo = 50;

    /// <summary><c>UnidadeAdministradoraSnapshot.IdentificadorNoEnderecoWeb</c> — espelha o limite do identificador no endereço web da unidade de origem.</summary>
    public const int UnidadeAdministradoraIdentificadorNoEnderecoWeb = 64;

    /// <summary><c>UnidadeAdministradoraSnapshot.Nome</c> — espelha <c>UnidadeConfiguration.Nome</c>.</summary>
    public const int UnidadeAdministradoraNome = 250;

    /// <summary><c>UnidadeAdministradoraSnapshot.Tipo</c> — espelha <c>UnidadeConfiguration.Tipo</c>.</summary>
    public const int UnidadeAdministradoraTipo = 30;

    // Cidade da unidade administradora.
    /// <summary><c>UnidadeAdministradoraSnapshot.CidadeCodigoIbge</c> — espelha <c>ProcessoSeletivoConfiguration</c> (owned).</summary>
    public const int UnidadeAdministradoraCidadeCodigoIbge = ReferenciaCidadeGeo.CodigoIbgeLength;

    /// <summary><c>UnidadeAdministradoraSnapshot.CidadeNome</c> — espelha <c>ProcessoSeletivoConfiguration</c> (owned).</summary>
    public const int UnidadeAdministradoraCidadeNome = ReferenciaCidadeGeo.NomeMaxLength;

    /// <summary><c>UnidadeAdministradoraSnapshot.CidadeUf</c> — espelha <c>ProcessoSeletivoConfiguration</c> (owned).</summary>
    public const int UnidadeAdministradoraCidadeUf = ReferenciaCidadeGeo.UfLength;

    // UNI-REQ-0144 — formulários por finalidade.
    /// <summary><c>PaginaDoFormulario.Codigo</c>, também o <c>paginaCodigo</c> de cada item.</summary>
    public const int CodigoPaginaFormulario = FormaDaPagina.CodigoMaxLength;

    /// <summary><c>PaginaDoFormulario.Titulo</c>.</summary>
    public const int TituloPaginaFormulario = FormaDaPagina.TituloMaxLength;

    /// <summary><c>PaginaDoFormulario.Descricao</c> e <c>Aviso</c>.</summary>
    public const int TextoPaginaFormulario = FormaDaPagina.TextoMaxLength;
    public const int AjudaDoCampo = FormaDoItem.AjudaMaxLength;

    /// <summary><c>FatoColetado.Impedimento.Mensagem</c>.</summary>
    public const int MensagemDoImpedimento = FormaDoItem.MensagemDoImpedimentoMaxLength;

    /// <summary><c>DocumentoExigido.Modelo.NomeArquivo</c>.</summary>
    public const int NomeArquivoDoModelo = Domain.Entities.ModeloDeDocumento.NomeArquivoMaxLength;

    /// <summary><c>FormularioProcesso.ModeloOrigemCodigo</c>.</summary>
    public const int CodigoModeloDeFormulario = Domain.Entities.FormularioProcesso.ModeloOrigemCodigoMaxLength;

    /// <summary><c>FormularioProcesso.ModeloOrigemNome</c>.</summary>
    public const int NomeModeloDeFormulario = Domain.Entities.FormularioProcesso.ModeloOrigemNomeMaxLength;

    // UNI-REQ-0086 — termos exigidos pelo formulário, copiados da versão do catálogo.
    /// <summary><c>TermoExigidoFormulario.Codigo</c>.</summary>
    public const int CodigoTermoExigido = FormaDoTermo.CodigoMaxLength;

    /// <summary>Código estável do termo no catálogo de termos de consentimento.</summary>
    public const int CodigoDoTermoNoCatalogo = Domain.Entities.TermoExigidoFormulario.TermoCodigoMaxLength;

    /// <summary>Nome do termo no catálogo de termos de consentimento.</summary>
    public const int NomeDoTermo = 200;

    /// <summary>Texto da versão do termo, o texto legal completo apresentado ao candidato.</summary>
    public const int TextoDoTermo = 20_000;

    /// <summary>Base legal da versão do termo.</summary>
    public const int BaseLegalDoTermo = 500;

    /// <summary>Hash SHA-256 em hexadecimal da versão do termo.</summary>
    public const int HashDaVersaoDoTermo = 64;

    // Divulgação pública.
    /// <summary><c>ConfiguracaoDivulgacao.Justificativa</c> — texto de negócio livre, não um rótulo curto.</summary>
    public const int Justificativa = 1000;

    // FADA: referencia a base legal tipada.
    /// <summary><c>ConfiguracaoDoFada.TipoInstrumento</c> — cópia do código canônico de <c>TipoInstrumentoNormativo</c>.</summary>
    public const int TipoInstrumentoNormativo = 30;

    /// <summary><c>ConfiguracaoDoFada.FatoElegibilidadeCodigo</c> — código do fato que decide quem recebe o acréscimo.</summary>
    public const int CodigoDoDadoDeElegibilidadeDoFada = ConfiguracaoDoFada.FatoElegibilidadeCodigoMaxLength;

    /// <summary><c>ConfiguracaoDoFada.BaseLegalCodigo</c> — cópia do código estável da Base Legal (teto do <c>CodigoDaBaseLegalDoFada</c>).</summary>
    public const int CodigoDaBaseLegalDoFada = 80;

    /// <summary><c>ConfiguracaoDoFada.Identificacao</c> — cópia da identificação da Base Legal.</summary>
    public const int IdentificacaoDaBaseLegalDoFada = 500;

    /// <summary><c>ConfiguracaoDoFada.Descricao</c> — cópia da descrição da Base Legal.</summary>
    public const int DescricaoDaBaseLegalDoFada = 2000;

    /// <summary><c>MunicipioDoFada.CodigoIbge</c> — mesma grandeza de <see cref="ReferenciaCidadeGeo.CodigoIbgeLength"/>: cópia do município do cadastro de Base Legal do FADA.</summary>
    public const int MunicipioDoFadaCodigoIbge = ReferenciaCidadeGeo.CodigoIbgeLength;

    /// <summary><c>MunicipioDoFada.Nome</c> — mesma grandeza de <see cref="ReferenciaCidadeGeo.NomeMaxLength"/>.</summary>
    public const int MunicipioDoFadaNome = ReferenciaCidadeGeo.NomeMaxLength;

    /// <summary><c>MunicipioDoFada.Uf</c> — mesma grandeza de <see cref="ReferenciaCidadeGeo.UfLength"/>.</summary>
    public const int MunicipioDoFadaUf = ReferenciaCidadeGeo.UfLength;
}
