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
internal static class CondicoesParaRegistrarConfiguracao
{
    private static readonly Conferencia[] Conferencias =
    [
        Conferencia.Unica("documentosExigidos", static (configuracao, _) => PendenciasDasExigencias.PendenciaDasExigenciasDocumentais(configuracao)),
        Conferencia.Unica("documentosExigidos", static (configuracao, _) => PendenciasDasExigencias.PendenciaDoFormularioDasExigencias(configuracao)),
        Conferencia.Unica("documentosExigidos", static (configuracao, cadastros) => ProtecaoDasExigencias.PendenciaDoFatoComprovadoForaDoProcesso(configuracao, cadastros.Protecao)),
        Conferencia.Unica("documentosExigidos", static (configuracao, cadastros) => ProtecaoDasExigencias.PendenciaDaProtecaoDasExigencias(configuracao, cadastros.Protecao)),
        Conferencia.Unica("coleta", static (configuracao, _) => ReferenciaTemporalDosFatos.PendenciaDaReferenciaTemporalFatos(configuracao)),
        Conferencia.Unica("distribuicaoVagas", static (configuracao, cadastros) => PendenciasDosFatos.PendenciaDasRegrasDasModalidades(configuracao, cadastros.FatosDeModalidade.MembroPorAgregado)),
        Conferencia.Unica("coleta", static (configuracao, _) => PendenciasDosFatos.PendenciaDeFatosCitados(configuracao)),
        Conferencia.Unica("formularios", static (configuracao, cadastros) => PendenciasDosFormularios.PendenciaDeFatoJaColetadoRemovido(configuracao, cadastros.FatosDasVersoesPublicadas)),
        Conferencia.Unica("formularios", static (configuracao, cadastros) => CitacoesDosFormularios.PendenciaDeFatoAusenteDeVersaoPublicada(configuracao, cadastros.FatosDasVersoesPublicadas)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFatos.PendenciaDeCampoOpcionalQueAlimentaRegra(configuracao)),
        Conferencia.Unica("documentosExigidos", static (configuracao, _) => PendenciasDasExigencias.PendenciaDaRepeticaoPorGrupo(configuracao)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(configuracao.Coleta.Campos, configuracao.Avaliacao.Classificacao)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFatos.PendenciaDeFatoColetadoSemValoresOfertados(configuracao)),
        Conferencia.Unica("fada", static (configuracao, _) => PendenciasDosFatos.PendenciaDeMunicipioDoFadaForaDaArea(configuracao)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDeTermoSemFormaDeAceite(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciasDaInscricao(configuracao)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDoConjuntoBasico(configuracao)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDaEdicaoDoEnem(configuracao)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDaFaseDosFormularios(configuracao)),
        new("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciasDaIsencao(configuracao)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDosItensForaDeSecao(configuracao)),
        Conferencia.Unica("formularios", static (configuracao, _) => PendenciasDosFormularios.PendenciaDosItensEmExcesso(configuracao)),
        Conferencia.Unica("distribuicaoVagas", static (configuracao, _) => RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(configuracao.Vagas.DistribuicaoVagas)),
        Conferencia.Unica("distribuicaoVagas", static (configuracao, _) => RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(configuracao.Vagas.DistribuicaoVagas)),
        Conferencia.Unica("distribuicaoVagas", static (configuracao, _) => PendenciasDaDistribuicaoDeVagas.PendenciaDasCotasDaLeiEntreOfertas(configuracao)),
        Conferencia.Unica("distribuicaoVagas", static (configuracao, _) => ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(configuracao)),
        Conferencia.Unica("coleta", static (configuracao, _) => PendenciasDosFatos.PendenciaDoGrafoConjunto(configuracao)),
        Conferencia.Unica("classificacao", static (configuracao, _) => PendenciasDoDesempate.PendenciaDosCriteriosDesempate(configuracao)),
    ];

    internal static IReadOnlyList<FieldError> Causas(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(cadastros);

        return
        [
            .. Conferencias.SelectMany(conferencia => conferencia.Pendencias(configuracao, cadastros)
                .Select(pendencia => new FieldError(conferencia.Bloco, pendencia))),
        ];
    }

    private sealed record Conferencia(string Bloco, Func<ConfiguracaoDeclarada, CadastrosDaConfiguracao, IEnumerable<DomainError>> Pendencias)
    {
        internal static Conferencia Unica(string bloco, Func<ConfiguracaoDeclarada, CadastrosDaConfiguracao, DomainError?> pendencia) =>
            new(bloco, (configuracao, cadastros) => pendencia(configuracao, cadastros) is { } causa ? [causa] : []);
    }
}
