namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaSessaoEditorialDoProcessoSeletivo;

/// <summary>
/// A <b>sessão editorial</b> de retificação (Story #860, ADR-0110 D3/D4/D7): a allowlist
/// que falha fechada, a abertura da sessão e a exclusividade dela frente ao atalho atômico.
/// </summary>
public sealed class ProcessoSeletivoSessaoEditorialTests
{
    // A trava anterior era uma denylist de um elemento ("bloqueia se, e só se, o status é
    // Publicado"): Encerrado e Cancelado aceitavam mutação em silêncio, e qualquer status
    // futuro nasceria mutável por omissão.
    [Theory(DisplayName = "CA-01: processo fora de Rascunho/Publicado RECUSA mutação da configuração — a allowlist falha fechada")]
    [InlineData(StatusProcesso.Encerrado)]
    [InlineData(StatusProcesso.Cancelado)]
    public void Definir_ForaDeEstadoEditavel_Recusa(StatusProcesso status)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ForcarStatus(processo, status);

        Result resultado = processo.DefinirEtapas(
            [EtapaDaProvaObjetiva("Nova etapa", CaraterEtapa.Classificatoria, 2m)], PrecondicaoIfMatch.Ausente);

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

    [Fact(DisplayName = "CA-10: abrir a retificação NÃO muda o status — o certame continua Publicado")]
    public void Abrir_NaoMudaStatus()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        Result<RascunhoRetificacao> abertura = AbrirSessao(processo, versao);

        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);
        processo.Status.Should().Be(
            StatusProcesso.Publicado,
            "o status marca o estado do ATO, não a atividade em curso — um certame com retificação aberta está publicado, juridicamente e para o candidato (D3)");
    }

    [Fact(DisplayName = "CA-08: abrir a retificação não drena evento algum — nada é congelado até o fechamento")]
    public void Abrir_NaoEmiteEvento()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        AbrirSessao(processo, versao).IsSuccess.Should().BeTrue();

        processo.DomainEvents.Should().BeEmpty(
            "abrir não emite ato nem abre VersaoConfiguracao — a versão nova nasce só no fechamento");
    }

    [Fact(DisplayName = "A sessão nasce na revisão 1, ancorada na versão corrente")]
    public void Abrir_NasceNaRevisao1()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        RascunhoRetificacao rascunho = AbrirSessao(processo, versao).Value!;

        rascunho.Revisao.Should().Be(1);
        rascunho.VersaoBaseId.Should().Be(versao.Id);
        rascunho.NumeroVersaoBase.Should().Be(versao.NumeroVersao);
        rascunho.AbertoPorContaId.Should().Be("user-conta-1");
        rascunho.ETag.Should().Be($"\"{rascunho.Id}:1\"");
    }

    [Theory(DisplayName = "CA-07: motivo em branco recusa a ABERTURA — a regra deixa de existir só no fechamento")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Abrir_MotivoEmBranco_Recusa(string motivo)
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        Result<RascunhoRetificacao> abertura = AbrirSessao(processo, versao, motivo);

        abertura.IsFailure.Should().BeTrue();
        abertura.Error!.Code.Should().Be("RascunhoRetificacao.MotivoObrigatorio");
        processo.Rascunho.Should().BeNull("uma abertura recusada não deixa sessão pela metade");
    }

    // O 'ç' decomposto em c + cedilha combinante.
    [Fact(DisplayName = "O motivo é normalizado na abertura (Trim + NFC) — é o valor que o fechamento vai congelar")]
    public void Abrir_NormalizaMotivo()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        const string Nfd = "  correção do prazo  ";

        RascunhoRetificacao rascunho = AbrirSessao(processo, versao, Nfd).Value!;

        rascunho.Motivo.Should().Be(
            "correção do prazo",
            "o canonicalizador aplica NFC ao congelar; guardar o motivo em forma decomposta o faria divergir do bloco congelado, e o Postgres não normaliza texto");
    }

    [Fact(DisplayName = "Motivo acima do teto recusa a abertura — o fechamento o recusaria, e a sessão nasceria condenada")]
    public void Abrir_MotivoMuitoLongo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);

        Result<RascunhoRetificacao> abertura = AbrirSessao(processo, versao, new string('a', RascunhoRetificacao.MotivoMaxLength + 1));

        abertura.IsFailure.Should().BeTrue();
        abertura.Error!.Code.Should().Be("RascunhoRetificacao.MotivoMuitoLongo");
    }

    [Fact(DisplayName = "Abrir com sessão já aberta recusa (RetificacaoJaAberta)")]
    public void Abrir_ComSessaoAberta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        AbrirSessao(processo, versao, "Primeira").IsSuccess.Should().BeTrue();

        Result<RascunhoRetificacao> segunda = AbrirSessao(processo, versao, "Segunda", "user-conta-2");

        segunda.IsFailure.Should().BeTrue();
        segunda.Error!.Code.Should().Be("RascunhoRetificacao.JaAberta");
        processo.Rascunho!.Motivo.Should().Be("Primeira", "a sessão existente não é sobrescrita pela tentativa recusada");
    }

    [Fact(DisplayName = "Abrir retificação de processo em rascunho recusa — não há versão congelada a retificar")]
    public void Abrir_ProcessoEmRascunho_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ProcessoSeletivo outro = NovoProcessoPublicado(out VersaoConfiguracao versaoDeOutro);

        Result<RascunhoRetificacao> abertura = AbrirSessao(processo, versaoDeOutro, "Correção");

        abertura.IsFailure.Should().BeTrue();
        abertura.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        outro.Should().NotBeNull();
    }

    [Fact(DisplayName = "Abrir sobre versão de OUTRO processo recusa — a cadeia de versões não atravessa certames")]
    public void Abrir_VersaoDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out _);
        NovoProcessoPublicado(out VersaoConfiguracao versaoAlheia);

        Result<RascunhoRetificacao> abertura = AbrirSessao(processo, versaoAlheia, "Correção");

        abertura.IsFailure.Should().BeTrue();
        abertura.Error!.Code.Should().Be("VersaoConfiguracao.VersaoDeOutroProcesso");
    }

    [Fact(DisplayName = "D7: o atalho POST /retificacoes RECUSA quando há sessão editorial aberta — invariante do domínio")]
    public void Retificar_ComSessaoAberta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        AbrirSessao(processo, versao, "Sessão em curso").IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Retificar(
            NovosDados(), versao, BytesCanonicos, "1.1", "canonical-json/sha256@v1", HashFixo,
            "user-conta-2", motivo: "Atalho concorrente", clock: new RelogioFixo(Agora), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        resultado.IsFailure.Should().BeTrue(
            "os dois caminhos retificam o MESMO ato — o atalho publicaria a versão N+1 a partir da configuração que a sessão está no meio de editar, e o rascunho sobreviveria apontando para uma base que deixou de ser o topo da cadeia");
        resultado.Error!.Code.Should().Be("RascunhoRetificacao.JaAberta");
    }
}
