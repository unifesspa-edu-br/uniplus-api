namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

internal static class ApoioDaRetificacaoDoProcessoSeletivo
{
    private const string VersaoDoEsquema = "1.0";
    private const string VersaoDoCanonicalizador = "canonical-json/sha256@v1";
    private const string Autor = "user-conta-123";

    // Datas de publicação iguais entre atos do mesmo processo são um estado válido (ADR-0104):
    // os testes avançam ou regridem o instante explicitamente quando o cenário exige.
    internal static RelogioManual Relogio() => new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Versão avulsa, sem vínculo com processo algum: serve a quem só precisa satisfazer o
    /// parâmetro não nulo de <see cref="ProcessoSeletivo.Retificar"/> ou provar que ela é de outro certame.
    /// </summary>
    internal static VersaoConfiguracao VersaoQualquer() => VersaoConfiguracao.Abrir(
        Guid.CreateVersion7(), BytesCanonicos, VersaoDoEsquema, VersaoDoCanonicalizador, Guid.CreateVersion7(), HashFixo, Autor, Relogio().GetUtcNow());

    internal static ProcessoSeletivo NovoProcessoPublicado(RelogioManual clock, out VersaoConfiguracao versaoAbertura)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Result<VersaoConfiguracao> publicacao = PublicarComDadosPadrao(processo, clock);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        versaoAbertura = publicacao.Value!;
        processo.DequeueDomainEvents();
        return processo;
    }

    internal static ProcessoSeletivo NovoProcessoPublicadoHaUmMinuto(RelogioManual clock, out VersaoConfiguracao versaoAbertura)
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));
        return processo;
    }

    internal static Result<VersaoConfiguracao> PublicarComDadosPadrao(ProcessoSeletivo processo, TimeProvider relogio) =>
        processo.Publicar(
            new CenarioDeCongelamento { Dados = NovosDados(), Documento = new DocumentoCongelado(BytesCanonicos, VersaoDoEsquema, VersaoDoCanonicalizador, HashFixo), AtorUsuarioContaId = Autor, Relogio = relogio });

    internal static Result<VersaoConfiguracao> RetificarComDadosPadrao(
        ProcessoSeletivo processo, VersaoConfiguracao versaoCorrente, string motivo, TimeProvider relogio) =>
        processo.Retificar(
            new CenarioDeCongelamento { Dados = NovosDados(), Documento = new DocumentoCongelado(BytesCanonicos, VersaoDoEsquema, VersaoDoCanonicalizador, HashFixo), AtorUsuarioContaId = Autor, Relogio = relogio }, versaoCorrente, motivo);

    internal static VersaoConfiguracao RetificarComSucesso(
        ProcessoSeletivo processo, VersaoConfiguracao versaoCorrente, string motivo, TimeProvider relogio)
    {
        Result<VersaoConfiguracao> resultado = RetificarComDadosPadrao(processo, versaoCorrente, motivo, relogio);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value!;
    }

    internal static Result<VersaoConfiguracao> FecharRetificacaoComDadosPadrao(
        ProcessoSeletivo processo, VersaoConfiguracao versaoBase, TimeProvider relogio) =>
        processo.FecharRetificacao(
            new CenarioDeCongelamento { Dados = NovosDados(), Documento = new DocumentoCongelado(BytesCanonicos, VersaoDoEsquema, VersaoDoCanonicalizador, HashFixo), AtorUsuarioContaId = Autor, Relogio = relogio }, versaoBase, PrecondicaoIfMatch.Curinga);

    internal static void AbrirRetificacaoComSucesso(
        ProcessoSeletivo processo, VersaoConfiguracao versaoBase, string motivo, RelogioManual relogio)
    {
        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            motivo, versaoBase, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), Autor, relogio.GetUtcNow());
        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);
    }

    /// <summary>
    /// Relógio manual determinístico: dá instantes distintos à abertura e à retificação
    /// (unicidade de data_publicacao) sem depender do pacote de relógio de teste da Microsoft.
    /// </summary>
    internal sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
    }
}
