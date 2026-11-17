namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a canonicalização exige da configuração: a referência temporal dos fatos, por exemplo, lança quando não resolve, e a
/// configuração inválida viraria exceção em vez de causa nomeada. Cada conferência é independente e devolve a sua causa, com o
/// bloco do documento em que ela se corrige; uma que depende do dado que outra recusa se guarda dentro do próprio predicado.
/// </summary>
internal static class CondicoesDaCanonicalizacao
{
    private static readonly Conferencia[] Conferencias =
    [
        new("documentosExigidos", static (estado, _) => PendenciasDasExigencias.PendenciaDasExigenciasDocumentais(estado)),
        new("documentosExigidos", static (estado, _) => PendenciasDasExigencias.PendenciaDaFinalidadeDasExigencias(estado)),
        new("documentosExigidos", static (estado, cadastros) => ProtecaoDasExigencias.PendenciaDoFatoComprovadoForaDoProcesso(estado, cadastros.Protecao)),
        new("documentosExigidos", static (estado, cadastros) => ProtecaoDasExigencias.PendenciaDaProtecaoDasExigencias(estado, cadastros.Protecao)),
        new("documentosExigidos", static (estado, cadastros) => ConsequenciaDeIndeferimento.PendenciaDeCoerenciaDaConsequenciaDeIndeferimento(estado, cadastros.FatosDeModalidade)),
        new("coleta", static (estado, _) => ReferenciaTemporalDosFatos.PendenciaDaReferenciaTemporalFatos(estado)),
        new("distribuicaoVagas", static (estado, cadastros) => PendenciasDosFatos.PendenciaDasRegrasDasModalidades(estado, cadastros.FatosDeModalidade.MembroPorAgregado)),
        new("coleta", static (estado, _) => PendenciasDosFatos.PendenciaDeFatosCitados(estado)),
        new("formularios", static (estado, _) => CitacoesDosFormularios.PendenciaDeFatoAusenteDeVersaoPublicada(estado)),
        new("formularios", static (estado, _) => PendenciasDosFatos.PendenciaDeCampoOpcionalQueAlimentaRegra(estado)),
        new("documentosExigidos", static (estado, _) => PendenciasDasExigencias.PendenciaDaRepeticaoPorGrupo(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(estado.Campos, estado.Classificacao)),
        new("formularios", static (estado, _) => PendenciasDosFatos.PendenciaDeFatoColetadoSemValoresOfertados(estado)),
        new("bonusRegional", static (estado, _) => PendenciasDosFatos.PendenciaDeMunicipioDoBonusForaDaArea(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.PendenciaDeTermoSemFormaDeAceite(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.PendenciaDoFormularioDeInscricao(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.PendenciaDoConjuntoBasico(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.PendenciaDaEdicaoDoEnem(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.PendenciaDaFaseDosFormularios(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.PendenciaDaIsencaoSemTaxa(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.PendenciaDosItensForaDeSecao(estado)),
        new("formularios", static (estado, _) => PendenciasDosFormularios.PendenciaDosItensEmExcesso(estado)),
        new("distribuicaoVagas", static (estado, _) => RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(estado.DistribuicaoVagas)),
        new("distribuicaoVagas", static (estado, _) => RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(estado.DistribuicaoVagas)),
        new("distribuicaoVagas", static (estado, _) => PendenciasDaDistribuicaoDeVagas.PendenciaDasCotasDaLeiEntreOfertas(estado)),
        new("distribuicaoVagas", static (estado, _) => PendenciasDaDistribuicaoDeVagas.PendenciaDaReservaPcdSemCotaPcdDaLei(estado)),
        new("distribuicaoVagas", static (estado, _) => ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(estado)),
        new("coleta", static (estado, _) => PendenciasDosFatos.PendenciaDoGrafoConjunto(estado)),
        new("classificacao", static (estado, _) => PendenciasDoDesempate.PendenciaDosCriteriosDesempate(estado)),
    ];

    internal static IReadOnlyList<FieldError> Causas(EstadoDaConfiguracao estado, CadastrosDaConfiguracao cadastros)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(cadastros);

        return
        [
            .. Conferencias
                .Select(conferencia => (conferencia.Bloco, Pendencia: conferencia.Pendencia(estado, cadastros)))
                .Where(static par => par.Pendencia is not null)
                .Select(static par => new FieldError(par.Bloco, par.Pendencia!)),
        ];
    }

    private sealed record Conferencia(string Bloco, Func<EstadoDaConfiguracao, CadastrosDaConfiguracao, DomainError?> Pendencia);
}
