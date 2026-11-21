namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
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
        new("documentosExigidos", static (configuracao, _) => PendenciasDasExigencias.PendenciaDasExigenciasDocumentais(configuracao)),
        new("documentosExigidos", static (configuracao, _) => PendenciasDasExigencias.PendenciaDaFinalidadeDasExigencias(configuracao)),
        new("documentosExigidos", static (configuracao, cadastros) => ProtecaoDasExigencias.PendenciaDoFatoComprovadoForaDoProcesso(configuracao, cadastros.Protecao)),
        new("documentosExigidos", static (configuracao, cadastros) => ProtecaoDasExigencias.PendenciaDaProtecaoDasExigencias(configuracao, cadastros.Protecao)),
        new("documentosExigidos", static (configuracao, cadastros) => ConsequenciaDeIndeferimento.PendenciaDeCoerenciaDaConsequenciaDeIndeferimento(configuracao, cadastros.FatosDeModalidade)),
        new("coleta", static (configuracao, _) => ReferenciaTemporalDosFatos.PendenciaDaReferenciaTemporalFatos(configuracao)),
        new("distribuicaoVagas", static (configuracao, cadastros) => PendenciasDosFatos.PendenciaDasRegrasDasModalidades(configuracao, cadastros.FatosDeModalidade.MembroPorAgregado)),
        new("coleta", static (configuracao, _) => PendenciasDosFatos.PendenciaDeFatosCitados(configuracao)),
        new("formularios", static (configuracao, cadastros) => PendenciasDosFormularios.PendenciaDeFatoJaColetadoRemovido(configuracao, cadastros.FatosDasVersoesPublicadas)),
        new("formularios", static (configuracao, cadastros) => CitacoesDosFormularios.PendenciaDeFatoAusenteDeVersaoPublicada(configuracao, cadastros.FatosDasVersoesPublicadas)),
        new("formularios", static (configuracao, _) => PendenciasDosFatos.PendenciaDeCampoOpcionalQueAlimentaRegra(configuracao)),
        new("documentosExigidos", static (configuracao, _) => PendenciasDasExigencias.PendenciaDaRepeticaoPorGrupo(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(configuracao.Coleta.Campos, configuracao.Avaliacao.Classificacao)),
        new("formularios", static (configuracao, _) => PendenciasDosFatos.PendenciaDeFatoColetadoSemValoresOfertados(configuracao)),
        new("bonusRegional", static (configuracao, _) => PendenciasDosFatos.PendenciaDeMunicipioDoBonusForaDaArea(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDeTermoSemFormaDeAceite(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDoFormularioDeInscricao(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDoConjuntoBasico(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDaEdicaoDoEnem(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDaFaseDosFormularios(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDaIsencaoSemTaxa(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDosItensForaDeSecao(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDosItensEmExcesso(configuracao)),
        new("distribuicaoVagas", static (configuracao, _) => RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(configuracao.Vagas.DistribuicaoVagas)),
        new("distribuicaoVagas", static (configuracao, _) => RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(configuracao.Vagas.DistribuicaoVagas)),
        new("distribuicaoVagas", static (configuracao, _) => PendenciasDaDistribuicaoDeVagas.PendenciaDasCotasDaLeiEntreOfertas(configuracao)),
        new("distribuicaoVagas", static (configuracao, _) => ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(configuracao)),
        new("coleta", static (configuracao, _) => PendenciasDosFatos.PendenciaDoGrafoConjunto(configuracao)),
        new("classificacao", static (configuracao, _) => PendenciasDoDesempate.PendenciaDosCriteriosDesempate(configuracao)),
    ];

    internal static IReadOnlyList<FieldError> Causas(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(cadastros);

        return
        [
            .. Conferencias
                .Select(conferencia => (conferencia.Bloco, Pendencia: conferencia.Pendencia(configuracao, cadastros)))
                .Where(static par => par.Pendencia is not null)
                .Select(static par => new FieldError(par.Bloco, par.Pendencia!)),
        ];
    }

    private sealed record Conferencia(string Bloco, Func<ConfiguracaoDeclarada, CadastrosDaConfiguracao, DomainError?> Pendencia);
}
