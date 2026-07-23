namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A <b>sessão editorial</b> de retificação (Story #860, ADR-0110 D3/D4/D5/D7): a allowlist
/// que falha fechada, o portador da retificação, e a precondição que protege a sessão
/// inteira.
/// </summary>
public sealed class ProcessoSeletivoSessaoEditorialTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly byte[] BytesCanonicos = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-01 — a allowlist falha FECHADA (D4)
    //
    // A trava anterior era uma DENYLIST DE UM ELEMENTO: "bloqueia se, e só se, o status é
    // Publicado". Todo outro estado — inclusive Encerrado e Cancelado — aceitava mutação da
    // configuração em silêncio, e qualquer status futuro nasceria mutável por omissão.
    //
    // Estes dois testes FALHAM na main: hoje o Definir* é ACEITO num processo encerrado.
    // ══════════════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "CA-01: processo fora de Rascunho/Publicado RECUSA mutação da configuração — a allowlist falha fechada")]
    [InlineData(StatusProcesso.Encerrado)]
    [InlineData(StatusProcesso.Cancelado)]
    public void Definir_ForaDeEstadoEditavel_Recusa(StatusProcesso status)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ForcarStatus(processo, status);

        Result resultado = processo.DefinirEtapas(
            [EtapaProcesso.Criar("Nova etapa", CaraterEtapa.Classificatoria, peso: 2m, ordem: 1)],
            PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue(
            $"um processo em {status} não é editável — antes da allowlist, a trava só olhava para Publicado e este Definir* passava");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");
    }

    [Fact(DisplayName = "CA-01: processo publicado SEM sessão editorial continua recusando mutação direta — utilize a retificação")]
    public void Definir_PublicadoSemRascunho_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out _);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Processo em rascunho aceita mutação sem precondição alguma — não há sessão, não há ETag a exigir")]
    public void Definir_EmRascunho_NaoExigePrecondicao()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Abertura da sessão (D3)
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-10: abrir a retificação NÃO muda o status — o certame continua Publicado")]
    public void Abrir_NaoMudaStatus()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            "Correção do prazo", versao, "user-sub-1", Agora);

        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);
        processo.Status.Should().Be(
            StatusProcesso.Publicado,
            "o status marca o estado do ATO, não a atividade em curso — um certame com retificação aberta está publicado, juridicamente e para o candidato (D3)");
    }

    [Fact(DisplayName = "CA-08: abrir a retificação não drena evento algum — nada é congelado até o fechamento")]
    public void Abrir_NaoEmiteEvento()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        processo.AbrirRetificacao("Correção do prazo", versao, "user-sub-1", Agora)
            .IsSuccess.Should().BeTrue();

        processo.DomainEvents.Should().BeEmpty(
            "abrir não emite ato nem abre VersaoConfiguracao — a versão nova nasce só no fechamento");
    }

    [Fact(DisplayName = "A sessão nasce na revisão 1, ancorada na versão corrente")]
    public void Abrir_NasceNaRevisao1()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        RascunhoRetificacao rascunho = processo
            .AbrirRetificacao("Correção do prazo", versao, "user-sub-1", Agora).Value!;

        rascunho.Revisao.Should().Be(1);
        rascunho.VersaoBaseId.Should().Be(versao.Id);
        rascunho.NumeroVersaoBase.Should().Be(versao.NumeroVersao);
        rascunho.AbertoPorSub.Should().Be("user-sub-1");
        rascunho.ETag.Should().Be($"\"{rascunho.Id}:1\"");
    }

    [Theory(DisplayName = "CA-07: motivo em branco recusa a ABERTURA — a regra deixa de existir só no fechamento")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Abrir_MotivoEmBranco_Recusa(string motivo)
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(motivo, versao, "user-sub-1", Agora);

        abertura.IsFailure.Should().BeTrue();
        abertura.Error!.Code.Should().Be("RascunhoRetificacao.MotivoObrigatorio");
        processo.Rascunho.Should().BeNull("uma abertura recusada não deixa sessão pela metade");
    }

    [Fact(DisplayName = "O motivo é normalizado na abertura (Trim + NFC) — é o valor que o fechamento vai congelar")]
    public void Abrir_NormalizaMotivo()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        // "correção" em NFD: o 'ç' decomposto em c + cedilha combinante.
        const string Nfd = "  correção do prazo  ";

        RascunhoRetificacao rascunho = processo
            .AbrirRetificacao(Nfd, versao, "user-sub-1", Agora).Value!;

        rascunho.Motivo.Should().Be(
            "correção do prazo",
            "o canonicalizador aplica NFC ao congelar; guardar o motivo em forma decomposta o faria divergir do bloco congelado, e o Postgres não normaliza texto");
    }

    [Fact(DisplayName = "Motivo acima do teto recusa a abertura — o fechamento o recusaria, e a sessão nasceria condenada")]
    public void Abrir_MotivoMuitoLongo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            new string('a', RascunhoRetificacao.MotivoMaxLength + 1), versao, "user-sub-1", Agora);

        abertura.IsFailure.Should().BeTrue();
        abertura.Error!.Code.Should().Be("RascunhoRetificacao.MotivoMuitoLongo");
    }

    [Fact(DisplayName = "Abrir com sessão já aberta recusa (RetificacaoJaAberta)")]
    public void Abrir_ComSessaoAberta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        processo.AbrirRetificacao("Primeira", versao, "user-sub-1", Agora).IsSuccess.Should().BeTrue();

        Result<RascunhoRetificacao> segunda = processo.AbrirRetificacao("Segunda", versao, "user-sub-2", Agora);

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Code.Should().Be("RascunhoRetificacao.JaAberta");
        processo.Rascunho!.Motivo.Should().Be("Primeira", "a sessão existente não é sobrescrita pela tentativa recusada");
    }

    [Fact(DisplayName = "Abrir retificação de processo em rascunho recusa — não há versão congelada a retificar")]
    public void Abrir_ProcessoEmRascunho_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ProcessoSeletivo outro = NovoProcessoPublicado(out VersaoConfiguracao versaoDeOutro);

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            "Correção", versaoDeOutro, "user-sub-1", Agora);

        abertura.IsFailure.Should().BeTrue();
        abertura.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        outro.Should().NotBeNull();
    }

    [Fact(DisplayName = "Abrir sobre versão de OUTRO processo recusa — a cadeia de versões não atravessa certames")]
    public void Abrir_VersaoDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out _);
        NovoProcessoPublicado(out VersaoConfiguracao versaoAlheia);

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            "Correção", versaoAlheia, "user-sub-1", Agora);

        abertura.IsFailure.Should().BeTrue();
        abertura.Error!.Code.Should().Be("VersaoConfiguracao.VersaoDeOutroProcesso");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // A precondição protege a sessão INTEIRA (D5) — não só o motivo
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-03: com sessão aberta, um Definir* SEM If-Match é recusado (precondição requerida)")]
    public void Definir_ComSessao_SemPrecondicao_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out _);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue(
            "os seis Definir* são as rotas que de fato alteram a configuração — uma revisão que governasse só o motivo seria decorativa (D5)");
        resultado.Error!.Code.Should().Be("Precondicao.Requerida");
    }

    [Fact(DisplayName = "CA-03: com sessão aberta, um Definir* com If-Match DEFASADO é recusado (precondição falhou)")]
    public void Definir_ComSessao_PrecondicaoDefasada_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        string tagInicial = rascunho.ETag;

        // Um primeiro administrador edita e faz a revisão avançar.
        processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([tagInicial]))
            .IsSuccess.Should().BeTrue();

        // O segundo ainda tem o tag antigo em mãos — e é exatamente esta a edição cega que a
        // precondição existe para impedir.
        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([tagInicial]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Precondicao.Falhou");
    }

    [Fact(DisplayName = "Toda mutação aceita sob sessão INCREMENTA a revisão")]
    public void Definir_ComSessao_IncrementaRevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        rascunho.Revisao.Should().Be(1);

        processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(2);

        processo.DefinirBonusRegional(null, PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(3);
    }

    [Fact(DisplayName = "O grafo de coleta de fatos NÃO é editável após a publicação — nem sob retificação (aguarda o congelamento conjunto de #928)")]
    public void DefinirFatosColetados_ProcessoPublicado_Recusa()
    {
        // Publicado, com sessão de retificação aberta: os demais Definir* aceitam neste estado.
        // O grafo de fatos não, porque ainda não entra no congelamento nem na restauração — aceitá-lo
        // deixaria estado mutável fora do envelope congelado e sem rollback no descarte.
        ProcessoSeletivo processo = ComSessaoAberta(out _);

        Result resultado = processo.DefinirFatosColetados([FatoColetado.Criar("PCD", 0, null).Value!]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.GrafoDeFatosSomenteEmRascunho");
    }

    [Fact(DisplayName = "Uma mutação RECUSADA não move a revisão — o ETag do cliente continua válido")]
    public void Definir_Recusado_NaoIncrementaRevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);

        // Ordem duplicada: recusa de REGRA DE NEGÓCIO, com a precondição correta. (Story
        // #851 §3.5: lista VAZIA deixou de ser recusa — um processo sem prova é válido —
        // por isso a contraprova de recusa de negócio migrou para ordem duplicada.)
        Result resultado = processo.DefinirEtapas(
            [
                EtapaProcesso.Criar("Prova A", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
                EtapaProcesso.Criar("Prova B", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
            ],
            PrecondicaoIfMatch.DeTags([rascunho.ETag]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        rascunho.Revisao.Should().Be(1, "a mutação não foi aceita — mover a revisão invalidaria o ETag de quem não mudou nada");
    }

    [Fact(DisplayName = "If-Match: * casa com a sessão existente")]
    public void Definir_ComCuringa_Aceita()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out _);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Uma lista VAZIA de tags fortes não casa — é o que resta de um If-Match só com weak tags, e sai 412, não 400")]
    public void Definir_ListaVaziaDeTagsFortes_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out _);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Precondicao.Falhou");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-04 — ABA: o ETag carrega a identidade da SESSÃO, não só a revisão
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-04 (ABA): o ETag de uma sessão encerrada NÃO valida a sessão nova, ainda que a revisão coincida")]
    public void ETag_DeSessaoAnterior_NaoValidaSessaoNova()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        RascunhoRetificacao primeira = processo
            .AbrirRetificacao("Primeira sessão", versao, "user-sub-1", Agora).Value!;
        string tagDaPrimeira = primeira.ETag;

        // A sessão morre e outra nasce. A revisão da nova reinicia em 1 — a MESMA da antiga.
        // Se o ETag fosse só a revisão, o tag antigo validaria a sessão nova: o administrador
        // que abandonou a primeira edição sobrescreveria, às cegas, o trabalho de quem abriu
        // a segunda.
        DescartarSessao(processo);
        RascunhoRetificacao segunda = processo
            .AbrirRetificacao("Segunda sessão", versao, "user-sub-2", Agora).Value!;

        segunda.Revisao.Should().Be(primeira.Revisao, "a contagem reinicia — é justamente esta coincidência que cria o ABA");
        segunda.Id.Should().NotBe(primeira.Id);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([tagDaPrimeira]));

        resultado.IsFailure.Should().BeTrue("o ETag morre com a sessão que o emitiu");
        resultado.Error!.Code.Should().Be("Precondicao.Falhou");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // O motivo é mutação como qualquer outra
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Alterar o motivo exige precondição e incrementa a revisão")]
    public void AlterarMotivo_ExigePrecondicaoEIncrementa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);

        processo.AlterarMotivoRetificacao("Novo motivo", PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("Precondicao.Requerida");

        Result aceito = processo.AlterarMotivoRetificacao("Novo motivo", PrecondicaoIfMatch.DeTags([rascunho.ETag]));

        aceito.IsSuccess.Should().BeTrue(aceito.Error?.Message);
        rascunho.Motivo.Should().Be("Novo motivo");
        rascunho.Revisao.Should().Be(2);
    }

    [Fact(DisplayName = "Alterar o motivo sem sessão aberta recusa (RetificacaoNaoAberta)")]
    public void AlterarMotivo_SemSessao_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out _);

        Result resultado = processo.AlterarMotivoRetificacao("Motivo", PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RascunhoRetificacao.NaoAberta");
    }

    [Fact(DisplayName = "Alterar o motivo passa pela allowlist INTEIRA — um processo fora de estado editável recusa, mesmo com a sessão aberta e a precondição correta")]
    public void AlterarMotivo_ForaDeEstadoEditavel_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        ForcarStatus(processo, StatusProcesso.Encerrado);

        Result resultado = processo.AlterarMotivoRetificacao("Novo motivo", PrecondicaoIfMatch.DeTags([rascunho.ETag]));

        resultado.IsFailure.Should().BeTrue(
            "esta rota é mutação como as outras — se ela só conferisse a precondição, a edição escaparia por uma porta que os Definir* já tinham fechado");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");
        rascunho.Revisao.Should().Be(1, "uma mutação recusada não move a revisão");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // O descarte sem reposição é IRREPRESENTÁVEL — não apenas desencorajado
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Descartar SEM ter reposto a configuração congelada é RECUSADO pelo próprio agregado — encerrar a sessão agora deixaria o certame servindo a configuração editada")]
    public void Descartar_SemRestauracao_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);

        // O administrador editou. A configuração viva já diverge do que o edital publicou.
        processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();

        // E alguém tenta encerrar a sessão sem repor.
        Result resultado = processo.DescartarRetificacao(PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue(
            "um fitness test diz 'ninguém faz isso hoje'; ele não diz 'isso não pode acontecer'. O agregado precisa recusar");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DescarteSemRestauracao");
        processo.Rascunho.Should().NotBeNull("a sessão sobrevive a um descarte recusado");
    }

    [Fact(DisplayName = "Descartar DEPOIS de repor a configuração congelada é aceito — e a sessão morre")]
    public void Descartar_ComRestauracao_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        RascunhoRetificacao rascunho = processo
            .AbrirRetificacao("Correção", versao, "user-sub-1", Agora).Value!;

        processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();

        // A reposição carimba a versão que repôs — é a prova que o descarte exige.
        processo.RestaurarConfiguracaoCongelada(versao, GrafoDoProcesso(processo))
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DescartarRetificacao(PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Rascunho.Should().BeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // D7 — o atalho atômico recusa com sessão aberta
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "O atalho atômico continua conferindo os contratos ANTES da sessão — argumento nulo lança, mesmo com retificação aberta")]
    public void Retificar_ComSessaoAberta_AindaConfereContratosPrimeiro()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out _);

        // Antes desta Feature, um `dados` nulo lançava. Antepor a recusa por RetificacaoJaAberta
        // faria o atalho mudar de comportamento — e ele prometeu não mudar.
        Action nulo = () => processo.Retificar(
            dados: null!, VersaoQualquerDoProcesso(processo), BytesCanonicos, "1.1",
            "canonical-json/sha256@v1", HashFixo, "user-sub-1", "motivo", new RelogioFixo(Agora));

        nulo.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "D7: o atalho POST /retificacoes RECUSA quando há sessão editorial aberta — invariante do domínio")]
    public void Retificar_ComSessaoAberta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        processo.AbrirRetificacao("Sessão em curso", versao, "user-sub-1", Agora).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            NovosDados(), versao, BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo,
            "user-sub-2", motivo: "Atalho concorrente", clock: new RelogioFixo(Agora));

        resultado.IsFailure.Should().BeTrue(
            "os dois caminhos retificam o MESMO ato — o atalho publicaria a versão N+1 a partir da configuração que a sessão está no meio de editar, e o rascunho sobreviveria apontando para uma base que deixou de ser o topo da cadeia");
        resultado.Error!.Code.Should().Be("RascunhoRetificacao.JaAberta");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════

    private static ProcessoSeletivo ComSessaoAberta(out RascunhoRetificacao rascunho)
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        rascunho = processo.AbrirRetificacao("Correção do prazo", versao, "user-sub-1", Agora).Value!;
        return processo;
    }

    /// <summary>
    /// O grafo das seis dimensões <b>como o agregado as tem agora</b>. Estes testes provam a
    /// mecânica do <b>carimbo de restauração</b>, não a fidelidade da reidratação — essa é do
    /// <c>RestauradorDeConfiguracao</c>, e tem prova de round-trip byte a byte própria.
    /// </summary>
    private static GrafoConfiguracao GrafoDoProcesso(ProcessoSeletivo processo) => new(
        [.. processo.Etapas],
        processo.OfertaAtendimento!,
        [.. processo.DistribuicaoVagas],
        processo.BonusRegional,
        [.. processo.CriteriosDesempate],
        processo.Classificacao!,
        [.. processo.CronogramaFases],
        [.. processo.DocumentosExigidos],
        [.. processo.NosExigencia],
        processo.ReferenciaTemporalFatos);

    /// <summary>Uma versão qualquer DESTE processo — o teste que a usa lança antes de tocá-la.</summary>
    private static VersaoConfiguracao VersaoQualquerDoProcesso(ProcessoSeletivo processo) =>
        VersaoConfiguracao.Abrir(
            processo.Id, BytesCanonicos, "1.1", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), HashFixo, "user-sub-1", Agora);

    /// <summary>
    /// O descarte é da S4 (#861) — aqui só se precisa do EFEITO dele sobre o agregado (a
    /// sessão deixa de existir), para provar o ABA. Reflection porque o setter é privado: o
    /// que este teste afirma é sobre o <b>ETag</b>, não sobre o descarte, que virá com a sua
    /// própria cobertura.
    /// </summary>
    private static void DescartarSessao(ProcessoSeletivo processo) =>
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.Rascunho))!
            .SetValue(processo, null);

    private static void ForcarStatus(ProcessoSeletivo processo, StatusProcesso status) =>
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.Status))!
            .SetValue(processo, status);

    private static DadosEdital NovosDados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static ProcessoSeletivo NovoProcessoPublicado(out VersaoConfiguracao versao)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo, "user-sub-123",
            new RelogioFixo(Agora));
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        versao = publicacao.Value!;
        processo.DequeueDomainEvents();
        return processo;
    }

    private static ProcessoSeletivo NovoProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>Uma fase mínima que satisfaz as três exigências do piso mínimo do fixture: agrupa etapas, produz resultado e coleta inscrição.</summary>
    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: Guid.CreateVersion7(),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: true,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_FINAL",
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
