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
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class ConformidadePublicabilidadeEstruturalTests
{
    /// <summary>A matriz legal completa (8×7), fallback AC — cada origem resolve nas outras sete, todas ofertadas.</summary>
    private static ConfiguracaoCascataRemanejamento CascataCompleta()
    {
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in OrigensFederais)
        {
            string[] ordemDestinos = [.. OrigensFederais.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(Destino(origem, i + 1, ordemDestinos[i]));
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(RegraCascataRemanejamento(), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>Só os itens de <see cref="ProcessoSeletivo.AvaliarConformidade"/> que se espera ver vermelhos.</summary>
    private static void SoEstesItensVermelhos(ProcessoSeletivo processo, params string[] itensVermelhos) =>
        SoEstesItensVermelhos(processo, ContextoDeContagemDePrazos.SemCalendario, itensVermelhos);

    private static void SoEstesItensVermelhos(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto, params string[] itensVermelhos) =>
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), contexto, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Where(static i => !i.Ok).Select(static i => i.Codigo)
            .Should().BeEquivalentTo(itensVermelhos);

    [Fact(DisplayName = "Cascata: modalidade SegueCascata fora do regime federal — item vermelho e Publicar recusa com CascataForaDoRegimeFederal")]
    public void Cascata_ForaDoRegimeFederal()
    {
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m,
            Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), regraAjuste: null, referenciaDemografica: null,
            [
                Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, 8),
                Modalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, 2),
            ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
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
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10, pr: 1m,
            Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), regraAjuste: null, referenciaDemografica: null,
            [Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, 10)], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
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
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "CERTIDAO_RESERVISTA", "Certidão de reservista", "MILITAR", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("exigencia_condicional_vazia_determina_resultado");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.CondicionalVaziaDeterminaResultado");
    }

    [Fact(DisplayName = "Pré-canonicalização: a fase deixou de responder o formulário que a exigência declara — item vermelho e Publicar recusa com FaseIncoerenteComFinalidade")]
    public void PreCanon_FaseDeixouDeResponderOFormularioDaExigencia()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid origem = processo.CronogramaFases.Single().FaseCanonicaOrigemId;
        processo.DefinirCronogramaFases([FaseBase(coletaSolicitacaoIsencao: true, faseCanonicaOrigemId: origem)], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            processo.CronogramaFases.Single().Id, Guid.CreateVersion7(), "COMPROVANTE_RENDA", "Comprovante de renda", "RENDA", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null,
            finalidade: FinalidadeFormulario.IsencaoTaxa).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseBase(faseCanonicaOrigemId: origem)], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        SoEstesItensVermelhos(processo, "exigencia_finalidade_incoerente_com_a_fase");
        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.Error!.Code.Should().Be(DocumentoExigidoErrorCodes.FaseIncoerenteComFinalidade);
    }

    [Fact(DisplayName = "Pré-canonicalização: exigência (folha) REMOVE_VANTAGEM sem vantagem viva — item vermelho e Publicar recusa com DocumentoExigido.RemoveVantagemSemVantagemViva")]
    public void PreCanon_ExigenciaRemoveVantagemSemVantagemViva()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: "REMOVE_VANTAGEM",
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // Sem DefinirBonusRegional — nenhuma vantagem viva para remover.

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("exigencia_remove_vantagem_sem_vantagem_viva");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "Pré-canonicalização: exigência (folha) com consequência incoerente com a ação da vaga — item vermelho e Publicar recusa com DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga")]
    public void PreCanon_ExigenciaConsequenciaIncoerenteComAcaoDaVaga()
    {
        ConfiguracaoDistribuicaoVagas oferta = DistribuicaoAmpla(10, acaoQuandoIndeferido: "RECLASSIFICAR_AC");
        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: "ELIMINA",
            condicoes: [], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("exigencia_consequencia_incoerente_com_acao_da_vaga");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    [Fact(DisplayName = "Pré-canonicalização: grupo OU/N-de REMOVE_VANTAGEM sem vantagem viva — item vermelho e Publicar recusa com NoExigencia.RemoveVantagemSemVantagemViva")]
    public void PreCanon_GrupoRemoveVantagemSemVantagemViva()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido documento = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        NoExigencia folha = NoExigencia.CriarFolha(documento, 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "REMOVE_VANTAGEM", [BaseLegalDoGrupo(StatusBaseLegal.Resolvido)], [folha]).Value!;
        processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("grupo_remove_vantagem_sem_vantagem_viva");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "Pré-canonicalização: grupo OU/N-de com consequência incoerente com a ação da vaga — item vermelho e Publicar recusa com NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga")]
    public void PreCanon_GrupoConsequenciaIncoerenteComAcaoDaVaga()
    {
        ConfiguracaoDistribuicaoVagas oferta = DistribuicaoAmpla(10, acaoQuandoIndeferido: "RECLASSIFICAR_AC");
        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido documento = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DOC_GERAL", "Documento geral", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        NoExigencia folha = NoExigencia.CriarFolha(documento, 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [BaseLegalDoGrupo(StatusBaseLegal.Resolvido)], [folha]).Value!;
        processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("grupo_consequencia_incoerente_com_acao_da_vaga");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga");
    }

    private static CondicaoGatilho GatilhoFaixaEtaria() =>
        CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;

    [Fact(DisplayName = "Pré-canonicalização: gatilho por FAIXA_ETARIA sem referência temporal configurada — item vermelho e Publicar recusa com ReferenciaTemporalFatosAusente")]
    public void PreCanon_ReferenciaTemporalFatosAusente()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single(static f => !f.ColetaInscricao).Id;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "DECLARACAO_MAIORIDADE", "Declaração de maioridade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [GatilhoFaixaEtaria()], basesLegais: [BaseLegalResolvida()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // Nenhuma ReferenciaTemporalFatos configurada.

        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).Should().ContainSingle(i => !i.Ok)
            .Which.Codigo.Should().Be("referencia_temporal_ausente_com_gatilho_etario");

        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosAusente");
    }
}
