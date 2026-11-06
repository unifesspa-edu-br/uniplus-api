namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A ordem única dos portões que uma configuração precisa passar para ser congelada. Publicar,
/// retificar de uma vez e fechar a sessão recusam na mesma sequência, e a primeira pendência
/// decide o código que o cliente vê.
/// </summary>
public static class ProntidaoParaCongelar
{
    /// <summary>Os portões, nomeados na ordem em que são avaliados.</summary>
    internal sealed record Portoes(
        Func<DomainError?> IdentificadorLegivel,
        Func<DomainError?> Localidade,
        Func<DomainError?> AlgoritmoDeContagem,
        Func<DomainError?> CalendarioVigente,
        Func<DomainError?> Conformidade,
        Func<DomainError?> Cronograma,
        Func<DomainError?> TipoDeAtoUnico,
        Func<DomainError?> ResultadoSemPapel,
        Func<DomainError?> TipoQueCongelaDeclaradoComoResultado,
        Func<DomainError?> Cascata,
        Func<DomainError?> PreCanonicalizacao)
    {
        /// <summary>Os onze portões do processo para este pedido, sobre a vista da configuração.</summary>
        internal static Portoes Para(EstadoDaConfiguracao estado, PedidoDeCongelamento pedido) => new(
            IdentificadorLegivel: () => ContagemDePrazos.PendenciaDoIdentificadorLegivel(estado),
            Localidade: () => ContagemDePrazos.PendenciaDaLocalidade(estado),
            AlgoritmoDeContagem: () => ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(estado),
            CalendarioVigente: () => ContagemDePrazos.PendenciaDoCalendarioVigente(estado, pedido.Contexto),
            Conformidade: () => ChecklistDeConformidade.PendenciaDeConformidade(estado),
            Cronograma: () => PendenciasDoCronograma.PendenciaDoCronograma(estado, pedido.Contexto.FusoInstitucional),
            TipoDeAtoUnico: () => TiposDeAtoDoCronograma.PendenciaDoTipoDeAtoUnico(estado, pedido.TiposDeAtoUnicos),
            ResultadoSemPapel: () => TiposDeAtoDoCronograma.PendenciaDoResultadoSemPapel(estado, pedido.TiposDeAtoUnicos),
            TipoQueCongelaDeclaradoComoResultado: () => TiposDeAtoDoCronograma.PendenciaDoTipoQueCongelaDeclaradoComoResultado(estado, pedido.TiposDeAtoUnicos),
            Cascata: () => CoberturaDaCascata.PendenciaDaCascata(estado),
            PreCanonicalizacao: () => PendenciaPreCanonicalizacao(estado, pedido.FatosDeModalidade));
    }

    /// <summary>A primeira pendência, ou nulo se todos passam. Para no primeiro portão que recusa.</summary>
    internal static DomainError? PrimeiraPendencia(Portoes portoes)
    {
        ArgumentNullException.ThrowIfNull(portoes);

        Func<DomainError?>[] emOrdem =
        [
            portoes.IdentificadorLegivel,
            portoes.Localidade,
            portoes.AlgoritmoDeContagem,
            portoes.CalendarioVigente,
            portoes.Conformidade,
            portoes.Cronograma,
            portoes.TipoDeAtoUnico,
            portoes.ResultadoSemPapel,
            portoes.TipoQueCongelaDeclaradoComoResultado,
            portoes.Cascata,
            portoes.PreCanonicalizacao,
        ];

        return emOrdem.Select(static portao => portao()).FirstOrDefault(static pendencia => pendencia is not null);
    }

    /// <summary>
    /// Os portões que o handler alcança antes de canonicalizar: a canonicalização lança quando a
    /// referência temporal dos fatos não resolve, e a publicação inválida viraria exceção em vez do
    /// erro nomeado que a transição devolveria depois. A ordem é a do checklist de conformidade.
    /// </summary>
    public static DomainError? PendenciaPreCanonicalizacao(
        EstadoDaConfiguracao estado, FatosDeModalidade fatosDeModalidade, ProtecaoVigenteDasExigencias? protecao = null)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);

        Func<DomainError?>[] emOrdem =
        [
            () => PendenciasDasExigencias.PendenciaDasExigenciasDocumentais(estado),
            () => PendenciasDasExigencias.PendenciaDaFinalidadeDasExigencias(estado),
            () => ProtecaoDasExigencias.PendenciaDoFatoComprovadoForaDoProcesso(estado, protecao),
            () => ProtecaoDasExigencias.PendenciaDaProtecaoDasExigencias(estado, protecao),
            () => ConsequenciaDeIndeferimento.PendenciaDeCoerenciaDaConsequenciaDeIndeferimento(estado, fatosDeModalidade),
            () => ReferenciaTemporalDosFatos.PendenciaDaReferenciaTemporalFatos(estado),
            () => PendenciasDosFatos.PendenciaDasRegrasDasModalidades(estado, fatosDeModalidade.MembroPorAgregado),
            () => PendenciasDosFatos.PendenciaDeFatosCitados(estado),
            () => CitacoesDosFormularios.PendenciaDeFatoAusenteDeVersaoPublicada(estado),
            () => PendenciasDosFatos.PendenciaDeCampoOpcionalQueAlimentaRegra(estado),
            () => PendenciasDasExigencias.PendenciaDaRepeticaoPorGrupo(estado),
            // Antes da falta de opções: fora do ENEM, a edição não tem opções a declarar, e o que se
            // corrige é o campo ou a classificação.
            () => PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(estado.Campos, estado.Classificacao),
            () => PendenciasDosFatos.PendenciaDeFatoColetadoSemValoresOfertados(estado),
            () => PendenciasDosFatos.PendenciaDeMunicipioDoBonusForaDaArea(estado),
            () => PendenciasDosFormularios.PendenciaDeTermoSemFormaDeAceite(estado),
            () => PendenciasDosFormularios.PendenciaDoFormularioDeInscricao(estado),
            () => PendenciasDosFormularios.PendenciaDoConjuntoBasico(estado),
            () => PendenciasDosFormularios.PendenciaDaEdicaoDoEnem(estado),
            () => PendenciasDosFormularios.PendenciaDaFaseDosFormularios(estado),
            () => PendenciasDosFormularios.PendenciaDaIsencaoSemTaxa(estado),
            () => PendenciasDosFormularios.PendenciaDosItensForaDeSecao(estado),
            () => PendenciasDosFormularios.PendenciaDosItensEmExcesso(estado),
            () => RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(estado.DistribuicaoVagas),
            () => RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(estado.DistribuicaoVagas),
            () => PendenciasDaDistribuicaoDeVagas.PendenciaDasCotasDaLeiEntreOfertas(estado),
            () => PendenciasDaDistribuicaoDeVagas.PendenciaDaReservaPcdSemCotaPcdDaLei(estado),
            () => ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(estado),
            () => PendenciasDosFatos.PendenciaDoGrafoConjunto(estado),
            () => PendenciasDoDesempate.PendenciaDosCriteriosDesempate(estado),
        ];

        return emOrdem.Select(static portao => portao()).FirstOrDefault(static pendencia => pendencia is not null);
    }
}
