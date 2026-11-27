namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As cópias que a distribuição de vagas, o atendimento, o FADA e as exigências guardam do que o cadastro de Configuração dá hoje.
/// Cada leitura de cadastro e cada comando que resolve o código usam estas, para que o rascunho e a configuração por documento
/// copiem o mesmo.
/// </summary>
internal static class CopiaDosCadastrosDasVagas
{
    internal static OfertaDeCursoCitada Oferta(OfertaCursoView oferta) => new(
        oferta.Id,
        oferta.Codigo,
        oferta.CursoNome,
        oferta.CursoGrau,
        oferta.UnidadeOfertanteNome,
        oferta.Turnos,
        oferta.VagasAnuaisAutorizadas,
        oferta.GrupoAreaEnem?.Codigo,
        oferta.GrupoAreaEnem?.Rotulo);

    internal static ModalidadeNoCadastro Modalidade(ModalidadeView view) => new()
    {
        Id = view.Id,
        Codigo = view.Codigo,
        Descricao = view.Descricao,
        NaturezaLegal = CodigoReconhecido<NaturezaLegalModalidade>(view.NaturezaLegal),
        ComposicaoVagas = CodigoReconhecido<ComposicaoVagasModalidade>(view.ComposicaoVagas),
        ComposicaoOrigemCodigo = view.ComposicaoOrigem,
        RegraRemanejamento = view.RegraRemanejamento is { } regra ? CatalogoDe<RegraRemanejamentoModalidade>.Obter(regra) : null,
        RemanejamentoDestino = view.RemanejamentoDestino,
        RemanejamentoPar = view.RemanejamentoPar,
        RemanejamentoFallback = view.RemanejamentoFallback,
        RegraDeAplicabilidade = view.RegraDeAplicabilidade,
        AcaoQuandoIndeferido = view.AcaoQuandoIndeferido,
        BaseLegal = view.BaseLegal ?? string.Empty,
    };

    internal static Result<ReferenciaReservaDemograficaSnapshot> ReferenciaDemografica(ReferenciaReservaDemograficaView view) =>
        ReferenciaReservaDemograficaSnapshot.Criar(
            view.Id, view.CensoReferencia, view.PpiPercentual, view.QuilombolaPercentual, view.PcdPercentual, view.BaseLegal);

    internal static OpcaoDeAtendimentoCitada Condicao(CondicaoAtendimentoView view) => new(view.Id, view.Codigo, view.Nome);

    internal static OpcaoDeAtendimentoCitada Recurso(RecursoAcessibilidadeView view) => new(view.Id, view.Codigo, view.Nome);

    internal static OpcaoDeAtendimentoCitada Deficiencia(TipoDeficienciaView view) => new(view.Id, view.Codigo, view.Nome);

    internal static CopiaDoTipoDeDocumento TipoDeDocumento(TipoDocumentoView tipo) =>
        new(tipo.Id, tipo.Codigo, tipo.Nome, tipo.Categoria, new ProtecaoDoDocumento(tipo.ClassificacaoProtecao, tipo.FinalidadeTratamento, tipo.HipoteseLegal));

    internal static BaseLegalCongelada BaseLegalDoFada(BaseLegalDoFadaView baseLegal) => new(
        baseLegal.Id,
        baseLegal.Codigo,
        baseLegal.TipoInstrumento,
        baseLegal.Identificacao,
        baseLegal.Descricao,
        [.. baseLegal.Municipios.Select(static municipio => (municipio.CodigoIbge, municipio.Nome, municipio.Uf))]);

    private static TEnum? CodigoReconhecido<TEnum>(string codigo)
        where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.TentarObter(codigo, out TEnum valor) ? valor : null;
}
