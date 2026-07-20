namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Reposição da configuração congelada: uma restauração recusada não pode alterar nada do
/// agregado (tudo ou nada), e a identidade das etapas reconciliadas segue a regra de
/// preservação de <c>Id</c> por referência de negócio (Story #859 critério de aceite sobre
/// restauração tudo-ou-nada; ADR-0110 decisão sobre identidade na reidratação).
/// </summary>
/// <remarks>
/// A propriedade central aqui é <b>tudo ou nada</b>: uma restauração que falha não pode
/// deixar o agregado meio-reposto. Se a validação fosse feita dimensão a dimensão,
/// enquanto se aplica, um grafo que falhasse na <b>última</b> checagem já teria trocado
/// etapas e distribuição — e o certame ficaria numa configuração que <b>nunca existiu</b>:
/// nem a viva, nem a congelada.
/// </remarks>
public sealed class ProcessoSeletivoRestaurarConfiguracaoTests
{
    private static readonly Guid EtapaOriginal = new("aaaa0000-0000-4000-8000-000000000001");
    private static readonly Guid EtapaCongelada = new("aaaa0000-0000-4000-8000-000000000002");

    [Fact(DisplayName = "Uma restauração que falha na ÚLTIMA validação não altera NADA")]
    public void RestauracaoQueFalha_NaoAlteraEstado()
    {
        // A falha é TARDIA de propósito: a classificação é a última dimensão validada.
        // Um caso trivial (etapas vazias) passaria mesmo numa implementação que aplicasse
        // etapas e distribuição antes de chegar à classificação — e não testaria nada.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        VersaoConfiguracao versao = VersaoDo(processo);

        Estado antes = Estado.De(processo);

        // PSIQ não é baseado em ENEM — ELIM-CORTE-REDACAO não se aplica (INV-B13).
        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            eliminacoes: [
                RegraEliminacao.Criar(
                    Regra(RegraEliminacaoCodigo.ElimCorteRedacao, 'e'),
                    new ArgsElimCorteRedacao(400m)).Value!,
            ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem");

        Estado.De(processo).Should().BeEquivalentTo(antes,
            "a validação acontece INTEIRA antes de qualquer escrita. Se a reposição aplicasse dimensão a dimensão, " +
            "este grafo já teria trocado etapas e distribuição antes de falhar na classificação — e o certame " +
            "ficaria numa configuração que nunca existiu.");
    }

    [Fact(DisplayName = "EtapaRef órfão também falha sem tocar no estado")]
    public void EtapaRefOrfao_NaoAlteraEstado()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            criterios: [
                CriterioDesempate.Criar(
                    1,
                    Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'),
                    // Aponta para uma etapa que NÃO está no grafo — é o que aconteceria se o
                    // decoder regenerasse o etapa.Id em vez de preservá-lo.
                    new ArgsDesempateMaiorNotaEtapa(Guid.NewGuid())).Value!,
            ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefDesempateInexistente");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Ids de etapa duplicados no grafo são recusados — a entidade não os validava")]
    public void IdsDeEtapaDuplicados_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        GrafoConfiguracao invalido = Grafo(etapas: [
            EtapaProcesso.Reidratar(EtapaCongelada, "Prova A", CaraterEtapa.Classificatoria, 1m, null, 1),
            EtapaProcesso.Reidratar(EtapaCongelada, "Prova B", CaraterEtapa.Classificatoria, 2m, null, 2),
        ]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "duas etapas com o mesmo Id são indistinguíveis para o etapa_ref, e o INSERT colidiria na chave " +
            "primária. A unicidade era garantida só pelo handler de PUT /etapas — a reposição não passa por ele.");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
    }

    [Fact(DisplayName = "Restaurar a configuração de OUTRO processo é recusado")]
    public void VersaoDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        ProcessoSeletivo alheio = ProcessoPublicado(TipoProcesso.SiSU);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(alheio), Grafo());

        resultado.IsFailure.Should().BeTrue(
            "repor num certame a configuração congelada de outro sobrescreveria o primeiro com uma configuração que " +
            "nunca foi dele — e a próxima publicação congelaria a troca");
        resultado.Error!.Code.Should().Be("VersaoConfiguracao.VersaoDeOutroProcesso");
    }

    [Fact(DisplayName = "Um processo em rascunho não tem configuração congelada a restaurar")]
    public void ProcessoEmRascunho_Recusa()
    {
        ProcessoSeletivo rascunho = ProcessoConforme(TipoProcesso.SiSU);
        ProcessoSeletivo publicado = ProcessoPublicado(TipoProcesso.SiSU);

        Result resultado = rascunho.RestaurarConfiguracaoCongelada(VersaoDo(publicado), Grafo());

        resultado.IsFailure.Should().BeTrue(
            "a reposição não é edição — ela devolve a configuração ao que a versão congelada já dizia. Num processo " +
            "que nunca publicou não há versão nenhuma, e a operação não tem sentido.");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.RestauracaoForaDePublicado");
    }

    [Fact(DisplayName = "A etapa que sobrevive é RECONCILIADA na mesma instância (o CreatedAt não se perde)")]
    public void EtapaSobrevivente_EReconciliada()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        EtapaProcesso instanciaViva = processo.Etapas.Single();

        // A etapa congelada tem o MESMO Id da viva, mas dados diferentes.
        GrafoConfiguracao grafo = Grafo(etapas: [
            EtapaProcesso.Reidratar(EtapaOriginal, "Nome Restaurado", CaraterEtapa.Ambas, 7m, 20m, 3),
        ]);

        processo.RestaurarConfiguracaoCongelada(versao, grafo).IsSuccess.Should().BeTrue();

        EtapaProcesso depois = processo.Etapas.Single();

        depois.Should().BeSameAs(instanciaViva,
            "substituir a instância tracked por outra com o mesmo Id colide com o identity map do EF — e o CreatedAt " +
            "original se perderia. A etapa é atualizada NA MESMA instância — o Id é preservado porque referências de " +
            "negócio (etapaRef) apontam para ele (ADR-0110).");
        depois.Nome.Should().Be("Nome Restaurado", "os dados vêm do grafo congelado, não da instância viva");
        depois.Peso.Should().Be(7m);
        depois.Ordem.Should().Be(3);
    }

    [Fact(DisplayName = "issue #848/ADR-0115 §3.7 — restauração com AcaoQuandoIndeferido divergente entre ofertas é recusada")]
    public void RestauracaoComAcaoQuandoIndeferidoDivergenteEntreOfertas_Recusa()
    {
        // AplicarGrafo reconstrói _distribuicaoVagas diretamente do grafo decodificado,
        // sem passar por DefinirDistribuicaoVagas — a checagem de consistência entre
        // ofertas precisa estar também em ValidarGrafo, senão a restauração de um
        // envelope congelado (que nunca poderia ter sido produzido pelo caminho normal
        // de escrita) reintroduziria o mesmo código de modalidade com ações divergentes.
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        static ModalidadeSelecionada Ac(int quantidade) => ModalidadeSelecionada.Criar(
            new Guid("cccc0000-0000-4000-8000-000000000001"), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
            RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            [], null, "base legal", quantidadeDeclarada: quantidade).Value!;

        static ModalidadeSelecionada V(string acaoQuandoIndeferido, int quantidade) => ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "V", null, NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal,
            null, RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null, [], acaoQuandoIndeferido, "base legal",
            quantidadeDeclarada: quantidade).Value!;

        ReferenciaRegra regra = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a');

        ConfiguracaoDistribuicaoVagas ofertaA = ConfiguracaoDistribuicaoVagas.Criar(
            new Guid("bbbb0000-0000-4000-8000-000000000001"), voBase: 10, pr: 1m, regra,
            regraAjuste: null, referenciaDemografica: null, [V("RECLASSIFICAR_AC", 2), Ac(8)]).Value!;
        ConfiguracaoDistribuicaoVagas ofertaB = ConfiguracaoDistribuicaoVagas.Criar(
            new Guid("bbbb0000-0000-4000-8000-000000000002"), voBase: 10, pr: 1m, regra,
            regraAjuste: null, referenciaDemografica: null, [V("RECLASSIFICAR_REGRA_EDITAL", 2), Ac(8)]).Value!;

        GrafoConfiguracao invalido = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [ofertaA, ofertaB],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.AcaoQuandoIndeferidoDivergente");
        Estado.De(processo).Should().BeEquivalentTo(antes,
            "a restauração recusada não pode deixar o agregado meio-reposto (CA-07)");
    }

    // ── NoExigencia.Reidratar não revalida os invariantes de NoExigencia.CriarGrupo — um
    // envelope 1.4 adulterado com uma folha carregando filhos, um grupo vazio ou um OU
    // pedindo mais filhos do que tem precisa ser recusado na restauração, não só no
    // SaveChanges (CHECK/índice) ou em silêncio. ──

    [Fact(DisplayName = "Árvore restaurada com uma FOLHA carregando filhos é recusada")]
    public void ArvoreComFolhaCarregandoFilhos_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia filhoOrfao = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, Guid.CreateVersion7(), DocumentoQualquer(fase.Id),
            1, null, null, null, null, null, [], []);
        NoExigencia folhaComFilhos = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento,
            1, null, null, null, null, null, [], [filhoOrfao]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [folhaComFilhos, filhoOrfao]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "NoExigencia.Reidratar não revalida os invariantes de CriarFolha/CriarGrupo — sem esta checagem, uma " +
            "folha carregando filhos só falharia depois, ao gerar consequência/resolver a árvore, e de forma não " +
            "nomeada");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NoExigenciaFolhaComFilhos");
        Estado.De(processo).Should().BeEquivalentTo(antes, "a restauração recusada não pode deixar o agregado meio-reposto (CA-07)");
    }

    [Fact(DisplayName = "Árvore restaurada com um GRUPO vazio é recusada")]
    public void ArvoreComGrupoVazio_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        NoExigencia grupoVazio = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null, null, null, null, null, [], []);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [], [grupoVazio]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "um grupo E/OU sem filhos nunca é produzido por NoExigencia.CriarGrupo — só alcançável por adulteração " +
            "do envelope, e a restauração precisa recusar, não repor um grupo vazio em silêncio");
        resultado.Error!.Code.Should().Be("NoExigencia.GrupoVazio");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com grupo OU pedindo mais filhos do que tem é recusada")]
    public void ArvoreComGrupoOuQuantidadeMinimaExcedeFilhos_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia folha = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        // Um único filho, mas quantidadeMinima=2 — NoExigencia.CriarGrupo nunca produziria isto
        // (o teto é filhos.Count).
        NoExigencia grupoOu = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoOu, 0, null, null, 2, "ELIMINA", null, null, null, null, [], [folha]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoOu, folha]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigencia.QuantidadeMinimaForaDosLimites");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

    [Fact(DisplayName = "Árvore restaurada com grupo OU de consequência fora do catálogo fechado é recusada")]
    public void ArvoreComGrupoOuConsequenciaInvalida_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);
        Estado antes = Estado.De(processo);

        FaseCronograma fase = FaseConforme();
        DocumentoExigido documento = DocumentoQualquer(fase.Id);
        NoExigencia folha = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, 1, null, null, null, null, null, [], []);
        // NoExigencia.CriarGrupo nunca produziria "FOO" — o catálogo fechado é
        // {ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM, PENDENCIA_REENVIO}.
        NoExigencia grupoOu = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoOu, 0, null, null, 1, "FOO", null, null, null, null, [], [folha]);

        GrafoConfiguracao invalido = GrafoComArvore(fase, [documento], [grupoOu, folha]);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, invalido);

        resultado.IsFailure.Should().BeTrue(
            "NoExigencia.Reidratar não revalida o vocabulário fechado de Consequencia que CriarGrupo garante — sem " +
            "esta checagem, o agregado reidrataria (e recanonicalizaria nos MESMOS bytes inválidos, provando o " +
            "round-trip) e passaria a emitir uma ConsequenciaEmitida desconhecida do vocabulário");
        resultado.Error!.Code.Should().Be("NoExigencia.ConsequenciaInvalida");
        Estado.De(processo).Should().BeEquivalentTo(antes);
    }

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
            TipoEntidade.MembroNucleoFamiliar, [], []);
        NoExigencia grupoRepetido = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null, null, null, null,
            TipoEntidade.PessoaJuridicaVinculada, [], [folhaRepetida]);

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
            EtapaProcesso.Reidratar(EtapaCongelada, "Etapa Que Voltou", CaraterEtapa.Classificatoria, 1m, null, 1),
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

        processo.AbrirRetificacao("Incluir exigência documental", versao, "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL",
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

    [Fact(DisplayName = "Story #554/issue #892 (achado Codex P1) — restauração limpa ReferenciaTemporalFatos definida durante a sessão")]
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

        processo.AbrirRetificacao("Ajustar referência temporal", versao, "testes", DateTimeOffset.UnixEpoch)
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

    [Fact(DisplayName = "Story #554, PR #903 (achado de revisão P2) — restaurar remapeia ExigidoNaFaseId/ReferenciaTemporalFatos.FaseId para a fase VIVA quando a sessão editorial trocou a fase da mesma Ordem")]
    public void Restauracao_RemapeiaReferenciasDeFaseParaAInstanciaViva()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        processo.AbrirRetificacao("Trocar a fase da Ordem 1", versao, "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        // A sessão editorial troca a fase da Ordem 1 por uma fase GENUINAMENTE diferente
        // (FaseCanonicaOrigemId novo, não reaproveita a identidade estável da fase
        // publicada) — DefinirCronogramaFases reconcilia por FaseCanonicaOrigemId (CA-04),
        // então isto insere uma instância NOVA em vez de atualizar a existente no lugar.
        FaseCronograma faseTrocada = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseTrocada], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        Guid faseVivaId = processo.CronogramaFases.Single().Id;

        // O grafo CONGELADO referencia a fase que existia QUANDO a versão foi publicada —
        // um Id diferente do da fase viva acima (Reidratar preserva o Id congelado no
        // envelope 1.2, ADR-0110 D2), mas na MESMA Ordem — o caso que a reconciliação por
        // Ordem de AplicarGrafo reusa a instância viva em vez da decodificada.
        Guid faseCongeladaId = Guid.CreateVersion7();
        FaseCronograma faseCongelada = FaseCronograma.Reidratar(
            faseCongeladaId, ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria, agrupaEtapas: true,
            permiteComplementacao: false, produzResultado: true, resultadoDefinitivo: true, coletaInscricao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null);

        DocumentoExigido documentoCongelado = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), exigidoNaFaseId: faseCongeladaId, tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE", tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", aplicabilidade: Aplicabilidade.Geral, obrigatorio: true,
            consequenciaIndeferimento: null, grupoSatisfacaoId: null, condicoes: [], basesLegais: [],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null);

        ReferenciaTemporalFatos referenciaCongelada = ReferenciaTemporalFatos.Criar(
            ReferenciaTipo.FimFase, null, faseCongeladaId).Value!;

        GrafoConfiguracao grafoCongelado = new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [faseCongelada],
            documentosExigidos: [documentoCongelado],
            nosExigencia: [],
            referenciaTemporalFatos: referenciaCongelada);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, grafoCongelado);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        FaseCronograma faseReposta = processo.CronogramaFases.Single();
        faseReposta.Id.Should().Be(faseVivaId,
            "a reconciliação de fases é por Ordem, não por Id (ux_fases_cronograma_processo_ordem) — a instância " +
            "VIVA sobrevive, não a decodificada");

        processo.DocumentosExigidos.Single().ExigidoNaFaseId.Should().Be(faseVivaId,
            "sem o remapeamento, o documento restaurado ficaria com ExigidoNaFaseId apontando para o Id " +
            "CONGELADO — ausente de CronogramaFases após a restauração (achado de revisão da PR #903)");

        processo.ReferenciaTemporalFatos!.FaseId.Should().Be(faseVivaId,
            "mesmo raciocínio do documento exigido: FIM_FASE precisa apontar para uma fase que realmente existe " +
            "em CronogramaFases após a restauração");
    }

    // ── Fábrica de cenários ──

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static ProcessoSeletivo ProcessoConforme(TipoProcesso tipo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Restauração", tipo, OrigemCandidatos.ImportacaoExterna);

        processo.DefinirEtapas([
            EtapaProcesso.Reidratar(EtapaOriginal, "Prova Original", CaraterEtapa.Classificatoria, 1m, null, 1),
        ], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao()], PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(Classificacao([]), PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente);

        return processo;
    }

    private static ProcessoSeletivo ProcessoPublicado(TipoProcesso tipo)
    {
        ProcessoSeletivo processo = ProcessoConforme(tipo);

        processo.Publicar(
            Dados(),
            configuracaoCongeladaCanonica: [1, 2, 3],
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: new string('a', 64),
            atorUsuarioSub: "testes",
            clock: TimeProvider.System).IsSuccess.Should().BeTrue();

        processo.ClearDomainEvents();
        return processo;
    }

    /// <summary>
    /// A versão que autentica a reposição. Os bytes não importam neste nível — a prova de
    /// que o grafo veio <b>daquela</b> versão é do <c>RestauradorDeConfiguracao</c>
    /// (Application), que recanonicaliza; o Domain não canonicaliza (ADR-0042).
    /// </summary>
    private static VersaoConfiguracao VersaoDo(ProcessoSeletivo processo) => VersaoConfiguracao.Abrir(
        processo.Id,
        [1, 2, 3],
        schemaVersion: "1.1",
        algoritmoHash: "canonical-json/sha256@v1",
        atoCriadorId: Guid.CreateVersion7(),
        atoCriadorHash: new string('a', 64),
        atorUsuarioSub: "testes",
        instante: DateTimeOffset.UnixEpoch);

    private static GrafoConfiguracao Grafo(
        IReadOnlyList<EtapaProcesso>? etapas = null,
        IReadOnlyList<CriterioDesempate>? criterios = null,
        IReadOnlyList<RegraEliminacao>? eliminacoes = null,
        IReadOnlyList<FaseCronograma>? cronogramaFases = null) => new(
            etapas: etapas ?? [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: criterios ?? [],
            classificacao: Classificacao(eliminacoes ?? []),
            cronogramaFases: cronogramaFases ?? [FaseConforme()],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null);

    /// <summary>
    /// Mesmo grafo de <see cref="Grafo"/>, com <c>documentosExigidos</c>/<c>nosExigencia</c>
    /// explícitos — usado pelos testes de árvore de satisfação (Story #923), que precisam de
    /// uma fase congelada específica para <paramref name="documentosExigidos"/> referenciar.
    /// </summary>
    private static GrafoConfiguracao GrafoComArvore(
        FaseCronograma fase, IReadOnlyList<DocumentoExigido> documentosExigidos, IReadOnlyList<NoExigencia> nosExigencia) => new(
            etapas: [EtapaProcesso.Reidratar(EtapaCongelada, "Prova", CaraterEtapa.Classificatoria, 1m, null, 1)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [fase],
            documentosExigidos: documentosExigidos,
            nosExigencia: nosExigencia,
            referenciaTemporalFatos: null);

    private static DocumentoExigido DocumentoQualquer(Guid faseId) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL",
        Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
        FormatosPermitidos.Criar(true, null).Value!, null).Value!;

    /// <summary>Uma fase mínima e conforme: agrupa etapas (há 1 etapa por padrão) e produz resultado (há vagas por padrão).</summary>
    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: new Guid("eeee0000-0000-4000-8000-000000000001"),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_FINAL",
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private static ConfiguracaoDistribuicaoVagas Distribuicao() =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: new Guid("bbbb0000-0000-4000-8000-000000000001"),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    new Guid("cccc0000-0000-4000-8000-000000000001"), "AC", null,
                    NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                    RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                    [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
            ]).Value!;

    private static ConfiguracaoClassificacao Classificacao(IReadOnlyList<RegraEliminacao> eliminacoes) =>
        ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'c'),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'd'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: eliminacoes).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 31),
        new Guid("dddd0000-0000-4000-8000-000000000001")).Value!;

    /// <summary>
    /// Snapshot das <b>seis dimensões</b> mais o status — é sobre ele que o critério de aceite de
    /// restauração tudo-ou-nada (Story #859, CA-07) asserta. Comparar só o <c>Result</c> deixaria
    /// passar exatamente a implementação que esse critério existe para proibir: a que aplica
    /// dimensão a dimensão e só depois falha, deixando o agregado meio-reposto.
    /// </summary>
    private sealed record Estado(
        StatusProcesso Status,
        IReadOnlyList<(Guid Id, string Nome, decimal? Peso, int? Ordem)> Etapas,
        int Condicoes,
        IReadOnlyList<(Guid Oferta, int VoBase, decimal Pr, int Modalidades)> Distribuicao,
        bool TemBonus,
        IReadOnlyList<int> OrdensDesempate,
        string RegraCalculo,
        int Eliminacoes)
    {
        internal static Estado De(ProcessoSeletivo processo) => new(
            processo.Status,
            [.. processo.Etapas.Select(e => (e.Id, e.Nome, e.Peso, e.Ordem)).OrderBy(e => e.Id)],
            processo.OfertaAtendimento!.Condicoes.Count,
            [.. processo.DistribuicaoVagas
                .Select(d => (d.OfertaCursoOrigemId, d.VoBase, d.Pr, d.Modalidades.Count))
                .OrderBy(d => d.OfertaCursoOrigemId)],
            processo.BonusRegional is not null,
            [.. processo.CriteriosDesempate.Select(c => c.Ordem).Order()],
            processo.Classificacao!.RegraCalculo.Codigo,
            processo.Classificacao.RegrasEliminacao.Count);
    }
}
