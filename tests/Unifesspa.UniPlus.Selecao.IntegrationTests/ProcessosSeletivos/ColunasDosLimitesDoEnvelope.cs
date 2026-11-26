namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

internal static class ColunasDosLimitesDoEnvelope
{
    internal static readonly (string Nome, Type Entidade, string Propriedade)[] Comprimentos =
    [
        (nameof(LimitesDoEnvelope.NomeDoProcesso), typeof(ProcessoSeletivo), nameof(ProcessoSeletivo.Nome)),
        (nameof(LimitesDoEnvelope.EtapaNome), typeof(EtapaProcesso), nameof(EtapaProcesso.Nome)),
        (nameof(LimitesDoEnvelope.ModalidadeCodigo), typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.Codigo)),
        (nameof(LimitesDoEnvelope.ModalidadeDescricao), typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.Descricao)),
        (nameof(LimitesDoEnvelope.Token), typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.AcaoQuandoIndeferido)),
        (nameof(LimitesDoEnvelope.BaseLegal), typeof(ModalidadeSelecionada), nameof(ModalidadeSelecionada.BaseLegal)),
        (nameof(LimitesDoEnvelope.CondicaoCodigo), typeof(CondicaoOfertada), nameof(CondicaoOfertada.CondicaoCodigo)),
        (nameof(LimitesDoEnvelope.TipoDeficienciaCodigo), typeof(DeficienciaAtendida), nameof(DeficienciaAtendida.TipoDeficienciaCodigo)),
        (nameof(LimitesDoEnvelope.NomeDeCadastro), typeof(CondicaoOfertada), nameof(CondicaoOfertada.CondicaoNome)),
        (nameof(LimitesDoEnvelope.RecursoCodigo), typeof(RecursoOfertado), nameof(RecursoOfertado.RecursoCodigo)),
        (nameof(LimitesDoEnvelope.NomeDeCadastro), typeof(RecursoOfertado), nameof(RecursoOfertado.RecursoNome)),
        (nameof(LimitesDoEnvelope.NomeDeCadastro), typeof(DeficienciaAtendida), nameof(DeficienciaAtendida.TipoDeficienciaNome)),
        (nameof(LimitesDoEnvelope.TipoInstrumentoNormativo), typeof(ConfiguracaoDoFada), nameof(ConfiguracaoDoFada.TipoInstrumento)),
        (nameof(LimitesDoEnvelope.CodigoDoDadoDeElegibilidadeDoFada), typeof(ConfiguracaoDoFada), nameof(ConfiguracaoDoFada.FatoElegibilidadeCodigo)),
        (nameof(LimitesDoEnvelope.CodigoDaBaseLegalDoFada), typeof(ConfiguracaoDoFada), nameof(ConfiguracaoDoFada.BaseLegalCodigo)),
        (nameof(LimitesDoEnvelope.IdentificacaoDaBaseLegalDoFada), typeof(ConfiguracaoDoFada), nameof(ConfiguracaoDoFada.Identificacao)),
        (nameof(LimitesDoEnvelope.DescricaoDaBaseLegalDoFada), typeof(ConfiguracaoDoFada), nameof(ConfiguracaoDoFada.Descricao)),
        (nameof(LimitesDoEnvelope.MunicipioDoFadaCodigoIbge), typeof(MunicipioDoFada), nameof(MunicipioDoFada.CodigoIbge)),
        (nameof(LimitesDoEnvelope.MunicipioDoFadaNome), typeof(MunicipioDoFada), nameof(MunicipioDoFada.Nome)),
        (nameof(LimitesDoEnvelope.MunicipioDoFadaUf), typeof(MunicipioDoFada), nameof(MunicipioDoFada.Uf)),
        (nameof(LimitesDoEnvelope.FaseCodigo), typeof(FaseCronograma), nameof(FaseCronograma.Codigo)),
        (nameof(LimitesDoEnvelope.TipoFaseNome), typeof(FaseCronograma), nameof(FaseCronograma.TipoFaseNome)),
        (nameof(LimitesDoEnvelope.TipoAtoCodigo), typeof(ProdutoDaFase), nameof(ProdutoDaFase.AtoCodigo)),
        (nameof(LimitesDoEnvelope.TipoBancaCodigo), typeof(BancaRequerida), CaminhoDaCopiaDoTipo(nameof(TipoDeBancaCopiado.Codigo))),
        (nameof(LimitesDoEnvelope.TipoBancaNome), typeof(BancaRequerida), CaminhoDaCopiaDoTipo(nameof(TipoDeBancaCopiado.Nome))),
        (nameof(LimitesDoEnvelope.CategoriaDocumentoCodigo), typeof(CategoriaJulgada), nameof(CategoriaJulgada.Codigo)),
        (nameof(LimitesDoEnvelope.CategoriaDocumentoNome), typeof(CategoriaJulgada), nameof(CategoriaJulgada.Nome)),
        (nameof(LimitesDoEnvelope.FaseCodigo), typeof(EtapaProcesso), nameof(EtapaProcesso.FaseCodigo)),
        (nameof(LimitesDoEnvelope.TipoAtoCodigo), typeof(ProdutoDaEtapa), nameof(ProdutoDaEtapa.AtoCodigo)),
        (nameof(LimitesDoEnvelope.TipoBancaCodigo), typeof(BancaDaEtapa), CaminhoDaCopiaDoTipo(nameof(TipoDeBancaCopiado.Codigo))),
        (nameof(LimitesDoEnvelope.TipoBancaNome), typeof(BancaDaEtapa), CaminhoDaCopiaDoTipo(nameof(TipoDeBancaCopiado.Nome))),
        (nameof(LimitesDoEnvelope.ItemDeAvaliacaoRotulo), typeof(ItemDeAvaliacao), nameof(ItemDeAvaliacao.Rotulo)),
        (nameof(LimitesDoEnvelope.TipoDocumentoCodigo), typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoCodigo)),
        (nameof(LimitesDoEnvelope.TipoDocumentoNome), typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoNome)),
        (nameof(LimitesDoEnvelope.TipoDocumentoCategoria), typeof(DocumentoExigido), nameof(DocumentoExigido.TipoDocumentoCategoria)),
        (nameof(LimitesDoEnvelope.FinalidadeTratamento), typeof(DocumentoExigido), nameof(DocumentoExigido.FinalidadeTratamento)),
        (nameof(LimitesDoEnvelope.HipoteseLegal), typeof(DocumentoExigido), nameof(DocumentoExigido.HipoteseLegal)),
        (nameof(LimitesDoEnvelope.Token), typeof(DocumentoExigido), nameof(DocumentoExigido.ConsequenciaDoDescumprimento)),
        (nameof(LimitesDoEnvelope.BaseLegal), typeof(DocumentoExigidoBaseLegal), nameof(DocumentoExigidoBaseLegal.Referencia)),
        (nameof(LimitesDoEnvelope.ObservacaoBaseLegal), typeof(DocumentoExigidoBaseLegal), nameof(DocumentoExigidoBaseLegal.Observacao)),
        (nameof(LimitesDoEnvelope.Fato), typeof(CondicaoGatilho), nameof(CondicaoGatilho.Fato)),
        (nameof(LimitesDoEnvelope.ModalidadeCodigo), typeof(ConfiguracaoCascataRemanejamento), nameof(ConfiguracaoCascataRemanejamento.FallbackCodigo)),
        (nameof(LimitesDoEnvelope.ModalidadeCodigo), typeof(DestinoRemanejamento), nameof(DestinoRemanejamento.ModalidadeOrigemCodigo)),
        (nameof(LimitesDoEnvelope.ModalidadeCodigo), typeof(DestinoRemanejamento), nameof(DestinoRemanejamento.ModalidadeDestinoCodigo)),
        (nameof(LimitesDoEnvelope.NomeDeCadastro), typeof(FatoColetado), nameof(FatoColetado.Rotulo)),
        (nameof(LimitesDoEnvelope.NomeDeCadastro), typeof(FormularioProcesso), nameof(FormularioProcesso.Titulo)),
        (nameof(LimitesDoEnvelope.CodigoPaginaFormulario), typeof(PaginaDoFormulario), nameof(PaginaDoFormulario.Codigo)),
        (nameof(LimitesDoEnvelope.CodigoPaginaFormulario), typeof(FatoColetado), nameof(FatoColetado.PaginaCodigo)),
        (nameof(LimitesDoEnvelope.Token), typeof(FatoColetado), nameof(FatoColetado.Formato)),
        (nameof(LimitesDoEnvelope.AjudaDoCampo), typeof(FatoColetado), nameof(FatoColetado.Ajuda)),
        (nameof(LimitesDoEnvelope.DescricaoDoFato), typeof(FatoColetado), nameof(FatoColetado.Descricao)),
        (nameof(LimitesDoEnvelope.ComponenteExibicao), typeof(FatoColetado), nameof(FatoColetado.ComponenteExibicao)),
        (nameof(LimitesDoEnvelope.NomeArquivoDoModelo), typeof(ModeloDaExigencia), nameof(ModeloDaExigencia.NomeArquivo)),
        (nameof(LimitesDoEnvelope.TituloPaginaFormulario), typeof(PaginaDoFormulario), nameof(PaginaDoFormulario.Titulo)),
        (nameof(LimitesDoEnvelope.TextoPaginaFormulario), typeof(PaginaDoFormulario), nameof(PaginaDoFormulario.Descricao)),
        (nameof(LimitesDoEnvelope.TextoPaginaFormulario), typeof(PaginaDoFormulario), nameof(PaginaDoFormulario.Aviso)),
        (nameof(LimitesDoEnvelope.CodigoModeloDeFormulario), typeof(FormularioProcesso), CaminhoDaOrigemDoModelo(nameof(ModeloDeOrigem.Codigo))),
        (nameof(LimitesDoEnvelope.NomeModeloDeFormulario), typeof(FormularioProcesso), CaminhoDaOrigemDoModelo(nameof(ModeloDeOrigem.Nome))),
        (nameof(LimitesDoEnvelope.CodigoTermoExigido), typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.Codigo)),
        (nameof(LimitesDoEnvelope.CodigoDoTermoNoCatalogo), typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.TermoCodigo)),
        (nameof(LimitesDoEnvelope.NomeDoTermo), typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.Nome)),
        (nameof(LimitesDoEnvelope.TextoDoTermo), typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.Texto)),
        (nameof(LimitesDoEnvelope.BaseLegalDoTermo), typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.BaseLegal)),
        (nameof(LimitesDoEnvelope.HashDaVersaoDoTermo), typeof(TermoExigidoFormulario), nameof(TermoExigidoFormulario.HashVersao)),
        (nameof(LimitesDoEnvelope.Justificativa), typeof(ConfiguracaoDivulgacao), nameof(ConfiguracaoDivulgacao.Justificativa)),
        (nameof(LimitesDoEnvelope.ResolucaoPesoAreaEnem), typeof(ConfiguracaoClassificacao), nameof(ConfiguracaoClassificacao.ResolucaoPesoAreaEnem)),
        (nameof(LimitesDoEnvelope.BaseLegalPesoAreaEnem), typeof(GrupoPesoAreaEnemCongelado), nameof(GrupoPesoAreaEnemCongelado.BaseLegal)),
        (nameof(LimitesDoEnvelope.AreaPesoAreaEnemCodigo), typeof(AreaPesoAreaEnemCongelada), nameof(AreaPesoAreaEnemCongelada.Codigo)),
        (nameof(LimitesDoEnvelope.AreaPesoAreaEnemRotulo), typeof(AreaPesoAreaEnemCongelada), nameof(AreaPesoAreaEnemCongelada.Rotulo)),
        (nameof(LimitesDoEnvelope.TipoAtoNome), typeof(TipoAtoPublicado), nameof(TipoAtoPublicado.Nome)),
    ];

    internal static readonly (string Nome, int Escala, Type Entidade, string Propriedade)[] Precisoes =
    [
        (nameof(LimitesDoEnvelope.PrecisaoEtapa), 4, typeof(EtapaProcesso), nameof(EtapaProcesso.Peso)),
        (nameof(LimitesDoEnvelope.PrecisaoEtapa), 4, typeof(EtapaProcesso), nameof(EtapaProcesso.NotaMinima)),
        (nameof(LimitesDoEnvelope.PrecisaoEtapa), 4, typeof(ItemDeAvaliacao), nameof(ItemDeAvaliacao.PontuacaoMaxima)),
        (nameof(LimitesDoEnvelope.PrecisaoDoFada), 4, typeof(ConfiguracaoDoFada), nameof(ConfiguracaoDoFada.Fator)),
        (nameof(LimitesDoEnvelope.PrecisaoDoFada), 4, typeof(ConfiguracaoDoFada), nameof(ConfiguracaoDoFada.Teto)),
        (nameof(LimitesDoEnvelope.PrecisaoPr), 4, typeof(ConfiguracaoDistribuicaoVagas), nameof(ConfiguracaoDistribuicaoVagas.PercentualDaReservaLegal)),
        (nameof(LimitesDoEnvelope.PrecisaoTaxaInscricao), ConfiguracaoTaxaInscricao.ValorEscala, typeof(ConfiguracaoTaxaInscricao), nameof(ConfiguracaoTaxaInscricao.Valor)),
        (nameof(LimitesDoEnvelope.PrecisaoPesoAreaEnem), 4, typeof(AreaPesoAreaEnemCongelada), nameof(AreaPesoAreaEnemCongelada.Peso)),
        (nameof(LimitesDoEnvelope.PrecisaoCorteAreaEnem), 4, typeof(AreaPesoAreaEnemCongelada), nameof(AreaPesoAreaEnemCongelada.Corte)),
    ];

    private static string CaminhoDaCopiaDoTipo(string propriedade) => $"{nameof(BancaRequerida.Tipo)}.{propriedade}";

    private static string CaminhoDaOrigemDoModelo(string propriedade) => $"{nameof(FormularioProcesso.ModeloDeOrigem)}.{propriedade}";
}
