namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

internal static class ApoioDaCoerenciaDaConsequencia
{
    internal static ModalidadeSelecionada NovaModalidadeComAcao(
        string codigo, NaturezaLegalModalidade naturezaLegal, ComposicaoVagasModalidade composicaoVagas, string? acaoQuandoIndeferido) =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: codigo,
            descricao: null,
            naturezaLegal: naturezaLegal,
            composicaoVagas: composicaoVagas,
            composicaoOrigemCodigo: null,
            regraRemanejamento: naturezaLegal == NaturezaLegalModalidade.CotaReservada
                ? RegraRemanejamentoModalidade.SegueCascata
                : RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(naturezaLegal),
            acaoQuandoIndeferido: acaoQuandoIndeferido,
            baseLegal: "Lei 12.711/2012",
            // Story #575: sob o regime federal (Lei 12.711), a quantidade de uma modalidade
            // DentroDaReservaLegal/RestanteDasVagasOfertadas é CALCULADA pela fórmula — informá-la é recusado
            // (ConfiguracaoDistribuicaoVagas.QuantidadeCalculadaNaoInformavel). Só a ação
            // afirmativa, cuja quantidade o edital fixa fora da fórmula, precisa do 10.
            quantidadeDeclarada: naturezaLegal == NaturezaLegalModalidade.AcaoAfirmativa ? 10 : null).Value!;

    /// <summary>
    /// A matriz legal 8×7 completa, fallback AC (Story #575) — usada por
    /// <see cref="NovoProcessoComModalidade"/> sempre que a modalidade do chamador é
    /// <see cref="NaturezaLegalModalidade.CotaReservada"/>: desde a #575, a cascata de remanejamento de cotas (UNI-REQ-0056)
    /// recusa <c>SegueCascata</c> fora do regime federal, então uma cota reservada só
    /// publica com uma cascata legítima por trás.
    /// </summary>
    internal static ConfiguracaoCascataRemanejamento CascataLegalCompleta()
    {
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in ModalidadesFederaisLei12711.Codigos)
        {
            string[] ordemDestinos = [.. ModalidadesFederaisLei12711.Codigos.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(DestinoRemanejamento.Criar(origem, i + 1, ordemDestinos[i]).Value!);
            }
        }

        ReferenciaRegra regraCascata = ReferenciaRegra.De(RegraRemanejamentoCodigo.Cascata, "v1", HashFixo);
        return ConfiguracaoCascataRemanejamento.Criar(regraCascata, ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>
    /// Variante de <see cref="NovoProcessoConforme"/> com UMA modalidade escolhida pelo
    /// chamador (Story #554, PR #903, CA-05) — para exercitar a coerência consequência↔ação
    /// da vaga contra uma natureza/ação específica, não a "AC" fixa do helper padrão.
    /// </summary>
    /// <remarks>
    /// Quando a modalidade do chamador é <see cref="NaturezaLegalModalidade.CotaReservada"/>
    /// (sempre <c>SegueCascata</c>, invariante de <c>ModalidadeSelecionada.Criar</c>), a oferta
    /// precisa ser Lei 12.711 completa (as 8 federais + AC, demografia, regra de ajuste) com
    /// uma cascata legal por trás — a cascata de remanejamento de cotas (UNI-REQ-0056, Story #575) recusa <c>SegueCascata</c> fora
    /// do regime federal. As demais naturezas (ampla, suplementar) continuam no ramo
    /// institucional simples de antes, com a única modalidade do chamador.
    /// </remarks>
    internal static ProcessoSeletivo NovoProcessoComModalidade(ModalidadeSelecionada modalidade)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            ApoioDaPublicacaoDoProcessoSeletivo.EtapaDaProvaObjetiva("Prova Objetiva", CaraterEtapa.Classificatoria, 1m, faseCodigo: "RESULTADO_FINAL"),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoDistribuicaoVagas distribuicao;
        if (modalidade.NaturezaLegal == NaturezaLegalModalidade.CotaReservada)
        {
            List<ModalidadeSelecionada> modalidadesFederais =
            [
                .. ModalidadesFederaisLei12711.Codigos
                    .Where(codigo => codigo != modalidade.Codigo)
                    .Select(codigo => ModalidadeSelecionada.Criar(
                        Guid.CreateVersion7(), codigo, null, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal,
                        null, RegraRemanejamentoModalidade.SegueCascata, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada), null, "Lei 12.711/2012", quantidadeDeclarada: null).Value!),
                modalidade,
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), ModalidadesFederaisLei12711.Ac, null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
                    null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "base legal", quantidadeDeclarada: null).Value!,
            ];
            distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: Guid.CreateVersion7(),
                voBase: 40,
                pr: 0.5m,
                regraDistribuicao: ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Lei12711, "v1", HashFixo),
                regraAjuste: ReferenciaRegra.De("RECONCILIACAO-VAGAS-ART11-PU", "v1", HashFixo),
                referenciaDemografica: ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!,
                modalidades: modalidadesFederais, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
            processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
            processo.DefinirCascataRemanejamento(CascataLegalCompleta(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }
        else
        {
            distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: Guid.CreateVersion7(),
                voBase: modalidade.QuantidadeDeclarada ?? 40,
                pr: 1m,
                regraDistribuicao: ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo),
                regraAjuste: null,
                referenciaDemografica: null,
                modalidades: [modalidade], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
            processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo),
            regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();


        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        RegrasDeAplicabilidadeDeTeste.DerivarOQueAOpcaoPelasCotasCita(processo);
        return processo;
    }

    internal static DocumentoExigido ExigenciaCondicionalPorModalidadeComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria,
        string modalidadeCodigo, string consequenciaIndeferimento, string fatoDeModalidade = "MODALIDADE") =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: tipoDocumentoCodigo,
            tipoDocumentoNome: tipoDocumentoNome,
            tipoDocumentoCategoria: tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: false,
            consequenciaIndeferimento: consequenciaIndeferimento,
            condicoes: [CondicaoGatilho.Criar(0, fatoDeModalidade, Operador.Igual, JsonSerializer.SerializeToElement(modalidadeCodigo)).Value!],
            basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;
}
