namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed partial class ProcessoSeletivoRestaurarConfiguracaoTests
{
    [Fact(DisplayName = "Árvore restaurada com base legal de grupo sem consequência é recusada")]
    public void ArvoreComBaseLegalDeGrupoSemConsequencia_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia folha = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        NoExigenciaBaseLegal baseLegal = NoExigenciaBaseLegal.Criar(
            "Lei 12.711/2012", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;
        // Consequencia null + base legal presente — NoExigencia.CriarGrupo recusa isto
        // (BaseLegalSemConsequencia); Reidratar não revalida.
        NoExigencia grupoOu = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoOu, 0, null, null, 1, null, null, null, null, null, [baseLegal], [folha]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoOu, folha]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.BaseLegalSemConsequencia");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    // ── Mais três invariantes de CriarFolha/CriarGrupo que Reidratar não revalida, fechados
    // pela reconstrução via ReconstruirNoParaValidarInvariantes. ──

    [Fact(DisplayName = "Árvore restaurada com folha de cardinalidade qualificada incoerente é recusada")]
    public void ArvoreComFolhaCardinalidadeIncoerente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        // chaveDistincao COMPETENCIA_MENSAL exige dataReferencia — CriarFolha recusa isto
        // (DataReferenciaObrigatoriaParaChaveCalendario); Reidratar não revalida.
        NoExigencia folhaIncoerente = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null,
            ChaveDistincao.CompetenciaMensal, null, null, null, [], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaIncoerente]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "NoExigencia.Reidratar não revalida a coerência chaveDistincao×dataReferencia que CriarFolha garante " +
            "— sem a reconstrução, a folha reidrataria e recanonicalizaria nos MESMOS bytes inválidos, provando o " +
            "round-trip, e SlotsEsperados() desreferenciaria DataReferencia nula em runtime");
        resultado.Error!.Code.Should().Be("NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com repetição por entidade aninhada é recusada")]
    public void ArvoreComRepeticaoPorEntidadeAninhada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        // Folha E grupo pai ambos repetePorEntidade — CriarGrupo recusa isto
        // (RepeticaoDeEntidadeAninhada); Reidratar não revalida.
        NoExigencia folhaRepetida = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null,
            "COMPOSICAO_FAMILIAR", [], []);
        NoExigencia grupoRepetido = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null, null, null, null,
            "PESSOAS_JURIDICAS", [], [folhaRepetida]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoRepetido, folhaRepetida]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "repetição não aninha (Story #922) — CriarGrupo recusa um nó repetido com descendente também repetido, " +
            "e Reidratar não revalida isso");
        resultado.Error!.Code.Should().Be("NoExigencia.RepeticaoDeEntidadeAninhada");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com duas folhas para o mesmo DocumentoExigido é recusada")]
    public void ArvoreComDocumentoExigidoDuplicado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        // Duas raízes-folha distintas apontando para o MESMO DocumentoExigido — a checagem
        // de "folha referencia um documento que existe" (HashSet) não pega isto sozinha;
        // ux_nos_exigencia_documento_exigido_id só falharia depois, no SaveChanges.
        NoExigencia folhaA = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        NoExigencia folhaB = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 1, documento.Id, documento, 1, null, null, null, null, null, [], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaA, folhaB]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaDocumentoExigidoDuplicado");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com folha de quantidadeMinima nula é recusada — CriarFolha a normalizaria em silêncio")]
    public void ArvoreComFolhaQuantidadeMinimaNula_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        // quantidadeMinima: null — CriarFolha normalizaria para QuantidadeMinimaPadrao (1) em
        // vez de recusar, e a RECONSTRUÇÃO sozinha sucederia; mas o nó DECODIFICADO (aplicado
        // ao agregado) continuaria com null, violando ck_nos_exigencia_tipo_campos_coerentes
        // no SaveChanges. ValidarCanonicidade compara reconstruído × decodificado e recusa
        // antes disso.
        NoExigencia folhaSemQuantidade = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, null, null, null, null, null, null, [], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaSemQuantidade]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "CriarFolha normaliza quantidadeMinima nula para o padrão (1) em vez de recusar — a reconstrução " +
            "sozinha não pega isso; é a comparação contra o nó decodificado que fecha a lacuna");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaQuantidadeMinimaAusente");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com grupo carregando chaveDistincao (campo exclusivo de folha) é recusada")]
    public void ArvoreComGrupoCarregandoChaveDistincao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia folha = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        // CriarGrupo nem aceita chaveDistincao como parâmetro — a reconstrução simplesmente
        // ignoraria este campo em vez de recusar, e o nó DECODIFICADO (com chaveDistincao
        // preenchida num grupo) é que seria aplicado ao agregado.
        NoExigencia grupoComChave = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null,
            ChaveDistincao.Ocorrencia, null, null, null, [], [folha]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoComChave, folha]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "chaveDistincao/dataReferencia/ocorrenciasEsperadas são exclusivos de folha — CriarGrupo nem os aceita " +
            "como parâmetro, então a reconstrução por si só não pega um grupo decodificado que os carrega");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaGrupoComCampoDeFolha");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com folha carregando base legal (campo exclusivo de grupo) é recusada")]
    public void ArvoreComFolhaCarregandoBaseLegal_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigenciaBaseLegal baseLegal = NoExigenciaBaseLegal.Criar(
            "Lei 12.711/2012", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null).Value!;
        // CriarFolha nem aceita basesLegais como parâmetro — base legal própria é exclusiva
        // de grupo OU/N-de. A reconstrução simplesmente a ignoraria em vez de recusar.
        NoExigencia folhaComBaseLegal = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null,
            [baseLegal], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaComBaseLegal]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "base legal própria é exclusiva de grupo OU/N-de — CriarFolha nem a aceita como parâmetro, então a " +
            "reconstrução por si só não pega uma folha decodificada que a carrega");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaFolhaComBaseLegal");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "A etapa que NÃO existe mais é reinserida com o Id congelado")]
    public void EtapaAusente_EReinseridaComOIdCongelado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaProcesso.Reidratar(EtapaCongelada, "Etapa Que Voltou", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva),
        ]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        processo.Etapas.Should().ContainSingle()
            .Which.Id.Should().Be(EtapaCongelada,
                "o Id congelado é preservado mesmo quando a etapa foi removida durante a sessão editorial — é ele " +
                "que o etapaRef do desempate e da eliminação referenciam");
    }

    [Fact(DisplayName = "Story #554/issue #547 — restauração limpa DocumentosExigidos configurados durante a sessão")]
    public void Restauracao_LimpaDocumentosExigidosDaSessao()
    {
        // O bloco documentosExigidos.exigencias do envelope ainda é stub (PR #895..PR #900) —
        // GrafoConfiguracao não tem como reconstruir a coleção a partir de bytes que não
        // a contêm. A guarda B-01 garante que TODA versão já congelada tem zero
        // DocumentoExigido; a restauração precisa repor esse mesmo estado vazio, mesmo
        // que a sessão editorial tenha configurado exigências.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Guid faseId = processo.CronogramaFases.Single().Id;

        processo.AbrirRetificacao("Incluir exigência documental", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Should().ContainSingle();

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty(
            "a versão congelada nunca poderia ter sido publicada com exigência configurada (B-01) — restaurar " +
            "precisa repor esse estado vazio, não preservar o que a sessão descartada editou");
    }

    [Fact(DisplayName = "Story #554/issue #892 — restauração limpa ReferenciaTemporalFatos definida durante a sessão")]
    public void Restauracao_LimpaReferenciaTemporalFatosDaSessao()
    {
        // Mesmo raciocínio de Restauracao_LimpaDocumentosExigidosDaSessao: o campo não é
        // materializado no envelope (isso é da PR #903), então não há valor congelado para
        // restaurar — e a versão congelada nunca teve gatilho por FAIXA_ETARIA que
        // dependesse dele (B-01 barra qualquer DocumentoExigido). Preservar o valor
        // editado pela sessão descartada vazaria a mutação não publicada.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Guid faseId = processo.CronogramaFases.Single().Id;

        processo.AbrirRetificacao("Ajustar referência temporal", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, faseId).Value!;
        processo.DefinirReferenciaTemporalFatos(referencia, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.ReferenciaTemporalFatos.Should().NotBeNull();

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ReferenciaTemporalFatos.Should().BeNull(
            "a versão congelada não materializa este campo (PR #903) e nunca dependeu dele — restaurar precisa " +
            "repor a ausência, não preservar o que a sessão descartada editou");
    }

    // ── issue #563 — CA-13: AplicarGrafo repõe ConfiguracaoDivulgacao campo a campo (bloco
    // ampliado) ou como ausência (bloco default, D5) — nunca preserva o que a sessão editou. ──

    [Fact(DisplayName = "issue #563 (CA-13, bloco ampliado): restaurar repõe ConfiguracaoDivulgacao campo a campo, substituindo o que a sessão editorial gravou")]
    public void Restauracao_RepoeConfiguracaoDivulgacaoAmpliadaCampoACampo()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        // A sessão editorial grava OUTRA configuração — é o estado que a restauração tem de
        // substituir, não preservar.
        processo.AbrirRetificacao("Editar a divulgação", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ConfiguracaoDivulgacao congelada = ConfiguracaoDivulgacao.Criar(
            ["numero_inscricao", "nome"], "Justificativa da versão congelada.").Value!;

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo(configuracaoDivulgacao: congelada));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ConfiguracaoDivulgacao.Should().NotBeNull();
        processo.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(congelada.CamposPublicos);
        processo.ConfiguracaoDivulgacao.Justificativa.Should().Be("Justificativa da versão congelada.");
        processo.ConfiguracaoDivulgacao.ProcessoSeletivoId.Should().Be(processo.Id);
    }
}
