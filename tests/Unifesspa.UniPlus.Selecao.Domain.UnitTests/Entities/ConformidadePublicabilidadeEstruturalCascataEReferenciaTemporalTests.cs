namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

public sealed class ConformidadePublicabilidadeEstruturalCascataEReferenciaTemporalTests
{
    [Fact(DisplayName = "Cascata: modalidade SegueCascata fora do regime federal — item vermelho e Publicar recusa com CascataForaDoRegimeFederal")]
    public void Cascata_ForaDoRegimeFederal()
    {
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 10,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
                Modalidades = [
                Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, 8),
                Modalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, 2),
            ]
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        ProcessoSeletivo processo = ProcessoConforme([oferta]);

        // A cascata (item agregado, comportamento pré-existente) TAMBÉM vai a vermelho —
        // ela já cobria este código antes da issue #1092; o item NOVO é o detalhamento.
        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_modalidade_fora_do_regime_federal");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataForaDoRegimeFederal");
    }

    [Fact(DisplayName = "Cascata: oferta federal SegueCascata sem cascata configurada — item vermelho e Publicar recusa com CascataOrigemAusente")]
    public void Cascata_OrigemAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaFederalCompleta()]);
        // Nenhuma DefinirCascataRemanejamento — a origem exigida não tem cascata configurada.

        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_origem_ausente");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataOrigemAusente");
    }

    [Fact(DisplayName = "Cascata: fallback fora das modalidades da oferta — item vermelho e Publicar recusa com CascataFallbackNaoSelecionadoNaOferta")]
    public void Cascata_FallbackNaoSelecionadoNaOferta()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaFederalCompleta()]);
        ConfiguracaoCascataRemanejamento cascataComFallbackInexistente = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascataRemanejamento(), "FALLBACK_INEXISTENTE",
            [.. CascataCompleta().Destinos]).Value!;
        processo.DefinirCascataRemanejamento(cascataComFallbackInexistente, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_fallback_nao_ofertado");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta");
    }

    [Fact(DisplayName = "Cascata: origem declarada que nenhuma oferta marca SegueCascata — item vermelho e Publicar recusa com CascataOrigemNaoSegueCascata")]
    public void Cascata_OrigemNaoSegueCascata()
    {
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 10, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, 10)] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        // Nenhuma modalidade desta oferta é SegueCascata — o loop por oferta não recusa nada; só
        // a checagem global (origem declarada × origens SegueCascata em alguma oferta) alcança.
        processo.DefinirCascataRemanejamento(
            ConfiguracaoCascataRemanejamento.Criar(
                RegraCascataRemanejamento(), "AC", [Destino(ModalidadesFederaisLei12711.LbPpi, 1, "AC")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_origem_nao_segue_cascata");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataOrigemNaoSegueCascata");
    }

    [Fact(DisplayName = "Cascata: destino declarado que não é modalidade de nenhuma oferta — item vermelho e Publicar recusa com CascataDestinoDesconhecido")]
    public void Cascata_DestinoDesconhecido()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaFederalCompleta()]);
        // A matriz legal completa resolve todas as origens dentro da oferta (o loop por oferta
        // não recusa nada); LB_PPI ganha um OITAVO destino (ordem contígua) apontando para um
        // código que não é modalidade de nenhuma oferta — só a checagem GLOBAL, que roda depois
        // do loop por oferta, alcança esse destino sobressalente. LB_Q perde um destino legal
        // (fica com 6, ainda contíguo a partir de 1 — os 6 restantes continuam todos resolvíveis
        // nesta oferta) para abrir espaço sob o teto de 56 destinos (7×7 + 6 + 1 = 56).
        List<DestinoRemanejamento> destinos =
        [
            .. CascataCompleta().Destinos.Where(d => !(d.ModalidadeOrigemCodigo == ModalidadesFederaisLei12711.LbQ && d.Ordem == 7)),
            Destino(ModalidadesFederaisLei12711.LbPpi, 8, "CODIGO_FORA_DA_OFERTA"),
        ];
        processo.DefinirCascataRemanejamento(
            ConfiguracaoCascataRemanejamento.Criar(RegraCascataRemanejamento(), ModalidadesFederaisLei12711.Ac, destinos).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(
            processo, "cascata_pendente", "cascata_destino_desconhecido");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CascataDestinoDesconhecido");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // PendenciaPreCanonicalizacao — doze razões (Story #554/#920/#927/#928)
    // ══════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Pré-canonicalização: exigência CONDICIONAL vazia que determina resultado — item vermelho e Publicar recusa com CondicionalVaziaDeterminaResultado")]
    public void PreCanon_CondicionalVaziaDeterminaResultado()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        ExigirFolhas(processo, DocumentoCondicional(FaseDoResultado(processo), "CERTIDAO_RESERVISTA", "Certidão de reservista", "MILITAR"));

        AfirmarPendenciaUnicaERecusa(processo, "exigencia_condicional_vazia_determina_resultado", "DocumentoExigido.CondicionalVaziaDeterminaResultado");
    }

    [Fact(DisplayName = "Pré-canonicalização: a fase deixou de responder o formulário que a exigência declara — item vermelho e Publicar recusa com FaseIncoerenteComFinalidade")]
    public void PreCanon_FaseDeixouDeResponderOFormularioDaExigencia()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid origem = processo.CronogramaFases.Single().FaseCanonicaOrigemId;
        processo.DefinirCronogramaFases([FaseBase(coletaSolicitacaoIsencao: true, faseCanonicaOrigemId: origem)], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        ExigirFolhas(processo, DocumentoGeral(processo.CronogramaFases.Single().Id, obrigatorio: false, consequencia: null, finalidade: FinalidadeFormulario.IsencaoTaxa));
        processo.DefinirCronogramaFases([FaseBase(faseCanonicaOrigemId: origem)], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo, "exigencia_finalidade_incoerente_com_a_fase");
        ExigirPublicacaoRecusada(processo, DocumentoExigidoErrorCodes.FaseIncoerenteComFinalidade);
    }

    [Fact(DisplayName = "Pré-canonicalização: exigência (folha) REMOVE_VANTAGEM sem vantagem viva — item vermelho e Publicar recusa com DocumentoExigido.RemoveVantagemSemVantagemViva")]
    public void PreCanon_ExigenciaRemoveVantagemSemVantagemViva()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        ExigirFolhas(processo, DocumentoGeral(FaseDoResultado(processo), obrigatorio: true, "REMOVE_VANTAGEM", [BaseLegalResolvida()]));
        // Sem DefinirBonusRegional — nenhuma vantagem viva para remover.

        AfirmarPendenciaUnicaERecusa(processo, "exigencia_remove_vantagem_sem_vantagem_viva", "DocumentoExigido.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "Pré-canonicalização: exigência (folha) com consequência incoerente com a ação da vaga — item vermelho e Publicar recusa com DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga")]
    public void PreCanon_ExigenciaConsequenciaIncoerenteComAcaoDaVaga()
    {
        ProcessoSeletivo processo = ProcessoConforme([DistribuicaoAmpla(10, acaoQuandoIndeferido: "RECLASSIFICAR_AC")]);
        ExigirFolhas(processo, DocumentoGeral(FaseDoResultado(processo), obrigatorio: true, "ELIMINA", [BaseLegalResolvida()]));

        AfirmarPendenciaUnicaERecusa(processo, "exigencia_consequencia_incoerente_com_acao_da_vaga", "DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "Pré-canonicalização: grupo OU/N-de REMOVE_VANTAGEM sem vantagem viva — item vermelho e Publicar recusa com NoExigencia.RemoveVantagemSemVantagemViva")]
    public void PreCanon_GrupoRemoveVantagemSemVantagemViva()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        ExigirGrupoOuDeUmDocumento(processo, "REMOVE_VANTAGEM");

        AfirmarPendenciaUnicaERecusa(processo, "grupo_remove_vantagem_sem_vantagem_viva", "NoExigencia.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "Pré-canonicalização: grupo OU/N-de com consequência incoerente com a ação da vaga — item vermelho e Publicar recusa com NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga")]
    public void PreCanon_GrupoConsequenciaIncoerenteComAcaoDaVaga()
    {
        ProcessoSeletivo processo = ProcessoConforme([DistribuicaoAmpla(10, acaoQuandoIndeferido: "RECLASSIFICAR_AC")]);
        ExigirGrupoOuDeUmDocumento(processo, "ELIMINA");

        AfirmarPendenciaUnicaERecusa(processo, "grupo_consequencia_incoerente_com_acao_da_vaga", "NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "Pré-canonicalização: gatilho por FAIXA_ETARIA sem referência temporal configurada — item vermelho e Publicar recusa com ReferenciaTemporalFatosAusente")]
    public void PreCanon_ReferenciaTemporalFatosAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        ExigirFolhas(processo, DocumentoCondicional(FaseDoResultado(processo), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", GatilhoFaixaEtaria()));
        // Nenhuma ReferenciaTemporalFatos configurada.

        AfirmarPendenciaUnicaERecusa(processo, "referencia_temporal_ausente_com_gatilho_etario", "ProcessoSeletivo.ReferenciaTemporalFatosAusente");
    }
}
